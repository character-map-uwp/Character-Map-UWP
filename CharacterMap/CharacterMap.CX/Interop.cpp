#include "pch.h"
#include "Interop.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include <string>
#include "DWHelpers.h"
#include "SVGGeometrySink.h"
#include "PathData.h"
#include "Windows.h"
#include <concurrent_vector.h>

using namespace Microsoft::WRL;
using namespace CharacterMapCX;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;
using namespace Platform::Collections;
using namespace Windows::Foundation::Numerics;
using namespace concurrency;


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

IAsyncAction^ Interop::ListenForFontSetExpirationAsync()
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

DWriteFontSet^ Interop::GetSystemFonts()
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
		m_appFontSet = GetFonts(fontSet3);
		m_isFontSetStale = false;

		// We listen for the expiration event on a background thread
		// with an infinite thread block, so don't await this.
		ListenForFontSetExpirationAsync();
	}

	return m_appFontSet;
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
			auto properties = GetDWriteProperties(fontSet, i, fontResource, ls, localeName);

			// Some cloud providers, like Microsoft Office, can cause issues with the underlying
			// DirectWrite system when they are open. This can cause us to be unable to create
			// a IDWriteFontFace3 from certain fonts, also leading us to not be able to get the
			// properties. Nothing we can do except *don't* crash.
			if (properties != nullptr) 
			{
				auto canvasFontFace = GetOrCreate<CanvasFontFace>(fontResource.Get());
				auto fontface = ref new DWriteFontFace(canvasFontFace, properties);

				if (properties->Source == DWriteFontSource::AppxPackage)
					appxCount++;
				else if (properties->Source == DWriteFontSource::RemoteFontProvider)
					cloudCount++;

				vec->Append(fontface);
			}
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

	// The following is known to fail if Microsoft Office with cloud fonts
	// is currently running on the users system. 
	ComPtr<IDWriteFontFace3> face;
	if (faceRef->CreateFontFace(&face) == S_OK)
	{
		// 2. Attempt to get FAMILY locale index
		String^ family = nullptr;
		ComPtr<IDWriteLocalizedStrings> names;
		if (SUCCEEDED(face->GetFamilyNames(&names)))
			family = GetLocaleString(names, ls, locale);

		// 3. Attempt to get FACE locale index
		String^ fname = nullptr;
		names = nullptr;
		if (SUCCEEDED(face->GetFaceNames(&names)))
			fname = GetLocaleString(names, ls, locale);

		return ref new DWriteProperties(fontSource, nullptr, family, fname, face->IsColorFont());
	};

	return nullptr;
}

String^ Interop::GetLocaleString(ComPtr<IDWriteLocalizedStrings> strings, int ls, wchar_t* locale)
{
	HRESULT hr = S_OK;
	UINT32 fidx = 0;
	BOOL exists = false;

	if (ls)
		hr = strings->FindLocaleName(locale, &fidx, &exists);

	if (SUCCEEDED(hr) && !exists) // if the above find did not find a match, retry with US English
		hr = strings->FindLocaleName(L"en-us", &fidx, &exists);

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

Platform::String^ Interop::GetPathData(CanvasFontFace^ fontFace, UINT16 glyphIndicie)
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

IVectorView<PathData^>^ Interop::GetPathDatas(CanvasFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies)
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

PathData^ Interop::GetPathData(CanvasGeometry^ geometry)
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

	p->Stream(sink.Get());

	auto data = ref new PathData(sink->GetPathData(), m);
	sink->Close();

	return data;
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

IAsyncOperation<bool>^ Interop::WriteToFileAsync(CanvasFontFace^ fontFace, StorageFile^ storageFile)
{
	return create_async([fontFace, storageFile]
		{
			// 1. Acquire the underlying FontLoader used to create the CanvasFontFace
			ComPtr<IDWriteFontFaceReference> fontFaceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);

			ComPtr<IDWriteFontFile> file;
			if (fontFaceRef->GetFontFile(&file) != S_OK)
				return task_from_result(false);

			ComPtr<IDWriteFontFileLoader> loader;
			if (file->GetLoader(&loader) != S_OK)
				return task_from_result(false);

			ComPtr<IDWriteLocalFontFileLoader> localLoader;
			if (loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK)
			{
				// 2. Get the font stream from the loader
				ComPtr<IDWriteFontFileStream> fileStream;

				const void* refKey = nullptr;
				uint32 size = 0;
				if (file->GetReferenceKey(&refKey, &size) == S_OK)
				{
					localLoader->CreateStreamFromKey(refKey, size, &fileStream);

					void* context;
					const void* fragment;

					// 3. Read the source file into memory
					fileStream->ReadFileFragment(&fragment, 0, fontFaceRef->GetFileSize(), &context);
					auto b = (byte*)fragment;

					// 4. Write the memory to file.
					return create_task(storageFile->OpenAsync(FileAccessMode::ReadWrite)).then([storageFile, b, fontFaceRef](IRandomAccessStream^ stream)
						{
							stream->Size = 0;
							DataWriter^ w = ref new DataWriter(stream->GetOutputStreamAt(0));
							w->WriteBytes(Platform::ArrayReference<BYTE>(b, fontFaceRef->GetFileSize()));
							
							return create_task(w->StoreAsync()).then([](bool result)
								{
									return task_from_result(result);
								}, task_continuation_context::use_arbitrary());

						}, task_continuation_context::use_arbitrary());
				}
			}

			return task_from_result(false);
		});
}

FontFileData^ Interop::GetFileBuffer(CanvasFontFace^ fontFace)
{
	// 1. Acquire the underlying FontLoader used to create the CanvasFontFace
	ComPtr<IDWriteFontFaceReference> fontFaceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);

	ComPtr<IDWriteFontFile> file;
	ComPtr<IDWriteFontFileLoader> loader;
	ComPtr<IDWriteLocalFontFileLoader> localLoader;
	if (fontFaceRef->GetFontFile(&file) == S_OK
		&& file->GetLoader(&loader) == S_OK
		&& loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK)
	{
		// 2. Get the font stream from the loader
		ComPtr<IDWriteFontFileStream> fileStream;

		const void* refKey = nullptr;
		uint32 size = 0;
		if (file->GetReferenceKey(&refKey, &size) == S_OK)
		{
			localLoader->CreateStreamFromKey(refKey, size, &fileStream);
			auto fileSize = fontFaceRef->GetFileSize();
			void* context;
			const void* fragment;

			// 3. Read the source file into memory
			fileStream->ReadFileFragment(&fragment, 0, fileSize, &context);
			auto b = (byte*)fragment;

			DataWriter^ writer = ref new DataWriter();
			writer->WriteBytes(Platform::ArrayReference<BYTE>(b, fileSize));
			IBuffer^ buffer = writer->DetachBuffer();

			delete writer;
			fileStream->Release();

			// 4. Try to get the file name
			Platform::String^ name;
			UINT filePathSize = 0;
			UINT filePathLength = 0;
			if (localLoader->GetFilePathLengthFromKey(refKey, size, &filePathLength) == S_OK)
			{
				wchar_t* namebuffer = new (std::nothrow) wchar_t[filePathLength + 1];
				if (localLoader->GetFilePathFromKey(refKey, size, namebuffer, filePathLength + 1) == S_OK)
				{
					name = ref new Platform::String(namebuffer);
				}
			}

			return ref new FontFileData(buffer, name);
		}
	}

	return nullptr;
}
