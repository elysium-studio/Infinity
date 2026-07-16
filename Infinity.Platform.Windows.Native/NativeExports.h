#pragma once

#include <windows.h>

struct DwmThumbnailVisualItem
{
    unsigned long long PreviewId;
    HWND SourceWindowHandle;
    int X;
    int Y;
    int Width;
    int Height;
    int ZIndex;
    int IsVisible;
    int IsElevated;
};

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable();

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, HANDLE sharedTargetHandle, DwmThumbnailVisualItem* items, int count);

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear();
