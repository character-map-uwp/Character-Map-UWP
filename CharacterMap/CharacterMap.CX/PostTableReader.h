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

		/// <summary>
		/// Mappings of a glyph index to a font-provided glyph name
		/// </summary>
		property IMapView<int, String^>^ Mapping;

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
				GlyphNameIndex = GetUInt16Vector(NumGlyphs);

				auto s = new vector<String^>();
				while (!IsAtEnd())
				{
					auto length = GetUInt8();
					auto str = GetCleanNativeString(length);

					if (IsValid(str, length))
						s->push_back(str);
					else
						s->push_back(nullptr);
				}

				auto nmap = ref new Platform::Collections::Map<int, String^>();
				for (int i = 0; i < NumGlyphs; i++) 
				{
					int idx = GlyphNameIndex->GetAt(i);
					if (idx < 258)
					{
						// do nothing for now (use default MACPOST name?)
					}
					else
					{
						auto target = idx - 258;
						if (target < s->size())
						{
							auto str = s->at(target);
							if (str != nullptr)
							{
								nmap->Insert(i, str);
								continue;
							}
						}
					}
				}

				Mapping = nmap->GetView();
			}
		};

	private:

		/*
		* Handle AGLFN mappings.
		* 'uXXXX' & 'uniXXXX' mappings should be ignored.
		*
		* Older fonts may use AGLF names from older versions of the Adobe Glyph
		* name mapping values, like 'afii' or 'commaaccent' that have been removed
		* from the spec and are not in our listings.
		*
		* We try to do as much as we can early in C++ to avoid marshalling costs
		* and garbage collection pressure in C#.
		*/

		bool IsValid(String^ string, UINT8 size)
		{
			auto str = string->Data();
			return !IsUni(str, size) && !IsAglfn(str, size);
		}

		bool IsUni(const wchar_t* d, UINT8 size)
		{
			if (size == 7 && (d[0] == 'u' && d[1] == 'n' && d[2] == 'i') && IsHex(d[3]) && IsHex(d[4]) && IsHex(d[5]) && IsHex(d[6]))
				return true;

			if (size == 5 && d[0] == 'u' && IsHex(d[1]) && IsHex(d[2]) && IsHex(d[3]) && IsHex(d[4]))
				return true;

			if (size == 6 && d[0] == 'u' && IsHex(d[1]) && IsHex(d[2]) && IsHex(d[3]) && IsHex(d[4]) && IsHex(d[5]))
				return true;

			return false;
		}

		/// Ignore certain AGLFN key names
		bool IsAglfn(const wchar_t* d, UINT8 size)
		{
			// Check contains "commaaccent"
			if (size >= 11 && wcsstr(d, L"commaaccent"))
				return true;

			// Check contains "dotaccent"
			if (size >= 9 && wcsstr(d, L"dotaccent"))
				return true;

			// Check starts with 'afii'
			return size >= 4 && d[0] == 'a' && d[1] == 'f' && d[2] == 'i' && d[3] == 'i';
		}

		bool IsHex(char c)
		{
			return c >= 0 && c <= 'F';
		}

		/*bool IsValid(string* str, UINT8 size)
		{
			if (!IsUni(str, size) && !IsAfii(str, size))
			{
				Clean(str, size);
				return true;
			}

			return false;
		}

		bool IsUni(string* d, UINT8 size)
		{
			if (d->length() == 7 && (d->at(0) == 'u' && d->at(1) == 'n' && d->at(2) == 'i') && IsHex(d->at(3)) && IsHex(d->at(4)) && IsHex(d->at(5)) && IsHex(d->at(6)))
				return true;

			if (d->length() == 5 && d->at(0) == 'u' && IsHex(d->at(1)) && IsHex(d->at(2)) && IsHex(d->at(3)) && IsHex(d->at(4)))
				return true;

			if (d->length() == 6 && d->at(0) == 'u' && IsHex(d->at(1)) && IsHex(d->at(2)) && IsHex(d->at(3)) && IsHex(d->at(4)) && IsHex(d->at(5)))
				return true;

			return false;

		}
		bool IsAfii(string* d, UINT8 size)
		{
			return d->length() >= 4 && d->at(0) == 'a' && d->at(1) == 'f' && d->at(2) == 'i' && d->at(3) == 'i';
		}

		bool IsHex(char c)
		{
			return c >= 0 && c <= 'F';
		}

		void Clean(string* d, UINT8 size)
		{
			for (int i = 0; i < size; i++)
			{
				if (d->at(i) == '-' || d->at(i) == '_')
					d->replace(i, i + 1, ' ', 1);
			}
		}*/
	};
}