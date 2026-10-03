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
	m_colrv0 = analyzer->HasColrV0;
	m_colrv1 = analyzer->HasColrV1;
	m_hasColorGlyphs = analyzer->HasColorGlyphs || m_colrv0 || m_colrv1;

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
		auto allGlyphs = ref new Vector<uint16>();
		auto allPalettes = ref new Vector<int>();

		for (unsigned int a = 0; a < analyzer->GlyphIndicies.size(); a = a + 1)
		{
			const auto& runGlyphs = analyzer->GlyphIndicies[a];
			auto ind = ref new Vector<uint16>();
			int paletteIdx = (a < analyzer->PaletteIndices.size() && analyzer->HasColorGlyphs)
				? static_cast<int>(analyzer->PaletteIndices[a])
				: -1;

			for (unsigned int b = 0; b < runGlyphs.size(); b = b + 1)
			{
				ind->Append(runGlyphs[b]);
				allGlyphs->Append(runGlyphs[b]);
				allPalettes->Append(paletteIdx);
			}

			gd[a] = ind->GetView();
		}

		m_indicies = gd;
		m_glyphIndices = allGlyphs->GetView();
		m_paletteIndices = allPalettes->GetView();
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
			|| (t & GlyphImageFormat::ColrPaintTree) == GlyphImageFormat::ColrPaintTree
			|| (t & GlyphImageFormat::Svg) == GlyphImageFormat::Svg)
		{
			m_containsVectorColorGlyphs = true;
		}

		if (m_containsBitmapGlyphs && m_containsVectorColorGlyphs)
			break;
	}

	if (m_colrv0 || m_colrv1)
	{
		m_containsVectorColorGlyphs = true;
	}

	auto vec = ref new Vector<GlyphImageFormat>(std::move(analyzer->GlyphFormats));
	m_glyphFormats = vec->GetView();
}