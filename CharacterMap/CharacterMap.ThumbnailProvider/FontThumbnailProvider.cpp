#include "FontThumbnailProvider.h"
#include <algorithm>
#include <cstring>

using Microsoft::WRL::ComPtr;

namespace
{
    constexpr size_t MAX_FONT_STREAM_BYTES = 64u * 1024u * 1024u;

    class RawMemoryFontFileStream final
        : public Microsoft::WRL::RuntimeClass<
              Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
              IDWriteFontFileStream>
    {
    public:
        RawMemoryFontFileStream(const uint8_t* data, UINT64 size)
            : m_data(data, data + size)
        {
        }

        IFACEMETHODIMP ReadFileFragment(
            void const** fragmentStart,
            UINT64 fileOffset,
            UINT64 fragmentSize,
            void** fragmentContext) override
        {
            if (fragmentStart == nullptr || fragmentContext == nullptr)
                return E_INVALIDARG;

            *fragmentStart = nullptr;
            *fragmentContext = nullptr;

            if (fileOffset > m_data.size() || fragmentSize > m_data.size() - fileOffset)
                return E_FAIL;

            *fragmentStart = m_data.data() + static_cast<size_t>(fileOffset);
            return S_OK;
        }

        IFACEMETHODIMP_(void) ReleaseFileFragment(void* /*fragmentContext*/) override
        {
        }

        IFACEMETHODIMP GetFileSize(UINT64* fileSize) override
        {
            if (fileSize == nullptr)
                return E_INVALIDARG;
            *fileSize = m_data.size();
            return S_OK;
        }

        IFACEMETHODIMP GetLastWriteTime(UINT64* lastWriteTime) override
        {
            if (lastWriteTime == nullptr)
                return E_INVALIDARG;
            *lastWriteTime = 0;
            return S_OK;
        }

    private:
        std::vector<uint8_t> m_data;
    };

    class SingleStreamFontFileLoader final
        : public Microsoft::WRL::RuntimeClass<
              Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
              IDWriteFontFileLoader>
    {
    public:
        explicit SingleStreamFontFileLoader(IDWriteFontFileStream* stream)
            : m_stream(stream)
        {
        }

        IFACEMETHODIMP CreateStreamFromKey(
            void const* /*fontFileReferenceKey*/,
            UINT32 /*fontFileReferenceKeySize*/,
            IDWriteFontFileStream** fontFileStream) override
        {
            if (fontFileStream == nullptr)
                return E_INVALIDARG;
            *fontFileStream = nullptr;
            if (m_stream == nullptr)
                return E_FAIL;
            return m_stream.CopyTo(fontFileStream);
        }

    private:
        ComPtr<IDWriteFontFileStream> m_stream;
    };

    struct LoaderUnregisterGuard
    {
        ComPtr<IDWriteFactory5> factory;
        ComPtr<IDWriteFontFileLoader> loader;

        ~LoaderUnregisterGuard()
        {
            if (factory && loader)
                factory->UnregisterFontFileLoader(loader.Get());
        }
    };

    std::wstring GetPrimaryFamilyName(IDWriteFontCollection* collection)
    {
        if (collection == nullptr || collection->GetFontFamilyCount() == 0)
            return L"";

        ComPtr<IDWriteFontFamily> family;
        if (FAILED(collection->GetFontFamily(0, &family)) || !family)
            return L"";

        ComPtr<IDWriteLocalizedStrings> names;
        if (FAILED(family->GetFamilyNames(&names)) || !names || names->GetCount() == 0)
            return L"";

        UINT32 index = 0;
        BOOL exists = FALSE;
        if (FAILED(names->FindLocaleName(L"en-us", &index, &exists)) || !exists)
            index = 0;

        UINT32 length = 0;
        if (FAILED(names->GetStringLength(index, &length)) || length == 0)
            return L"";

        std::wstring result(length + 1, L'\0');
        if (FAILED(names->GetString(index, &result[0], length + 1)))
            return L"";

        result.resize(length);
        return result;
    }
}

IFACEMETHODIMP FontThumbnailProvider::Initialize(IStream* pstream, DWORD /*grfMode*/)
{
    if (pstream == nullptr)
        return E_INVALIDARG;

    m_fontData.clear();

    STATSTG stat{};
    if (SUCCEEDED(pstream->Stat(&stat, STATFLAG_NONAME)) && stat.cbSize.QuadPart > 0)
    {
        if (stat.cbSize.QuadPart > MAX_FONT_STREAM_BYTES)
            return E_FAIL;
        m_fontData.reserve(static_cast<size_t>(stat.cbSize.QuadPart));
    }

    uint8_t buffer[65536];
    ULONG bytesRead = 0;
    while (true)
    {
        HRESULT hr = pstream->Read(buffer, sizeof(buffer), &bytesRead);
        if (FAILED(hr))
            return hr;
        if (bytesRead == 0)
            break;
        if (m_fontData.size() + bytesRead > MAX_FONT_STREAM_BYTES)
            return E_FAIL;
        m_fontData.insert(m_fontData.end(), buffer, buffer + bytesRead);
    }

    return m_fontData.empty() ? E_FAIL : S_OK;
}

IFACEMETHODIMP FontThumbnailProvider::GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha)
{
    if (phbmp == nullptr || pdwAlpha == nullptr || cx == 0)
        return E_INVALIDARG;

    *phbmp = nullptr;
    *pdwAlpha = WTSAT_UNKNOWN;

    if (m_fontData.empty())
        return E_FAIL;

    ComPtr<IDWriteFactory5> dwriteFactory;
    HRESULT hr = DWriteCreateFactory(
        DWRITE_FACTORY_TYPE_SHARED,
        __uuidof(IDWriteFactory5),
        reinterpret_cast<IUnknown**>(dwriteFactory.GetAddressOf()));
    if (FAILED(hr))
        return hr;

    ComPtr<IDWriteFontFace3> fontFace;
    ComPtr<IDWriteFontCollection1> fontCollection;
    std::wstring familyName;
    hr = CreateFontFaceFromData(dwriteFactory.Get(), &fontFace, &fontCollection, familyName);
    if (FAILED(hr))
        return hr;

    hr = RenderThumbnailBitmap(cx, dwriteFactory.Get(), fontFace.Get(), fontCollection.Get(), familyName, phbmp);
    if (SUCCEEDED(hr))
        *pdwAlpha = WTSAT_ARGB;

    return hr;
}

HRESULT FontThumbnailProvider::CreateFontFaceFromData(
    IDWriteFactory5* dwriteFactory,
    IDWriteFontFace3** outFontFace,
    IDWriteFontCollection1** outFontCollection,
    std::wstring& outFamilyName)
{
    *outFontFace = nullptr;
    *outFontCollection = nullptr;
    outFamilyName.clear();

    const UINT32 dataSize = static_cast<UINT32>(m_fontData.size());
    DWRITE_CONTAINER_TYPE containerType = dwriteFactory->AnalyzeContainerType(m_fontData.data(), dataSize);

    ComPtr<IDWriteFontFileStream> fontStream;
    if (containerType == DWRITE_CONTAINER_TYPE_WOFF || containerType == DWRITE_CONTAINER_TYPE_WOFF2)
    {
        HRESULT hr = dwriteFactory->UnpackFontFile(containerType, m_fontData.data(), dataSize, &fontStream);
        if (FAILED(hr) || !fontStream)
            return FAILED(hr) ? hr : E_FAIL;
    }
    else
    {
        fontStream = Microsoft::WRL::Make<RawMemoryFontFileStream>(m_fontData.data(), dataSize);
    }

    auto loader = Microsoft::WRL::Make<SingleStreamFontFileLoader>(fontStream.Get());
    HRESULT hr = dwriteFactory->RegisterFontFileLoader(loader.Get());
    if (FAILED(hr))
        return hr;

    LoaderUnregisterGuard guard{ dwriteFactory, loader };

    const UINT32 key = 1;
    ComPtr<IDWriteFontFile> fontFile;
    hr = dwriteFactory->CreateCustomFontFileReference(&key, sizeof(key), loader.Get(), &fontFile);
    if (FAILED(hr))
        return hr;

    ComPtr<IDWriteFontSetBuilder1> setBuilder;
    hr = dwriteFactory->CreateFontSetBuilder(&setBuilder);
    if (FAILED(hr))
        return hr;

    hr = setBuilder->AddFontFile(fontFile.Get());
    if (FAILED(hr))
        return hr;

    ComPtr<IDWriteFontSet> fontSet;
    hr = setBuilder->CreateFontSet(&fontSet);
    if (FAILED(hr))
        return hr;

    if (fontSet->GetFontCount() == 0)
        return E_FAIL;

    ComPtr<IDWriteFontCollection1> collection;
    hr = dwriteFactory->CreateFontCollectionFromFontSet(fontSet.Get(), &collection);
    if (FAILED(hr))
        return hr;

    ComPtr<IDWriteFontFaceReference> faceRef;
    hr = fontSet->GetFontFaceReference(0, &faceRef);
    if (FAILED(hr))
        return hr;

    ComPtr<IDWriteFontFace3> face;
    hr = faceRef->CreateFontFace(&face);
    if (FAILED(hr))
        return hr;

    outFamilyName = GetPrimaryFamilyName(collection.Get());
    *outFontFace = face.Detach();
    *outFontCollection = collection.Detach();
    return S_OK;
}

HRESULT FontThumbnailProvider::RenderThumbnailBitmap(
    UINT cx,
    IDWriteFactory5* dwriteFactory,
    IDWriteFontFace3* fontFace,
    IDWriteFontCollection1* fontCollection,
    const std::wstring& familyName,
    HBITMAP* phbmp)
{
    const UINT size = (std::max)(cx, 32u);

    ComPtr<IWICImagingFactory> wicFactory;
    HRESULT hr = CoCreateInstance(
        CLSID_WICImagingFactory,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&wicFactory));
    if (FAILED(hr))
        return hr;

    ComPtr<IWICBitmap> wicBitmap;
    hr = wicFactory->CreateBitmap(size, size, GUID_WICPixelFormat32bppPBGRA, WICBitmapCacheOnLoad, &wicBitmap);
    if (FAILED(hr))
        return hr;

    ComPtr<ID2D1Factory> d2dFactory;
    hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, IID_PPV_ARGS(&d2dFactory));
    if (FAILED(hr))
        return hr;

    D2D1_RENDER_TARGET_PROPERTIES rtProps = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));

    ComPtr<ID2D1RenderTarget> rt;
    hr = d2dFactory->CreateWicBitmapRenderTarget(wicBitmap.Get(), &rtProps, &rt);
    if (FAILED(hr))
        return hr;

    ComPtr<ID2D1SolidColorBrush> cardFillBrush;
    ComPtr<ID2D1SolidColorBrush> cardBorderBrush;
    ComPtr<ID2D1SolidColorBrush> textBrush;
    hr = rt->CreateSolidColorBrush(D2D1::ColorF(0xFFFFFF, 1.0f), &cardFillBrush);
    if (FAILED(hr)) return hr;
    hr = rt->CreateSolidColorBrush(D2D1::ColorF(0xCFCFCF, 1.0f), &cardBorderBrush);
    if (FAILED(hr)) return hr;
    hr = rt->CreateSolidColorBrush(D2D1::ColorF(0x1A1A1A, 1.0f), &textBrush);
    if (FAILED(hr)) return hr;

    const float fSize = static_cast<float>(size);
    const float margin = (std::max)(2.0f, fSize * 0.06f);
    const float radius = (std::max)(2.0f, fSize * 0.06f);
    const D2D1_ROUNDED_RECT cardRect = D2D1::RoundedRect(
        D2D1::RectF(margin, margin, fSize - margin, fSize - margin),
        radius,
        radius);

    rt->BeginDraw();
    rt->Clear(D2D1::ColorF(0, 0.0f));
    rt->FillRoundedRectangle(cardRect, cardFillBrush.Get());
    rt->DrawRoundedRectangle(cardRect, cardBorderBrush.Get(), (std::max)(1.0f, fSize * 0.015f));

    // Check if the font supports Latin 'A', 'b', 'g'; if not (e.g. icon/symbol font),
    // fall back to drawing the first few glyphs directly from the font face.
    bool hasLatinSample =
        fontFace->HasCharacter(L'A') &&
        fontFace->HasCharacter(L'b') &&
        fontFace->HasCharacter(L'g');

    if (hasLatinSample && !familyName.empty())
    {
        const wchar_t* sampleText = L"Abg";
        const float innerWidth = fSize - margin * 2.8f;
        const float innerHeight = fSize - margin * 2.8f;
        float fontSize = fSize * 0.36f;

        ComPtr<IDWriteTextFormat> textFormat;
        hr = dwriteFactory->CreateTextFormat(
            familyName.c_str(),
            fontCollection,
            fontFace->GetWeight(),
            fontFace->GetStyle(),
            fontFace->GetStretch(),
            fontSize,
            L"en-us",
            &textFormat);

        if (SUCCEEDED(hr) && textFormat)
        {
            textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
            textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
            textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);

            ComPtr<IDWriteTextLayout> textLayout;
            hr = dwriteFactory->CreateTextLayout(sampleText, 3, textFormat.Get(), innerWidth, innerHeight, &textLayout);
            if (SUCCEEDED(hr) && textLayout)
            {
                DWRITE_TEXT_METRICS metrics{};
                if (SUCCEEDED(textLayout->GetMetrics(&metrics)) && metrics.width > innerWidth && metrics.width > 0.0f)
                {
                    float scaledSize = fontSize * (innerWidth / metrics.width) * 0.92f;
                    textLayout->SetFontSize((std::max)(8.0f, scaledSize), DWRITE_TEXT_RANGE{ 0, 3 });
                }

                rt->DrawTextLayout(
                    D2D1::Point2F(margin * 1.4f, margin * 1.4f),
                    textLayout.Get(),
                    textBrush.Get(),
                    D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
            }
        }
    }
    else
    {
        const UINT16 glyphCount = fontFace->GetGlyphCount();
        const UINT32 drawCount = (std::min)(static_cast<UINT32>(glyphCount > 1 ? glyphCount - 1 : glyphCount), 3u);
        if (drawCount > 0)
        {
            UINT16 indices[3]{};
            FLOAT advances[3]{};
            DWRITE_GLYPH_METRICS glyphMetrics[3]{};

            for (UINT32 i = 0; i < drawCount; ++i)
                indices[i] = static_cast<UINT16>((glyphCount > 1 ? 1 : 0) + i);

            DWRITE_FONT_METRICS fontMetrics{};
            fontFace->GetMetrics(&fontMetrics);
            const float emSize = fSize * 0.34f;
            const float scale = fontMetrics.designUnitsPerEm > 0
                ? (emSize / static_cast<float>(fontMetrics.designUnitsPerEm))
                : (emSize / 1000.0f);

            float totalAdvance = 0.0f;
            if (SUCCEEDED(fontFace->GetDesignGlyphMetrics(indices, drawCount, glyphMetrics, FALSE)))
            {
                for (UINT32 i = 0; i < drawCount; ++i)
                {
                    advances[i] = static_cast<float>(glyphMetrics[i].advanceWidth) * scale;
                    totalAdvance += advances[i];
                }
            }

            const float baselineX = (fSize - totalAdvance) * 0.5f;
            const float baselineY = fSize * 0.5f + (static_cast<float>(fontMetrics.ascent - fontMetrics.descent) * scale) * 0.5f;

            DWRITE_GLYPH_RUN run{};
            run.fontFace = fontFace;
            run.fontEmSize = emSize;
            run.glyphCount = drawCount;
            run.glyphIndices = indices;
            run.glyphAdvances = advances;

            rt->DrawGlyphRun(
                D2D1::Point2F(baselineX, baselineY),
                &run,
                textBrush.Get(),
                DWRITE_MEASURING_MODE_NATURAL);
        }
    }

    hr = rt->EndDraw();
    if (FAILED(hr))
        return hr;

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = static_cast<LONG>(size);
    bmi.bmiHeader.biHeight = -static_cast<LONG>(size); // top-down DIB
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* pBits = nullptr;
    HDC hdc = GetDC(nullptr);
    HBITMAP hbmp = CreateDIBSection(hdc, &bmi, DIB_RGB_COLORS, &pBits, nullptr, 0);
    ReleaseDC(nullptr, hdc);

    if (hbmp == nullptr || pBits == nullptr)
        return E_OUTOFMEMORY;

    const UINT stride = size * 4;
    hr = wicBitmap->CopyPixels(nullptr, stride, stride * size, static_cast<BYTE*>(pBits));
    if (FAILED(hr))
    {
        DeleteObject(hbmp);
        return hr;
    }

    *phbmp = hbmp;
    return S_OK;
}

