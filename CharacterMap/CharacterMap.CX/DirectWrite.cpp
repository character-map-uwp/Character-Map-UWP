#pragma once
#include "pch.h"
#include "CanvasTextLayoutAnalysis.h"
#include "GsubTableReader.h"


#include "DWriteNamedFontAxisValue.h"
#include "DWriteKnownFontAxisValues.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;
using namespace Platform::Collections;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;
using namespace concurrency;

CanvasFontSet^ DirectWrite::CreateFontSet(String^ path)
{
	/* 
		Sometimes creating a CanvasFontSet directly in Win2D
		throws error:
		"The font URI specified is not a valid application URI that 
		can be opened by StorageFile.GetFileFromApplicationUriAsync"
		So, we create an IDWriteFontSet directly and cast it to CanvasFontSet;
	*/

	auto customFontManager = CustomFontManager::GetInstance();

	auto fontCollection = customFontManager->GetFontCollection(path);

	if (!fontCollection)
		ThrowHR(E_INVALIDARG);

	ComPtr<IDWriteFontSet> dwFontSet;
	ThrowIfFailed(fontCollection->GetFontSet(&dwFontSet));

	CanvasFontSet^ fontSet = GetOrCreate<CanvasFontSet>(dwFontSet.Get());
	return fontSet;
}

ComPtr<IDWriteFontSet> DirectWrite::CreateIDWriteFontSet(String^ path)
{
	auto customFontManager = CustomFontManager::GetInstance();
	auto fontCollection = customFontManager->GetFontCollection(path);

	if (!fontCollection)
		ThrowHR(E_INVALIDARG);

	ComPtr<IDWriteFontSet> dwFontSet;
	ThrowIfFailed(fontCollection->GetFontSet(&dwFontSet));

	return dwFontSet;
}

String^ DirectWrite::GetTagName(UINT32 tag)
{
	return GetTagName(GetFeatureTag(tag));
}

String^ DirectWrite::GetTagName(String^ tag)
{
	/* Variation Tags */
	if (tag == "wght") return "Weight";
	else if (tag == "slnt") return "Slant";
	else if (tag == "CONT") return "Contrast";
	else if (tag == "MIDL") return "Midline";
	else if (tag == "wdth") return "Width";

	/* OpenType feature Tags */
	/* Only a subset of common tags are identified here */
	/* TODO: Implement this better, including details of rendering 
	         properties for tags, like is it for single characters
			 or glyph runs, editable, etc.
    */
	else if (tag == "aalt") return "Access All Alternates";
	else if (tag == "abvf") return "Above-base Forms";
	else if (tag == "abvm") return "Above-base Mark Positioning";
	else if (tag == "abvs") return "Above-base Substitutions";
	else if (tag == "akhn") return "Akhand";
	else if (tag == "blwf") return "Below-base Forms";
	else if (tag == "blwm") return "Below-base Mark Positioning";
	else if (tag == "blws") return "Below-base Substitutions";
	else if (tag == "cfar") return "Conjunct Form After Ro";
	else if (tag == "cjct") return "Conjunct Forms";
	else if (tag == "dist") return "Distances";
	else if (tag == "dpng") return "Diphthongs";
	else if (tag == "dnom") return "Denominators";
	else if (tag == "falt") return "Final Glyph on Line Alternates";
	else if (tag == "fin2") return "Terminal Form #2";
	else if (tag == "fin3") return "Terminal Form #3";
	else if (tag == "fina") return "Terminal Forms";
	else if (tag == "init") return "Initial Forms";
	else if (tag == "isol") return "Isolated Forms";
	else if (tag == "ital") return "Italics";
	else if (tag == "ljmo") return "Leading Jamo Forms";
	else if (tag == "mark") return "Mark Positioning";
	else if (tag == "med2") return "Medial Forms #2";
	else if (tag == "medi") return "Medial Forms";
	else if (tag == "nukt") return "Nukta Forms";
	else if (tag == "numr") return "Numerators";
	else if (tag == "opsz") return "Optical size";
	else if (tag == "ornm") return "Ornaments";
	else if (tag == "pkna") return "Proportional Kana";
	else if (tag == "pref") return "Pre-base Forms";
	else if (tag == "pres") return "Pre-base Substitutions";
	else if (tag == "pstf") return "Post-base Forms";
	else if (tag == "psts") return "Post-base Substitutions";
	else if (tag == "rclt") return "Required Contextual Alternates";
	else if (tag == "rkrf") return "Rakar Forms";
	else if (tag == "rphf") return "Reph Form";
	else if (tag == "rtlm") return "Right-to-left mirrored forms";
	else if (tag == "rvrn") return "Required Variation Alternates";
	else if (tag == "size") return "Optical size";
	else if (tag == "stch") return "Stretching Glyph Decomposition";
	else if (tag == "tjmo") return "Trailing Jamo Forms";
	else if (tag == "valt") return "Alternate Vertical Metrics";
	else if (tag == "vatu") return "Vattu Variants";
	else if (tag == "vhal") return "Alternate Vertical Half Metrics";
	else if (tag == "vjmo") return "Vowel Jamo Forms";
	else if (tag == "vkna") return "Vertical Kana Alternates";
	else if (tag == "vkrn") return "Vertical Kerning";
	else if (tag == "vpal") return "Proportional Alternate Vertical Metrics";
	else
	{
		auto d = tag->Data();
		if (d[0] == 'c' && d[1] == 'v')
			return "Character Variant " + d[2] + d[3];

		else return tag;
	}
}

/// <summary>
/// Opposite of DWRITE_MAKE_OPENTYPE_TAG, returns String
/// representation of OpenType tag.
/// </summary>
String^ DirectWrite::GetFeatureTag(UINT32 value)
{
	return GetOpenTypeFeatureTag(value);
}

IMapView<UINT32, UINT32>^ DirectWrite::GetSupportedTypography(DWriteFontFace^ canvasFontFace)
{
	ComPtr<IDWriteFontFaceReference> faceRef = canvasFontFace->GetReference();
	return GetSupportedTypography(faceRef);
}

IMapView<UINT32, UINT32>^ DirectWrite::GetSupportedTypography(ComPtr<IDWriteFontFaceReference> faceRef)
{
	// https://docs.microsoft.com/en-us/typography/opentype/spec/gsub
	// https://docs.microsoft.com/en-us/typography/opentype/spec/chapter2#flTbl

	ComPtr<IDWriteFontFace3> f3;
	faceRef->CreateFontFace(&f3);

	ComPtr<IDWriteFontFace5> face;
	f3.As(&face);

	ComPtr<IDWriteFontResource> resource;
	face->GetFontResource(&resource);

	const void* tableData;
	UINT32 tableSize;
	BOOL exists;
	void* context;
	face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('G', 'S', 'U', 'B'), &tableData, &tableSize, &context, &exists);

	IMapView<UINT32, UINT32>^ map = nullptr;

	if (exists)
	{
		auto reader = ref new GsubTableReader(tableData, tableSize);
		map = reader->FeatureMap;
		delete reader;
	}
	else
	{
		map = (ref new Map<UINT32, UINT32>())->GetView();
	}

	face->ReleaseFontTable(context);

	return map;
}

IVectorView<DWriteFontAxis^>^ DirectWrite::GetAxis(DWriteFontFace^ canvasFontFace)
{
	ComPtr<IDWriteFontFaceReference> faceRef = canvasFontFace->GetReference();
	return GetAxis(faceRef);
}

IVectorView<DWriteFontAxis^>^ DirectWrite::GetAxis(ComPtr<IDWriteFontFaceReference> faceRef)
{
	// 1. Get native DirectWrite resources
	ComPtr<IDWriteFontFace3> f3;
	faceRef->CreateFontFace(&f3);

	ComPtr<IDWriteFontFace5> face;
	f3.As(&face);

	ComPtr<IDWriteFontResource> resource;
	face->GetFontResource(&resource);

	// 2. Get axis data
	auto axisCount = resource->GetFontAxisCount();
	auto fontaxisCount = face->GetFontAxisValueCount();

	DWRITE_FONT_AXIS_RANGE* ranges = new (std::nothrow) DWRITE_FONT_AXIS_RANGE[axisCount];
	ThrowIfFailed(resource->GetFontAxisRanges(ranges, axisCount));

	DWRITE_FONT_AXIS_VALUE* defaults = new (std::nothrow) DWRITE_FONT_AXIS_VALUE[axisCount];
	ThrowIfFailed(resource->GetDefaultFontAxisValues(defaults, axisCount));

	DWRITE_FONT_AXIS_VALUE* values = new (std::nothrow) DWRITE_FONT_AXIS_VALUE[fontaxisCount];
	ThrowIfFailed(face->GetFontAxisValues(values, fontaxisCount));

	// 3. Create list
	Vector<DWriteFontAxis^>^ items = ref new Vector<DWriteFontAxis^>();
	for (int i = 0; i < axisCount; i++)
	{
		auto attribute = resource->GetFontAxisAttributes(i);
		auto range = ranges[i];
		auto def = defaults[i];

		String^ name = "";

		if (attribute == DWRITE_FONT_AXIS_ATTRIBUTES_VARIABLE)
		{
			ComPtr<IDWriteLocalizedStrings> strings;
			resource->GetAxisNames(i, &strings);
			name = GetLocaleString(strings, 0, nullptr);
		}

		auto item = ref new DWriteFontAxis(
			attribute, 
			range, 
			def, 
			values[i], 
			def.axisTag, 
			name);

		items->Append(item);
	}

	return items->GetView();
}

IVectorView<DWriteKnownFontAxisValues^>^ DirectWrite::GetNamedAxisValues(DWriteFontFace^ face)
{
	ComPtr<IDWriteFontFaceReference> faceRef = face->GetReference();
	return GetNamedAxisValues(faceRef);
}

IVectorView<DWriteKnownFontAxisValues^>^ DirectWrite::GetNamedAxisValues(ComPtr<IDWriteFontFaceReference> faceRef)
{
	// 1. Get native DirectWrite resources
	ComPtr<IDWriteFontFace3> f3;
	faceRef->CreateFontFace(&f3);

	ComPtr<IDWriteFontFace5> face;
	f3.As(&face);

	ComPtr<IDWriteFontResource> resource;
	face->GetFontResource(&resource);

	// 2. Get axis data
	auto axisCount = resource->GetFontAxisCount();

	// 3. Create list
	Vector<DWriteKnownFontAxisValues^>^ items = ref new Vector<DWriteKnownFontAxisValues^>();
	for (int i = 0; i < axisCount; i++)
	{
		auto namedCount = resource->GetAxisValueNameCount(i);
		if (namedCount > 0)
		{
			// Get named instances for this axis
			Vector<DWriteNamedFontAxisValue^>^ instances = ref new Vector<DWriteNamedFontAxisValue^>();
			String^ name;
			for (int a = 0; a < namedCount; a++)
			{
				DWRITE_FONT_AXIS_RANGE range;
				ComPtr<IDWriteLocalizedStrings> strings;
				resource->GetAxisValueNames(i, a, &range, &strings);

				uint32 lgt;
				strings->GetStringLength(0, &lgt);
				lgt += 1;
				wchar_t* buffer = new wchar_t[lgt];

				strings->GetString(0, buffer, lgt);
				name = ref new String(buffer);
				delete[] buffer;

				instances->Append(ref new DWriteNamedFontAxisValue(range, GetFeatureTag(range.axisTag), name));
			}

			items->Append(ref new DWriteKnownFontAxisValues(GetTagName(instances->GetAt(0)->Tag), instances->GetView()));
		}
	}

	return items->GetView();
}

DWriteFontSet^ DirectWrite::GetFonts(Uri^ uri, ComPtr<IDWriteFactory7> fac)
{
	CanvasFontSet^ set = CreateFontSet(uri->Path);
	ComPtr<IDWriteFontSet3> fontSet = GetWrappedResource<IDWriteFontSet3>(set);

	ComPtr<IDWriteFontCollection1> f1;
	ComPtr<IDWriteFontCollection3> f3;
	fac->CreateFontCollectionFromFontSet(fontSet.Get(), &f1);

	f1.As<IDWriteFontCollection3>(&f3);

	return GetFonts(f3);
}

IVectorView<DWriteFontSet^>^ DirectWrite::GetFonts(IVectorView<Uri^>^ uris, ComPtr<IDWriteFactory7> fac)
{
	Vector<DWriteFontSet^>^ fontSets = ref new Vector<DWriteFontSet^>();

	for (Uri^ uri : uris)
	{
		fontSets->Append(GetFonts(uri, fac));
	}

	return fontSets->GetView();
}

DWriteFontSet^ DirectWrite::GetFonts(ComPtr<IDWriteFontCollection3> fontSet)
{
	auto vec = ref new Vector<DWriteFontFamily^>();
	auto familyCount = fontSet->GetFontFamilyCount();

	for (uint32_t i = 0; i < familyCount; ++i)
	{
		ComPtr<IDWriteFontFamily2> family;
		fontSet->GetFontFamily(i, &family);

		if (family != nullptr)
			vec->Append(ref new DWriteFontFamily(family));
	}

	return ref new DWriteFontSet(vec->GetView());
}

String^ DirectWrite::GetLocaleString(ComPtr<IDWriteLocalizedStrings> strings, int ls, wchar_t* locale)
{
	HRESULT hr = E_FAIL;
	UINT32 fidx = 0;
	BOOL exists = false;

	if (ls && locale != nullptr)
		hr = strings->FindLocaleName(locale, &fidx, &exists);

	if (!SUCCEEDED(hr) || !exists) // if the above find did not find a match, retry with US English
		hr = strings->FindLocaleName(L"en-us", &fidx, &exists);
	
	if (!SUCCEEDED(hr) || !exists) // if we fail again, use the first name
	{
		hr = NOERROR;
		fidx = 0;
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

	auto str = ref new String(name);
	delete[] name;
	return str;
}

Platform::String^ DirectWrite::GetFileName(DWriteFontFace^ fontFace)
{
	Platform::String^ name = nullptr;

	// 1. Acquire the underlying FontLoader used to create the font face
	ComPtr<IDWriteFontFaceReference> fontFaceRef = fontFace->GetReference();
	ComPtr<IDWriteFontFile> file;
	ComPtr<IDWriteFontFileLoader> loader;
	ComPtr<IDWriteLocalFontFileLoader> localLoader;
	ComPtr<IDWriteRemoteFontFileLoader> remoteLoader;
	uint32 keySize = 0;
	const void* refKey = nullptr;

	if (fontFaceRef->GetFontFile(&file) == S_OK
		&& file->GetLoader(&loader) == S_OK
		&& file->GetReferenceKey(&refKey, &keySize) == S_OK)
	{
		// 2. We can only get fileNames for fonts from the Local FontFileLoader.
		//    Remote fonts have no filenames and will return nullptr
		if (loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK)
		{
			UINT filePathSize = 0;
			UINT filePathLength = 0;
			if (localLoader->GetFilePathLengthFromKey(refKey, keySize, &filePathLength) == S_OK)
			{
				wchar_t* namebuffer = new (std::nothrow) wchar_t[filePathLength + 1];
				if (localLoader->GetFilePathFromKey(refKey, keySize, namebuffer, filePathLength + 1) == S_OK)
					name = ref new Platform::String(namebuffer);

				delete[] namebuffer;
			}
		}
	}

	return name;
}

bool DirectWrite::HasValidFonts(StorageFile^ file)
{
	/*
	   To avoid garbage collection issues with CanvasFontSet in C# preventing us from
	   immediately deleting the StorageFile, we shall do this here in C++
	   */

	auto path = file->Path->Data();
	auto dwFontSet = CreateIDWriteFontSet(file->Path);
	bool valid = false;

	if (dwFontSet->GetFontCount() > 0)
	{
		/*
			We need to validate the font has a family name.
			Although other platforms and font renderers can read and understand fonts 
			without a FamilyName set in the 'name' table (for example, WOFF fonts), 
			XAML font rendering engine does not support these types of fonts. 
			Our basic WOFF conversion may give us fonts that are perfectly fine 
			except for this missing field.

			WOFF2 fonts may also give the same problem.

		*/

		ComPtr<IDWriteStringList> names;
		dwFontSet->GetPropertyValues(
			DWRITE_FONT_PROPERTY_ID_WIN32_FAMILY_NAME,
			&names);

		// We just need to *prove* there is a readable name - we don't need to 
		// read it.
		UINT32 nameLength;
		names->GetStringLength(0, &nameLength);
		valid = nameLength > 0;
	}

	dwFontSet = nullptr;

	return valid;
}

bool DirectWrite::IsFontLocal(DWriteFontFace^ fontFace)
{
	ComPtr<IDWriteFontFile> file;
	ComPtr<IDWriteFontFileLoader> loader;
	const void* refKey = nullptr;
	uint32 size = 0;

	return (fontFace->GetReference()->GetFontFile(&file) == S_OK
		&& file->GetLoader(&loader) == S_OK
		&& file->GetReferenceKey(&refKey, &size) == S_OK
		&& IsLocalFont(loader, refKey, size));
}

bool DirectWrite::IsLocalFont(ComPtr<IDWriteFontFileLoader> loader, const void* refKey, uint32 size)
{
	ComPtr<IDWriteRemoteFontFileLoader> remoteLoader;
	if (loader->QueryInterface<IDWriteRemoteFontFileLoader>(&remoteLoader) == S_OK)
	{
		DWRITE_LOCALITY loc;
		if (remoteLoader->GetLocalityFromKey(refKey, size, &loc) == S_OK)
			return loc == DWRITE_LOCALITY::DWRITE_LOCALITY_LOCAL;
	}

	return true;
}

IAsyncOperation<bool>^ DirectWrite::SaveFontStreamAsync(ComPtr<IDWriteFontFileStream> fileStream, IOutputStream^ stream)
{
	return create_async([&]
		{
			uint64 fileSize = 0;
			fileStream->GetFileSize(&fileSize);

			// 1. Copy stream to byte array
			void* context;
			const void* fragment;
			fileStream->ReadFileFragment(&fragment, 0, fileSize, &context);
			auto b = (byte*)fragment;

			// 2. Write the byte array to stream.
			DataWriter^ w = ref new DataWriter(stream);
			w->WriteBytes(Platform::ArrayReference<BYTE>(b, fileSize));

			return create_task(w->StoreAsync()).then([w, fileStream, context](bool result)
				{
					fileStream->ReleaseFileFragment(context);
					delete w;
					return task_from_result(result);
				}, task_continuation_context::use_arbitrary());
		});
}

IAsyncOperation<bool>^ DirectWrite::WriteToStreamAsync(DWriteFontFace^ fontFace, IOutputStream^ stream)
{
	// 1. Acquire the underlying FontLoader used to create the CanvasFontFace
	ComPtr<IDWriteFontFaceReference> fontFaceRef = fontFace->GetReference();
	ComPtr<IDWriteFontFile> file;
	ComPtr<IDWriteFontFileLoader> loader;
	ComPtr<IDWriteRemoteFontFileLoader> remoteLoader;
	ComPtr<IDWriteFontFileStream> fileStream;

	const void* refKey = nullptr;
	uint32 size = 0;

	if (fontFaceRef->GetFontFile(&file) == S_OK
		&& file->GetLoader(&loader) == S_OK
		&& file->GetReferenceKey(&refKey, &size) == S_OK
		&& IsLocalFont(loader, refKey, size)
		&& loader->CreateStreamFromKey(refKey, size, &fileStream) == S_OK)
	{
		return SaveFontStreamAsync(fileStream, stream);
	}

	return create_async([] { return task_from_result(false); });
}

IBuffer^ DirectWrite::GetImageDataBuffer(DWriteFontFace^ fontFace, UINT32 pixelsPerEm, UINT unicodeIndex, GlyphImageFormat format)
{
	// 1. Get font reference
	ComPtr<IDWriteFontFace3> face = fontFace->GetFontFace();
	ComPtr<IDWriteFontFace5> face5;
	face.As(&face5);

	// 2. Get index of glyph inside the font
	UINT16 idx = 0;
	auto arr = new UINT[1];
	arr[0] = unicodeIndex;
	auto hr3 = face5->GetGlyphIndices(arr, 1, &idx);
	delete[] arr;

	// 3. Get the actual image data
	DWRITE_GLYPH_IMAGE_DATA data;
	void* context;
	auto formats = face5->GetGlyphImageData(idx, pixelsPerEm, static_cast<DWRITE_GLYPH_IMAGE_FORMATS>(format), &data, &context);

	// 4. Write image data to a WinRT buffer
	auto b = (byte*)data.imageData;
	DataWriter^ writer = ref new DataWriter();
	writer->WriteBytes(Platform::ArrayReference<BYTE>(b, data.imageDataSize));
	IBuffer^ buffer = writer->DetachBuffer();

	// 5. Cleanup
	face5->ReleaseGlyphImageData(context);
	delete writer;

	// 6. Return buffer
	return buffer;
}