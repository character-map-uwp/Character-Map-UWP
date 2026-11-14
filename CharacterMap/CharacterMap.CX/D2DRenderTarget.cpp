#include "pch.h"
#include "D2DRenderTarget.h"
#include "CompositionDeviceManager.h"
#include <inkrenderer.h>
#include <shcore.h>
#include <ppltasks.h>
#include <algorithm>
#include <cmath>
#include <memory>
#include <vector>

#pragma comment(lib, "shcore.lib")

using namespace CharacterMapCX;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Windows::UI::Input::Inking;

namespace
{
    GUID ToWicContainerFormat(D2DBitmapFileFormat format)
    {
        switch (format)
        {
        case D2DBitmapFileFormat::Jpeg:
            return GUID_ContainerFormatJpeg;
        case D2DBitmapFileFormat::Bmp:
            return GUID_ContainerFormatBmp;
        case D2DBitmapFileFormat::Png:
        default:
            return GUID_ContainerFormatPng;
        }
    }
}

D2DRenderTarget::D2DRenderTarget(float width, float height)
{
    Initialize(width, height, 96.0f);
}

D2DRenderTarget::D2DRenderTarget(float width, float height, float dpi)
{
    Initialize(width, height, dpi);
}

D2DRenderTarget::~D2DRenderTarget()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_d2dContext != nullptr)
    {
        if (m_inDraw)
        {
            m_d2dContext->EndDraw();
            m_inDraw = false;
        }
        m_d2dContext->SetTarget(nullptr);
    }
    m_targetBitmap.Reset();
    m_d2dContext.Reset();
}

void D2DRenderTarget::Initialize(float width, float height, float dpi)
{
    m_dpi = dpi > 0.0f ? dpi : 96.0f;
    m_width = (std::max)(1.0f, width);
    m_height = (std::max)(1.0f, height);

    float scale = m_dpi / 96.0f;
    m_pixelWidth = (std::max)(1u, static_cast<uint32>(std::ceil(m_width * scale)));
    m_pixelHeight = (std::max)(1u, static_cast<uint32>(std::ceil(m_height * scale)));

    ComPtr<ID2D1Device> d2dDevice = CompositionDeviceManager::GetD2DDevice();
    if (d2dDevice == nullptr)
        throw ref new Platform::COMException(E_FAIL);

    HRESULT hr = d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &m_d2dContext);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    D2D1_BITMAP_PROPERTIES1 props = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
        m_dpi,
        m_dpi);

    hr = m_d2dContext->CreateBitmap(D2D1::SizeU(m_pixelWidth, m_pixelHeight), nullptr, 0, &props, &m_targetBitmap);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    m_d2dContext->SetDpi(m_dpi, m_dpi);
    m_d2dContext->SetTarget(m_targetBitmap.Get());
}

void D2DRenderTarget::Clear(Windows::UI::Color color)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_d2dContext == nullptr)
        return;

    if (!m_inDraw)
    {
        m_d2dContext->BeginDraw();
        m_inDraw = true;
    }

    float a = color.A / 255.0f;
    m_d2dContext->Clear(D2D1::ColorF((color.R / 255.0f) * a, (color.G / 255.0f) * a, (color.B / 255.0f) * a, a));
}

void D2DRenderTarget::DrawInk(IIterable<InkStroke^>^ strokes)
{
    if (strokes == nullptr)
        return;

    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_d2dContext == nullptr)
        return;

    ComPtr<IInkD2DRenderer> inkRenderer;
    HRESULT hr = CoCreateInstance(__uuidof(InkD2DRenderer), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&inkRenderer));
    if (FAILED(hr) || inkRenderer == nullptr)
        throw ref new Platform::COMException(hr);

    if (!m_inDraw)
    {
        m_d2dContext->BeginDraw();
        m_inDraw = true;
    }

    hr = inkRenderer->Draw(m_d2dContext.Get(), reinterpret_cast<IUnknown*>(strokes), FALSE);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);
}

IAsyncAction^ D2DRenderTarget::SaveAsync(IRandomAccessStream^ stream)
{
    return SaveAsync(stream, D2DBitmapFileFormat::Png);
}

IAsyncAction^ D2DRenderTarget::SaveAsync(IRandomAccessStream^ stream, D2DBitmapFileFormat format)
{
    if (stream == nullptr)
        throw ref new Platform::InvalidArgumentException(L"stream");

    std::vector<uint8_t> pixels;
    uint32 width = 0;
    uint32 height = 0;
    uint32 pitch = 0;
    float dpi = 96.0f;

    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (m_d2dContext == nullptr || m_targetBitmap == nullptr)
            throw ref new Platform::COMException(E_UNEXPECTED);

        if (m_inDraw)
        {
            HRESULT hrEnd = m_d2dContext->EndDraw();
            m_inDraw = false;
            if (FAILED(hrEnd))
                throw ref new Platform::COMException(hrEnd);
        }

        width = m_pixelWidth;
        height = m_pixelHeight;
        dpi = m_dpi;

        D2D1_BITMAP_PROPERTIES1 stagingProps = D2D1::BitmapProperties1(
            D2D1_BITMAP_OPTIONS_CPU_READ | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
            D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
            m_dpi,
            m_dpi);

        ComPtr<ID2D1Bitmap1> stagingBitmap;
        HRESULT hr = m_d2dContext->CreateBitmap(D2D1::SizeU(width, height), nullptr, 0, &stagingProps, &stagingBitmap);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = stagingBitmap->CopyFromBitmap(nullptr, m_targetBitmap.Get(), nullptr);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        D2D1_MAPPED_RECT mapped{};
        hr = stagingBitmap->Map(D2D1_MAP_OPTIONS_READ, &mapped);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        pitch = width * 4u;
        pixels.resize(static_cast<size_t>(pitch) * height);
        for (uint32 y = 0; y < height; ++y)
            memcpy(pixels.data() + static_cast<size_t>(y) * pitch, mapped.bits + static_cast<size_t>(y) * mapped.pitch, pitch);

        stagingBitmap->Unmap();
    }

    auto pixelData = std::make_shared<std::vector<uint8_t>>(std::move(pixels));
    return concurrency::create_async([stream, format, pixelData, width, height, pitch, dpi]()
    {
        ComPtr<IWICImagingFactory2> wicFactory = CompositionDeviceManager::GetWICFactory();
        if (wicFactory == nullptr)
            throw ref new Platform::COMException(E_FAIL);

        ComPtr<IStream> comStream;
        HRESULT hr = CreateStreamOverRandomAccessStream(reinterpret_cast<IUnknown*>(stream), IID_PPV_ARGS(&comStream));
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        ComPtr<IWICBitmapEncoder> encoder;
        hr = wicFactory->CreateEncoder(ToWicContainerFormat(format), nullptr, &encoder);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = encoder->Initialize(comStream.Get(), WICBitmapEncoderNoCache);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        ComPtr<IWICBitmapFrameEncode> frame;
        hr = encoder->CreateNewFrame(&frame, nullptr);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = frame->Initialize(nullptr);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = frame->SetSize(width, height);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = frame->SetResolution(dpi, dpi);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat32bppPBGRA;
        hr = frame->SetPixelFormat(&pixelFormat);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = frame->WritePixels(height, pitch, static_cast<UINT>(pixelData->size()), pixelData->data());
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = frame->Commit();
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = encoder->Commit();
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);
    });
}
