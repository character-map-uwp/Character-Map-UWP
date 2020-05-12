#include "pch.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DirectWrite.h"
#include "DWriteFontAxis.h"

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI;
using namespace CharacterMapCX;

CharacterMapCX::CanvasTextLayoutAnalysis::CanvasTextLayoutAnalysis(ComPtr<ColorTextAnalyzer> analyzer, ComPtr<IDWriteFontFaceReference> fontFaceRef)
{
	m_hasColorGlyphs = analyzer->HasColorGlyphs;

	if (analyzer->IsCharacterAnalysisMode)
	{
		m_glyphLayerCount = analyzer->GlyphLayerCount;

		auto colors = ref new Array<Color>(analyzer->RunColors.size());
		float max = 255.0;
		for (unsigned int a = 0; a < analyzer->RunColors.size(); a = a + 1)
		{
			DWRITE_COLOR_F color = analyzer->RunColors[a];
			Color c = ColorHelper::FromArgb((UINT)(color.a * max), (UINT)(color.r * max), (UINT)(color.g * max), (UINT)(color.b * max));
			colors[a] = c;
		}
		m_colors = colors;

		auto gd = ref new Array<IVectorView<uint16>^>(analyzer->GlyphIndicies.size());

		for(unsigned int a = 0; a < analyzer->GlyphIndicies.size(); a = a + 1)
		{
			auto i = analyzer->GlyphIndicies[a];
			auto ind = ref new Vector<uint16>(sizeof(i));
			for (unsigned int b = 0; b < sizeof(i); b = b + 1)
			{
				ind->Append(i[b]);
			}

			gd[a] = ind->GetView();
		}

		m_indicies = gd;
	}

	for (GlyphImageFormat t : analyzer->GlyphFormats)
	{
		if (t == GlyphImageFormat::Png
			|| t == GlyphImageFormat::Jpeg
			|| t == GlyphImageFormat::Tiff
			|| t == GlyphImageFormat::PremultipliedB8G8R8A8)
		{
			m_containsBitmapGlyphs = true;
		}
		else if ((t & GlyphImageFormat::Colr) == GlyphImageFormat::Colr
			|| (t & GlyphImageFormat::Svg) == GlyphImageFormat::Svg)
		{
			m_containsVectorColorGlyphs = true;
		}

		if (m_containsBitmapGlyphs && m_containsVectorColorGlyphs)
			break;
	}

	auto vec = ref new Vector<GlyphImageFormat>(std::move(analyzer->GlyphFormats));
	m_glyphFormats = vec->GetView();

}