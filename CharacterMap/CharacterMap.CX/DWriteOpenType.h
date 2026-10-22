#pragma once

#include <pch.h>

using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteOpenTypeSubstitution sealed
	{
	public:
		property uint16 InputGlyph;
		property uint16 TargetGlyph;
		property IVectorView<uint16>^ InputSequence;
		property IVectorView<uint16>^ TargetSequence;
	};

	public ref class DWriteOpenTypeFeatureGroup sealed
	{
	public:
		property uint32 FeatureTag;
		property String^ Tag;
		property String^ FeatureName;
		property String^ Description;
		property IVectorView<DWriteOpenTypeSubstitution^>^ Substitutions;
	};
}
