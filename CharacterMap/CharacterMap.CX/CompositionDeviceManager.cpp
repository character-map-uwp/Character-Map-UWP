#include "pch.h"
#include "CompositionDeviceManager.h"
#include "GlyphAtlasManager.h"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")

using namespace Microsoft::WRL;
using namespace CharacterMapCX;
using namespace Windows::UI::Composition;
using namespace Windows::Graphics::DirectX;

MIDL_INTERFACE("25297D5C-3AD4-4C9C-B5CF-E36A38512330")
ICompositorInterop : public IUnknown
{
public:
    virtual HRESULT STDMETHODCALLTYPE CreateCompositionSurfaceForHandle(
        HANDLE swapChain,
        IUnknown** result) = 0;

    virtual HRESULT STDMETHODCALLTYPE CreateCompositionSurfaceForSwapChain(
        IUnknown* swapChain,
        IUnknown** result) = 0;

    virtual HRESULT STDMETHODCALLTYPE CreateGraphicsDevice(
        IUnknown* renderingDevice,
        IUnknown** result) = 0;
};

std::mutex CompositionDeviceManager::s_mutex;
std::mutex CompositionDeviceManager::s_renderMutex;
ComPtr<ID3D11Device> CompositionDeviceManager::s_d3dDevice = nullptr;
ComPtr<ID2D1Device> CompositionDeviceManager::s_d2dDevice = nullptr;
ComPtr<ID2D1Factory5> CompositionDeviceManager::s_d2dFactory = nullptr;
ComPtr<IDWriteFactory7> CompositionDeviceManager::s_dwriteFactory = nullptr;
ComPtr<IWICImagingFactory2> CompositionDeviceManager::s_wicFactory = nullptr;
std::map<IUnknown*, CompositionGraphicsDevice^> CompositionDeviceManager::s_graphicsDevices;
std::map<std::pair<IUnknown*, UINT32>, CompositionColorBrush^> CompositionDeviceManager::s_colorBrushes;

std::mutex& CompositionDeviceManager::GetRenderMutex()
{
    return s_renderMutex;
}

void CompositionDeviceManager::EnsureDevices()
{
    if (s_d3dDevice != nullptr)
        return;

    UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

    D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0,
        D3D_FEATURE_LEVEL_10_1,
        D3D_FEATURE_LEVEL_10_0
    };

    D3D_FEATURE_LEVEL featureLevel;
    ComPtr<ID3D11DeviceContext> d3dContext;
    HRESULT hr = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        0,
        creationFlags,
        featureLevels,
        ARRAYSIZE(featureLevels),
        D3D11_SDK_VERSION,
        &s_d3dDevice,
        &featureLevel,
        &d3dContext);

    if (FAILED(hr))
    {
        // Fallback to WARP if hardware acceleration is unavailable
        D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_WARP,
            0,
            creationFlags,
            featureLevels,
            ARRAYSIZE(featureLevels),
            D3D11_SDK_VERSION,
            &s_d3dDevice,
            &featureLevel,
            &d3dContext);
    }

    if (s_d3dDevice != nullptr)
    {
        ComPtr<ID3D11Multithread> multithread;
        if (SUCCEEDED(s_d3dDevice.As(&multithread)))
            multithread->SetMultithreadProtected(TRUE);

        ComPtr<IDXGIDevice> dxgiDevice;
        if (SUCCEEDED(s_d3dDevice.As(&dxgiDevice)))
        {
            if (s_d2dFactory == nullptr)
            {
                D2D1_FACTORY_OPTIONS options{};
                D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, __uuidof(ID2D1Factory5), &options, &s_d2dFactory);
            }

            if (s_d2dFactory != nullptr)
                s_d2dFactory->CreateDevice(dxgiDevice.Get(), &s_d2dDevice);
        }
    }

    if (s_dwriteFactory == nullptr)
        DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory7), &s_dwriteFactory);
}

ComPtr<ID2D1Factory5> CompositionDeviceManager::GetD2DFactory()
{
    std::lock_guard<std::mutex> lock(s_mutex);
    EnsureDevices();
    return s_d2dFactory;
}

ComPtr<IDWriteFactory7> CompositionDeviceManager::GetDWriteFactory()
{
    std::lock_guard<std::mutex> lock(s_mutex);
    EnsureDevices();
    return s_dwriteFactory;
}

ComPtr<ID2D1Device> CompositionDeviceManager::GetD2DDevice()
{
    std::lock_guard<std::mutex> lock(s_mutex);
    EnsureDevices();
    return s_d2dDevice;
}

ComPtr<IWICImagingFactory2> CompositionDeviceManager::GetWICFactory()
{
    std::lock_guard<std::mutex> lock(s_mutex);
    if (s_wicFactory == nullptr)
    {
        CoCreateInstance(
            CLSID_WICImagingFactory,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&s_wicFactory));
    }
    return s_wicFactory;
}

void CompositionDeviceManager::DrawGlyphRunWithColorSupport(
    ID2D1DeviceContext* context,
    IDWriteFactory* dwriteFactory,
    D2D1_POINT_2F baselineOrigin,
    const DWRITE_GLYPH_RUN* glyphRun,
    ID2D1Brush* defaultBrush,
    GlyphImageFormat preferredFormat,
    DWRITE_MEASURING_MODE measuringMode)
{
    if (!context || !glyphRun)
        return;

    bool drewColor = false;

    bool color = preferredFormat != GlyphImageFormat::TrueType;
    if (color)
    {
        ComPtr<ID2D1DeviceContext7> context7;
        if (SUCCEEDED(context->QueryInterface(IID_PPV_ARGS(&context7))) && (preferredFormat == GlyphImageFormat::None || preferredFormat == GlyphImageFormat::ColrPaintTree))
        {
            context7->DrawGlyphRunWithColorSupport(
                baselineOrigin,
                glyphRun,
                nullptr,
                defaultBrush,
                nullptr,
                0,
                measuringMode,
                D2D1_COLOR_BITMAP_GLYPH_SNAP_OPTION_DEFAULT);
            drewColor = true;
        }
        else if (dwriteFactory != nullptr)
        {
            ComPtr<IDWriteFactory4> factory4;
            if (SUCCEEDED(dwriteFactory->QueryInterface(IID_PPV_ARGS(&factory4))))
            {
                ComPtr<IDWriteColorGlyphRunEnumerator1> colorLayers;
                DWRITE_GLYPH_IMAGE_FORMATS glyphFormats =
                    DWRITE_GLYPH_IMAGE_FORMATS_TRUETYPE |
                    DWRITE_GLYPH_IMAGE_FORMATS_CFF |
                    DWRITE_GLYPH_IMAGE_FORMATS_COLR;

                if (preferredFormat != GlyphImageFormat::Colr)
                {
                    glyphFormats |=
                        DWRITE_GLYPH_IMAGE_FORMATS_SVG |
                        DWRITE_GLYPH_IMAGE_FORMATS_PNG |
                        DWRITE_GLYPH_IMAGE_FORMATS_JPEG |
                        DWRITE_GLYPH_IMAGE_FORMATS_TIFF |
                        DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8;
                }

                HRESULT hr = factory4->TranslateColorGlyphRun(
                    baselineOrigin,
                    glyphRun,
                    nullptr,
                    glyphFormats,
                    measuringMode,
                    nullptr,
                    0,
                    &colorLayers);

                if (SUCCEEDED(hr))
                {
                    while (true)
                    {
                        BOOL hasRun = FALSE;
                        if (FAILED(colorLayers->MoveNext(&hasRun)) || !hasRun)
                            break;

                        const DWRITE_COLOR_GLYPH_RUN1* colorRun = nullptr;
                        if (FAILED(colorLayers->GetCurrentRun(&colorRun)) || !colorRun)
                            break;

                        drewColor = true;
                        D2D1_POINT_2F runOrigin = D2D1::Point2F(colorRun->baselineOriginX, colorRun->baselineOriginY);

                        ComPtr<ID2D1Brush> layerBrush = defaultBrush;
                        if (colorRun->paletteIndex != 0xFFFF)
                        {
                            ComPtr<ID2D1SolidColorBrush> solidBrush;
                            if (SUCCEEDED(context->CreateSolidColorBrush(colorRun->runColor, &solidBrush)))
                                layerBrush = solidBrush;
                        }

                        switch (colorRun->glyphImageFormat)
                        {
                        case DWRITE_GLYPH_IMAGE_FORMATS_PNG:
                        case DWRITE_GLYPH_IMAGE_FORMATS_JPEG:
                        case DWRITE_GLYPH_IMAGE_FORMATS_TIFF:
                        case DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8:
                        {
                            ComPtr<ID2D1DeviceContext4> context4;
                            if (SUCCEEDED(context->QueryInterface(IID_PPV_ARGS(&context4))))
                            {
                                context4->DrawColorBitmapGlyphRun(
                                    colorRun->glyphImageFormat,
                                    runOrigin,
                                    &colorRun->glyphRun,
                                    colorRun->measuringMode);
                            }
                            break;
                        }
                        case DWRITE_GLYPH_IMAGE_FORMATS_SVG:
                        {
                            ComPtr<ID2D1DeviceContext4> context4;
                            if (SUCCEEDED(context->QueryInterface(IID_PPV_ARGS(&context4))))
                            {
                                context4->DrawSvgGlyphRun(
                                    runOrigin,
                                    &colorRun->glyphRun,
                                    layerBrush.Get(),
                                    nullptr,
                                    colorRun->paletteIndex == 0xFFFF ? 0 : colorRun->paletteIndex,
                                    colorRun->measuringMode);
                            }
                            break;
                        }
                        case DWRITE_GLYPH_IMAGE_FORMATS_COLR:
                        default:
                        {
                            context->DrawGlyphRun(
                                runOrigin,
                                &colorRun->glyphRun,
                                layerBrush.Get(),
                                colorRun->measuringMode);
                            break;
                        }
                        }
                    }
                }
            }
        }
    }

    if (!drewColor)
    {
        context->DrawGlyphRun(baselineOrigin, glyphRun, defaultBrush, measuringMode);
    }
}

CompositionGraphicsDevice^ CompositionDeviceManager::GetGraphicsDevice(Compositor^ compositor)
{
    if (compositor == nullptr)
        return nullptr;

    ComPtr<IUnknown> unk;
    if (FAILED(reinterpret_cast<IUnknown*>(compositor)->QueryInterface(IID_PPV_ARGS(&unk))))
        return nullptr;

    IUnknown* key = unk.Get();

    std::lock_guard<std::mutex> lock(s_mutex);
    EnsureDevices();

    auto it = s_graphicsDevices.find(key);
    if (it != s_graphicsDevices.end() && it->second != nullptr)
        return it->second;

    if (s_d2dDevice != nullptr)
    {
        ComPtr<ICompositorInterop> compositorInterop;
        if (SUCCEEDED(unk.As(&compositorInterop)))
        {
            ComPtr<IUnknown> graphicsDeviceUnknown;
            if (SUCCEEDED(compositorInterop->CreateGraphicsDevice(s_d2dDevice.Get(), &graphicsDeviceUnknown)))
            {
                auto graphicsDevice = reinterpret_cast<CompositionGraphicsDevice^>(graphicsDeviceUnknown.Get());
                s_graphicsDevices[key] = graphicsDevice;
                return graphicsDevice;
            }
        }
    }

    return nullptr;
}

CompositionColorBrush^ CompositionDeviceManager::GetColorBrush(Compositor^ compositor, Windows::UI::Color color)
{
    if (compositor == nullptr)
        return nullptr;

    ComPtr<IUnknown> unk;
    if (FAILED(reinterpret_cast<IUnknown*>(compositor)->QueryInterface(IID_PPV_ARGS(&unk))))
        return nullptr;

    IUnknown* key = unk.Get();
    UINT32 colorKey = (static_cast<UINT32>(color.A) << 24)
        | (static_cast<UINT32>(color.R) << 16)
        | (static_cast<UINT32>(color.G) << 8)
        | static_cast<UINT32>(color.B);

    std::lock_guard<std::mutex> lock(s_mutex);
    auto brushKey = std::make_pair(key, colorKey);
    auto it = s_colorBrushes.find(brushKey);
    if (it != s_colorBrushes.end() && it->second != nullptr)
        return it->second;

    if (s_colorBrushes.size() > 64)
        s_colorBrushes.clear();

    auto brush = compositor->CreateColorBrush(color);
    s_colorBrushes[brushKey] = brush;
    return brush;
}

void CompositionDeviceManager::ReleaseGraphicsDevice(Compositor^ compositor)
{
    if (compositor == nullptr)
        return;

    ComPtr<IUnknown> unk;
    if (FAILED(reinterpret_cast<IUnknown*>(compositor)->QueryInterface(IID_PPV_ARGS(&unk))))
        return;

    std::lock_guard<std::mutex> lock(s_mutex);
    IUnknown* key = unk.Get();

    auto it = s_graphicsDevices.find(key);
    if (it != s_graphicsDevices.end())
    {
        if (it->second != nullptr)
        {
            try
            {
                delete it->second;
            }
            catch (...)
            {
            }
        }
        s_graphicsDevices.erase(it);
    }

    for (auto bit = s_colorBrushes.begin(); bit != s_colorBrushes.end();)
    {
        if (bit->first.first == key)
            bit = s_colorBrushes.erase(bit);
        else
            ++bit;
    }
}

void CompositionDeviceManager::ClearAtlases(Compositor^ compositor)
{
    GlyphAtlasManager::Clear();
    Trim();
}

void CompositionDeviceManager::HandleDeviceLost()
{
    std::lock_guard<std::mutex> lock(s_mutex);
    GlyphAtlasManager::Clear();
    s_colorBrushes.clear();
    s_graphicsDevices.clear();
    s_d2dDevice = nullptr;
    s_d3dDevice = nullptr;
    EnsureDevices();
}

void CompositionDeviceManager::Trim()
{
    std::lock_guard<std::mutex> lock(s_mutex);
    GlyphAtlasManager::Clear();

    if (s_d3dDevice != nullptr)
    {
        ComPtr<IDXGIDevice3> dxgiDevice;
        if (SUCCEEDED(s_d3dDevice.As(&dxgiDevice)))
            dxgiDevice->Trim();
    }

    // Flush D2D's internal glyph rasterization cache. Without this, D2D accumulates
    // per-glyph texture atlases for every unique glyph ever rendered and never evicts
    // them. For large CJK fonts (e.g. MS PMincho ~26k glyphs) this is the primary
    // source of unbounded memory growth.
    if (s_d2dDevice != nullptr)
        s_d2dDevice->ClearResources(0);

    for (const auto& pair : s_graphicsDevices)
    {
        if (pair.second != nullptr)
            pair.second->Trim();
    }
}

void CompositionDeviceManager::TrimD2DResources()
{
    // Called from the atlas eviction path to keep D2D's internal glyph cache
    // synchronised with our own atlas eviction budget.
    std::lock_guard<std::mutex> lock(s_mutex);
    if (s_d2dDevice != nullptr)
        s_d2dDevice->ClearResources(0);
}

void CompositionDeviceManager::TrimWorkingSet()
{
    Trim(); // Trims DXGI device and graphics devices
    HeapCompact(GetProcessHeap(), 0);
}


/*
 * Renders a glyph run directly into a CompositionDrawingSurface using Direct2D.
 * 
 * Passing nullptr to BeginDraw updates the entire surface in physical device pixels,
 * avoiding DIP/DPI coordinate mismatch across varying Windows display scalings.
 * The glyph run is translated and drawn either as multi-layer color glyphs (COLR/SVG/bitmap)
 * or as monochrome alpha mask depending on isColor and font support.
 */
bool CompositionDeviceManager::RenderGlyphToSurface(
    CompositionDrawingSurface^ surface,
    GlyphImageFormat preferredFormat,
    float padLeft, float padTop, float baseline,
    IDWriteFontFace* rawFace,
    FLOAT fontSize,
    const std::vector<UINT16>& glyphIndices,
    const std::vector<FLOAT>& glyphAdvances,
    const std::vector<DWRITE_GLYPH_OFFSET>& glyphOffsets,
    Windows::UI::Xaml::Media::Brush^ foreground,
    Windows::UI::Xaml::Media::StyleSimulations simulations,
    LONG requiredWidth, LONG requiredHeight)
{
    if (surface == nullptr || rawFace == nullptr || glyphIndices.empty() || requiredWidth <= 0 || requiredHeight <= 0)
        return false;

    ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
    if (FAILED(reinterpret_cast<IUnknown*>(surface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop))))
        return false;

    POINT updateOffset{};
    ComPtr<ID2D1DeviceContext> d2dContext;

    {
        std::lock_guard<std::mutex> renderLock(GetRenderMutex());

        HRESULT hr = surfaceInterop->BeginDraw(nullptr, IID_PPV_ARGS(&d2dContext), &updateOffset);
        if (FAILED(hr))
        {
            if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET)
                HandleDeviceLost();
            return false;
        }

        if (d2dContext != nullptr)
        {
            d2dContext->Clear(D2D1::ColorF(0, 0, 0, 0));
            d2dContext->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y)));

            DWRITE_GLYPH_RUN glyphRun{};
            glyphRun.fontFace = rawFace;
            glyphRun.fontEmSize = fontSize;
            glyphRun.glyphCount = static_cast<UINT32>(glyphIndices.size());
            glyphRun.glyphIndices = glyphIndices.data();
            glyphRun.glyphAdvances = glyphAdvances.data();
            glyphRun.glyphOffsets = glyphOffsets.empty() ? nullptr : glyphOffsets.data();
            glyphRun.isSideways = FALSE;
            glyphRun.bidiLevel = 0;

            D2D1_POINT_2F baselineOrigin = D2D1::Point2F(padLeft, baseline + padTop);

            if (preferredFormat == GlyphImageFormat::TrueType)
            {
                ComPtr<ID2D1SolidColorBrush> alphaBrush;
                d2dContext->CreateSolidColorBrush(D2D1::ColorF(1.0f, 1.0f, 1.0f, 1.0f), &alphaBrush);
                d2dContext->DrawGlyphRun(baselineOrigin, &glyphRun, alphaBrush.Get(), DWRITE_MEASURING_MODE_NATURAL);
            }
            else
            {
                D2D1_COLOR_F brushColor = D2D1::ColorF(D2D1::ColorF::White);
                if (auto scb = dynamic_cast<Windows::UI::Xaml::Media::SolidColorBrush^>(foreground))
                {
                    auto c = scb->Color;
                    brushColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(scb->Opacity));
                }

                ComPtr<ID2D1SolidColorBrush> d2dBrush;
                d2dContext->CreateSolidColorBrush(brushColor, &d2dBrush);

                DrawGlyphRunWithColorSupport(
                    d2dContext.Get(),
                    GetDWriteFactory().Get(),
                    baselineOrigin,
                    &glyphRun,
                    d2dBrush.Get(),
                    preferredFormat);
            }

            surfaceInterop->EndDraw();
        }
    }

    return true;
}

