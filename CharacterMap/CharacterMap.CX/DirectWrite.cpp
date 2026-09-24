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
		We need to validate the font has a family name.
		Although other platforms and font renderers can read and understand fonts
		without a FamilyName set in the 'name' table (for example, WOFF fonts),
		XAML font rendering engine does not support these types of fonts.
		Our basic WOFF conversion may give us fonts that are perfectly fine
		except for this missing field.

		WOFF2 fonts may also give the same problem.
	*/

	ComPtr<IDWriteFontSet> dwFontSet = CreateIDWriteFontSet(path);
	if (!dwFontSet)
		ThrowHR(E_INVALIDARG);

	CanvasFontSet^ fontSet = GetOrCreate<CanvasFontSet>(dwFontSet.Get());
	return fontSet;
}

ComPtr<IDWriteFontSet> DirectWrite::CreateIDWriteFontSet(String^ path)
{
	if (path == nullptr || path->IsEmpty())
		return nullptr;

	try
	{
		ComPtr<IDWriteFactory7> factory;
		HRESULT hr = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory7), &factory);
		if (FAILED(hr))
			return nullptr;

		ComPtr<IDWriteFontFile> fontFile;
		hr = factory->CreateFontFileReference(path->Data(), nullptr, &fontFile);
		if (FAILED(hr))
			return nullptr;

		BOOL isSupported = FALSE;
		DWRITE_FONT_FILE_TYPE fileType;
		DWRITE_FONT_FACE_TYPE faceType;
		UINT32 numberOfFaces = 0;
		hr = fontFile->Analyze(&isSupported, &fileType, &faceType, &numberOfFaces);
		if (FAILED(hr) || !isSupported || numberOfFaces == 0)
			return nullptr;

		ComPtr<IDWriteFontSetBuilder1> builder;
		hr = factory->CreateFontSetBuilder(&builder);
		if (FAILED(hr))
			return nullptr;

		for (UINT32 i = 0; i < numberOfFaces; ++i)
		{
			ComPtr<IDWriteFontFaceReference> faceRef;
			if (SUCCEEDED(factory->CreateFontFaceReference(fontFile.Get(), i, DWRITE_FONT_SIMULATIONS_NONE, &faceRef)))
				builder->AddFontFaceReference(faceRef.Get());
		}

		ComPtr<IDWriteFontSet> dwFontSet;
		hr = builder->CreateFontSet(&dwFontSet);
		if (FAILED(hr))
			return nullptr;

		return dwFontSet;
	}
	catch (...)
	{
		return nullptr;
	}
}

String^ DirectWrite::GetTagName(UINT32 tag)
{
	return GetTagName(GetFeatureTag(tag));
}

String^ DirectWrite::GetTagName(String^ tag)
{
	/* Variation Tags */
	if (tag == "wght") return "Weight";
	if (tag == "slnt") return "Slant";
	if (tag == "CONT") return "Contrast";
	if (tag == "MIDL") return "Midline";
	if (tag == "wdth") return "Width";

	/* Ligature & Contextual Features */
	if (tag == "liga") return "Standard Ligatures";
	if (tag == "dlig") return "Discretionary Ligatures";
	if (tag == "hlig") return "Historical Ligatures";
	if (tag == "clig") return "Contextual Ligatures";
	if (tag == "rlig") return "Required Ligatures";
	if (tag == "locl") return "Localized Forms";
	if (tag == "calt") return "Contextual Alternates";
	if (tag == "ccmp") return "Glyph Composition / Decomposition";

	/* OpenType feature Tags */
	/* Only a subset of common tags are identified here */
	/* TODO: Implement this better, including details of rendering 
	         properties for tags, like is it for single characters
			 or glyph runs, editable, etc.
    */
	if (tag == "aalt") return "Access All Alternates";
	if (tag == "abvf") return "Above-base Forms";
	if (tag == "abvm") return "Above-base Mark Positioning";
	if (tag == "abvs") return "Above-base Substitutions";
	if (tag == "afrc") return "Alternative Fractions";
	if (tag == "akhn") return "Akhand";
	if (tag == "apkn") return "Kerning for Alternate Proportional Widths";
	if (tag == "blwf") return "Below-base Forms";
	if (tag == "blwm") return "Below-base Mark Positioning";
	if (tag == "blws") return "Below-base Substitutions";
	if (tag == "c2pc") return "Petite Capitals From Capitals";
	if (tag == "c2sc") return "Small Capitals From Capitals";
	if (tag == "calt") return "Contextual Alternates";
	if (tag == "case") return "Case-sensitive Forms";
	if (tag == "ccmp") return "Glyph Composition / Decomposition";
	if (tag == "cfar") return "Conjunct Form After Ro";
	if (tag == "chws") return "Contextual Half-width Spacing";
	if (tag == "cjct") return "Conjunct Forms";
	if (tag == "clig") return "Contextual Ligatures";
	if (tag == "cpct") return "Centered CJK Punctuation";
	if (tag == "cpsp") return "Capital Spacing";
	if (tag == "cswh") return "Contextual Swash";
	if (tag == "curs") return "Cursive Positioning";
	if (tag == "dist") return "Distances";
	if (tag == "dlig") return "Discretionary Ligatures";
	if (tag == "dnom") return "Denominators";
	if (tag == "dpng") return "Diphthongs";
	if (tag == "dtls") return "Dotless Forms";
	if (tag == "expt") return "Expert Forms";
	if (tag == "falt") return "Final Glyph on Line Alternates";
	if (tag == "fin2") return "Terminal Forms #2";
	if (tag == "fin3") return "Terminal Forms #3";
	if (tag == "fina") return "Terminal Forms";
	if (tag == "flac") return "Flattened Accent Forms";
	if (tag == "frac") return "Fractions";
	if (tag == "fwid") return "Full Widths";
	if (tag == "half") return "Half Forms";
	if (tag == "haln") return "Halant Forms";
	if (tag == "halt") return "Alternate Half Widths";
	if (tag == "hist") return "Historical Forms";
	if (tag == "hkna") return "Horizontal Kana Alternates";
	if (tag == "hlig") return "Historical Ligatures";
	if (tag == "hngl") return "Hangul";
	if (tag == "hojo") return "Hojo Kanji Forms (JIS X 0212-1990 Kanji Forms)";
	if (tag == "hwid") return "Half Widths";
	if (tag == "init") return "Initial Forms";
	if (tag == "isol") return "Isolated Forms";
	if (tag == "ital") return "Italics";
	if (tag == "jalt") return "Justification Alternates";
	if (tag == "jp78") return "JIS78 Forms";
	if (tag == "jp83") return "JIS83 Forms";
	if (tag == "jp90") return "JIS90 Forms";
	if (tag == "jp04") return "JIS2004 Forms";
	if (tag == "kern") return "Kerning";
	if (tag == "lfbd") return "Left Bounds";
	if (tag == "liga") return "Standard Ligatures";
	if (tag == "ljmo") return "Leading Jamo Forms";
	if (tag == "lnum") return "Lining Figures";
	if (tag == "locl") return "Localized Forms";
	if (tag == "ltra") return "Left-to-right Alternates";
	if (tag == "ltrm") return "Left-to-right Mirrored Forms";
	if (tag == "mark") return "Mark Positioning";
	if (tag == "med2") return "Medial Forms #2";
	if (tag == "medi") return "Medial Forms";
	if (tag == "mgrk") return "Mathematical Greek";
	if (tag == "mkmk") return "Mark to Mark Positioning";
	if (tag == "mset") return "Mark Positioning via Substitution";
	if (tag == "nalt") return "Alternate Annotation Forms";
	if (tag == "nlck") return "NLC Kanji Forms";
	if (tag == "nukt") return "Nukta Forms";
	if (tag == "numr") return "Numerators";
	if (tag == "onum") return "Oldstyle Figures";
	if (tag == "opbd") return "Optical Bounds";
	if (tag == "opsz") return "Optical size";
	if (tag == "ordn") return "Ordinals";
	if (tag == "ornm") return "Ornaments";
	if (tag == "palt") return "Proportional Alternate Widths";
	if (tag == "pcap") return "Petite Capitals";
	if (tag == "pkna") return "Proportional Kana";
	if (tag == "pnum") return "Proportional Figures";
	if (tag == "pref") return "Pre-base Forms";
	if (tag == "pres") return "Pre-base Substitutions";
	if (tag == "pstf") return "Post-base Forms";
	if (tag == "psts") return "Post-base Substitutions";
	if (tag == "pwid") return "Proportional Widths";
	if (tag == "qwid") return "Quarter Widths";
	if (tag == "rand") return "Randomize";
	if (tag == "rclt") return "Required Contextual Alternates";
	if (tag == "rkrf") return "Rakar Forms";
	if (tag == "rlig") return "Required Ligatures";
	if (tag == "rphf") return "Reph Form";
	if (tag == "rtbd") return "Right Bounds";
	if (tag == "rtla") return "Right-to-left Alternates";
	if (tag == "rtlm") return "Right-to-left Mirrored Forms";
	if (tag == "ruby") return "Ruby Notation Forms";
	if (tag == "rvrn") return "Required Variation Alternates";
	if (tag == "salt") return "Stylistic Alternates";
	if (tag == "sinf") return "Scientific Inferiors";
	if (tag == "size") return "Optical size";
	if (tag == "smcp") return "Small Capitals";
	if (tag == "smpl") return "Simplified Forms";
	if (tag == "ssty") return "Math Script-style Alternates";
	if (tag == "stch") return "Stretching Glyph Decomposition";
	if (tag == "subs") return "Subscript";
	if (tag == "sups") return "Superscript";
	if (tag == "swsh") return "Swash";
	if (tag == "titl") return "Titling";
	if (tag == "tjmo") return "Trailing Jamo Forms";
	if (tag == "tnam") return "Traditional Name Forms";
	if (tag == "tnum") return "Tabular Figures";
	if (tag == "trad") return "Traditional Forms";
	if (tag == "twid") return "Third Widths";
	if (tag == "unic") return "Unicase";
	if (tag == "valt") return "Alternate Vertical Metrics";
	if (tag == "vapk") return "Kerning for Alternate Proportional Vertical Metrics";
	if (tag == "vatu") return "Vattu Variants";
	if (tag == "vchw") return "Vertical Contextual Half-width Spacing";
	if (tag == "vert") return "Vertical Alternates";
	if (tag == "vhal") return "Alternate Vertical Half Metrics";
	if (tag == "vjmo") return "Vowel Jamo Forms";
	if (tag == "vkna") return "Vertical Kana Alternates";
	if (tag == "vkrn") return "Vertical Kerning";
	if (tag == "vpal") return "Proportional Alternate Vertical Metrics";
	if (tag == "vrt2") return "Vertical Alternates and Rotation";
	if (tag == "vrtr") return "Vertical Alternates for Rotation";
	if (tag == "zero") return "Slashed Zero";

	if (tag != nullptr && tag->Length() >= 4)
	{
		auto d = tag->Data();
		if (d[0] == 'c' && d[1] == 'v')
			return "Character Variant " + d[2] + d[3];
		if (d[0] == 's' && d[1] == 's')
			return "Stylistic Set " + d[2] + d[3];
	}

	return tag;
}

String^ DirectWrite::GetFeatureName(UINT32 tag)
{
	return GetFeatureName(GetFeatureTag(tag));
}

String^ DirectWrite::GetFeatureName(String^ tag)
{
	return GetTagName(tag);
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

		face->ReleaseFontTable(context);
	}
	else
	{
		map = (ref new Map<UINT32, UINT32>())->GetView();
	}

	return map;
}

IVectorView<DWriteLigatureFeature^>^ DirectWrite::GetLigatures(DWriteFontFace^ canvasFontFace)
{
	ComPtr<IDWriteFontFaceReference> faceRef = canvasFontFace->GetReference();
	return GetLigatures(faceRef);
}

IVectorView<DWriteLigatureFeature^>^ DirectWrite::GetLigatures(ComPtr<IDWriteFontFaceReference> faceRef)
{
	ComPtr<IDWriteFontFace3> f3;
	HRESULT hr = faceRef->CreateFontFace(&f3);
	if (FAILED(hr) || f3 == nullptr)
		return (ref new Vector<DWriteLigatureFeature^>())->GetView();

	ComPtr<IDWriteFontFace5> face;
	hr = f3.As(&face);
	if (FAILED(hr) || face == nullptr)
		return (ref new Vector<DWriteLigatureFeature^>())->GetView();

	const void* tableData;
	UINT32 tableSize;
	BOOL exists;
	void* context;
	face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('G', 'S', 'U', 'B'), &tableData, &tableSize, &context, &exists);

	IVectorView<DWriteLigatureFeature^>^ list = nullptr;

	if (exists)
	{
		auto reader = ref new GsubTableReader(tableData, tableSize);
		list = reader->LigatureFeatures;
		delete reader;

		face->ReleaseFontTable(context);
	}
	else
	{
		list = (ref new Vector<DWriteLigatureFeature^>())->GetView();
	}

	return list;
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
    ThrowIfFailed(faceRef->CreateFontFace(&f3));

    ComPtr<IDWriteFontFace5> face;
    ThrowIfFailed(f3.As(&face));

    ComPtr<IDWriteFontResource> resource;
    ThrowIfFailed(face->GetFontResource(&resource));

    // 2. Get axis data
    UINT32 axisCount = resource->GetFontAxisCount();
    UINT32 fontAxisCount = face->GetFontAxisValueCount();

    std::vector<DWRITE_FONT_AXIS_RANGE> ranges(axisCount);
    ThrowIfFailed(resource->GetFontAxisRanges(ranges.data(), axisCount));

    std::vector<DWRITE_FONT_AXIS_VALUE> defaults(axisCount);
    ThrowIfFailed(resource->GetDefaultFontAxisValues(defaults.data(), axisCount));

    std::vector<DWRITE_FONT_AXIS_VALUE> values;
    if (fontAxisCount > 0)
    {
        values.resize(fontAxisCount);
        ThrowIfFailed(face->GetFontAxisValues(values.data(), fontAxisCount));
    }

    // 3. Create list, matching by axisTag rather than by index
    Vector<DWriteFontAxis^>^ items = ref new Vector<DWriteFontAxis^>();
    for (UINT32 i = 0; i < axisCount; ++i)
    {
        auto attribute = resource->GetFontAxisAttributes(i);
        auto range = ranges[i];
        auto def = defaults[i];

        // Find a matching value by axisTag in the face values
        DWRITE_FONT_AXIS_VALUE chosen = def; // fallback to default
        for (UINT32 j = 0; j < values.size(); ++j)
        {
            if (values[j].axisTag == def.axisTag)
            {
                chosen = values[j];
                break;
            }
        }

        String^ name = "";
        if (attribute != DWRITE_FONT_AXIS_ATTRIBUTES_NONE)
        {
            ComPtr<IDWriteLocalizedStrings> strings;
            ThrowIfFailed(resource->GetAxisNames(i, &strings));
            name = GetLocaleString(strings, 0, nullptr);
        }

        auto item = ref new DWriteFontAxis(
            attribute,
            range,
            def,
            chosen,
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
				std::vector<wchar_t> buffer(lgt);
				strings->GetString(0, buffer.data(), lgt);
				name = ref new String(buffer.data());

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
	if (file == nullptr)
		return false;

	auto dwFontSet = CreateIDWriteFontSet(file->Path);
	if (!dwFontSet || dwFontSet->GetFontCount() == 0)
		return false;

	bool valid = false;

	try
	{
		ComPtr<IDWriteStringList> names;
		if (SUCCEEDED(dwFontSet->GetPropertyValues(
			DWRITE_FONT_PROPERTY_ID_WIN32_FAMILY_NAME,
			&names)) && names && names->GetCount() > 0)
		{
			UINT32 nameLength = 0;
			if (SUCCEEDED(names->GetStringLength(0, &nameLength)))
				valid = nameLength > 0;
		}
	}
	catch (...)
	{
		valid = false;
	}

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
	return create_async([fileStream, stream]
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

IBuffer^ DirectWrite::GetGlyphImageDataBuffer(DWriteFontFace^ fontFace, UINT32 pixelsPerEm, UINT16 glyphIndex, GlyphImageFormat format)
{
	ComPtr<IDWriteFontFace3> face = fontFace->GetFontFace();
	ComPtr<IDWriteFontFace5> face5;
	face.As(&face5);

	DWRITE_GLYPH_IMAGE_DATA data;
	void* context;
	auto formats = face5->GetGlyphImageData(glyphIndex, pixelsPerEm, static_cast<DWRITE_GLYPH_IMAGE_FORMATS>(format), &data, &context);

	auto b = (byte*)data.imageData;
	DataWriter^ writer = ref new DataWriter();
	writer->WriteBytes(Platform::ArrayReference<BYTE>(b, data.imageDataSize));
	IBuffer^ buffer = writer->DetachBuffer();

	face5->ReleaseGlyphImageData(context);
	delete writer;

	return buffer;
}