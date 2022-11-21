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

IMapView<UINT32, UINT32>^ DirectWrite::GetSupportedTypography(CanvasFontFace^ canvasFontFace)
{
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(canvasFontFace);
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

IVectorView<DWriteFontAxis^>^ DirectWrite::GetAxis(CanvasFontFace^ canvasFontFace)
{
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(canvasFontFace);
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
	resource->GetFontAxisRanges(ranges, axisCount);

	DWRITE_FONT_AXIS_VALUE* defaults = new (std::nothrow) DWRITE_FONT_AXIS_VALUE[axisCount];
	resource->GetDefaultFontAxisValues(defaults, axisCount);

	DWRITE_FONT_AXIS_VALUE* values = new (std::nothrow) DWRITE_FONT_AXIS_VALUE[fontaxisCount];
	face->GetFontAxisValues(values, fontaxisCount);

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

IVectorView<DWriteKnownFontAxisValues^>^ DirectWrite::GetNamedAxisValues(CanvasFontFace^ canvasFontFace)
{
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(canvasFontFace);
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

				instances->Append(ref new DWriteNamedFontAxisValue(range, GetFeatureTag(range.axisTag), name));
			}

			items->Append(ref new DWriteKnownFontAxisValues(GetTagName(instances->GetAt(0)->Tag), instances->GetView()));
		}
	}

	return items->GetView();
}

DWriteFontSet^ DirectWrite::GetFonts(Uri^ uri)
{
	CanvasFontSet^ set = ref new CanvasFontSet(uri);
	ComPtr<IDWriteFontSet3> fontSet = GetWrappedResource<IDWriteFontSet3>(set);
	return GetFonts(fontSet);
}

IVectorView<DWriteFontSet^>^ DirectWrite::GetFonts(IVectorView<Uri^>^ uris)
{
	Vector<DWriteFontSet^>^ fontSets = ref new Vector<DWriteFontSet^>();

	for (Uri^ uri : uris)
	{
		fontSets->Append(GetFonts(uri));
	}

	return fontSets->GetView();
}


DWriteFontSet^ DirectWrite::GetFonts(ComPtr<IDWriteFontSet3> fontSet)
{
	auto vec = ref new Vector<DWriteFontFace^>();
	auto fontCount = fontSet->GetFontCount();

	wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
	int ls = GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH);

	int appxCount = 0;
	int cloudCount = 0;
	int variableCount = 0;

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
				
				if (properties->HasVariations)
					variableCount++;

				vec->Append(fontface);
			}
		}
	}

	return ref new DWriteFontSet(vec->GetView(), appxCount, cloudCount, variableCount);
}

DWriteProperties^ DirectWrite::GetDWriteProperties(
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
	ComPtr<IDWriteFontFace3> f3;
	if (faceRef->CreateFontFace(&f3) == S_OK)
	{
		ComPtr<IDWriteFontFace5> face;
		f3.As(&face);

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

		return ref new DWriteProperties(fontSource, nullptr, family, fname, face->IsColorFont(), face->HasVariations());
	};

	return nullptr;
}

DWriteProperties^ DirectWrite::GetDWriteProperties(CanvasFontSet^ fontSet, UINT index)
{
	// 1. Get Font Source
	ComPtr<IDWriteFontSet3> set = GetWrappedResource<IDWriteFontSet3>(fontSet);
	DWriteFontSource fontSource = static_cast<DWriteFontSource>(set->GetFontSourceType(index));

	auto fontFace = fontSet->Fonts->GetAt(index);
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFace3> f3;
	ComPtr<IDWriteFontFace5> face;
	faceRef->CreateFontFace(&f3);
	f3.As(&face);

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

	return ref new DWriteProperties(fontSource, sourceName, nullptr, nullptr, face->IsColorFont(), face->HasVariations());
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

	return ref new String(name);
}


Platform::String^ DirectWrite::GetFileName(CanvasFontFace^ fontFace)
{
	Platform::String^ name = nullptr;

	// 1. Acquire the underlying FontLoader used to create the CanvasFontFace
	ComPtr<IDWriteFontFaceReference> fontFaceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFile> file;
	ComPtr<IDWriteFontFileLoader> loader;
	ComPtr<IDWriteLocalFontFileLoader> localLoader;
	uint32 keySize = 0;
	const void* refKey = nullptr;

	if (fontFaceRef->GetFontFile(&file) == S_OK
		&& file->GetLoader(&loader) == S_OK
		&& loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK
		&& file->GetReferenceKey(&refKey, &keySize) == S_OK)
	{
		UINT filePathSize = 0;
		UINT filePathLength = 0;
		if (localLoader->GetFilePathLengthFromKey(refKey, keySize, &filePathLength) == S_OK)
		{
			wchar_t* namebuffer = new (std::nothrow) wchar_t[filePathLength + 1];
			if (localLoader->GetFilePathFromKey(refKey, keySize, namebuffer, filePathLength + 1) == S_OK)
				name = ref new Platform::String(namebuffer);
		}
	}

	return name;
}

bool DirectWrite::HasValidFonts(Uri^ uri)
{
	/*
	   To avoid garbage collection issues with CanvasFontSet in C# preventing us from
	   immediately deleting the StorageFile, we shall do this here in C++
	   */
	CanvasFontSet^ fontset = ref new CanvasFontSet(uri);
	bool valid = false;
	if (fontset->Fonts->Size > 0)
	{
		/*
			We need to validate the font has a family name.
			Although other platforms and font renders can read and
			understand fonts without a FamilyName set in the 'name'
			table (for example, WOFF fonts), XAML font rendering engine does not support
			these types of fonts. Our basic WOFF conversion may give us fonts that are
			perfectly fine except for this missing field.
		*/

		wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
		int ls = GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH);
		auto l = ref new String(localeName);

		auto font = fontset->Fonts->GetAt(0);
		auto results = font->GetInformationalStrings(CanvasFontInformation::Win32FamilyNames);

		valid = results->HasKey(l) || results->HasKey("en-us");

		/*
			In the future we can technically support *any* locale even if en-us is not included.
			We have to update DirectWrite::GetLocaleString(...)
		*/
		/*if (!valid)
		{
			for (auto p : results)
			{
				auto cod = p->Key;
				auto val = p->Value;
			}

			auto itr = results->First();
			if (itr->HasCurrent)
			{
				auto cur = itr->Current;
				auto val = cur->Value;
				valid = true;
			}
		}*/
	}
	delete fontset;
	return valid;
}

bool DirectWrite::IsFontLocal(CanvasFontFace^ fontFace)
{
	ComPtr<IDWriteFontFaceReference> fontFaceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFile> file;
	ComPtr<IDWriteFontFileLoader> loader;
	const void* refKey = nullptr;
	uint32 size = 0;

	return (fontFaceRef->GetFontFile(&file) == S_OK
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

IAsyncOperation<bool>^ DirectWrite::WriteToStreamAsync(CanvasFontFace^ fontFace, IOutputStream^ stream)
{
	return create_async([&]
		{
			// 1. Acquire the underlying FontLoader used to create the CanvasFontFace
			ComPtr<IDWriteFontFaceReference> fontFaceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
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
				uint64 fileSize = 0;
				fileStream->GetFileSize(&fileSize);

				// 2. Read the source file into memory
				// TODO: We don't really want to load the entire file into memory.
				//       Perhaps we can use a rolling buffer?
				void* context;
				const void* fragment;
				fileStream->ReadFileFragment(&fragment, 0, fileSize, &context);
				auto b = (byte*)fragment;

				// 3. Write the memory to stream.
				DataWriter^ w = ref new DataWriter(stream);
				w->WriteBytes(Platform::ArrayReference<BYTE>(b, fileSize));

				return create_task(w->StoreAsync()).then([w, fileStream, context](bool result)
					{
						fileStream->ReleaseFileFragment(context);
						delete w;
						return task_from_result(result);
					}, task_continuation_context::use_arbitrary());
			}
			return task_from_result(false);
		});
}

IBuffer^ DirectWrite::GetImageDataBuffer(CanvasFontFace^ fontFace, UINT32 pixelsPerEm, UINT unicodeIndex, GlyphImageFormat format)
{
	// 1. Get font reference
	ComPtr<IDWriteFontFaceReference> faceRef = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
	ComPtr<IDWriteFontFace3> face;
	faceRef->CreateFontFace(&face);

	ComPtr<IDWriteFontFace5> face5;
	face.As(&face5);

	// 2. Get index of glyph inside the font
	UINT16 idx = 0;
	auto arr = new UINT[1];
	arr[0] = unicodeIndex;
	auto hr3 = face5->GetGlyphIndices(arr, 1, &idx);
	delete arr;

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