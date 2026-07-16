#include "DwmThumbnailVisual.h"

#include <windows.h>
#include <inspectable.h>
#include <dcomp.h>
#include <dwmapi.h>
#include <wrl/client.h>

#pragma comment(lib, "dcomp.lib")
#pragma comment(lib, "dwmapi.lib")

#ifndef DWM_TNP_ENABLE3D
#define DWM_TNP_ENABLE3D 0x04000000
#endif

using Microsoft::WRL::ComPtr;

namespace Infinity::Platform::Windows::Native
{
    using DwmpCreateSharedThumbnailVisual = HRESULT(WINAPI*)(HWND destinationWindowHandle, HWND sourceWindowHandle, DWORD thumbnailFlags, DWM_THUMBNAIL_PROPERTIES* thumbnailProperties, IDCompositionDevice* compositionDevice, void** visual, HTHUMBNAIL* thumbnailHandle);
    using DwmpQueryWindowThumbnailSourceSize = HRESULT(WINAPI*)(HWND sourceWindowHandle, BOOL clientOnly, SIZE* size);

    struct __declspec(uuid("C0EEAB6C-C897-5AC6-A1C9-63ABD5055B9B")) IMicrosoftCompositionVisual :
        IInspectable
    {
    };

    static HMODULE dwmapiModule;
    static DwmpCreateSharedThumbnailVisual createSharedThumbnailVisual;
    static DwmpQueryWindowThumbnailSourceSize queryWindowThumbnailSourceSize;

    static HRESULT SafeCreateSharedThumbnailVisual(HWND ownerWindowHandle,
        HWND sourceWindowHandle,
        DWM_THUMBNAIL_PROPERTIES* properties,
        IDCompositionDevice* compositionDevice,
        void** visual,
        HTHUMBNAIL* thumbnailHandle)
    {
        __try
        {
            return createSharedThumbnailVisual(ownerWindowHandle,
                sourceWindowHandle,
                2,
                properties,
                compositionDevice,
                visual,
                thumbnailHandle);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return HRESULT_FROM_WIN32(ERROR_NOACCESS);
        }
    }

    static HRESULT SafeQuerySourceSize(HWND sourceWindowHandle, SIZE* size)
    {
        __try
        {
            return queryWindowThumbnailSourceSize(sourceWindowHandle, FALSE, size);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return HRESULT_FROM_WIN32(ERROR_NOACCESS);
        }
    }

    static HRESULT LoadPrivateDwmApi()
    {
        if (createSharedThumbnailVisual)
        {
            return S_OK;
        }

        dwmapiModule = LoadLibraryW(L"dwmapi.dll");

        if (!dwmapiModule)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        createSharedThumbnailVisual = reinterpret_cast<DwmpCreateSharedThumbnailVisual>(GetProcAddress(dwmapiModule, MAKEINTRESOURCEA(147)));
        queryWindowThumbnailSourceSize = reinterpret_cast<DwmpQueryWindowThumbnailSourceSize>(GetProcAddress(dwmapiModule, MAKEINTRESOURCEA(162)));
        return createSharedThumbnailVisual ? S_OK : HRESULT_FROM_WIN32(ERROR_PROC_NOT_FOUND);
    }

    static SIZE GetSourceSize(HWND windowHandle)
    {
        SIZE size{};

        if (queryWindowThumbnailSourceSize &&
            SUCCEEDED(SafeQuerySourceSize(windowHandle, &size)) &&
            size.cx > 0 && size.cy > 0)
        {
            return size;
        }

        RECT rect{};

        if (GetWindowRect(windowHandle, &rect))
        {
            size.cx = max(1L, rect.right - rect.left);
            size.cy = max(1L, rect.bottom - rect.top);
            return size;
        }

        size.cx = 1;
        size.cy = 1;
        return size;
    }

    static DWM_THUMBNAIL_PROPERTIES CreateThumbnailProperties(HWND sourceWindowHandle, int width, int height, bool isVisible)
    {
        SIZE sourceSize = GetSourceSize(sourceWindowHandle);
        DWM_THUMBNAIL_PROPERTIES properties{};
        properties.dwFlags = DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE | DWM_TNP_ENABLE3D;
        properties.fVisible = isVisible ? TRUE : FALSE;
        properties.opacity = 255;
        properties.rcDestination = RECT{ 0, 0, width, height };
        properties.rcSource = RECT{ 0, 0, sourceSize.cx, sourceSize.cy };
        return properties;
    }

    int DwmThumbnailVisual_IsAvailable()
    {
        return SUCCEEDED(LoadPrivateDwmApi()) ? 1 : 0;
    }

    int DwmThumbnailVisual_Create(HWND ownerWindowHandle,
        HWND sourceWindowHandle,
        IUnknown* compositor,
        void** visual,
        HTHUMBNAIL* thumbnailHandle)
    {
        if (!ownerWindowHandle || !sourceWindowHandle || !compositor || !visual || !thumbnailHandle)
        {
            return E_INVALIDARG;
        }

        *visual = nullptr;
        *thumbnailHandle = nullptr;

        HRESULT result = LoadPrivateDwmApi();

        if (FAILED(result))
        {
            return result;
        }

        ComPtr<IDCompositionDevice> compositionDevice;
        result = compositor->QueryInterface(IID_PPV_ARGS(compositionDevice.GetAddressOf()));

        if (FAILED(result))
        {
            return result;
        }

        DWM_THUMBNAIL_PROPERTIES properties = CreateThumbnailProperties(sourceWindowHandle, 1, 1, false);
        ComPtr<IDCompositionVisual2> thumbnailVisual;
        result = SafeCreateSharedThumbnailVisual(ownerWindowHandle,
            sourceWindowHandle,
            &properties,
            compositionDevice.Get(),
            reinterpret_cast<void**>(thumbnailVisual.GetAddressOf()),
            thumbnailHandle);

        if (FAILED(result) || !thumbnailVisual || !*thumbnailHandle)
        {
            if (*thumbnailHandle)
            {
                DwmUnregisterThumbnail(*thumbnailHandle);
                *thumbnailHandle = nullptr;
            }

            return FAILED(result) ? result : E_FAIL;
        }

        ComPtr<IMicrosoftCompositionVisual> compositionVisual;
        result = thumbnailVisual.As(&compositionVisual);

        if (FAILED(result))
        {
            DwmUnregisterThumbnail(*thumbnailHandle);
            *thumbnailHandle = nullptr;
            return result;
        }

        *visual = compositionVisual.Detach();
        return S_OK;
    }

    int DwmThumbnailVisual_Update(HTHUMBNAIL thumbnailHandle,
        HWND sourceWindowHandle,
        int width,
        int height,
        int isVisible)
    {
        if (!thumbnailHandle || !sourceWindowHandle || width <= 0 || height <= 0)
        {
            return E_INVALIDARG;
        }

        DWM_THUMBNAIL_PROPERTIES properties = CreateThumbnailProperties(sourceWindowHandle,
            width,
            height,
            isVisible != 0);
        return DwmUpdateThumbnailProperties(thumbnailHandle, &properties);
    }

    void DwmThumbnailVisual_Destroy(HTHUMBNAIL thumbnailHandle)
    {
        if (thumbnailHandle)
        {
            DwmUnregisterThumbnail(thumbnailHandle);
        }
    }
}
