#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include "DWriteFontSource.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteProperties sealed
	{
	public:
		static DWriteProperties^ CreateDefault()
		{
			return ref new DWriteProperties(DWriteFontSource::PerMachine, nullptr, "Segoe UI", "Regular", false, false);
		}

		property bool IsColorFont	{ bool get() { return m_isColorFont; } }

		property bool HasVariations { bool get() { return m_hasVariations; } }

		property String^ FamilyName { String^ get() { return m_familyName; } }

		property String^ FaceName	{ String^ get() { return m_faceName; } }

		/// <summary>
		/// Friendly name of the remote provider, if applicable
		/// </summary>
		property String^ RemoteProviderName
		{
			String^ get() { return m_remoteSource; }
		}

		/// <summary>
		/// Source of the file
		/// </summary>
		property DWriteFontSource Source
		{
			DWriteFontSource get() { return m_source; }
		}

	internal:
		DWriteProperties(DWriteFontSource source, String^ remoteSource, String^ familyName, String^ faceName, bool isColor, bool hasVariations)
		{
			m_isColorFont = isColor;
			m_source = source;
			m_remoteSource = remoteSource;
			m_familyName = familyName;
			m_faceName = faceName;
			m_hasVariations = hasVariations;
		}

	private:
		inline DWriteProperties() { }

		bool m_hasVariations = false;
		bool m_isColorFont = false;
		String^ m_remoteSource = nullptr;
		String^ m_familyName = nullptr;
		String^ m_faceName = nullptr;
		DWriteFontSource m_source = DWriteFontSource::Unknown;
	};
}