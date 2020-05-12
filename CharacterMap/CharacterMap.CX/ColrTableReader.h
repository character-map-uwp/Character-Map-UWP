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
using namespace CharacterMapCX;
using namespace std;

namespace CharacterMapCX
{
	ref class ColrTableReader sealed : TableReader
	{
	public:

		virtual ~ColrTableReader()
		{
		}

		property uint16 Version;
		property uint16 BaseGlyphRecordsCount;
		property uint32 BaseGlyphRecordsOffset;
		property uint32 LayersRecordsOffset;
		property uint16 LayerRecordsCount;

	internal:
		ColrTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			BaseGlyphRecordsCount = GetUInt16();
			BaseGlyphRecordsOffset = GetUInt32();
			LayersRecordsOffset = GetUInt32();
			LayerRecordsCount = GetUInt16();
		};

	private:


	};
}