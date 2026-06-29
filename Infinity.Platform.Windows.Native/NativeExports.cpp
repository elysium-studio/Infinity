#include "NativeExports.h"
#include "DwmThumbnailVisual.h"

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable()
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_IsAvailable();
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, DwmThumbnailVisualItem* items, int count)
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_RenderBatch(ownerWindowHandle, items, count);
}

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear()
{
    Infinity::Platform::Windows::Native::DwmThumbnailVisual_Clear();
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_GetLastHResult()
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_GetLastHResult();
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_GetLastBridgeHResult()
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_GetLastBridgeHResult();
}