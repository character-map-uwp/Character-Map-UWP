#pragma once
#include <basetsd.h>

template <class T> void SafeRelease(T** ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

namespace
{
	/// <summary>
/// Opposite of DWRITE_MAKE_OPENTYPE_TAG, returns String
/// representation of OpenType tag.
/// </summary>
	Platform::String^ GetOpenTypeFeatureTag(UINT32 value)
	{
		wchar_t buffer[] = L"    ";
		buffer[3] = (wchar_t)((value >> 24) & 0xFF);
		buffer[2] = (wchar_t)((value >> 16) & 0xFF);
		buffer[1] = (wchar_t)((value >> 8) & 0xFF);
		buffer[0] = (wchar_t)((value >> 0) & 0xFF);

		return ref new Platform::String(buffer);
	}
}