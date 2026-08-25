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

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_ShowElevated(HWND ownerWindowHandle, HWND sourceWindowHandle, int x, int y, int width, int height)
{
	return Infinity::Platform::Windows::Native::DwmThumbnailVisual_ShowElevated(ownerWindowHandle, sourceWindowHandle, x, y, width, height);
}

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_HideElevated()
{
	Infinity::Platform::Windows::Native::DwmThumbnailVisual_HideElevated();
}

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear()
{
	Infinity::Platform::Windows::Native::DwmThumbnailVisual_Clear();
}
