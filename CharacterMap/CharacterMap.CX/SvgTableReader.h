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
	ref class SvgTableReader sealed : TableReader
	{
	public:

		virtual ~SvgTableReader()
		{
		}

		property uint16 Version;
		property uint32 SvgListOffset;
		property uint16 EntryCount;

	internal:
		SvgTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			SvgListOffset = GetUInt32();
			GoToPosition(SvgListOffset);

			EntryCount = GetUInt16();
		};

	private:


	};
}