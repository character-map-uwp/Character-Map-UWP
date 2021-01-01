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
	ref class DataMap
	{
	public:
		property String^ Tag;
		property uint32 Offset;
		property uint32 Length;
	};

	ref class MetaTableReader sealed : TableReader
	{
	public:

		virtual ~MetaTableReader()
		{
		}

		property uint32 Version;
		property uint32 Flags;

		property String^ DesignLanguages;
		property String^ ScriptLanguages;


	internal:
		MetaTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt32();
			Flags = GetUInt32();
			auto reserved = GetUInt32();
			auto count = GetUInt32();

			auto map = ref new Vector<DataMap^>();

			for (int i = 0; i < count; i++)
			{
				auto data = ref new DataMap();
				data->Tag = GetTag();
				data->Offset = GetUInt32();
				data->Length = GetUInt32();

				map->Append(data);
			}

			for (auto data : map)
			{
				if (data->Tag == "dlng")
				{
					GoToPosition(data->Offset);
					DesignLanguages = GetNativeString(data->Length);
				}
				else if (data->Tag == "slng")
				{
					GoToPosition(data->Offset);
					ScriptLanguages = GetNativeString(data->Length);
				}
			}

		};

	private:


	};
}