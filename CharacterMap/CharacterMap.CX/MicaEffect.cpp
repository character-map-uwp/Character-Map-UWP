#include "pch.h"
#pragma comment(lib, "dxguid.lib")
#include "MicaEffect.h"

using namespace CharacterMapCX;

Windows::Graphics::Effects::IGraphicsEffect^ MicaEffectFactory::CreateMicaEffect(
    Windows::Graphics::Effects::IGraphicsEffectSource^ backdropSource,
    Windows::UI::Color initialTint)
{
    Microsoft::WRL::ComPtr<MicaEffect> effect;
    Microsoft::WRL::MakeAndInitialize<MicaEffect>(
        &effect,
        reinterpret_cast<ABI::Windows::Graphics::Effects::IGraphicsEffectSource*>(backdropSource),
        initialTint);

    // Cast the WRL pointer back to the C++/CX ref class representation
    return reinterpret_cast<Windows::Graphics::Effects::IGraphicsEffect^>(effect.Get());
}
