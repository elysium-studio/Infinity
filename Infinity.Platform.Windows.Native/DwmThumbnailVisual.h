#pragma once

#include <windows.h>
#include <dwmapi.h>
#include "NativeExports.h"

namespace Infinity::Platform::Windows::Native
{
    int DwmThumbnailVisual_IsAvailable();

    int DwmThumbnailVisual_Create(HWND ownerWindowHandle,
        HWND sourceWindowHandle,
        IUnknown* compositor,
        void** visual,
        HTHUMBNAIL* thumbnailHandle);

    int DwmThumbnailVisual_Update(HTHUMBNAIL thumbnailHandle,
        HWND sourceWindowHandle,
        int width,
        int height,
        int isVisible);

    void DwmThumbnailVisual_Destroy(HTHUMBNAIL thumbnailHandle);
}
