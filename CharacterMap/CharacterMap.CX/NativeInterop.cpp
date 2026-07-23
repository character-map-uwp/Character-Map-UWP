#include "pch.h"
#include "NativeInterop.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include <string>
#include <algorithm>
#include <functional>
#include "SVGGeometrySink.h"
#include "PathData.h"
#include "Windows.h"
#include <concurrent_vector.h>
#include <robuffer.h>

using namespace Microsoft::WRL;
using namespace CharacterMapCX;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;
using namespace Platform::Collections;
using namespace Windows::Foundation::Numerics;
using namespace concurrency;

NativeInterop^ NativeInterop::_Current = nullptr;

NativeInterop::NativeInterop(CanvasDevice^ device)
{
	DWriteCreateFactory(DWRITE_FACTORY_TYPE::DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory7), &m_dwriteFactory);

	// Initialize Direct2D resources.
	D2D1_FACTORY_OPTIONS options;
	ZeroMemory(&options, sizeof(D2D1_FACTORY_OPTIONS));

	D2D1CreateFactory(
		D2D1_FACTORY_TYPE_MULTI_THREADED,
		__uuidof(ID2D1Factory5),
		&options,
		&m_d2dFactory
	);

	ComPtr<ID2D1Device1> d2ddevice = GetWrappedResource<ID2D1Device1>(device);
	d2ddevice->CreateDeviceContext(
		D2D1_DEVICE_CONTEXT_OPTIONS_ENABLE_MULTITHREADED_OPTIMIZATIONS,
		&m_d2dContext);

	m_fontManager = new CustomFontManager(m_dwriteFactory);
	_Current = this;
}

NativeInterop::~NativeInterop()
{
	delete m_fontManager;
	m_fontManager = nullptr;
	if (_Current == this)
		_Current = nullptr;
}

IAsyncAction^ NativeInterop::ListenForFontSetExpirationAsync()
{
	return create_async([this]
		{
			if (m_systemFontSet != nullptr)
			{
				auto handle = m_systemFontSet->GetExpirationEvent();
				WaitForSingleObject(handle, INFINITE);

				m_isFontSetStale = true;
				FontSetInvalidated(this, nullptr);
			}
		});
}

DWriteFontSet^ NativeInterop::GetFonts(Uri^ uri)
{
	return DirectWrite::GetFonts(uri, m_dwriteFactory);
}

IVectorView<DWriteFontSet^>^ NativeInterop::GetFonts(IVectorView<Uri^>^ uris)
{
	return DirectWrite::GetFonts(uris, m_dwriteFactory);
}

DWriteFontSet^ NativeInterop::GetSystemFonts()
{
	if (m_isFontSetStale)
	{
		m_systemFontSet = nullptr;
		m_appFontSet = nullptr;
	}

	if (m_systemFontSet == nullptr || m_appFontSet == nullptr)
	{
		ComPtr<IDWriteFontSet1> fontSet;
		ComPtr<IDWriteFontCollection3> fontCollection;

		ThrowIfFailed(m_dwriteFactory->GetSystemFontCollection(true, DWRITE_FONT_FAMILY_MODEL_WEIGHT_STRETCH_STYLE, &fontCollection));
		ThrowIfFailed(fontCollection->GetFontSet(&fontSet));
		m_fontCollection = fontCollection;

		ComPtr<IDWriteFontSet3> fontSet3;
		ThrowIfFailed(fontSet.As(&fontSet3));
		m_systemFontSet = fontSet3;

        m_appFontSet = DirectWrite::GetFonts(fontCollection);
		m_isFontSetStale = false;

		// We listen for the expiration event on a background thread
		// with an infinite thread block, so don't await this.
		ListenForFontSetExpirationAsync();
	}

	return m_appFontSet;
}

IVectorView<DWriteFontSet^>^ NativeInterop::GetFonts(IVectorView<StorageFile^>^ files)
{
	Vector<DWriteFontSet^>^ fontSets = ref new Vector<DWriteFontSet^>();

	for (StorageFile^ file : files)
	{
		fontSets->Append(GetFonts(file));
	}

	return fontSets->GetView();
}

DWriteFontSet^ NativeInterop::GetFonts(StorageFile^ file)
{
	auto collection = m_fontManager->GetFontCollection(file->Path);
	DWriteFontSet^ set = DirectWrite::GetFonts(collection)->Inflate();
	return set;
}

DWriteFallbackFont^ NativeInterop::CreateEmptyFallback()
{
	ComPtr<IDWriteFontFallbackBuilder> builder;
	m_dwriteFactory->CreateFontFallbackBuilder(&builder);

	ComPtr<IDWriteFontFallback> fallback;
	builder->CreateFontFallback(&fallback);

	return ref new DWriteFallbackFont(fallback);
}

Platform::String^ NativeInterop::GetPathData(DWriteFontFace^ fontFace, UINT16 glyphIndicie)
{
	ComPtr<IDWriteFontFace3> face = fontFace->GetFontFace();

	uint16 indicies[1];
	indicies[0] = glyphIndicie;

	ComPtr<ID2D1PathGeometry> geom;
	m_d2dFactory->CreatePathGeometry(&geom);

	ComPtr<ID2D1GeometrySink> geometrySink;
	geom->Open(&geometrySink);
	
	face->GetGlyphRunOutline(
		64,
		indicies,
		nullptr,
		nullptr,
		ARRAYSIZE(indicies),
		false,
		false,
		geometrySink.Get());

	geometrySink->Close();

	ComPtr<SVGGeometrySink> sink = new (std::nothrow) SVGGeometrySink();
	geom->Stream(sink.Get());
	sink->Close();

	//delete[] indicies;
	return sink->GetPathData();
}

IVectorView<PathData^>^ NativeInterop::GetPathDatas(DWriteFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies)
{
	ComPtr<IDWriteFontFace3> face = fontFace->GetFontFace();
	Vector<PathData^>^ paths = ref new Vector<PathData^>();

	for (int i = 0; i < glyphIndicies->Length; i++)
	{
		auto ind = glyphIndicies[i];
		if (ind == 0)
			continue;

		uint16 indicies[1];
		indicies[0] = ind;

		ComPtr<ID2D1PathGeometry> geom;
		m_d2dFactory->CreatePathGeometry(&geom);

		ComPtr<ID2D1GeometrySink> geometrySink;
		geom->Open(&geometrySink);

		face->GetGlyphRunOutline(
			256,
			indicies,
			nullptr,
			nullptr,
			ARRAYSIZE(indicies),
			false,
			false,
			geometrySink.Get());

		geometrySink->Close();

		ComPtr<SVGGeometrySink> sink = new (std::nothrow) SVGGeometrySink();
		geom->Stream(sink.Get());

		D2D1_RECT_F bounds;
		geom->GetBounds(D2D1_MATRIX_3X2_F { 1, 0, 0, 1, 0, 0 }, &bounds);
		
		if (isinf(bounds.left) || isinf(bounds.top))
		{
			paths->Append(
				ref new PathData(ref new String(), Rect::Empty));
		}
		else
		{
			paths->Append(
				ref new PathData(sink->GetPathData(), Rect(bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top)));
		}

		sink->Close();

		//delete[] indicies;
		sink = nullptr;
		geometrySink = nullptr;
		geom = nullptr;
	}

	return paths->GetView();
}

static inline BYTE ClampByte(float v)
{
	if (v <= 0.0f) return 0;
	if (v >= 255.0f) return 255;
	return static_cast<BYTE>(v);
}

static inline Windows::UI::Color MakeColor(BYTE a, BYTE r, BYTE g, BYTE b)
{
	Windows::UI::Color c;
	c.A = a;
	c.R = r;
	c.G = g;
	c.B = b;
	return c;
}

static Windows::UI::Color ResolveDWriteColor(const DWRITE_PAINT_COLOR& paintColor, IDWriteFontFace3* fontFace, uint32 paletteIndex)
{
	DWRITE_COLOR_F c = paintColor.value;
	if (paintColor.colorAttributes == DWRITE_PAINT_ATTRIBUTES_USES_PALETTE ||
		(paintColor.paletteEntryIndex != 0xFFFF && paintColor.paletteEntryIndex < 0xFFFE))
	{
		if (fontFace && fontFace->GetPaletteEntryCount() > 0)
		{
			fontFace->GetPaletteEntries(paletteIndex, paintColor.paletteEntryIndex, 1, &c);
		}
	}

	float alphaMult = (paintColor.alphaMultiplier > 0.0f) ? paintColor.alphaMultiplier : 1.0f;
	BYTE r = ClampByte(c.r * 255.0f);
	BYTE g = ClampByte(c.g * 255.0f);
	BYTE b = ClampByte(c.b * 255.0f);
	BYTE a = ClampByte(c.a * alphaMult * 255.0f);
	return MakeColor(a, r, g, b);
}

struct GlyphPathResult
{
	String^ Path;
	Rect Bounds;
	bool IsEvenOdd;
};

IVectorView<ColorPathData^>^ NativeInterop::GetColorPathDatas(DWriteFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies, uint32 paletteIndex)
{
	ComPtr<IDWriteFontFace3> face3 = fontFace->GetFontFace();
	auto result = ref new Vector<ColorPathData^>();
	if (glyphIndicies == nullptr || glyphIndicies->Length == 0)
		return result->GetView();

	ComPtr<IDWriteFontFace7> face7;
	ComPtr<IDWritePaintReader> paintReader;
	bool hasPaintReader = false;
	if (SUCCEEDED(face3.As(&face7)) && face7 != nullptr)
	{
		HRESULT hrPaint = face7->CreatePaintReader(
			DWRITE_GLYPH_IMAGE_FORMATS_COLR_PAINT_TREE,
			DWRITE_PAINT_FEATURE_LEVEL_COLR_V1,
			&paintReader);
		if (SUCCEEDED(hrPaint) && paintReader != nullptr)
		{
			paintReader->SetColorPaletteIndex(paletteIndex);
			DWRITE_COLOR_F textColor = { 0.0f, 0.0f, 0.0f, 1.0f };
			paintReader->SetTextColor(textColor);
			hasPaintReader = true;
		}
	}

	const float emSize = 64.0f;

	auto transformPoint = [](const D2D1_MATRIX_3X2_F& m, float x, float y) -> D2D1_POINT_2F
	{
		return D2D1::Point2F(
			x * m.m11 + y * m.m21 + m.dx,
			x * m.m12 + y * m.m22 + m.dy
		);
	};

	auto getGlyphPathAndBounds = [this, &face3, emSize](UINT16 glyphIdx, const D2D1_MATRIX_3X2_F& transform = D2D1::Matrix3x2F::Identity()) -> GlyphPathResult
	{
		DWRITE_GLYPH_RUN layerRun = {};
		layerRun.fontFace = face3.Get();
		layerRun.fontEmSize = emSize;
		layerRun.glyphCount = 1;
		layerRun.glyphIndices = &glyphIdx;
		FLOAT adv = 0.0f;
		layerRun.glyphAdvances = &adv;

		ComPtr<ID2D1PathGeometry> geom;
		m_d2dFactory->CreatePathGeometry(&geom);
		ComPtr<ID2D1GeometrySink> sink;
		geom->Open(&sink);
		layerRun.fontFace->GetGlyphRunOutline(
			layerRun.fontEmSize,
			layerRun.glyphIndices,
			layerRun.glyphAdvances,
			nullptr,
			1,
			FALSE,
			FALSE,
			sink.Get());
		sink->Close();

		bool isIdentity = (transform.m11 == 1.0f && transform.m12 == 0.0f &&
			transform.m21 == 0.0f && transform.m22 == 1.0f &&
			transform.dx == 0.0f && transform.dy == 0.0f);

		ComPtr<ID2D1Geometry> finalGeom;
		ComPtr<ID2D1TransformedGeometry> transGeom;
		if (!isIdentity)
		{
			m_d2dFactory->CreateTransformedGeometry(geom.Get(), &transform, &transGeom);
			finalGeom = transGeom;
		}
		else
		{
			finalGeom = geom;
		}

		ComPtr<SVGGeometrySink> svgSink = new (std::nothrow) SVGGeometrySink();
		if (!isIdentity)
		{
			geom->Simplify(D2D1_GEOMETRY_SIMPLIFICATION_OPTION_CUBICS_AND_LINES, &transform, D2D1_DEFAULT_FLATTENING_TOLERANCE, svgSink.Get());
		}
		else
		{
			geom->Stream(svgSink.Get());
		}
		svgSink->Close();

		D2D1_RECT_F dBounds;
		finalGeom->GetBounds(D2D1::Matrix3x2F::Identity(), &dBounds);
		Rect boundsRect(dBounds.left, dBounds.top, dBounds.right - dBounds.left, dBounds.bottom - dBounds.top);

		GlyphPathResult res;
		res.Path = svgSink->GetPathData();
		res.Bounds = boundsRect;
		res.IsEvenOdd = (svgSink->m_fillMode == D2D1_FILL_MODE_ALTERNATE);
		return res;
	};

	for (unsigned int i = 0; i < glyphIndicies->Length; ++i)
	{
		UINT16 glyph = glyphIndicies[i];
		if (glyph == 0)
			continue;

		bool colrV1Processed = false;
		if (hasPaintReader)
		{
			DWRITE_PAINT_ELEMENT rootElement = {};
			D2D_RECT_F clipBox = {};
			DWRITE_PAINT_ATTRIBUTES attrs = {};
			HRESULT hrGlyph = paintReader->SetCurrentGlyph(glyph, &rootElement, &clipBox, &attrs);
			if (SUCCEEDED(hrGlyph) && rootElement.paintType != DWRITE_PAINT_TYPE_NONE)
			{
				UINT32 gradientCounter = 0;

				std::function<void(const DWRITE_PAINT_ELEMENT&, UINT32, const D2D1_MATRIX_3X2_F&, const D2D1_MATRIX_3X2_F&, UINT32)> processPaintElement;
				processPaintElement = [&](const DWRITE_PAINT_ELEMENT& element, UINT32 elementId, const D2D1_MATRIX_3X2_F& currentTransform, const D2D1_MATRIX_3X2_F& glyphTransform, UINT32 activeGlyphIndex)
				{
					if (element.paintType == DWRITE_PAINT_TYPE_LAYERS)
					{
						UINT32 childCount = element.paint.layers.childCount;
						DWRITE_PAINT_ELEMENT child = {};
						if (SUCCEEDED(paintReader->MoveToFirstChild(&child)))
						{
							for (UINT32 c = 0; c < childCount; ++c)
							{
								processPaintElement(child, elementId * 10 + c, currentTransform, glyphTransform, activeGlyphIndex);
								if (c + 1 < childCount)
									paintReader->MoveToNextSibling(&child);
							}
							paintReader->MoveToParent();
						}
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_TRANSFORM)
					{
						D2D1_MATRIX_3X2_F m = D2D1::Matrix3x2F(
							element.paint.transform.m11,
							element.paint.transform.m12,
							element.paint.transform.m21,
							element.paint.transform.m22,
							element.paint.transform.dx * emSize,
							element.paint.transform.dy * emSize
						);
						D2D1_MATRIX_3X2_F nextTransform = m * currentTransform;

						DWRITE_PAINT_ELEMENT child = {};
						if (SUCCEEDED(paintReader->MoveToFirstChild(&child)))
						{
							processPaintElement(child, elementId * 10 + 1, nextTransform, glyphTransform, activeGlyphIndex);
							paintReader->MoveToParent();
						}
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_COLOR_GLYPH)
					{
						UINT32 colorGlyphIdx = element.paint.colorGlyph.glyphIndex;
						DWRITE_PAINT_ELEMENT child = {};
						if (SUCCEEDED(paintReader->MoveToFirstChild(&child)))
						{
							processPaintElement(child, elementId * 10 + 1, currentTransform, currentTransform, colorGlyphIdx);
							paintReader->MoveToParent();
						}
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_COMPOSITE)
					{
						DWRITE_PAINT_ELEMENT child = {};
						if (SUCCEEDED(paintReader->MoveToFirstChild(&child)))
						{
							processPaintElement(child, elementId * 10 + 1, currentTransform, glyphTransform, activeGlyphIndex);
							if (SUCCEEDED(paintReader->MoveToNextSibling(&child)))
							{
								processPaintElement(child, elementId * 10 + 2, currentTransform, glyphTransform, activeGlyphIndex);
							}
							paintReader->MoveToParent();
						}
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_SOLID_GLYPH)
					{
						UINT16 gIdx = static_cast<UINT16>(element.paint.solidGlyph.glyphIndex);
						auto gb = getGlyphPathAndBounds(gIdx, currentTransform);

						auto cp = ref new ColorPathData();
						cp->Path = gb.Path;
						cp->Bounds = gb.Bounds;
						cp->FillRuleEvenOdd = gb.IsEvenOdd;
						cp->Color = ResolveDWriteColor(element.paint.solidGlyph.color, face3.Get(), paletteIndex);
						result->Append(cp);
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_GLYPH)
					{
						UINT32 gIdx = element.paint.glyph.glyphIndex;
						DWRITE_PAINT_ELEMENT child = {};
						if (SUCCEEDED(paintReader->MoveToFirstChild(&child)))
						{
							processPaintElement(child, elementId * 10 + 1, currentTransform, currentTransform, gIdx);
							paintReader->MoveToParent();
						}
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_SOLID)
					{
						if (activeGlyphIndex != 0)
						{
							auto gb = getGlyphPathAndBounds(static_cast<UINT16>(activeGlyphIndex), glyphTransform);

							auto cp = ref new ColorPathData();
							cp->Path = gb.Path;
							cp->Bounds = gb.Bounds;
							cp->FillRuleEvenOdd = gb.IsEvenOdd;
							cp->Color = ResolveDWriteColor(element.paint.solid, face3.Get(), paletteIndex);
							result->Append(cp);
						}
					}
					else if (element.paintType == DWRITE_PAINT_TYPE_LINEAR_GRADIENT ||
						element.paintType == DWRITE_PAINT_TYPE_RADIAL_GRADIENT ||
						element.paintType == DWRITE_PAINT_TYPE_SWEEP_GRADIENT)
					{
						if (activeGlyphIndex != 0)
						{
							auto gb = getGlyphPathAndBounds(static_cast<UINT16>(activeGlyphIndex), glyphTransform);

							auto cp = ref new ColorPathData();
							cp->Path = gb.Path;
							cp->Bounds = gb.Bounds;
							cp->FillRuleEvenOdd = gb.IsEvenOdd;

							UINT32 stopCount = (element.paintType == DWRITE_PAINT_TYPE_LINEAR_GRADIENT) ? element.paint.linearGradient.gradientStopCount :
								(element.paintType == DWRITE_PAINT_TYPE_RADIAL_GRADIENT) ? element.paint.radialGradient.gradientStopCount :
								element.paint.sweepGradient.gradientStopCount;

							std::vector<D2D1_GRADIENT_STOP> d2dStops(stopCount);
							std::vector<DWRITE_PAINT_COLOR> stopColors(stopCount);
							if (stopCount > 0)
							{
								paintReader->GetGradientStops(0, stopCount, d2dStops.data());
								paintReader->GetGradientStopColors(0, stopCount, stopColors.data());
							}

							wchar_t gradIdBuf[64];
							swprintf_s(gradIdBuf, L"grad_%u_%u", glyph, ++gradientCounter);
							String^ gradId = ref new String(gradIdBuf);

							std::wstring stopsXml = L"";
							for (UINT32 s = 0; s < stopCount; ++s)
							{
								Windows::UI::Color sc = ResolveDWriteColor(stopColors[s], face3.Get(), paletteIndex);
								wchar_t stopBuf[256];
								if (sc.A == 255)
								{
									swprintf_s(stopBuf, L"<stop offset=\"%.2f\" stop-color=\"#%02X%02X%02X\" />",
										d2dStops[s].position, sc.R, sc.G, sc.B);
								}
								else
								{
									double opacity = sc.A / 255.0;
									swprintf_s(stopBuf, L"<stop offset=\"%.2f\" stop-color=\"#%02X%02X%02X\" stop-opacity=\"%.2f\" />",
										d2dStops[s].position, sc.R, sc.G, sc.B, opacity);
								}
								stopsXml += stopBuf;
							}

							cp->IsGradient = true;
							cp->PaintReference = L"url(#" + gradId + L")";

							wchar_t gradDefBuf[1024];
							if (element.paintType == DWRITE_PAINT_TYPE_LINEAR_GRADIENT)
							{
								D2D1_POINT_2F p0 = transformPoint(currentTransform, element.paint.linearGradient.x0 * emSize, -element.paint.linearGradient.y0 * emSize);
								D2D1_POINT_2F p1 = transformPoint(currentTransform, element.paint.linearGradient.x1 * emSize, -element.paint.linearGradient.y1 * emSize);

								swprintf_s(gradDefBuf, L"<linearGradient id=\"%s\" gradientUnits=\"userSpaceOnUse\" x1=\"%.2f\" y1=\"%.2f\" x2=\"%.2f\" y2=\"%.2f\">%s</linearGradient>",
									gradId->Data(),
									p0.x,
									p0.y,
									p1.x,
									p1.y,
									stopsXml.c_str());
							}
							else if (element.paintType == DWRITE_PAINT_TYPE_RADIAL_GRADIENT)
							{
								D2D1_POINT_2F c0 = transformPoint(currentTransform, element.paint.radialGradient.x0 * emSize, -element.paint.radialGradient.y0 * emSize);
								D2D1_POINT_2F c1 = transformPoint(currentTransform, element.paint.radialGradient.x1 * emSize, -element.paint.radialGradient.y1 * emSize);
								float scaleFactor = sqrtf(currentTransform.m11 * currentTransform.m11 + currentTransform.m12 * currentTransform.m12);
								if (scaleFactor <= 0.0001f) scaleFactor = 1.0f;
								float r0 = element.paint.radialGradient.radius0 * emSize * scaleFactor;
								float r1 = element.paint.radialGradient.radius1 * emSize * scaleFactor;

								if (r0 > 0.0f)
								{
									swprintf_s(gradDefBuf, L"<radialGradient id=\"%s\" gradientUnits=\"userSpaceOnUse\" cx=\"%.2f\" cy=\"%.2f\" r=\"%.2f\" fx=\"%.2f\" fy=\"%.2f\" fr=\"%.2f\">%s</radialGradient>",
										gradId->Data(),
										c1.x,
										c1.y,
										r1,
										c0.x,
										c0.y,
										r0,
										stopsXml.c_str());
								}
								else
								{
									swprintf_s(gradDefBuf, L"<radialGradient id=\"%s\" gradientUnits=\"userSpaceOnUse\" cx=\"%.2f\" cy=\"%.2f\" r=\"%.2f\" fx=\"%.2f\" fy=\"%.2f\">%s</radialGradient>",
										gradId->Data(),
										c1.x,
										c1.y,
										r1,
										c0.x,
										c0.y,
										stopsXml.c_str());
								}
							}
							else
							{
								D2D1_POINT_2F centerP = transformPoint(currentTransform, element.paint.sweepGradient.centerX * emSize, -element.paint.sweepGradient.centerY * emSize);
								swprintf_s(gradDefBuf, L"<radialGradient id=\"%s\" gradientUnits=\"userSpaceOnUse\" cx=\"%.2f\" cy=\"%.2f\" r=\"%.2f\">%s</radialGradient>",
									gradId->Data(),
									centerP.x,
									centerP.y,
									32.0f,
									stopsXml.c_str());
							}
							cp->PaintDefinition = ref new String(gradDefBuf);
							result->Append(cp);
						}
					}
				};

				processPaintElement(rootElement, 1, D2D1::Matrix3x2F::Identity(), D2D1::Matrix3x2F::Identity(), 0);
				colrV1Processed = true;
			}
		}

		if (!colrV1Processed)
		{
			// COLRv0 fallback
			DWRITE_GLYPH_RUN glyphRun = {};
			glyphRun.fontFace = face3.Get();
			glyphRun.fontEmSize = 64.0f;
			glyphRun.glyphCount = 1;
			glyphRun.glyphIndices = &glyph;
			FLOAT advance = 0.0f;
			glyphRun.glyphAdvances = &advance;

			D2D1_POINT_2F baselineOrigin = D2D1::Point2F(0, 0);
			DWRITE_GLYPH_IMAGE_FORMATS supportedFormats =
				DWRITE_GLYPH_IMAGE_FORMATS_TRUETYPE |
				DWRITE_GLYPH_IMAGE_FORMATS_CFF |
				DWRITE_GLYPH_IMAGE_FORMATS_COLR;

			ComPtr<IDWriteColorGlyphRunEnumerator1> enumerator;
			HRESULT hr = m_dwriteFactory->TranslateColorGlyphRun(
				baselineOrigin,
				&glyphRun,
				nullptr,
				supportedFormats,
				DWRITE_MEASURING_MODE_NATURAL,
				nullptr,
				0,
				&enumerator);

			if (SUCCEEDED(hr) && enumerator != nullptr)
			{
				BOOL haveRun = FALSE;
				while (SUCCEEDED(enumerator->MoveNext(&haveRun)) && haveRun)
				{
					DWRITE_COLOR_GLYPH_RUN1 const* colorRun = nullptr;
					if (FAILED(enumerator->GetCurrentRun(&colorRun)) || !colorRun)
						continue;

					for (UINT32 k = 0; k < colorRun->glyphRun.glyphCount; ++k)
					{
						UINT16 layerGlyph = colorRun->glyphRun.glyphIndices[k];
						auto gb = getGlyphPathAndBounds(layerGlyph);

						Windows::UI::Color layerColor = MakeColor(255, 0, 0, 0);
						if (colorRun->runColor.a > 0.0f)
						{
							BYTE r = ClampByte(colorRun->runColor.r * 255.0f);
							BYTE g = ClampByte(colorRun->runColor.g * 255.0f);
							BYTE b = ClampByte(colorRun->runColor.b * 255.0f);
							BYTE a = ClampByte(colorRun->runColor.a * 255.0f);
							layerColor = MakeColor(a, r, g, b);
						}
						else if (colorRun->paletteIndex != 0xFFFF && face3->GetPaletteEntryCount() > 0)
						{
							DWRITE_COLOR_F palColor = {};
							if (SUCCEEDED(face3->GetPaletteEntries(paletteIndex, colorRun->paletteIndex, 1, &palColor)))
							{
								BYTE r = ClampByte(palColor.r * 255.0f);
								BYTE g = ClampByte(palColor.g * 255.0f);
								BYTE b = ClampByte(palColor.b * 255.0f);
								BYTE a = ClampByte(palColor.a * 255.0f);
								layerColor = MakeColor(a, r, g, b);
							}
						}

						auto cp = ref new ColorPathData();
						cp->Path = gb.Path;
						cp->Color = layerColor;
						cp->Bounds = gb.Bounds;
						cp->FillRuleEvenOdd = gb.IsEvenOdd;
						result->Append(cp);
					}
				}
			}
			else
			{
				auto monoPath = GetPathDatas(fontFace, glyphIndicies);
				for (auto pd : monoPath)
				{
					auto cp = ref new ColorPathData();
					cp->Path = pd->Path;
					cp->Color = MakeColor(255, 0, 0, 0);
					cp->Bounds = pd->Bounds;
					result->Append(cp);
				}
			}
		}
	}
	return result->GetView();
}

IVectorView<ColorPathData^>^ NativeInterop::GetColorPathDatas(DWriteFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies)
{
	return GetColorPathDatas(fontFace, glyphIndicies, 0);
}

PathData^ NativeInterop::GetPathData(CanvasGeometry^ geometry)
{
	ComPtr<ID2D1Geometry> geom = GetWrappedResource<ID2D1Geometry>(geometry);
	ComPtr<SVGGeometrySink> sink = new (std::nothrow) SVGGeometrySink();

	D2D1_MATRIX_3X2_F matrix = D2D1::Matrix3x2F::Identity();
	ComPtr<ID2D1PathGeometry> pathGeom;

	ComPtr<ID2D1GeometryGroup> groupGeom;
	if (SUCCEEDED(geom.As(&groupGeom)))
	{
		UINT32 count = groupGeom->GetSourceGeometryCount();
		if (count > 0)
		{
			ComPtr<ID2D1Geometry> g;
			groupGeom->GetSourceGeometries(&g, 1);

			ComPtr<ID2D1TransformedGeometry> t;
			if (SUCCEEDED(g.As(&t)))
			{
				t->GetTransform(&matrix);
				ComPtr<ID2D1Geometry> s;
				t->GetSourceGeometry(&s);
				s.As(&pathGeom);
			}
			else
			{
				g.As(&pathGeom);
			}
		}
	}
	else
	{
		ComPtr<ID2D1TransformedGeometry> t;
		if (SUCCEEDED(geom.As(&t)))
		{
			t->GetTransform(&matrix);
			ComPtr<ID2D1Geometry> s;
			t->GetSourceGeometry(&s);
			s.As(&pathGeom);
		}
		else
		{
			geom.As(&pathGeom);
		}
	}

	if (pathGeom != nullptr)
	{
		sink->SetOffset(matrix.dx, matrix.dy);
		matrix.dx = 0;
		matrix.dy = 0;
		pathGeom->Stream(sink.Get());
	}
	else if (geom != nullptr)
	{
		geom->Simplify(D2D1_GEOMETRY_SIMPLIFICATION_OPTION::D2D1_GEOMETRY_SIMPLIFICATION_OPTION_CUBICS_AND_LINES, 
			&matrix, 
			sink.Get());
	}

	auto m = static_cast<D2D1::Matrix3x2F*>(&matrix);
	auto data = ref new PathData(sink->GetPathData(), m);
	sink->Close();

	return data;
}

CanvasTextLayoutAnalysis^ NativeInterop::AnalyzeCharacterLayout(CanvasTextLayout^ layout)
{
	ComPtr<IDWriteTextLayout4> context = GetWrappedResource<IDWriteTextLayout4>(layout);

	ComPtr<ColorTextAnalyzer> ana = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
	ana->IsCharacterAnalysisMode = true;
	context->Draw(m_d2dContext.Get(), ana.Get(), 0, 0);

	CanvasTextLayoutAnalysis^ analysis = ref new CanvasTextLayoutAnalysis(ana, nullptr);

	ana = nullptr;
	return analysis;
}

CanvasTextLayoutAnalysis^ NativeInterop::AnalyzeGlyphLayout(DWriteFontFace^ fontFace, UINT16 glyphIndex)
{
	ComPtr<IDWriteFontFace3> face = fontFace->GetFontFace();

	ComPtr<ColorTextAnalyzer> ana = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
	ana->IsCharacterAnalysisMode = true;

	DWRITE_GLYPH_RUN glyphRun = {};
	glyphRun.fontFace = face.Get();
	glyphRun.fontEmSize = 64;
	glyphRun.glyphCount = 1;

	UINT16 indices[1] = { glyphIndex };
	glyphRun.glyphIndices = indices;

	FLOAT advances[1] = { 0 };
	glyphRun.glyphAdvances = advances;

	ana->DrawGlyphRun(nullptr, 0, 0, DWRITE_MEASURING_MODE_NATURAL, &glyphRun, nullptr, nullptr);

	CanvasTextLayoutAnalysis^ analysis = ref new CanvasTextLayoutAnalysis(ana, nullptr);

	ana = nullptr;
	return analysis;
}

byte* GetPointerToPixelData(IBuffer^ pixelBuffer, unsigned int* length)
{
	if (length != nullptr)
	{
		*length = pixelBuffer->Length;
	}
	// Query the IBufferByteAccess interface.  
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(pixelBuffer)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));

	// Retrieve the buffer data.  
	byte* pixels = nullptr;
	bufferByteAccess->Buffer(&pixels);
	return pixels;
}

IAsyncOperation<bool>^ NativeInterop::UnpackWOFF2Async(IBuffer^ buffer, IOutputStream^ stream)
{
	// 1. Unpack the WOFF2 data
	unsigned int length;
	auto bytes = GetPointerToPixelData(buffer, &length);
	ComPtr<IDWriteFactory7> factory = m_fontManager->GetIsolatedFactory();
	ComPtr<IDWriteFontFileStream> fileStream;
	auto result = factory->UnpackFontFile(DWRITE_CONTAINER_TYPE_WOFF2, bytes, length, &fileStream);

	if (result != S_OK)
		return create_async([] { return task_from_result(false); });
	else
		return DirectWrite::SaveFontStreamAsync(fileStream, stream);
}

CanvasTextFormat^ CharacterMapCX::NativeInterop::CreateTextFormat(DWriteFontFace^ fontFace, FontWeight weight, FontStyle style, FontStretch stretch, float fontSize)
{
	ComPtr<IDWriteTextFormat3> idFormat = CreateIDWriteTextFormat(fontFace, weight, style, stretch, fontSize);
	return GetOrCreate<CanvasTextFormat>(idFormat.Get());
}

ComPtr<IDWriteTextFormat3> CharacterMapCX::NativeInterop::CreateIDWriteTextFormat(DWriteFontFace^ fontFace, FontWeight weight, FontStyle style, FontStretch stretch, float fontSize)
{
	ComPtr<IDWriteTextFormat> tempFormat;
	m_dwriteFactory->CreateTextFormat(
		fontFace->Properties->FamilyName->Data(),
		fontFace->GetFontCollection().Get(),
		static_cast<DWRITE_FONT_WEIGHT>(weight.Weight),
		static_cast<DWRITE_FONT_STYLE>(style),
		static_cast<DWRITE_FONT_STRETCH>(stretch),
		fontSize,
		L"en-us",
		&tempFormat);

	tempFormat->SetFlowDirection(DWRITE_FLOW_DIRECTION_TOP_TO_BOTTOM);
	tempFormat->SetReadingDirection(DWRITE_READING_DIRECTION_LEFT_TO_RIGHT);

	ComPtr<IDWriteTextFormat3> idFormat;
	ThrowIfFailed(tempFormat.As(&idFormat));
	return idFormat;
}
