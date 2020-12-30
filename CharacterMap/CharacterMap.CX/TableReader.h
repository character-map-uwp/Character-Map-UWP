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

		int GetUInt24()
		{
			position += 3;
			byte highByte = reader->ReadByte();
			return (highByte << 16) | reader->ReadUInt16();
		}

		float GetFixed()
		{
			position += 4;
			return reader->ReadUInt32() / (1 << 16);
		}

		INT16 GetFWord()
		{
			position += 2;
			return reader->ReadInt16();
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

		Platform::String^ GetNativeString(UINT length)
		{
			position += length;
			return reader->ReadString(length);
		}

		IVectorView<uint16>^ GetUInt16Vector(uint16 count)
		{
			auto vec = ref new Vector<uint16>();
			for (int i = 0; i < count; i++)
				vec->Append(GetUInt16());
			return vec->GetView();
		}

		Array<uint16>^ GetUInt16Array(uint16 count)
		{
			Array<uint16>^ array = ref new Array<uint16>(count);
			for (int i = 0; i < count; i++)
				array[i] = GetUInt16();
			return array;
		}

		Array<uint16>^ GetUInt16Array(uint32 count)
		{
			Array<uint16>^ array = ref new Array<uint16>(count);
			for (int i = 0; i < count; i++)
				array[i] = GetUInt16();
			return array;
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