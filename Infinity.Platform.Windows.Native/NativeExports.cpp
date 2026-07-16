#include "NativeExports.h"
#include "DwmThumbnailVisual.h"

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable()
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_IsAvailable();
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, HANDLE sharedTargetHandle, DwmThumbnailVisualItem* items, int count)
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_RenderBatch(ownerWindowHandle, sharedTargetHandle, items, count);
}

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear()
{
    Infinity::Platform::Windows::Native::DwmThumbnailVisual_Clear();
}
