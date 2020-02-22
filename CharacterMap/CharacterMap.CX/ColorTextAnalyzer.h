#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "GlyphImageFormat.h"

namespace CharacterMapCX
{
	class ColorTextAnalyzer : public IDWriteTextRenderer
	{
	public:
		ColorTextAnalyzer(
			Microsoft::WRL::ComPtr<ID2D1Factory> d2dFactory,
			Microsoft::WRL::ComPtr<IDWriteFactory4> dWriteFactory,
			Microsoft::WRL::ComPtr<ID2D1DeviceContext1> m_d2dContext
		);

		~ColorTextAnalyzer();

		bool HasColorGlyphs;

		bool IsCharacterAnalysisMode = false;

		int GlyphLayerCount = 0;

		std::vector<GlyphImageFormat> GlyphFormats;

		std::vector<uint16*> GlyphIndicies;

		std::vector<DWRITE_COLOR_F> RunColors;

		IFACEMETHOD(IsPixelSnappingDisabled)(
			_In_opt_ void* clientDrawingContext,
			_Out_ BOOL* isDisabled
			);

		IFACEMETHOD(GetCurrentTransform)(
			_In_opt_ void* clientDrawingContext,
			_Out_ DWRITE_MATRIX* transform
			);

		IFACEMETHOD(GetPixelsPerDip)(
			_In_opt_ void* clientDrawingContext,
			_Out_ FLOAT* pixelsPerDip
			);

		IFACEMETHOD(DrawGlyphRun)(
			_In_opt_ void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_MEASURING_MODE measuringMode,
			_In_ DWRITE_GLYPH_RUN const* glyphRun,
			_In_ DWRITE_GLYPH_RUN_DESCRIPTION const* glyphRunDescription,
			IUnknown* clientDrawingEffect
			);

		IFACEMETHOD(DrawUnderline)(
			_In_opt_ void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			_In_ DWRITE_UNDERLINE const* underline,
			IUnknown* clientDrawingEffect
			);

		IFACEMETHOD(DrawStrikethrough)(
			_In_opt_ void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			_In_ DWRITE_STRIKETHROUGH const* strikethrough,
			IUnknown* clientDrawingEffect
			);

		IFACEMETHOD(DrawInlineObject)(
			_In_opt_ void* clientDrawingContext,
			FLOAT originX,
			FLOAT originY,
			IDWriteInlineObject* inlineObject,
			BOOL isSideways,
			BOOL isRightToLeft,
			IUnknown* clientDrawingEffect
			);

	public:
		IFACEMETHOD_(unsigned long, AddRef) ();
		IFACEMETHOD_(unsigned long, Release) ();
		IFACEMETHOD(QueryInterface) (
			IID const& riid,
			void** ppvObject
			);

	private:
		unsigned long                                m_refCount;
		Microsoft::WRL::ComPtr<ID2D1Factory>         m_d2dFactory;
		Microsoft::WRL::ComPtr<IDWriteFactory4>      m_dwriteFactory;
		Microsoft::WRL::ComPtr<ID2D1DeviceContext1>  m_d2dDeviceContext;
	};

	inline void ThrowIfFailed(HRESULT hr)
	{
		if (FAILED(hr))
		{
			// Set a breakpoint on this line to catch Win32 API errors.
			throw Platform::Exception::CreateException(hr);
		}
	}
};