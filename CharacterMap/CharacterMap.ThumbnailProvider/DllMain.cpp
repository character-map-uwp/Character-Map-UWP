#include "FontThumbnailProvider.h"
#include <wrl/module.h>

using namespace Microsoft::WRL;

CoCreatableClass(FontThumbnailProvider);

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID /*lpReserved*/)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);
        Module<InProc>::GetModule().Create();
    }
    else if (reason == DLL_PROCESS_DETACH)
    {
        Module<InProc>::GetModule().Terminate();
    }
    return TRUE;
}

_Check_return_
STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ LPVOID* ppv)
{
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, ppv);
}

__control_entrypoint(DllExport)
STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().Terminate() ? S_OK : S_FALSE;
}

