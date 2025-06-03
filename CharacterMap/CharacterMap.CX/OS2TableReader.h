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
		NoSubsetting = 256,
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

			// Parse fsType bits
			FontEmbeddingType type = FontEmbeddingType::Installable;
			
			// Bits 0 to 3 must be mutually exclusive.
			// The specification for versions 0 to 2 did not specify that bits 0 to 3 must be mutually exclusive.
			// Rather, those specifications stated that, in the event that more than one of bits 0 to 3 are set 
			// in a given font, then the least - restrictive permission indicated take precedence.
			// As such, this interpretation of bits 0 to 3 should cover all versions of the OS/2 table.
			if (FSType & 0x0008)
				type = FontEmbeddingType::Editable;
			else if (FSType & 0x0004)
				type = FontEmbeddingType::PreviewPrint;
			else if (FSType & 0x0002)
				type = FontEmbeddingType::Restricted;
		
			// Some fonts using a version 0 to version 2 OS/2 table have both bit 2 and bit 3 set with the 
			// intent to indicate both preview/print and edit permissions. Applications are permitted to use 
			// this behavior for fonts with a version 0 to version 2 OS/2 table.
			if (Version <= 2 && (FSType & 0x0004) && (FSType & 0x0008))
				type = FontEmbeddingType::PreviewPrint | FontEmbeddingType::Editable;

			// Versions 0 to 1: only bits 0 to 3 were assigned. 
			// Applications must ignore bits 4 to 15 when reading a version 0 or version 1 table.
			if (Version > 1)
			{
				if (FSType & 0x0100)
					type = type | FontEmbeddingType::NoSubsetting;

				if (FSType & 0x0200)
					type = type | FontEmbeddingType::BitmapOnly;
			}

			EmbeddingType = type;


			// Don't care about the rest of the table right now 8)
		};

	private:


	};
}