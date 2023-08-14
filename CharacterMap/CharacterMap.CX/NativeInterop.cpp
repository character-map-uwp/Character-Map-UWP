#pragma once
#include "pch.h"
#include "NativeInterop.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include <string>
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
	auto collection = m_fontManager->GetFontCollectionFromFile(file);
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

	ComPtr<IDWriteTextFormat3> idFormat;
	ThrowIfFailed(tempFormat.As(&idFormat));

	return idFormat;
}
