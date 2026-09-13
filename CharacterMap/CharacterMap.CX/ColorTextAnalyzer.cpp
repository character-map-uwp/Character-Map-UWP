#include "pch.h"
#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include "ColrTableReader.h"

using namespace CharacterMapCX;

ColorTextAnalyzer::ColorTextAnalyzer(
	ComPtr<ID2D1Factory> d2dFactory,
	ComPtr<IDWriteFactory4> dWriteFactory,
	ComPtr<ID2D1DeviceContext1> d2dContext
) :
	m_refCount(0),
	m_d2dFactory(d2dFactory),
	m_dwriteFactory(dWriteFactory),
	m_d2dDeviceContext(d2dContext)
{
	HasColorGlyphs = false;
	HasColrV0 = false;
	HasColrV1 = false;
}

ColorTextAnalyzer::~ColorTextAnalyzer()
{
	m_d2dDeviceContext = nullptr;
	m_dwriteFactory = nullptr;
	m_d2dFactory = nullptr;
}

HRESULT ColorTextAnalyzer::DrawGlyphRun(
	_In_opt_ void* clientDrawingContext,
	FLOAT baselineOriginX,
	FLOAT baselineOriginY,
	DWRITE_MEASURING_MODE measuringMode,
	_In_ DWRITE_GLYPH_RUN const* glyphRun,
	_In_ DWRITE_GLYPH_RUN_DESCRIPTION const* glyphRunDescription,
	IUnknown* clientDrawingEffect
)
{
	HRESULT hr = DWRITE_E_NOCOLOR;

	if (glyphRun != nullptr && glyphRun->fontFace != nullptr && glyphRun->glyphCount > 0)
	{
		ComPtr<IDWriteFontFace4> fontFace4;
		if (SUCCEEDED(glyphRun->fontFace->QueryInterface(__uuidof(IDWriteFontFace4), &fontFace4)))
		{
			for (UINT32 i = 0; i < glyphRun->glyphCount; ++i)
			{
				DWRITE_GLYPH_IMAGE_FORMATS formats = DWRITE_GLYPH_IMAGE_FORMATS_NONE;
				if (SUCCEEDED(fontFace4->GetGlyphImageFormats(glyphRun->glyphIndices[i], 0, UINT32_MAX, &formats)))
				{
					if ((formats & DWRITE_GLYPH_IMAGE_FORMATS_COLR) != 0)
						HasColrV0 = true;
					if ((formats & DWRITE_GLYPH_IMAGE_FORMATS_COLR_PAINT_TREE) != 0)
						HasColrV1 = true;
				}
			}
		}

		if (!HasColrV0 || !HasColrV1)
		{
			const void* tableData = nullptr;
			UINT32 tableSize = 0;
			void* tableContext = nullptr;
			BOOL exists = FALSE;
			if (SUCCEEDED(glyphRun->fontFace->TryGetFontTable(
				DWRITE_MAKE_OPENTYPE_TAG('C', 'O', 'L', 'R'),
				&tableData, &tableSize, &tableContext, &exists)) && exists)
			{
				auto reader = ref new ColrTableReader(tableData, tableSize);
				for (UINT32 i = 0; i < glyphRun->glyphCount; ++i)
				{
					if (!HasColrV0 && reader->HasGlyphColrV0(glyphRun->glyphIndices[i]))
						HasColrV0 = true;
					if (!HasColrV1 && reader->HasGlyphColrV1(glyphRun->glyphIndices[i]))
						HasColrV1 = true;
				}
				delete reader;
				glyphRun->fontFace->ReleaseFontTable(tableContext);
			}
		}
	}

	D2D1_POINT_2F baselineOrigin = D2D1::Point2F(baselineOriginX, baselineOriginY);

	DWRITE_GLYPH_IMAGE_FORMATS supportedFormats =
		DWRITE_GLYPH_IMAGE_FORMATS_TRUETYPE |
		DWRITE_GLYPH_IMAGE_FORMATS_CFF |
		DWRITE_GLYPH_IMAGE_FORMATS_COLR |
		DWRITE_GLYPH_IMAGE_FORMATS_SVG |
		DWRITE_GLYPH_IMAGE_FORMATS_PNG |
		DWRITE_GLYPH_IMAGE_FORMATS_JPEG |
		DWRITE_GLYPH_IMAGE_FORMATS_TIFF |
		DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8;

	ComPtr<IDWriteColorGlyphRunEnumerator1> glyphRunEnumerator;
	hr = m_dwriteFactory->TranslateColorGlyphRun(
		baselineOrigin,
		glyphRun,
		glyphRunDescription,
		supportedFormats,
		measuringMode,
		nullptr,
		0,
		&glyphRunEnumerator
	);

	HasColorGlyphs = (hr != DWRITE_E_NOCOLOR) || HasColrV0 || HasColrV1;

	if (hr != DWRITE_E_NOCOLOR)
	{

		for (;;)
		{
			BOOL haveRun;
			ThrowIfFailed(glyphRunEnumerator->MoveNext(&haveRun));
			if (!haveRun)
				break;

			DWRITE_COLOR_GLYPH_RUN1 const* colorRun;
			ThrowIfFailed(glyphRunEnumerator->GetCurrentRun(&colorRun));

			GlyphImageFormat format = static_cast<GlyphImageFormat>(colorRun->glyphImageFormat);
			GlyphFormats.push_back(format);

			if ((format & GlyphImageFormat::Colr) == GlyphImageFormat::Colr)
			{
				HasColrV0 = true;
			}

			if (IsCharacterAnalysisMode)
			{
				RunColors.push_back(colorRun->runColor);
				PaletteIndices.push_back(colorRun->paletteIndex);

				std::vector<uint16> glyphIndices(colorRun->glyphRun.glyphIndices, colorRun->glyphRun.glyphIndices + colorRun->glyphRun.glyphCount);
				GlyphIndicies.push_back(std::move(glyphIndices));

				if ((format & GlyphImageFormat::Colr) == GlyphImageFormat::Colr)
				{
					GlyphLayerCount++;
				}
			}
		}
	}
	else
	{
		if (IsCharacterAnalysisMode && glyphRun != nullptr && glyphRun->glyphCount > 0)
		{
			std::vector<uint16> glyphIndices(glyphRun->glyphIndices, glyphRun->glyphIndices + glyphRun->glyphCount);
			GlyphIndicies.push_back(std::move(glyphIndices));
			PaletteIndices.push_back(0xFFFF);
		}
	}

	if (HasColrV0)
	{
		bool hasColr = false;
		for (auto f : GlyphFormats)
		{
			if ((f & GlyphImageFormat::Colr) == GlyphImageFormat::Colr)
			{
				hasColr = true;
				break;
			}
		}
		if (!hasColr)
			GlyphFormats.push_back(GlyphImageFormat::Colr);
	}

	if (HasColrV1)
	{
		bool hasColrPaintTree = false;
		for (auto f : GlyphFormats)
		{
			if ((f & GlyphImageFormat::ColrPaintTree) == GlyphImageFormat::ColrPaintTree)
			{
				hasColrPaintTree = true;
				break;
			}
		}
		if (!hasColrPaintTree)
			GlyphFormats.push_back(GlyphImageFormat::ColrPaintTree);
	}

	return S_OK;
}

IFACEMETHODIMP ColorTextAnalyzer::DrawUnderline(
	_In_opt_ void* clientDrawingContext,
	FLOAT baselineOriginX,
	FLOAT baselineOriginY,
	_In_ DWRITE_UNDERLINE const* underline,
	IUnknown* clientDrawingEffect
)
{
	// Not implemented
	return E_NOTIMPL;
}

IFACEMETHODIMP ColorTextAnalyzer::DrawStrikethrough(
	_In_opt_ void* clientDrawingContext,
	FLOAT baselineOriginX,
	FLOAT baselineOriginY,
	_In_ DWRITE_STRIKETHROUGH const* strikethrough,
	IUnknown* clientDrawingEffect
)
{
	// Not implemented
	return E_NOTIMPL;
}

IFACEMETHODIMP ColorTextAnalyzer::DrawInlineObject(
	_In_opt_ void* clientDrawingContext,
	FLOAT originX,
	FLOAT originY,
	IDWriteInlineObject* inlineObject,
	BOOL isSideways,
	BOOL isRightToLeft,
	IUnknown* clientDrawingEffect
)
{
	// Not implemented
	return E_NOTIMPL;
}

IFACEMETHODIMP_(unsigned long) ColorTextAnalyzer::AddRef()
{
	return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(unsigned long) ColorTextAnalyzer::Release()
{
	unsigned long newCount = InterlockedDecrement(&m_refCount);
	if (newCount == 0)
	{
		delete this;
		return 0;
	}

	return newCount;
}

IFACEMETHODIMP ColorTextAnalyzer::IsPixelSnappingDisabled(
	_In_opt_ void* clientDrawingContext,
	_Out_ BOOL* isDisabled
)
{
	return false;
}

IFACEMETHODIMP ColorTextAnalyzer::GetCurrentTransform(
	_In_opt_ void* clientDrawingContext,
	_Out_ DWRITE_MATRIX* transform
)
{
	m_d2dDeviceContext->GetTransform(reinterpret_cast<D2D1_MATRIX_3X2_F*>(transform));
	return S_OK;
}

IFACEMETHODIMP ColorTextAnalyzer::GetPixelsPerDip(
	_In_opt_ void* clientDrawingContext,
	_Out_ FLOAT* pixelsPerDip
)
{
	return 96;
}

IFACEMETHODIMP ColorTextAnalyzer::QueryInterface(
	IID const& riid,
	void** ppvObject
)
{
	if (__uuidof(IDWriteTextRenderer) == riid)
	{
		*ppvObject = this;
	}
	else if (__uuidof(IDWritePixelSnapping) == riid)
	{
		*ppvObject = this;
	}
	else if (__uuidof(IUnknown) == riid)
	{
		*ppvObject = this;
	}
	else
	{
		*ppvObject = nullptr;
		return E_FAIL;
	}

	this->AddRef();

	return S_OK;
}