#pragma once

#include <windows.h>

struct DwmThumbnailVisualItem
{
	unsigned long long PreviewId;
	HWND SourceWindowHandle;
	HANDLE SharedTargetHandle;
	int Width;
	int Height;
	int IsVisible;
};

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable();

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, DwmThumbnailVisualItem* items, int count);

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear();
