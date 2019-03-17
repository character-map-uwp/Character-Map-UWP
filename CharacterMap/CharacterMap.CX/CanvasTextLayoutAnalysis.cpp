#include "pch.h"
#include "CanvasTextLayoutAnalysis.h"

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;

CharacterMapCX::CanvasTextLayoutAnalysis::CanvasTextLayoutAnalysis(ComPtr<ColorTextAnalyzer> analyzer)
{
	m_hasColorGlyphs = analyzer->HasColorGlyphs;

	if (analyzer->IsCharacterAnalysisMode)
	{
		m_glyphLayerCount = analyzer->GlyphLayerCount;
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