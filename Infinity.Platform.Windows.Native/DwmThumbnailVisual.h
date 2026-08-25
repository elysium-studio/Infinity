#pragma once

#include <windows.h>
#include "NativeExports.h"

namespace Infinity::Platform::Windows::Native
{
	int DwmThumbnailVisual_IsAvailable();

	int DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, DwmThumbnailVisualItem* items, int count);

	int DwmThumbnailVisual_ShowElevated(HWND ownerWindowHandle, HWND sourceWindowHandle, int x, int y, int width, int height);

	void DwmThumbnailVisual_HideElevated();

	void DwmThumbnailVisual_Clear();
}
