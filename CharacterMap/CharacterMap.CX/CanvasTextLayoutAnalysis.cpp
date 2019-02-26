#include "pch.h"
#include "CanvasTextLayoutAnalysis.h"

using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;

CharacterMapCX::CanvasTextLayoutAnalysis::CanvasTextLayoutAnalysis(bool hasColorGlyphs, std::vector<GlyphImageFormat> formats)
{
	m_hasColorGlyphs = hasColorGlyphs;
	auto vec = ref new Vector<GlyphImageFormat>(std::move(formats));
	m_glyphFormats = vec->GetView();

	for each (auto t in GlyphFormats)
	{
		if (t == GlyphImageFormat::Png
			|| t == GlyphImageFormat::Jpeg
			|| t == GlyphImageFormat::Tiff
			|| t == GlyphImageFormat::PremultipliedB8G8R8A8)
		{
			m_containsBitmapGlyphs = true;
			break;
		}
	}
}
