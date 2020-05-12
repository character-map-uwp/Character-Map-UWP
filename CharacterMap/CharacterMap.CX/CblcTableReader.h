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
	ref class CblcTableReader sealed : TableReader
	{
	public:

		virtual ~CblcTableReader()
		{
		}

		property uint16 MajorVersion;
		property uint16 MinorVersion;
		property uint32 SizeTableCount;

	internal:
		CblcTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			MajorVersion = GetUInt16();
			MinorVersion = GetUInt16();
			SizeTableCount = GetUInt32();
		};

	private:


	};
}