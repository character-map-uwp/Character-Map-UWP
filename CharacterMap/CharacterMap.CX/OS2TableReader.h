#pragma once
#include <pch.h>
#include <TableReader.h>
#include "DWriteFontAxisAttribute.h"
#include "DWriteNamedFontAxisValue.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Platform;
using namespace Platform::Metadata;
using namespace CharacterMapCX;
using namespace std;

namespace CharacterMapCX
{
	[FlagsAttribute]
	public enum class FontEmbeddingType : unsigned int
	{
		Installable = 0,
		Restricted = 2,
		PreviewPrint = 4,
		Editable = 8,
		NoSubsetting = 265,
		BitmapOnly = 512
	};

	ref class OS2TableReader sealed : TableReader
	{
	public:

		virtual ~OS2TableReader()
		{
		}

		property uint32 Version;
		property uint16 FSType; // Font embedding information
		property FontEmbeddingType EmbeddingType; // Parsed fsType data

	internal:
		OS2TableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			auto xAvgCharWidth = GetFWord();
			auto usWeightClass = GetUInt16();
			auto usWidthClass = GetUInt16();

			// Contains font embedding information
			FSType = GetUInt16(); 

			//bool installable = (FSType & 0x0002) == 0 && (FSType & 0x0004) == 0 && (FSType & 0x0008) == 0;

			FontEmbeddingType type = FontEmbeddingType::Installable;
			if (FSType & 0x0002)
				type = FontEmbeddingType::Restricted;
			else if (FSType & 0x0004)
				type = FontEmbeddingType::PreviewPrint;
			else if (FSType & 0x0008)
				type = FontEmbeddingType::Editable;

			if (FSType & 0x0100)
				type = type | FontEmbeddingType::NoSubsetting;

			if (FSType & 0x0200)
				type = type | FontEmbeddingType::BitmapOnly;

			EmbeddingType = type;


			// Don't care about the rest of the table right now 8)
			
		};

	private:


	};
}