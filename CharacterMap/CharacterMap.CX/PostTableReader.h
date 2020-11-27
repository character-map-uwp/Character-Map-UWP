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
using namespace Platform::Collections;
using namespace CharacterMapCX;
using namespace std;

namespace CharacterMapCX
{
	ref class PostTableReader sealed : TableReader
	{
	public:

		virtual ~PostTableReader()
		{
		}

		/*property float Version;
		property float ItalicAngle;*/
		property uint32 Version;
		property uint32 ItalicAngle;
		property int16 UnderlinePosition;
		property int16 UnderlineThickness;
		property uint32 IsFixedPitch;
		property uint32 MinMemType42;
		property uint32 MaxMemType42;
		property uint32 MinMemType1;
		property uint32 MaxMemType1;

		property uint32 NumGlyphs;
		property IVectorView<uint16>^ GlyphNameIndex;
		property IVectorView<String^>^ Names;

	internal:
		PostTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			/*Version = GetFixed();
			ItalicAngle = GetFixed();*/
			Version = GetUInt32();
			ItalicAngle = GetUInt32();
			UnderlinePosition = GetFWord();
			UnderlineThickness = GetFWord();
			IsFixedPitch = GetUInt32();
			MinMemType42 = GetUInt32();
			MaxMemType42 = GetUInt32();
			MinMemType1 = GetUInt32();
			MaxMemType1 = GetUInt32();

			if (Version == 0x00020000)
			{
				NumGlyphs = GetUInt16();

				auto vec = ref new Vector<uint16>();
				for (int i = 0; i < NumGlyphs; i++)
					vec->Append(GetUInt16());

				GlyphNameIndex = vec->GetView();

				auto nvec = ref new Vector<String^>();
				for (int i = 0; i < NumGlyphs; i++) 
				{
					if (i < 258)
						nvec->Append("MacPost");
					else
					{
						auto length = GetUInt8();
						nvec->Append(GetNativeString(length));
					}
				}

				Names = nvec->GetView();
			}
		};

	private:


	};
}