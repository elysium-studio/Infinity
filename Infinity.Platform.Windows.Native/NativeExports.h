#pragma once

#include <windows.h>
#include <dwmapi.h>

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable();

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_Create(HWND ownerWindowHandle,
    HWND sourceWindowHandle,
    IUnknown* compositor,
    void** visual,
    HTHUMBNAIL* thumbnailHandle);

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_Update(HTHUMBNAIL thumbnailHandle,
    HWND sourceWindowHandle,
    int width,
    int height,
    int isVisible);

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Destroy(HTHUMBNAIL thumbnailHandle);
