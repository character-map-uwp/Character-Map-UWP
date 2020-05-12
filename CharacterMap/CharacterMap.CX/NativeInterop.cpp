#include "pch.h"
#include "NativeInterop.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include <string>
#include "SVGGeometrySink.h"
#include "PathData.h"
#include "Windows.h"
#include <concurrent_vector.h>
#include "DWriteFontAxis.h"

using namespace Microsoft::WRL;
using namespace CharacterMapCX;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;
using namespace Platform::Collections;
using namespace Windows::Foundation::Numerics;
using namespace concurrency;


NativeInterop::NativeInterop(CanvasDevice^ device)
{
	DWriteCreateFactory(DWRITE_FACTORY_TYPE::DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory7), &m_dwriteFactory);

	// Initialize Direct2D resources.
	D2D1_FACTORY_OPTIONS options;
	ZeroMemory(&options, sizeof(D2D1_FACTORY_OPTIONS));

	D2D1CreateFactory(
		D2D1_FACTORY_TYPE_SINGLE_THREADED,
		__uuidof(ID2D1Factory5),
		&options,
		&m_d2dFactory
	);

	ComPtr<ID2D1Device1> d2ddevice = GetWrappedResource<ID2D1Device1>(device);
	d2ddevice->CreateDeviceContext(
		D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
		&m_d2dContext);
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

DWriteFontSet^ NativeInterop::GetSystemFonts()
{
	if (m_isFontSetStale)
	{
		m_systemFontSet = nullptr;
		m_appFontSet = nullptr;
	}

	if (m_systemFontSet == nullptr || m_appFontSet == nullptr)
	{
		ComPtr<IDWriteFontSet2> fontSet;
		ThrowIfFailed(m_dwriteFactory->GetSystemFontSet(true, &fontSet));

		ComPtr<IDWriteFontSet3> fontSet3;
		ThrowIfFailed(fontSet.As(&fontSet3));
		m_systemFontSet = fontSet3;
		m_appFontSet = DirectWrite::GetFonts(fontSet3);
		m_isFontSetStale = false;

		// We listen for the expiration event on a background thread
		// with an infinite thread block, so don't await this.
		ListenForFontSetExpirationAsync();
	}

	return m_appFontSet;
}

DWriteFallbackFont^ NativeInterop::CreateEmptyFallback()
{
	ComPtr<IDWriteFontFallbackBuilder> builder;
	m_dwriteFactory->CreateFontFallbackBuilder(&builder);

	ComPtr<IDWriteFontFallback> fallback;
	builder->CreateFontFallback(&fallback);

	return ref new DWriteFallbackFont(fallback);
}


Platform::String^ NativeInterop::GetPathData(CanvasFontFace^ fontFace, UINT16 glyphIndicie)
{
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);

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

	return sink->GetPathData();
}

IVectorView<PathData^>^ NativeInterop::GetPathDatas(CanvasFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies)
{
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);

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

		sink = nullptr;
		geometrySink = nullptr;
		geom = nullptr;
	}

	return paths->GetView();
}

PathData^ NativeInterop::GetPathData(CanvasGeometry^ geometry)
{
	ComPtr<ID2D1GeometryGroup> geom = GetWrappedResource<ID2D1GeometryGroup>(geometry);
	ComPtr<SVGGeometrySink> sink = new (std::nothrow) SVGGeometrySink();

	ComPtr<ID2D1Geometry> g;
	geom->GetSourceGeometries(&g, 1);

	ComPtr<ID2D1TransformedGeometry> t;
	g.As<ID2D1TransformedGeometry>(&t);

	D2D1_MATRIX_3X2_F matrix;
	t->GetTransform(&matrix);
	auto m = static_cast<D2D1::Matrix3x2F*>(&matrix);
	
	ComPtr<ID2D1Geometry> s;
	t->GetSourceGeometry(&s);

	ComPtr<ID2D1PathGeometry> p;
	s.As<ID2D1PathGeometry>(&p);

	sink->SetOffset(m->dx, m->dy);
	m->dx = 0;
	m->dy = 0;

	p->Stream(sink.Get());

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