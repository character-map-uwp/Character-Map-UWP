#pragma once
#include <pch.h>
#include <TableReader.h>
#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
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
	ref class SbixTableReader sealed : TableReader
	{
	public:

		virtual ~SbixTableReader()
		{
		}

		property uint16 Version;
		property uint16 Flags;
		property uint16 NumberOfStrikes;

	internal:
		SbixTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			Flags = GetUInt16();
			NumberOfStrikes = GetUInt32();

			// We don't read offset of strikes
		};

		
	private:
	

	};
}