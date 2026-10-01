#pragma once

#include "pch.h"
#include <wrl.h>
#include <wrl/client.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <collection.h>
#include <WindowsNumerics.h>
#include <algorithm>
#include <float.h>

#include "DWHelpers.h"
#include "DWriteFontFace.h"
#include "DirectWrite.h"

using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Storage::Streams;
using namespace Windows::UI::Text;
using namespace Platform;
using namespace Platform::Collections;

namespace CharacterMapCX
{
	class OutlineGeometrySink : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteGeometrySink>
	{
	public:
		Vector<IVectorView<float2>^>^ Contours = ref new Vector<IVectorView<float2>^>();
		Vector<bool>^ PointOnCurve = ref new Vector<bool>();
		Vector<float2>^ CurrentContour = nullptr;
		float MinX = FLT_MAX, MinY = FLT_MAX, MaxX = -FLT_MAX, MaxY = -FLT_MAX;

		void UpdateBounds(float x, float y)
		{
			if (x < MinX) MinX = x;
			if (x > MaxX) MaxX = x;
			if (y < MinY) MinY = y;
			if (y > MaxY) MaxY = y;
		}

		STDMETHOD_(void, SetFillMode)(D2D1_FILL_MODE fillMode) override { }
		STDMETHOD_(void, SetSegmentFlags)(D2D1_PATH_SEGMENT vertexFlags) override { }

		STDMETHOD_(void, BeginFigure)(D2D1_POINT_2F startPoint, D2D1_FIGURE_BEGIN figureBegin) override
		{
			CurrentContour = ref new Vector<float2>();
			CurrentContour->Append(float2(startPoint.x, startPoint.y));
			PointOnCurve->Append(true);
			UpdateBounds(startPoint.x, startPoint.y);
		}

		STDMETHOD_(void, AddLines)(const D2D1_POINT_2F* points, UINT32 pointsCount) override
		{
			if (CurrentContour != nullptr)
			{
				for (UINT32 i = 0; i < pointsCount; ++i)
				{
					CurrentContour->Append(float2(points[i].x, points[i].y));
					PointOnCurve->Append(true);
					UpdateBounds(points[i].x, points[i].y);
				}
			}
		}

		STDMETHOD_(void, AddBeziers)(const D2D1_BEZIER_SEGMENT* beziers, UINT32 beziersCount) override
		{
			if (CurrentContour != nullptr && CurrentContour->Size > 0)
			{
				for (UINT32 i = 0; i < beziersCount; ++i)
				{
					float2 p0 = CurrentContour->GetAt(CurrentContour->Size - 1);
					float2 p1 = float2(beziers[i].point1.x, beziers[i].point1.y);
					float2 p2 = float2(beziers[i].point2.x, beziers[i].point2.y);
					float2 p3 = float2(beziers[i].point3.x, beziers[i].point3.y);

					float2 q1 = p0 + 0.75f * (p1 - p0);
					float2 q2 = p3 + 0.75f * (p2 - p3);
					float2 m = (q1 + q2) / 2.0f;

					CurrentContour->Append(q1);
					PointOnCurve->Append(false);
					CurrentContour->Append(m);
					PointOnCurve->Append(true);

					CurrentContour->Append(q2);
					PointOnCurve->Append(false);
					CurrentContour->Append(p3);
					PointOnCurve->Append(true);

					UpdateBounds(q1.x, q1.y);
					UpdateBounds(m.x, m.y);
					UpdateBounds(q2.x, q2.y);
					UpdateBounds(p3.x, p3.y);
				}
			}
		}

		STDMETHOD_(void, EndFigure)(D2D1_FIGURE_END figureEnd) override
		{
			if (CurrentContour != nullptr && CurrentContour->Size > 0)
			{
				Contours->Append(CurrentContour->GetView());
			}
			CurrentContour = nullptr;
		}

		STDMETHOD(Close)() override { return S_OK; }
	};

	class ScopedCommandList
	{
	public:
		ComPtr<ID2D1DeviceContext> Context;
		ComPtr<ID2D1CommandList> CommandList;

		ScopedCommandList(ID2D1Device* device)
		{
			if (device && SUCCEEDED(device->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &Context)))
			{
				if (SUCCEEDED(Context->CreateCommandList(&CommandList)))
				{
					Context->SetTarget(CommandList.Get());
					Context->BeginDraw();
				}
			}
		}

		bool IsValid() const { return Context != nullptr && CommandList != nullptr; }

		HRESULT Finish()
		{
			if (!IsValid()) return E_FAIL;
			HRESULT hr = Context->EndDraw();
			Context->SetTarget(nullptr);
			if (FAILED(hr)) return hr;
			return CommandList->Close();
		}

		D2D1_RECT_F GetBounds(const D2D1_RECT_F& fallback = D2D1::RectF(0, 0, 0, 0)) const
		{
			if (!IsValid()) return fallback;
			D2D1_RECT_F bounds{};
			HRESULT hr = Context->GetImageLocalBounds(CommandList.Get(), &bounds);
			float w = bounds.right - bounds.left;
			float h = bounds.bottom - bounds.top;
			if (FAILED(hr) || w <= 0.0f || h <= 0.0f)
				return fallback;
			return bounds;
		}
	};

	inline IRandomAccessStream^ RasterizeCommandListToPNG(
		ID2D1DeviceContext* context,
		ID2D1CommandList* commandList,
		const D2D1_RECT_F& inkBounds,
		UINT32 targetWidth,
		UINT32 targetHeight,
		bool fitToTarget = true)
	{
		if (!context || !commandList || targetWidth == 0 || targetHeight == 0)
			return nullptr;

		float inkWidth = inkBounds.right - inkBounds.left;
		float inkHeight = inkBounds.bottom - inkBounds.top;
		if (inkWidth <= 0.0f || inkHeight <= 0.0f)
			return nullptr;

		D2D1_BITMAP_PROPERTIES1 bp = D2D1::BitmapProperties1(
			D2D1_BITMAP_OPTIONS_TARGET,
			D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
			96.0f,
			96.0f);

		ComPtr<ID2D1Bitmap1> targetBitmap;
		HRESULT hr = context->CreateBitmap(D2D1::SizeU(targetWidth, targetHeight), nullptr, 0, &bp, &targetBitmap);
		if (FAILED(hr))
			return nullptr;

		context->SetTarget(targetBitmap.Get());
		context->BeginDraw();
		context->Clear(D2D1::ColorF(0, 0, 0, 0));

		float scale = (std::min)(static_cast<float>(targetWidth) / inkWidth, static_cast<float>(targetHeight) / inkHeight);
		if (!fitToTarget)
			scale = (std::min)(1.0f, scale);

		float x = (static_cast<float>(targetWidth) - inkWidth * scale) / 2.0f - inkBounds.left * scale;
		float y = (static_cast<float>(targetHeight) - inkHeight * scale) / 2.0f - inkBounds.top * scale;

		context->SetTransform(
			D2D1::Matrix3x2F::Scale(scale, scale) *
			D2D1::Matrix3x2F::Translation(x, y));

		context->DrawImage(commandList);
		hr = context->EndDraw();
		context->SetTarget(nullptr);
		if (FAILED(hr))
			return nullptr;

		return DirectWrite::GetPNGStream(targetBitmap, targetWidth, targetHeight);
	}

	inline ComPtr<IDWriteTextLayout> CreateTextLayout(
		IDWriteFactory7* factory,
		DWriteFontFace^ fontFace,
		Platform::String^ text,
		float size,
		IVectorView<UINT32>^ typographyFeatures)
	{
		if (!factory || !fontFace || !text || text->Length() == 0 || size <= 0.0f)
			return nullptr;

		FontWeight weight = fontFace->Properties->Weight;
		FontStyle style = fontFace->Properties->Style;
		FontStretch stretch = fontFace->Properties->Stretch;

		ComPtr<IDWriteTextFormat> tempFormat;
		HRESULT hr = factory->CreateTextFormat(
			fontFace->Properties->FamilyName->Data(),
			fontFace->GetFontCollection().Get(),
			static_cast<DWRITE_FONT_WEIGHT>(weight.Weight),
			static_cast<DWRITE_FONT_STYLE>(style),
			static_cast<DWRITE_FONT_STRETCH>(stretch),
			size,
			L"en-us",
			&tempFormat);
		if (FAILED(hr))
			return nullptr;

		tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
		tempFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

		ComPtr<IDWriteTextLayout> textLayout;
		hr = factory->CreateTextLayout(
			text->Data(),
			text->Length(),
			tempFormat.Get(),
			size,
			size,
			&textLayout);
		if (FAILED(hr))
			return nullptr;

		if (typographyFeatures != nullptr && typographyFeatures->Size > 0)
		{
			ComPtr<IDWriteTypography> typo;
			if (SUCCEEDED(factory->CreateTypography(&typo)))
			{
				for (unsigned int i = 0; i < typographyFeatures->Size; ++i)
				{
					DWRITE_FONT_FEATURE f;
					f.nameTag = static_cast<DWRITE_FONT_FEATURE_TAG>(typographyFeatures->GetAt(i));
					f.parameter = 1;
					typo->AddFontFeature(f);
				}
				textLayout->SetTypography(typo.Get(), DWRITE_TEXT_RANGE{ 0, text->Length() });
			}
		}

		return textLayout;
	}
}
