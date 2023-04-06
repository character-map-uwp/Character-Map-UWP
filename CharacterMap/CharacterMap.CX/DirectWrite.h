#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <collection.h>
#include "DWriteFontSet.h"
#include "DWriteFontAxis.h"
#include "DWriteFontFace.h"
#include "DWriteKnownFontAxisValues.h"

using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;

namespace CharacterMapCX
{
	public ref class DirectWrite sealed
	{
	public:

		static IVectorView<DWriteFontAxis^>^ GetAxis(DWriteFontFace^ canvasFontFace);

		static IVectorView<DWriteKnownFontAxisValues^>^ GetNamedAxisValues(DWriteFontFace^ canvasFontFace);

		static String^ GetFeatureTag(UINT32 value);

		static String^ GetTagName(String^ tag);

		static String^ GetTagName(UINT32 tag);

		/// <summary>
		/// Get a buffer representing an SVG or Bitmap image glyph. SVG glyphs may be compressed.
		/// </summary>
		static IBuffer^ GetImageDataBuffer(DWriteFontFace^ fontFace, UINT32 pixelsPerEm, UINT unicodeIndex, GlyphImageFormat format);

		/// <summary>
		/// Verifies if a font file actually contains a font(s) usable by the system.
		/// </summary>
		static bool HasValidFonts(Uri^ uri);

		/// <summary>
		/// Verifies if a font is actually completely on a users system. Some cloud fonts may only be partially downloaded.
		/// </summary>
		static bool IsFontLocal(DWriteFontFace^ fontFace);

		/// <summary>
		/// Writes the underlying source file of a FontFace to a stream. 
		/// </summary>
		static IAsyncOperation<bool>^ WriteToStreamAsync(DWriteFontFace^ fontFace, IOutputStream^ stream);

		//static Platform::String^ GetFileName(CanvasFontFace^ fontFace);

		/// <summary>
		/// Attempts to get the source filename of a font. Will return NULL for cloud fonts.
		/// </summary>
		static Platform::String^ GetFileName(DWriteFontFace^ fontFace);

		static IMapView<UINT32, UINT32>^ GetSupportedTypography(DWriteFontFace^ fontFace);

	internal:
		static __inline String^ GetLocaleString(ComPtr<IDWriteLocalizedStrings> strings, int ls, wchar_t* locale);

		static __inline bool IsLocalFont(ComPtr<IDWriteFontFileLoader> loader, const void* refKey, uint32 size);

		static IVectorView<DWriteKnownFontAxisValues^>^ GetNamedAxisValues(ComPtr<IDWriteFontFaceReference> faceRef);

		static IVectorView<DWriteFontAxis^>^ GetAxis(ComPtr<IDWriteFontFaceReference> faceRef);

		static IMapView<UINT32, UINT32>^ GetSupportedTypography(ComPtr<IDWriteFontFaceReference> faceRef);

		//static __inline DWriteFontSet^ GetFonts(ComPtr<IDWriteFontSet3> fontSet);

		static __inline DWriteFontSet^ GetFonts(ComPtr<IDWriteFontCollection3> fontSet);

		static __inline DWriteFontSet^ GetFonts(Uri^ uri, ComPtr<IDWriteFactory7> fac);

		static IVectorView<DWriteFontSet^>^ GetFonts(IVectorView<Uri^>^ uris, ComPtr<IDWriteFactory7> fac);

	private:
		DirectWrite() { };

	};
}