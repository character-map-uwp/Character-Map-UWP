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
			return ref new DWriteProperties(DWriteFontSource::PerMachine, nullptr, "Segoe UI", "Regular", false);
		}

		property bool IsColorFont
		{
			bool get() { return m_isColorFont; }
		}

		/// <summary>
		/// Source of the file
		/// </summary>
		property DWriteFontSource Source
		{
			DWriteFontSource get() { return m_source; }
		}

		/// <summary>
		/// Friendly name of the remote provider, if applicable
		/// </summary>
		property String^ RemoteProviderName
		{
			String^ get() { return m_remoteSource; }
		}

		property String^ FamilyName
		{
			String^ get() { return m_familyName; }
		}

		property String^ FaceName
		{
			String^ get() { return m_faceName; }
		}

	internal:
		DWriteProperties(DWriteFontSource source, String^ remoteSource, String^ familyName, String^ faceName, bool isColor)
		{
			m_isColorFont = isColor;
			m_source = source;
			m_remoteSource = remoteSource;
			m_familyName = familyName;
			m_faceName = faceName;
		}

	private:
		inline DWriteProperties() { }

		bool m_isColorFont = false;
		DWriteFontSource m_source = DWriteFontSource::Unknown;
		String^ m_remoteSource = nullptr;
		String^ m_familyName = nullptr;
		String^ m_faceName = nullptr;
	};
}