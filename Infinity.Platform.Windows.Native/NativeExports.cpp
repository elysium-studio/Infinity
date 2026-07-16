#include "NativeExports.h"
#include "DwmThumbnailVisual.h"

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable()
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_IsAvailable();
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_Create(HWND ownerWindowHandle,
    HWND sourceWindowHandle,
    IUnknown* compositor,
    void** visual,
    HTHUMBNAIL* thumbnailHandle)
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_Create(ownerWindowHandle,
        sourceWindowHandle,
        compositor,
        visual,
        thumbnailHandle);
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_Update(HTHUMBNAIL thumbnailHandle,
    HWND sourceWindowHandle,
    int width,
    int height,
    int isVisible)
{
    return Infinity::Platform::Windows::Native::DwmThumbnailVisual_Update(thumbnailHandle,
        sourceWindowHandle,
        width,
        height,
        isVisible);
}

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Destroy(HTHUMBNAIL thumbnailHandle)
{
    Infinity::Platform::Windows::Native::DwmThumbnailVisual_Destroy(thumbnailHandle);
}
