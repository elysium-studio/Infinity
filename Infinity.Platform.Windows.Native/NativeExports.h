#pragma once

#include <windows.h>

struct DwmThumbnailVisualItem
{
    HWND SourceWindowHandle;
    HANDLE SharedTargetHandle;
    int Width;
    int Height;
    int ResultHResult;
};

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable();

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, DwmThumbnailVisualItem* items, int count);

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear();

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_GetLastHResult();

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_GetLastBridgeHResult();