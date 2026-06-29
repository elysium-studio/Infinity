#pragma once

#include <windows.h>
#include "NativeExports.h"

namespace Infinity::Platform::Windows::Native
{
    int DwmThumbnailVisual_IsAvailable();

    int DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, DwmThumbnailVisualItem* items, int count);

    void DwmThumbnailVisual_Clear();

    int DwmThumbnailVisual_GetLastHResult();

    int DwmThumbnailVisual_GetLastBridgeHResult();
}