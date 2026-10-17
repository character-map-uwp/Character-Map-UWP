#pragma once

using namespace Windows::Foundation::Collections;

namespace CharacterMapCX
{
	public ref class DWriteLigature sealed
	{
	public:
		property uint16 LigatureGlyph;
		property IVectorView<uint16>^ ComponentGlyphs;
	};

	public ref class DWriteLigatureFeature sealed
	{
	public:
		property uint32 FeatureTag;
		property String^ FeatureName;
		property IVectorView<DWriteLigature^>^ Ligatures;
	};
}
