#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include <vector>

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class CanvasTextLayoutAnalysis sealed
	{
	public:
		inline CanvasTextLayoutAnalysis() { }

		property bool HasColorGlyphs
		{
			bool get() { return m_hasColorGlyphs; }
		}

		property bool ContainsBitmapGlyphs
		{
			bool get() { return m_containsBitmapGlyphs; }
		}

		property bool ContainsVectorColorGlyphs
		{
			bool get() { return m_containsVectorColorGlyphs; }
		}

		property bool IsFullVectorBased
		{
			bool get() { return !m_containsBitmapGlyphs; }
		}

		property bool IsMultiLayerColorGlyph
		{
			bool get() { return m_glyphLayerCount > 1; }
		}

		/// <summary>
		/// The number of glyphs that make up this rendered character. For
		/// COLR fonts this defines the number of glyphs composited together
		/// to create the color version of the glyph.
		/// </summary>
		property int GlyphLayerCount
		{
			int get() { return m_glyphLayerCount; }
		}

		/// <summary>
		/// Size of the underlying font file in bytes
		/// </summary>
		property int FileSize
		{
			int get() { return m_fileSize; }
		}

		/// <summary>
		/// Absolute path to the underlying font file
		/// </summary>
		property Platform::String^ FilePath
		{
			Platform::String^ get() { return m_filePath; }
		}

		property IVectorView<GlyphImageFormat>^ GlyphFormats
		{
			IVectorView<GlyphImageFormat>^ get() { return m_glyphFormats; }
		}

		property Array<Windows::UI::Color>^ Colors
		{
			Array<Windows::UI::Color>^ get() { return m_colors; }
		}

		property Array<IVectorView<uint16>^>^ Indicies
		{
			Array<IVectorView<uint16>^>^ get() { return m_indicies; }
		}

	internal:
		CanvasTextLayoutAnalysis(ComPtr<ColorTextAnalyzer> analyzer, ComPtr<IDWriteFontFaceReference> layout);

	private:
		bool m_hasColorGlyphs = false;
		bool m_containsBitmapGlyphs = false;
		bool m_containsVectorColorGlyphs = false;
		int m_glyphLayerCount = 1;
		int m_fileSize = 0;
		Platform::String^ m_filePath = nullptr;
		IVectorView<GlyphImageFormat>^ m_glyphFormats;
		Array<Windows::UI::Color>^ m_colors;
		Array<IVectorView<uint16>^>^ m_indicies;
	};
}