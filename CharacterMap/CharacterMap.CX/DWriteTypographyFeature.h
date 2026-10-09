#pragma once

#include "DWriteFontSource.h"

namespace CharacterMapCX
{
	public value struct DWriteTypographyFeature
	{
		DWriteTypographyFeatureName Name;
		uint32 Parameter;
	};

	inline bool operator==(const DWriteTypographyFeature& a, const DWriteTypographyFeature& b)
	{
		return a.Name == b.Name && a.Parameter == b.Parameter;
	}

	inline bool operator!=(const DWriteTypographyFeature& a, const DWriteTypographyFeature& b)
	{
		return !(a == b);
	}
}
