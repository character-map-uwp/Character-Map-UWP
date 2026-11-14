#pragma once

#include <d2d1_3.h>
#include <wrl.h>
#include <mutex>

namespace CharacterMapCX
{
    public enum class D2DBitmapFileFormat
    {
        Png = 0,
        Jpeg = 1,
        Bmp = 2
    };

    public ref class D2DRenderTarget sealed
    {
    public:
        D2DRenderTarget(float width, float height);
        D2DRenderTarget(float width, float height, float dpi);
        virtual ~D2DRenderTarget();

        property float Width { float get() { return m_width; } }
        property float Height { float get() { return m_height; } }
        property float Dpi { float get() { return m_dpi; } }
        property uint32 PixelWidth { uint32 get() { return m_pixelWidth; } }
        property uint32 PixelHeight { uint32 get() { return m_pixelHeight; } }

        void Clear(Windows::UI::Color color);
        void DrawInk(Windows::Foundation::Collections::IIterable<Windows::UI::Input::Inking::InkStroke^>^ strokes);

        Windows::Foundation::IAsyncAction^ SaveAsync(Windows::Storage::Streams::IRandomAccessStream^ stream);

        [Windows::Foundation::Metadata::DefaultOverload]
        Windows::Foundation::IAsyncAction^ SaveAsync(
            Windows::Storage::Streams::IRandomAccessStream^ stream,
            D2DBitmapFileFormat format);

    internal:
        ID2D1DeviceContext* GetContext() { return m_d2dContext.Get(); }

    private:
        void Initialize(float width, float height, float dpi);

        std::mutex m_mutex;
        float m_width = 0.0f;
        float m_height = 0.0f;
        float m_dpi = 96.0f;
        uint32 m_pixelWidth = 1;
        uint32 m_pixelHeight = 1;
        bool m_inDraw = false;

        Microsoft::WRL::ComPtr<ID2D1DeviceContext> m_d2dContext;
        Microsoft::WRL::ComPtr<ID2D1Bitmap1> m_targetBitmap;
    };
}

