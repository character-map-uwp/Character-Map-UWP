#pragma once
#include <pch.h>
#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
#include "DWriteFontAxisAttribute.h"
#include "DWriteNamedFontAxisValue.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;
using namespace Platform::Collections;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;
using namespace concurrency;

namespace CharacterMapCX
{
	public ref class DWriteFallbackFont sealed
	{

	internal:
		DWriteFallbackFont(ComPtr<IDWriteFontFallback> fallback)
		{
			Fallback = fallback;
		}

		ComPtr<IDWriteFontFallback> Fallback;
	};
}


//namespace CharacterMapCX
//{
//	// Class used to enable parralell loading
//	public ref class DWriteIntermediateFontSet sealed
//	{
//	public:
//		Analyse()
//		{
//			int appxCount = 0;
//			int cloudCount = 0;
//			int variableCount = 0;
//
//			for (uint32_t i = 0; i < Vectors->Size; ++i)
//			{
//				auto vec = Vectors->GetAt(i);
//
//				for (uint32_t j = 0; i < vec->Size; ++j)
//				{
//					auto jvec = Vectors->GetAt(i);
//				}
//			}
//		}
//
//	internal:
//
//		Vector<Vector<ComPtr<IDWriteFontFaceReference1>>^>^ Vectors;
//
//		DWriteIntermediateFontSet(ComPtr<IDWriteFontSet3> fontSet)
//		{
//			Vectors = ref new Vector<Vector<ComPtr<IDWriteFontFaceReference1>>^>();
//			auto fontCount = fontSet->GetFontCount();
//
//			auto curr = ref new Vector<ComPtr<IDWriteFontFaceReference1>>();
//			vec->Append(curr);
//
//			for (uint32_t i = 0; i < fontCount; ++i)
//			{
//				ComPtr<IDWriteFontFaceReference1> fontResource;
//				ThrowIfFailed(fontSet->GetFontFaceReference(i, &fontResource));
//
//				if (fontResource->GetLocality() == DWRITE_LOCALITY::DWRITE_LOCALITY_LOCAL)
//				{
//					curr->Append(fontResource);
//
//					if (curr->Size == 20)
//					{
//						curr = ref new Vector<ComPtr<IDWriteFontFaceReference1>>();
//						vec->Append(curr);
//					}
//				}
//			}
//		}
//	};
//}