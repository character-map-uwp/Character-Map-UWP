#include "pch.h"
#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"

using namespace Microsoft::WRL;
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

	GlyphFormats.resize(0);

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

	HasColorGlyphs = hr != DWRITE_E_NOCOLOR;

	if (HasColorGlyphs)
	{
		for (;;)
		{
			BOOL haveRun;
			ThrowIfFailed(glyphRunEnumerator->MoveNext(&haveRun));
			if (!haveRun)
				break;

			DWRITE_COLOR_GLYPH_RUN1 const* colorRun;
			ThrowIfFailed(glyphRunEnumerator->GetCurrentRun(&colorRun));

			GlyphFormats.push_back(static_cast<GlyphImageFormat>(colorRun->glyphImageFormat));
		}
	}

	return hr;
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