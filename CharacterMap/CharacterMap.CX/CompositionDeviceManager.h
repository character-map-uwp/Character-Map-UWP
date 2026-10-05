#pragma once

#include <d3d11_4.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <wincodec.h>
#include <wrl.h>
#include <mutex>
#include <map>
#include <vector>
#include <memory>

MIDL_INTERFACE("FD04E6E3-FE0C-4C3C-AB19-A07601A576EE")
ICompositionDrawingSurfaceInterop : public IUnknown
{
public:
    virtual HRESULT STDMETHODCALLTYPE BeginDraw(
        const RECT* updateRect,
        REFIID iid,
        void** updateObject,
        POINT* updateOffset) = 0;

    virtual HRESULT STDMETHODCALLTYPE EndDraw() = 0;

    virtual HRESULT STDMETHODCALLTYPE Resize(
        SIZE sizePixels) = 0;

    virtual HRESULT STDMETHODCALLTYPE Scroll(
        const RECT* scrollRect,
        const RECT* clipRect,
        int offsetX,
        int offsetY) = 0;

    virtual HRESULT STDMETHODCALLTYPE ResumeDraw() = 0;

    virtual HRESULT STDMETHODCALLTYPE SuspendDraw() = 0;
};

namespace CharacterMapCX
{
    public ref class CompositionDeviceManager sealed
    {
    public:
        static void Trim();
        static void TrimWorkingSet();
        static void TrimD2DResources();

        static void TrimDXGI()
        {
            std::lock_guard<std::mutex> lock(s_mutex);
            if (s_d3dDevice != nullptr)
            {
                Microsoft::WRL::ComPtr<IDXGIDevice3> dxgiDevice;
                if (SUCCEEDED(s_d3dDevice.As(&dxgiDevice)))
                    dxgiDevice->Trim();
            }
        }

    internal:
        static Microsoft::WRL::ComPtr<ID2D1Factory5> GetD2DFactory();
        static Microsoft::WRL::ComPtr<IDWriteFactory7> GetDWriteFactory();
        static Microsoft::WRL::ComPtr<ID2D1Device> GetD2DDevice();
        static Windows::UI::Composition::CompositionGraphicsDevice^ GetGraphicsDevice(Windows::UI::Composition::Compositor^ compositor);
        static Windows::UI::Composition::CompositionColorBrush^ GetColorBrush(Windows::UI::Composition::Compositor^ compositor, Windows::UI::Color color);
        static void ReleaseGraphicsDevice(Windows::UI::Composition::Compositor^ compositor);
        static std::mutex& GetRenderMutex();
        static void HandleDeviceLost();
        static void ClearAtlases(Windows::UI::Composition::Compositor^ compositor);
        static Microsoft::WRL::ComPtr<IWICImagingFactory2> GetWICFactory();
        static void DrawGlyphRunWithColorSupport(
            ID2D1DeviceContext* context,
            IDWriteFactory* dwriteFactory,
            D2D1_POINT_2F baselineOrigin,
            const DWRITE_GLYPH_RUN* glyphRun,
            ID2D1Brush* defaultBrush,
            GlyphImageFormat preferredFormat = GlyphImageFormat::None,
            DWRITE_MEASURING_MODE measuringMode = DWRITE_MEASURING_MODE_NATURAL);
        static bool RenderGlyphToSurface(
            Windows::UI::Composition::CompositionDrawingSurface^ surface,
            GlyphImageFormat preferredFormat,
            float padLeft, float padTop, float baseline,
            IDWriteFontFace* rawFace,
            FLOAT fontSize,
            const std::vector<UINT16>& glyphIndices,
            const std::vector<FLOAT>& glyphAdvances,
            const std::vector<DWRITE_GLYPH_OFFSET>& glyphOffsets,
            Windows::UI::Xaml::Media::Brush^ foreground,
            LONG requiredWidth, LONG requiredHeight);

    private:
        static void EnsureDevices();

        static std::mutex s_mutex;
        static std::mutex s_renderMutex;
        static Microsoft::WRL::ComPtr<ID3D11Device> s_d3dDevice;
        static Microsoft::WRL::ComPtr<ID2D1Device> s_d2dDevice;
        static Microsoft::WRL::ComPtr<ID2D1Factory5> s_d2dFactory;
        static Microsoft::WRL::ComPtr<IDWriteFactory7> s_dwriteFactory;
        static Microsoft::WRL::ComPtr<IWICImagingFactory2> s_wicFactory;
        static std::map<IUnknown*, Windows::UI::Composition::CompositionGraphicsDevice^> s_graphicsDevices;
        static std::map<std::pair<IUnknown*, UINT32>, Windows::UI::Composition::CompositionColorBrush^> s_colorBrushes;
    };
}

