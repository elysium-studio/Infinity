#include "DwmThumbnailVisual.h"

#include <windows.h>
#include <inspectable.h>
#include <winstring.h>
#include <roapi.h>
#include <d3d11.h>
#include <dxgi1_3.h>
#include <d2d1_2.h>
#include <dcomp.h>
#include <dwmapi.h>
#include <wrl/client.h>
#include <vector>
#include <utility>
#include <cwchar>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dcomp.lib")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "runtimeobject.lib")

#ifndef DWM_TNP_ENABLE3D
#define DWM_TNP_ENABLE3D 0x04000000
#endif

using Microsoft::WRL::ComPtr;

namespace Infinity::Platform::Windows::Native
{
    using DwmpCreateSharedThumbnailVisual = HRESULT(WINAPI*)(HWND destinationWindowHandle, HWND sourceWindowHandle, DWORD thumbnailFlags, DWM_THUMBNAIL_PROPERTIES* thumbnailProperties, IDCompositionDevice* compositionDevice, void** visual, HTHUMBNAIL* thumbnailHandle);
    using DwmpQueryWindowThumbnailSourceSize = HRESULT(WINAPI*)(HWND sourceWindowHandle, BOOL clientOnly, SIZE* size);

    struct __declspec(uuid("e7894c70-af56-4f52-b382-4b3cd263dc6f")) IInteropCompositorPartner :
        IUnknown
    {
    };

    struct __declspec(uuid("22118adf-23f1-4801-bcfa-66cbf48cc51b")) IInteropCompositorFactoryPartner :
        IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE CreateInteropCompositor(IUnknown* renderingDevice, IUnknown* callback, REFIID iid, void** instance) = 0;

        virtual HRESULT STDMETHODCALLTYPE CheckEnabled(boolean* enableInteropCompositor, boolean* enableExposeVisual) = 0;
    };

    struct __declspec(uuid("b403ca50-7f8c-4e83-985f-cc45060036d8")) IPlatformCompositor :
        IInspectable
    {
    };

    struct __declspec(uuid("117E202D-A859-4C89-873B-C2AA566788E3")) IPlatformVisual :
        IInspectable
    {
    };

    struct __declspec(uuid("9CBD9312-070d-4588-9bf3-bbf528cf3e84")) ICompositionPartner :
        IUnknown
    {
    };

    struct __declspec(uuid("A1BEA8BA-D726-4663-8129-6B5E7927FFA6")) IVisualTargetPartner :
        IUnknown
    {
        virtual HRESULT STDMETHODCALLTYPE GetRoot(IUnknown** root) = 0;

        virtual HRESULT STDMETHODCALLTYPE SetRoot(IUnknown* root) = 0;
    };

    using OpenSharedTargetFromHandle = HRESULT(STDMETHODCALLTYPE*)(ICompositionPartner* partner, HANDLE handle, IVisualTargetPartner** target);

    struct ThumbnailTarget
    {
        HWND SourceWindowHandle{};
        HANDLE SharedTargetHandle{};
        HTHUMBNAIL ThumbnailHandle{};
        SIZE SourceSize{};
        int Width{};
        int Height{};
        bool IsActive{};
        ComPtr<IVisualTargetPartner> VisualTarget;
        ComPtr<IDCompositionVisual2> RootVisual;
        ComPtr<IDCompositionVisual2> ThumbnailVisual;
    };

    static HMODULE dwmapiModule;
    static DwmpCreateSharedThumbnailVisual createSharedThumbnailVisual;
    static DwmpQueryWindowThumbnailSourceSize queryWindowThumbnailSourceSize;
    static HRESULT lastResult = S_OK;
    static HRESULT lastBridgeResult = S_OK;

    static HWND ownerWindowHandle;
    static std::vector<ThumbnailTarget> targets;

    static ComPtr<ID3D11Device> d3dDevice;
    static ComPtr<IDXGIDevice> dxgiDevice;
    static ComPtr<ID2D1Factory1> d2dFactory;
    static ComPtr<ID2D1Device> d2dDevice;
    static ComPtr<IInteropCompositorPartner> interopCompositor;
    static ComPtr<IPlatformCompositor> platformCompositor;
    static ComPtr<ICompositionPartner> compositionPartner;
    static ComPtr<IDCompositionDesktopDevice> compositionDesktopDevice;
    static ComPtr<IDCompositionDevice> compositionDevice;

    static LONG MaxLong(LONG left, LONG right)
    {
        return left > right ? left : right;
    }

    static void SetLastResult(HRESULT result)
    {
        lastResult = result;
        lastBridgeResult = result;
    }

    // The three Safe* functions below are the only places in this file that use
    // __try/__except. MSVC forbids mixing SEH with any C++ objects that require
    // unwinding in the same function (error C2712), so each of these takes only
    // POD parameters and declares no locals beyond the return value, keeping them
    // eligible for SEH regardless of what the rest of the file does.

    static HRESULT SafeOpenSharedTarget(OpenSharedTargetFromHandle function, ICompositionPartner* partner, HANDLE handle, IVisualTargetPartner** target)
    {
        __try
        {
            return function(partner, handle, target);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return HRESULT_FROM_WIN32(ERROR_NOACCESS);
        }
    }

    static HRESULT SafeCreateSharedThumbnailVisual(DwmpCreateSharedThumbnailVisual function, HWND destinationWindowHandle, HWND sourceWindowHandle, DWORD thumbnailFlags, DWM_THUMBNAIL_PROPERTIES* thumbnailProperties, IDCompositionDevice* device, void** visual, HTHUMBNAIL* thumbnailHandle)
    {
        __try
        {
            return function(destinationWindowHandle, sourceWindowHandle, thumbnailFlags, thumbnailProperties, device, visual, thumbnailHandle);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return HRESULT_FROM_WIN32(ERROR_NOACCESS);
        }
    }

    static HRESULT SafeQuerySourceSize(DwmpQueryWindowThumbnailSourceSize function, HWND sourceWindowHandle, BOOL clientOnly, SIZE* size)
    {
        __try
        {
            return function(sourceWindowHandle, clientOnly, size);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return HRESULT_FROM_WIN32(ERROR_NOACCESS);
        }
    }

    static HRESULT GetActivationFactory(const wchar_t* className, REFIID iid, void** factory)
    {
        HSTRING classString = nullptr;
        HRESULT result = WindowsCreateString(className, static_cast<UINT32>(std::wcslen(className)), &classString);

        if (FAILED(result))
        {
            return result;
        }

        result = RoGetActivationFactory(classString, iid, factory);
        WindowsDeleteString(classString);
        return result;
    }

    static HRESULT CreateD3DDevice()
    {
        if (d3dDevice)
        {
            return S_OK;
        }

        HRESULT result = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, d3dDevice.GetAddressOf(), nullptr, nullptr);

        if (FAILED(result))
        {
            result = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, d3dDevice.GetAddressOf(), nullptr, nullptr);
        }

        if (FAILED(result))
        {
            return result;
        }

        result = d3dDevice.As(&dxgiDevice);

        if (FAILED(result))
        {
            return result;
        }

        return S_OK;
    }

    static HRESULT CreateD2DDevice()
    {
        if (d2dDevice)
        {
            return S_OK;
        }

        HRESULT result = CreateD3DDevice();

        if (FAILED(result))
        {
            return result;
        }

        D2D1_FACTORY_OPTIONS options{};
        result = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), &options, reinterpret_cast<void**>(d2dFactory.GetAddressOf()));

        if (FAILED(result))
        {
            return result;
        }

        result = d2dFactory->CreateDevice(dxgiDevice.Get(), d2dDevice.GetAddressOf());

        if (FAILED(result))
        {
            return result;
        }

        return S_OK;
    }

    static HRESULT CreateInteropCompositorFactory(IInteropCompositorFactoryPartner** factory)
    {
        HRESULT result = GetActivationFactory(L"Windows.UI.Composition.Compositor", __uuidof(IInteropCompositorFactoryPartner), reinterpret_cast<void**>(factory));

        if (SUCCEEDED(result))
        {
            return result;
        }

        return GetActivationFactory(L"Microsoft.UI.Composition.Compositor", __uuidof(IInteropCompositorFactoryPartner), reinterpret_cast<void**>(factory));
    }

    static HRESULT EnsureInteropCompositionDevice()
    {
        if (compositionDevice && compositionPartner)
        {
            return S_OK;
        }

        HRESULT result = CreateD2DDevice();

        if (FAILED(result))
        {
            return result;
        }

        ComPtr<IInteropCompositorFactoryPartner> factory;
        result = CreateInteropCompositorFactory(factory.GetAddressOf());

        if (FAILED(result))
        {
            return result;
        }

        result = factory->CreateInteropCompositor(d2dDevice.Get(), nullptr, __uuidof(IInteropCompositorPartner), reinterpret_cast<void**>(interopCompositor.GetAddressOf()));

        if (FAILED(result))
        {
            return result;
        }

        result = interopCompositor.As(&compositionDesktopDevice);

        if (FAILED(result))
        {
            return result;
        }

        result = compositionDesktopDevice.As(&compositionDevice);

        if (FAILED(result))
        {
            return result;
        }

        result = interopCompositor.As(&platformCompositor);

        if (FAILED(result))
        {
            return result;
        }

        result = platformCompositor.As(&compositionPartner);

        if (FAILED(result))
        {
            return result;
        }

        return S_OK;
    }

    static HRESULT OpenVisualTargetFromHandle(HANDLE sharedTargetHandle, IVisualTargetPartner** target)
    {
        if (!compositionPartner || !sharedTargetHandle || !target)
        {
            return E_INVALIDARG;
        }

        void** table = *reinterpret_cast<void***>(compositionPartner.Get());
        OpenSharedTargetFromHandle openSharedTargetFromHandle = reinterpret_cast<OpenSharedTargetFromHandle>(table[9]);

        if (!openSharedTargetFromHandle)
        {
            return E_POINTER;
        }

        return SafeOpenSharedTarget(openSharedTargetFromHandle, compositionPartner.Get(), sharedTargetHandle, target);
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

        if (!createSharedThumbnailVisual)
        {
            return HRESULT_FROM_WIN32(ERROR_PROC_NOT_FOUND);
        }

        return S_OK;
    }

    static SIZE GetSourceSize(HWND windowHandle)
    {
        SIZE size{};

        if (queryWindowThumbnailSourceSize &&
            SUCCEEDED(SafeQuerySourceSize(queryWindowThumbnailSourceSize, windowHandle, FALSE, &size)) &&
            size.cx > 0 && size.cy > 0)
        {
            return size;
        }

        RECT rect{};

        if (GetWindowRect(windowHandle, &rect))
        {
            size.cx = MaxLong(1, rect.right - rect.left);
            size.cy = MaxLong(1, rect.bottom - rect.top);
            return size;
        }

        size.cx = 1;
        size.cy = 1;
        return size;
    }

    static DWM_THUMBNAIL_PROPERTIES CreateThumbnailProperties(SIZE sourceSize, int width, int height)
    {
        DWM_THUMBNAIL_PROPERTIES properties{};
        properties.dwFlags = DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE | DWM_TNP_ENABLE3D;
        properties.fVisible = TRUE;
        properties.opacity = 255;
        properties.rcDestination = RECT{ 0, 0, width, height };
        properties.rcSource = RECT{ 0, 0, sourceSize.cx, sourceSize.cy };
        return properties;
    }

    static void DestroyTarget(ThumbnailTarget& target)
    {
        if (target.RootVisual)
        {
            target.RootVisual->RemoveAllVisuals();
        }

        if (target.VisualTarget)
        {
            target.VisualTarget->SetRoot(nullptr);
        }

        if (target.ThumbnailHandle)
        {
            DwmUnregisterThumbnail(target.ThumbnailHandle);
            target.ThumbnailHandle = nullptr;
        }

        target.ThumbnailVisual.Reset();
        target.RootVisual.Reset();
        target.VisualTarget.Reset();
        target.SourceWindowHandle = nullptr;
        target.SharedTargetHandle = nullptr;
        target.SourceSize = {};
        target.Width = 0;
        target.Height = 0;
        target.IsActive = false;
    }

    static ThumbnailTarget* FindTarget(HANDLE sharedTargetHandle)
    {
        for (ThumbnailTarget& target : targets)
        {
            if (target.SharedTargetHandle == sharedTargetHandle)
            {
                return &target;
            }
        }

        return nullptr;
    }

    static HRESULT CreateTarget(HWND currentOwnerWindowHandle, DwmThumbnailVisualItem const& item, ThumbnailTarget& target)
    {
        target.SourceWindowHandle = item.SourceWindowHandle;
        target.SharedTargetHandle = item.SharedTargetHandle;
        target.Width = item.Width;
        target.Height = item.Height;
        target.SourceSize = GetSourceSize(item.SourceWindowHandle);
        target.IsActive = true;

        HRESULT result = OpenVisualTargetFromHandle(item.SharedTargetHandle, target.VisualTarget.GetAddressOf());

        if (FAILED(result))
        {
            return result;
        }

        ComPtr<IDCompositionVisual> rootVisualBase;
        result = compositionDevice->CreateVisual(rootVisualBase.GetAddressOf());

        if (FAILED(result))
        {
            return result;
        }

        result = rootVisualBase.As(&target.RootVisual);

        if (FAILED(result))
        {
            return result;
        }

        ComPtr<IPlatformVisual> platformRootVisual;
        result = target.RootVisual.As(&platformRootVisual);

        if (FAILED(result))
        {
            return result;
        }

        result = target.VisualTarget->SetRoot(platformRootVisual.Get());

        if (FAILED(result))
        {
            return result;
        }

        DWM_THUMBNAIL_PROPERTIES properties = CreateThumbnailProperties(target.SourceSize, item.Width, item.Height);
        void* visual = nullptr;

        result = SafeCreateSharedThumbnailVisual(createSharedThumbnailVisual, currentOwnerWindowHandle, item.SourceWindowHandle, 2, &properties, compositionDevice.Get(), &visual, &target.ThumbnailHandle);

        if (FAILED(result) || !visual || !target.ThumbnailHandle)
        {
            return FAILED(result) ? result : E_FAIL;
        }

        target.ThumbnailVisual.Attach(static_cast<IDCompositionVisual2*>(visual));
        target.ThumbnailVisual->SetOffsetX(0.0f);
        target.ThumbnailVisual->SetOffsetY(0.0f);

        result = target.RootVisual->AddVisual(target.ThumbnailVisual.Get(), TRUE, nullptr);

        if (FAILED(result))
        {
            return result;
        }

        return S_OK;
    }

    static HRESULT UpdateTarget(DwmThumbnailVisualItem const& item, ThumbnailTarget& target)
    {
        target.SourceSize = GetSourceSize(item.SourceWindowHandle);
        target.Width = item.Width;
        target.Height = item.Height;
        target.IsActive = true;

        DWM_THUMBNAIL_PROPERTIES properties = CreateThumbnailProperties(target.SourceSize, item.Width, item.Height);
        HRESULT result = DwmUpdateThumbnailProperties(target.ThumbnailHandle, &properties);

        if (FAILED(result))
        {
            return result;
        }

        if (target.ThumbnailVisual)
        {
            target.ThumbnailVisual->SetOffsetX(0.0f);
            target.ThumbnailVisual->SetOffsetY(0.0f);
        }

        return S_OK;
    }

    static void RemoveInactiveTargets()
    {
        size_t index = 0;

        while (index < targets.size())
        {
            if (targets[index].IsActive)
            {
                index++;
                continue;
            }

            DestroyTarget(targets[index]);
            targets.erase(targets.begin() + index);
        }
    }

    int DwmThumbnailVisual_IsAvailable()
    {
        HRESULT result = LoadPrivateDwmApi();
        lastResult = result;
        return SUCCEEDED(result) ? 1 : 0;
    }

    int DwmThumbnailVisual_RenderBatch(HWND currentOwnerWindowHandle, DwmThumbnailVisualItem* items, int count)
    {
        if (!currentOwnerWindowHandle)
        {
            SetLastResult(E_INVALIDARG);
            return lastResult;
        }

        HRESULT result = LoadPrivateDwmApi();

        if (FAILED(result))
        {
            SetLastResult(result);
            return result;
        }

        result = EnsureInteropCompositionDevice();

        if (FAILED(result))
        {
            SetLastResult(result);
            return result;
        }

        if (ownerWindowHandle != currentOwnerWindowHandle)
        {
            for (ThumbnailTarget& target : targets)
            {
                DestroyTarget(target);
            }

            targets.clear();
            ownerWindowHandle = currentOwnerWindowHandle;
        }

        for (ThumbnailTarget& target : targets)
        {
            target.IsActive = false;
        }

        HRESULT lastItemResult = S_OK;

        if (items && count > 0)
        {
            for (int index = 0; index < count; index++)
            {
                DwmThumbnailVisualItem& item = items[index];
                item.ResultHResult = S_OK;

                if (!item.SourceWindowHandle || !item.SharedTargetHandle || item.Width <= 0 || item.Height <= 0)
                {
                    item.ResultHResult = E_INVALIDARG;
                    continue;
                }

                ThumbnailTarget* target = FindTarget(item.SharedTargetHandle);

                if (target && target->SourceWindowHandle != item.SourceWindowHandle)
                {
                    DestroyTarget(*target);
                    target = nullptr;
                }

                HRESULT itemResult;

                if (!target)
                {
                    targets.push_back({});
                    target = &targets.back();

                    itemResult = CreateTarget(currentOwnerWindowHandle, item, *target);

                    if (FAILED(itemResult))
                    {
                        DestroyTarget(*target);
                        targets.pop_back();
                    }
                }
                else
                {
                    itemResult = UpdateTarget(item, *target);

                    if (FAILED(itemResult))
                    {
                        DestroyTarget(*target);
                    }
                }

                item.ResultHResult = itemResult;

                if (FAILED(itemResult))
                {
                    lastItemResult = itemResult;
                }
            }
        }

        RemoveInactiveTargets();

        result = compositionDevice->Commit();

        if (FAILED(result))
        {
            SetLastResult(result);
            return result;
        }

        if (FAILED(lastItemResult))
        {
            SetLastResult(lastItemResult);
            return lastItemResult;
        }

        SetLastResult(S_OK);
        return S_OK;
    }

    void DwmThumbnailVisual_Clear()
    {
        for (ThumbnailTarget& target : targets)
        {
            DestroyTarget(target);
        }

        targets.clear();
        ownerWindowHandle = nullptr;

        if (compositionDevice)
        {
            HRESULT result = compositionDevice->Commit();
            SetLastResult(result);
            return;
        }

        SetLastResult(S_OK);
    }

    int DwmThumbnailVisual_GetLastHResult()
    {
        return lastResult;
    }

    int DwmThumbnailVisual_GetLastBridgeHResult()
    {
        return lastBridgeResult;
    }
}