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

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RefreshSource(unsigned long long previewId);

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear();

extern "C" __declspec(dllexport) int __stdcall ApplicationCatalog_Enumerate(wchar_t** buffer, int* characterCount);

extern "C" __declspec(dllexport) void __stdcall ApplicationCatalog_Free(wchar_t* buffer);

extern "C" __declspec(dllexport) int __stdcall ApplicationLauncher_Launch(const wchar_t* parsingName);
