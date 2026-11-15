#pragma once

#include <windows.h>
#include <thumbcache.h>
#include <propsys.h>
#include <wrl/implements.h>
#include <wrl/client.h>
#include <dwrite_3.h>
#include <d2d1.h>
#include <wincodec.h>
#include <string>
#include <vector>
#include <cstdint>

class __declspec(uuid("7E6B4C9A-3D21-4F8E-9A15-2C8D4E6F1033")) FontThumbnailProvider final
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          IInitializeWithStream,
          IThumbnailProvider>
{
public:
    FontThumbnailProvider() = default;
    ~FontThumbnailProvider() = default;

    // IInitializeWithStream
    IFACEMETHODIMP Initialize(IStream* pstream, DWORD grfMode) override;

    // IThumbnailProvider
    IFACEMETHODIMP GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha) override;

private:
    HRESULT CreateFontFaceFromData(
        IDWriteFactory5* dwriteFactory,
        IDWriteFontFace3** outFontFace,
        IDWriteFontCollection1** outFontCollection,
        std::wstring& outFamilyName);

    HRESULT RenderThumbnailBitmap(
        UINT cx,
        IDWriteFactory5* dwriteFactory,
        IDWriteFontFace3* fontFace,
        IDWriteFontCollection1* fontCollection,
        const std::wstring& familyName,
        HBITMAP* phbmp);

    std::vector<uint8_t> m_fontData;
};
