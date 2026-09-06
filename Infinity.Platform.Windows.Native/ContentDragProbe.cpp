#include "NativeExports.h"
#include <ole2.h>
#include <new>

namespace
{
    constexpr wchar_t ProbeClassName[] = L"Infinity.ContentDragProbe";

    class ContentDragProbe final : public IDropTarget
    {
        LONG references = 1;

    public:
        HWND window = nullptr;
        unsigned int kinds = 0;

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) override
        {
            if (result == nullptr)
            {
                return E_POINTER;
            }
            *result = nullptr;
            if (iid != IID_IUnknown && iid != IID_IDropTarget)
            {
                return E_NOINTERFACE;
            }
            *result = static_cast<IDropTarget*>(this);
            AddRef();
            return S_OK;
        }

        ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&references); }

        ULONG STDMETHODCALLTYPE Release() override
        {
            ULONG remaining = InterlockedDecrement(&references);
            if (remaining == 0)
            {
                delete this;
            }
            return remaining;
        }

        HRESULT STDMETHODCALLTYPE DragEnter(IDataObject*, DWORD, POINTL, DWORD* effect) override
        {
            *effect = DROPEFFECT_NONE;
            kinds = 32;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE DragOver(DWORD, POINTL, DWORD* effect) override
        {
            *effect = DROPEFFECT_NONE;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE DragLeave() override
        {
            kinds = 0;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE Drop(IDataObject*, DWORD, POINTL, DWORD* effect) override
        {
            *effect = DROPEFFECT_NONE;
            kinds = 0;
            return S_OK;
        }
    };

    LRESULT CALLBACK ProbeWindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_MOUSEACTIVATE)
        {
            return MA_NOACTIVATE;
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }
}

extern "C" __declspec(dllexport) int __stdcall ContentDragProbe_Create(void** result)
{
    if (result == nullptr)
    {
        return E_POINTER;
    }
    *result = nullptr;
    HRESULT initialized = OleInitialize(nullptr);
    if (FAILED(initialized))
    {
        return initialized;
    }
    HINSTANCE instance = GetModuleHandleW(L"Infinity.Platform.Windows.Native.dll");
    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = ProbeWindowProc;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = ProbeClassName;
    if (RegisterClassW(&windowClass) == 0 && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
    {
        HRESULT error = HRESULT_FROM_WIN32(GetLastError());
        OleUninitialize();
        return error;
    }
    ContentDragProbe* probe = new (std::nothrow) ContentDragProbe();
    if (probe == nullptr)
    {
        OleUninitialize();
        return E_OUTOFMEMORY;
    }
    POINT point{};
    GetCursorPos(&point);
    probe->window = CreateWindowExW(WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED,
        ProbeClassName, L"", WS_POPUP, point.x - 12, point.y - 12, 24, 24, nullptr, nullptr, instance, nullptr);
    HRESULT registered = probe->window == nullptr ? HRESULT_FROM_WIN32(GetLastError()) : RegisterDragDrop(probe->window, probe);
    if (FAILED(registered))
    {
        if (probe->window != nullptr)
        {
            DestroyWindow(probe->window);
        }
        probe->Release();
        OleUninitialize();
        return registered;
    }
    SetLayeredWindowAttributes(probe->window, 0, 1, LWA_ALPHA);
    ShowWindow(probe->window, SW_SHOWNOACTIVATE);
    *result = probe;
    return S_OK;
}

extern "C" __declspec(dllexport) unsigned int __stdcall ContentDragProbe_Poll(void* context)
{
    ContentDragProbe* probe = static_cast<ContentDragProbe*>(context);
    if (probe == nullptr)
    {
        return 0;
    }
    if (probe->kinds == 0)
    {
        POINT point{};
        GetCursorPos(&point);
        SetWindowPos(probe->window, HWND_TOPMOST, point.x - 12, point.y - 12, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }
    return probe->kinds;
}

extern "C" __declspec(dllexport) void __stdcall ContentDragProbe_Destroy(void* context)
{
    ContentDragProbe* probe = static_cast<ContentDragProbe*>(context);
    if (probe != nullptr)
    {
        ShowWindow(probe->window, SW_HIDE);
        RevokeDragDrop(probe->window);
        DestroyWindow(probe->window);
        probe->Release();
        OleUninitialize();
    }
}
