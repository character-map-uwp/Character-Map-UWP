#include "pch.h"
#include "Interop.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include <string>
#include "DWHelpers.h"
#include "SVGGeometrySink.h"
#include "Windows.h"

using namespace Microsoft::WRL;
using namespace CharacterMapCX;
using namespace Windows::Storage::Streams;
using namespace Platform::Collections;

Interop::Interop(CanvasDevice^ device)
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

DWriteFontSet^ Interop::GetSystemFonts()
{
	ComPtr<IDWriteFontSet2> fontSet;
	ThrowIfFailed(m_dwriteFactory->GetSystemFontSet(true, &fontSet));

	ComPtr<IDWriteFontSet3> fontSet3;
	ThrowIfFailed(fontSet.As(&fontSet3));

	return GetFonts(fontSet3);
}

DWriteFontSet^ Interop::GetFonts(Uri^ uri)
{
	CanvasFontSet^ set = ref new CanvasFontSet(uri);
	ComPtr<IDWriteFontSet3> fontSet = GetWrappedResource<IDWriteFontSet3>(set);
	return GetFonts(fontSet);
}

DWriteFontSet^ Interop::GetFonts(ComPtr<IDWriteFontSet3> fontSet)
{
	auto vec = ref new Vector<DWriteFontFace^>();
	auto fontCount = fontSet->GetFontCount();

	wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
	int ls = GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH);

	int appxCount = 0;
	int cloudCount = 0;

	for (uint32_t i = 0; i < fontCount; ++i)
	{
		ComPtr<IDWriteFontFaceReference1> fontResource;
		ThrowIfFailed(fontSet->GetFontFaceReference(i, &fontResource));

		if (fontResource->GetLocality() == DWRITE_LOCALITY::DWRITE_LOCALITY_LOCAL)
		{
			auto canvasFontFace = GetOrCreate<CanvasFontFace>(fontResource.Get());
			auto properties = GetDWriteProperties(fontSet, i, fontResource, ls, localeName);
			auto fontface = ref new DWriteFontFace(canvasFontFace, properties);

			if (properties->Source == DWriteFontSource::AppxPackage)
				appxCount++;
			else if (properties->Source == DWriteFontSource::RemoteFontProvider)
				cloudCount++;

			vec->Append(fontface);
		}
	}

	return ref new DWriteFontSet(vec->GetView(), appxCount, cloudCount);
}


DWriteProperties^ Interop::GetDWriteProperties(
	ComPtr<IDWriteFontSet3>fontSet, 
	UINT index, 
	ComPtr<IDWriteFontFaceReference1> faceRef, 
	int ls, 
	wchar_t* locale)
{
	// 1. Get Font Source
	DWriteFontSource fontSource = static_cast<DWriteFontSource>(fontSet->GetFontSourceType(index));

	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);

	// 2. Attempt to get FAMILY locale index
	String^ family = nullptr;
	ComPtr<IDWriteLocalizedStrings> names;
	if (SUCCEEDED(face->GetFamilyNames(&names)))
		family = GetLocaleString(names, ls, locale);


	// 4. Attempt to get FACE locale index
	String^ fname = nullptr;
	names = nullptr;
	if (SUCCEEDED(face->GetFaceNames(&names)))
		fname = GetLocaleString(names, ls, locale);

	return ref new DWriteProperties(fontSource, nullptr, family, fname, face->IsColorFont());
}

String^ Interop::GetLocaleString(ComPtr<IDWriteLocalizedStrings> strings, int ls, wchar_t* locale)
{
	HRESULT hr = S_OK;
	UINT32 fidx = 0;
	BOOL exists = false;
	{
		if (ls)
		{
			hr = strings->FindLocaleName(locale, &fidx, &exists);
		}
		if (SUCCEEDED(hr) && !exists) // if the above find did not find a match, retry with US English
		{
			hr = strings->FindLocaleName(L"en-us", &fidx, &exists);
		}
	}

	// 3. Get FAMILY Locale string
	UINT32 length = 0;
	if (SUCCEEDED(hr))
		hr = strings->GetStringLength(fidx, &length);

	wchar_t* name = new (std::nothrow) wchar_t[length + 1];
	if (name == NULL)
		hr = E_OUTOFMEMORY;

	if (SUCCEEDED(hr))
		hr = strings->GetString(fidx, name, length + 1);

	return ref new String(name);
}

DWriteProperties^ Interop::GetDWriteProperties(CanvasFontSet^ fontSet, UINT index)
{
	// 1. Get Font Source
	ComPtr<IDWriteFontSet3> set = GetWrappedResource<IDWriteFontSet3>(fontSet);
	DWriteFontSource fontSource = static_cast<DWriteFontSource>(set->GetFontSourceType(index));

	auto fontFace = fontSet->Fonts->GetAt(index);
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);

	// 2. Get Font Provider Name
	Platform::String^ sourceName = nullptr;
	/*if (fontSource == DWriteFontSource::AppxPackage || fontSource == DWriteFontSource::RemoteFontProvider)
	{
		UINT length = set->GetFontSourceNameLength(index);
		if (length > 0)
		{
			WCHAR* buffer = new (std::nothrow) WCHAR(length + 1);
			if (set->GetFontSourceName(index, buffer, length + 1) == S_OK)
				sourceName = ref new Platform::String(buffer);
		}
	}*/

	return ref new DWriteProperties(fontSource, sourceName, nullptr, nullptr, face->IsColorFont());
}

IBuffer^ Interop::GetImageDataBuffer(CanvasFontFace^ fontFace, UINT32 pixelsPerEm, UINT unicodeIndex, UINT imageType)
{
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);
	/*IDWriteFontFace5* face5;
	face->QueryInterface(__uuidof(IDWriteFontFace5), reinterpret_cast<void**>(&face5));*/

	ComPtr<IDWriteFontFace5> face5;
	face.As(&face5);
	
	UINT16 idx = 0;
	auto arr = new UINT[1];
	arr[0] = unicodeIndex;
	auto hr3 = face5->GetGlyphIndices(arr, 1, &idx);
	delete arr;

	DWRITE_GLYPH_IMAGE_DATA data;
	void* context;
	auto formats = face5->GetGlyphImageData(idx, pixelsPerEm, static_cast<DWRITE_GLYPH_IMAGE_FORMATS>(imageType), &data, &context);

	auto b = (byte*)data.imageData;
	DataWriter^ writer = ref new DataWriter();
	writer->WriteBytes(Platform::ArrayReference<BYTE>(b, data.imageDataSize));
	IBuffer^ buffer = writer->DetachBuffer();

	face5->ReleaseGlyphImageData(context);
	delete writer;

	return buffer;
}


CanvasTextLayoutAnalysis^ Interop::AnalyzeFontLayout(CanvasTextLayout^ layout, CanvasFontFace^ fontFace)
{
	ComPtr<IDWriteTextLayout4> context = GetWrappedResource<IDWriteTextLayout4>(layout);

	ComPtr<ColorTextAnalyzer> ana = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
	context->Draw(m_d2dContext.Get(), ana.Get(), 0, 0);

	ComPtr<IDWriteFontFaceReference> fontFaceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	CanvasTextLayoutAnalysis^ analysis = ref new CanvasTextLayoutAnalysis(ana, fontFaceRef);

	fontFaceRef = nullptr;
	ana = nullptr;
	return analysis;
}

CanvasTextLayoutAnalysis^ Interop::AnalyzeCharacterLayout(CanvasTextLayout^ layout)
{
	ComPtr<IDWriteTextLayout4> context = GetWrappedResource<IDWriteTextLayout4>(layout);

	ComPtr<ColorTextAnalyzer> ana = new (std::nothrow) ColorTextAnalyzer(m_d2dFactory, m_dwriteFactory, m_d2dContext);
	ana->IsCharacterAnalysisMode = true;
	context->Draw(m_d2dContext.Get(), ana.Get(), 0, 0);

	CanvasTextLayoutAnalysis^ analysis = ref new CanvasTextLayoutAnalysis(ana, nullptr);

	ana = nullptr;
	return analysis;
}

bool Interop::HasValidFonts(Uri^ uri)
{
	/* 
	   To avoid garbage collection issues with CanvasFontSet in C# preventing us from
	   immediately deleting the StorageFile, we shall do this here in C++ 
	   */
	CanvasFontSet^ fontset = ref new CanvasFontSet(uri);
	bool valid = fontset->Fonts->Size > 0;
	delete fontset;
	return valid;
}

Platform::String^ Interop::GetPathGeometry(CanvasTextLayoutAnalysis^ analysis , CanvasFontFace^ canvasFace)
{
	return nullptr;
	/*SVGGeometrySink* sink = new SVGGeometrySink();

	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(canvasFace);
	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);
	IDWriteFontFace5* face5;
	face->QueryInterface(__uuidof(IDWriteFontFace5), reinterpret_cast<void**>(&face5));

	auto count = analysis->Indicies[0]->Size;
	uint16* items = new uint16[count];
	for (uint16 a = 0; a < count; a = a + 1)
	{
		items[a] = analysis->Indicies[0]->GetAt(a);
	}

	face5->GetGlyphRunOutline(
		120,
		items,
		nullptr,
		nullptr,
		1,
		false,
		false,
		sink);

	auto path = sink->GetPathData();
	SafeRelease(&sink);

	return path;*/
}