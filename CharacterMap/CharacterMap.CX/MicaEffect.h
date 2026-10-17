#pragma once
#include <wrl.h>
#include <wrl/client.h>
#include <windows.graphics.effects.h>
#include <windows.graphics.effects.interop.h>
#include <d2d1effects.h>
#include <Windows.Foundation.h>

namespace CharacterMapCX
{
    // C++/CX wrapper that we will return to C#
    public ref class MicaEffectFactory sealed
    {
    public:
        static Windows::Graphics::Effects::IGraphicsEffect^ CreateMicaEffect(
            Windows::Graphics::Effects::IGraphicsEffectSource^ backdropSource,
            Windows::UI::Color initialTint);
    };

    // WRL Implementation of the Effect
    class MicaEffect : public Microsoft::WRL::RuntimeClass<
        Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::WinRtClassicComMix>,
        ABI::Windows::Graphics::Effects::IGraphicsEffect,
        ABI::Windows::Graphics::Effects::IGraphicsEffectSource,
        ABI::Windows::Graphics::Effects::IGraphicsEffectD2D1Interop>
    {
        InspectableClass(L"CharacterMapCX.MicaEffect", BaseTrust);

    public:
        HRESULT RuntimeClassInitialize(
            ABI::Windows::Graphics::Effects::IGraphicsEffectSource* source,
            Windows::UI::Color tint)
        {
            m_source = source;
            m_tint = tint;
            return S_OK;
        }

        // IGraphicsEffect
        IFACEMETHODIMP get_Name(HSTRING* name) override
        {
            return WindowsCreateString(L"MicaAltBlend", 12, name);
        }

        IFACEMETHODIMP put_Name(HSTRING name) override
        {
            return S_OK; // Ignore
        }

        // IGraphicsEffectD2D1Interop
        IFACEMETHODIMP GetEffectId(GUID* id) override
        {
            *id = CLSID_D2D1ArithmeticComposite;
            return S_OK;
        }

        IFACEMETHODIMP GetNamedPropertyMapping(
            LPCWSTR name,
            UINT* index,
            ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING* mapping) override
        {
            if (wcscmp(name, L"Tint.Color") == 0)
            {
                *index = 0xFFFFFFFF; // Special marker for our custom handling
                *mapping = ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING_COLOR_TO_VECTOR4;
                return S_OK;
            }
            return E_INVALIDARG;
        }

        IFACEMETHODIMP GetPropertyCount(UINT* count) override
        {
            *count = 2; // Coefficients, ClampOutput
            return S_OK;
        }

        IFACEMETHODIMP GetProperty(UINT index, ABI::Windows::Foundation::IPropertyValue** value) override
        {
            Microsoft::WRL::ComPtr<ABI::Windows::Foundation::IPropertyValueStatics> statics;
            ABI::Windows::Foundation::GetActivationFactory(
                Microsoft::WRL::Wrappers::HStringReference(RuntimeClass_Windows_Foundation_PropertyValue).Get(),
                &statics);

            switch (index)
            {
            case D2D1_ARITHMETICCOMPOSITE_PROP_COEFFICIENTS:
            {
                // Vector4 for MultiplyAmount(C1), Source1Amount(C2), Source2Amount(C3), Offset(C4)
                float coeff[4] = { 0.0f, 0.20f, 0.80f, 0.0f };
                return statics->CreateSingleArray(4, coeff, reinterpret_cast<IInspectable**>(value));
            }
            case D2D1_ARITHMETICCOMPOSITE_PROP_CLAMP_OUTPUT:
                return statics->CreateBoolean(FALSE, reinterpret_cast<IInspectable**>(value));
            }
            return E_INVALIDARG;
        }

        IFACEMETHODIMP GetSource(
            UINT index,
            ABI::Windows::Graphics::Effects::IGraphicsEffectSource** source) override
        {
            if (index == 0)
            {
                return m_source.CopyTo(source);
            }
            else if (index == 1)
            {
                Microsoft::WRL::ComPtr<FloodEffect> flood;
                Microsoft::WRL::MakeAndInitialize<FloodEffect>(&flood, m_tint);
                return flood.CopyTo(source);
            }
            return E_INVALIDARG;
        }

        IFACEMETHODIMP GetSourceCount(UINT* count) override
        {
            *count = 2;
            return S_OK;
        }

    private:
        Microsoft::WRL::ComPtr<ABI::Windows::Graphics::Effects::IGraphicsEffectSource> m_source;
        Windows::UI::Color m_tint;

        class FloodEffect : public Microsoft::WRL::RuntimeClass<
            Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::WinRtClassicComMix>,
            ABI::Windows::Graphics::Effects::IGraphicsEffect,
            ABI::Windows::Graphics::Effects::IGraphicsEffectSource,
            ABI::Windows::Graphics::Effects::IGraphicsEffectD2D1Interop>
        {
            InspectableClass(L"CharacterMapCX.FloodEffect", BaseTrust);
        public:
            HRESULT RuntimeClassInitialize(Windows::UI::Color tint) { m_tint = tint; return S_OK; }
            IFACEMETHODIMP get_Name(HSTRING* name) override { return WindowsCreateString(L"Tint", 4, name); }
            IFACEMETHODIMP put_Name(HSTRING name) override { return S_OK; }
            IFACEMETHODIMP GetEffectId(GUID* id) override { *id = CLSID_D2D1Flood; return S_OK; }
            IFACEMETHODIMP GetNamedPropertyMapping(LPCWSTR name, UINT* idx, ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING* mapping) override
            {
                if (wcscmp(name, L"Color") == 0)
                {
                    *idx = D2D1_FLOOD_PROP_COLOR;
                    *mapping = ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING_COLOR_TO_VECTOR4;
                    return S_OK;
                }
                return E_INVALIDARG;
            }
            IFACEMETHODIMP GetPropertyCount(UINT* count) override { *count = 1; return S_OK; }
            IFACEMETHODIMP GetProperty(UINT index, ABI::Windows::Foundation::IPropertyValue** value) override
            {
                if (index == D2D1_FLOOD_PROP_COLOR)
                {
                    float colorArray[4] = { m_tint.R / 255.f, m_tint.G / 255.f, m_tint.B / 255.f, m_tint.A / 255.f };
                    Microsoft::WRL::ComPtr<ABI::Windows::Foundation::IPropertyValueStatics> statics;
                    ABI::Windows::Foundation::GetActivationFactory(Microsoft::WRL::Wrappers::HStringReference(RuntimeClass_Windows_Foundation_PropertyValue).Get(), &statics);
                    return statics->CreateSingleArray(4, colorArray, reinterpret_cast<IInspectable**>(value));
                }
                return E_INVALIDARG;
            }
            IFACEMETHODIMP GetSource(UINT index, ABI::Windows::Graphics::Effects::IGraphicsEffectSource** source) override { return E_INVALIDARG; }
            IFACEMETHODIMP GetSourceCount(UINT* count) override { *count = 0; return S_OK; }
        private:
            Windows::UI::Color m_tint;
        };
    };
}
