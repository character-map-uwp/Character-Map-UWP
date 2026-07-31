#pragma once

#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include "DWriteFontSource.h"
#include "DWriteFontSimulations.h"

using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Text;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteProperties sealed
	{
	public:
		static DWriteProperties^ CreateDefault()
		{
			return ref new DWriteProperties(DWriteFontSource::PerMachine, nullptr, "Segoe UI", "Regular", false);
		}

		property DWriteFontSimulations Simulations { DWriteFontSimulations get() { return m_simulations; } }

		property bool IsSimulated { bool get() { return m_isSimulated; } }

		property bool IsMonospacedFont { bool get() { return m_font->IsMonospacedFont(); } }

		property bool IsSymbolFont	{ bool get() { return m_isSymbolFont; } }

		property String^ FamilyName { String^ get() { return m_familyName; } }

		property String^ FaceName	{ String^ get() { return m_faceName; } }

		property FontWeight Weight { FontWeight get() { return m_weight; } }

		property FontStyle Style { FontStyle get() { return m_style; } }

		property FontStretch Stretch { FontStretch get() { return m_stretch; } }

		property bool IsColorFont		{ bool get() { EnsureFontFacePropertiesLoaded(); return m_isColorFont; } }
		property bool HasCFFOutlines	{ bool get() { EnsureFontFacePropertiesLoaded(); return m_hasCFF; } }
		property bool HasTTFOutlines	{ bool get() { EnsureFontFacePropertiesLoaded(); return m_hasTTF; } }
		property bool HasSVGOutlines	{ bool get() { EnsureFontFacePropertiesLoaded(); return m_hasSVG; } }
		property bool HasColorBitmapOutlines	{ bool get() { EnsureFontFacePropertiesLoaded(); return m_hasColorBitmap; } }
		property bool HasMonoBitmapOutlines		{ bool get() { EnsureFontFacePropertiesLoaded(); return m_hasMonoBitmap; } }


		/// <summary>
		/// If true, font is from a remote provider
		/// </summary>
		property bool IsRemote { bool get() { LoadRemoteProperties(); return m_isRemote; } }

		property bool HasVariations 
		{ 
			bool get() 
			{ 
				if (!m_loadedVariations)
				{
					ComPtr<IDWriteFontFace3> f3;
					ComPtr<IDWriteFontFace5> face;
					m_font->CreateFontFace(&f3);
					f3.As<IDWriteFontFace5>(&face);
					m_hasVariations = face->HasVariations();
					m_loadedVariations = true;
				}

				return m_hasVariations; 
			}
		}

		/// <summary>
		/// Source of the file
		/// </summary>
		property DWriteFontSource Source { DWriteFontSource get() { return m_source; } }

		property Array<UINT8>^ Panose { Array<UINT8>^ get() 
		{
			DWRITE_PANOSE pan[10];
			m_font->GetPanose(pan);

			bool valid = false;
			for (int i = 0; i < 10; i++)
			{
				if (pan->values[i] > 0)
				{
					valid = true;
					break;
				}
			}

			if (valid == false)
				return nullptr;

			return Platform::ArrayReference<UINT8>(reinterpret_cast<UINT8*>(pan), 10);
		}}


		/// <summary>
		/// Friendly name of the remote provider, if applicable
		/// </summary>
		property String^ RemoteProviderName
		{
			String^ get() { return m_remoteSource; }
		}

	internal:

		DWriteProperties(DWriteFontSource source, String^ remoteSource, String^ familyName, String^ faceName, ComPtr<IDWriteFont3> font)
		{
			m_weight = Windows::UI::Text::FontWeight{ static_cast<uint16_t>(font->GetWeight()) };
			m_style = static_cast<Windows::UI::Text::FontStyle>(font->GetStyle());
			m_stretch = static_cast<Windows::UI::Text::FontStretch>(font->GetStretch());

			m_isSymbolFont = font->IsSymbolFont();
			m_isColorFont = font->IsColorFont();

			m_font = font;

			m_source = source;
			m_remoteSource = remoteSource;
			m_familyName = familyName;
			m_faceName = faceName;

			m_simulations = static_cast<DWriteFontSimulations>(font->GetSimulations());
			m_isSimulated = m_simulations != DWriteFontSimulations::None;
		}

		DWriteProperties(DWriteFontSource source, String^ remoteSource, String^ familyName, String^ faceName, bool isColor, bool hasVariations)
		{
			m_isColorFont = isColor;
			m_source = source;
			m_remoteSource = remoteSource;
			m_familyName = familyName;
			m_faceName = faceName;
			m_hasVariations = hasVariations;
		}

		bool m_isSimulated = false;
		DWriteFontSource m_source = DWriteFontSource::Unknown;

	private:
		inline DWriteProperties() { }

		ComPtr<IDWriteFont3> m_font = nullptr;

		void LoadRemoteProperties()
		{
			if (m_loadedRemote)
				return;

			ComPtr<IDWriteFontFaceReference> ref;
			ComPtr<IDWriteFontFile> file;
			ComPtr<IDWriteFontFileLoader> loader;
			ComPtr<IDWriteRemoteFontFileLoader> remoteLoader;
			const void* refKey = nullptr;
			uint32 size = 0;

			if (m_font->GetFontFaceReference(&ref) == S_OK
				&& ref->GetFontFile(&file) == S_OK
				&& file->GetLoader(&loader) == S_OK
				&& file->GetReferenceKey(&refKey, &size) == S_OK
				&& loader->QueryInterface<IDWriteRemoteFontFileLoader>(&remoteLoader) == S_OK)
			{
				m_isRemote = true;
				m_source = DWriteFontSource::RemoteFontProvider;
			}

			m_loadedRemote = true;
		}

		void EnsureFontFacePropertiesLoaded()
		{
			if (m_loadedOutlines) return;
			m_loadedOutlines = true;
			if (m_font == nullptr) return;
			ComPtr<IDWriteFontFace> fontFace;

			if (SUCCEEDED(m_font->CreateFontFace(&fontFace)))
			{
				const void* data = nullptr;
				UINT32 size = 0;
				void* ctx = nullptr;
				BOOL exists = FALSE;

				// Check CFF
				if ((SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'F', 'F', ' '), &data, &size, &ctx, &exists)) && exists) ||
					(SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'F', 'F', '2'), &data, &size, &ctx, &exists)) && exists))
				{
					m_hasCFF = true;
					fontFace->ReleaseFontTable(ctx);
				}
				// Check TTF
				if (SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('g', 'l', 'y', 'f'), &data, &size, &ctx, &exists)) && exists)
				{
					m_hasTTF = true;
					fontFace->ReleaseFontTable(ctx);
				}
				// Check SVG
				if (SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('S', 'V', 'G', ' '), &data, &size, &ctx, &exists)) && exists)
				{
					m_hasSVG = true;
					fontFace->ReleaseFontTable(ctx);
				}
				// Check Color Bitmaps (sbix, CBLC)
				if ((SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('s', 'b', 'i', 'x'), &data, &size, &ctx, &exists)) && exists) ||
					(SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'B', 'L', 'C'), &data, &size, &ctx, &exists)) && exists))
				{
					m_hasColorBitmap = true;
					fontFace->ReleaseFontTable(ctx);
				}
				// Check Legacy Monochrome Bitmaps (EBLC)
				if (SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('E', 'B', 'L', 'C'), &data, &size, &ctx, &exists)) && exists)
				{
					m_hasMonoBitmap = true;
					fontFace->ReleaseFontTable(ctx);
				}

				// Perform a more extensive double-check on IsColor
				if (m_isColorFont == false)
				{
					if ((SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('S', 'V', 'G', ' '), &data, &size, &ctx, &exists)) && exists) ||
						(SUCCEEDED(fontFace->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'O', 'L', 'R'), &data, &size, &ctx, &exists)) && exists))
					{
						m_isColorFont = true;
						fontFace->ReleaseFontTable(ctx);
					}
				}
			}
		}

		FontWeight m_weight;
		FontStyle m_style = FontStyle::Normal;
		FontStretch m_stretch = FontStretch::Normal;
		DWriteFontSimulations m_simulations = DWriteFontSimulations::None;

		bool m_loadedVariations = false;
		bool m_loadedRemote = false;

		bool m_isRemote = false;
		bool m_hasVariations = false;
		bool m_isColorFont = false;
		bool m_isSymbolFont = false;
		String^ m_remoteSource = nullptr;
		String^ m_familyName = nullptr;
		String^ m_faceName = nullptr;

		bool m_loadedOutlines = false;
		bool m_hasCFF    = false;
		bool m_hasTTF    = false;
		bool m_hasSVG    = false;
		bool m_hasColorBitmap = false;
		bool m_hasMonoBitmap = false;
    
	};
}