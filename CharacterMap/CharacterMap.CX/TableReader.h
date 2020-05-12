#pragma once
#include <pch.h>
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
	ref class TableReader
	{
	public:

		virtual ~TableReader()
		{
			delete reader;
		}

	internal:
		TableReader(
			const void* tableData,
			uint32 size)
		{
			auto b = (byte*)tableData;
			DataWriter^ writer = ref new DataWriter();

			writer->WriteBytes(Platform::ArrayReference<BYTE>(b, size));
			IBuffer^ buffer = writer->DetachBuffer();
			delete writer;

			reader = DataReader::FromBuffer(buffer);
		};

		UINT8 GetUInt8()
		{
			position++;
			return reader->ReadByte();
		}

		UINT16 GetUInt16()
		{
			position += 2;
			return reader->ReadUInt16();
		}

		UINT32 GetUInt32()
		{
			position += 4;
			return reader->ReadUInt32();
		}

		string GetString(int length)
		{
			wchar_t* buffer = new wchar_t(length);

			for (int i = 0; i < length; i++)
			{
				buffer[i] = GetUInt8();
			}

			wstring ws(buffer);
			string str(ws.begin(), ws.end());

			return str;
		}

		void GoToPosition(int i)
		{
			while (position < i)
				GetUInt8();
		}

		UINT32 GetPosition()
		{
			return position;
		}

	private:
		DataReader^ reader;
		UINT32 position = 0;
	
	};
}