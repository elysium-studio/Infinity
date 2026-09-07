#include <windows.h>
#include <cassert>
#include <cstdio>

using GetIcon = int(__stdcall*)(HWND, int, unsigned char**, int*, int*);
using FreeIcon = void(__stdcall*)(unsigned char*);

int wmain(int argc, wchar_t** argv)
{
    assert(argc == 2);
    HMODULE library = LoadLibraryW(argv[1]);
    assert(library != nullptr);
    GetIcon getIcon = reinterpret_cast<GetIcon>(GetProcAddress(library, "WindowIcon_GetIcon"));
    FreeIcon freeIcon = reinterpret_cast<FreeIcon>(GetProcAddress(library, "ApplicationCatalog_FreeIcon"));
    assert(getIcon != nullptr && freeIcon != nullptr);
    WNDCLASSW windowClass = {};
    windowClass.lpfnWndProc = DefWindowProcW;
    windowClass.hInstance = GetModuleHandleW(nullptr);
    windowClass.lpszClassName = L"InfinityWindowIconTest";
    windowClass.hIcon = LoadIconW(nullptr, IDI_INFORMATION);
    assert(RegisterClassW(&windowClass) != 0);
    HWND window = CreateWindowExW(0, windowClass.lpszClassName, L"Icon test", WS_OVERLAPPEDWINDOW, 0, 0, 100, 100, nullptr, nullptr, windowClass.hInstance, nullptr);
    assert(window != nullptr);
    HICON titleIcon = LoadIconW(nullptr, IDI_WARNING);
    unsigned char* classPixels = nullptr;
    int width = 0;
    int height = 0;
    assert(getIcon(window, 32, &classPixels, &width, &height) == S_OK);
    assert(width == 32 && height == 32);
    SendMessageW(window, WM_SETICON, ICON_BIG, reinterpret_cast<LPARAM>(titleIcon));
    unsigned char* titlePixels = nullptr;
    assert(getIcon(window, 32, &titlePixels, &width, &height) == S_OK);
    assert(memcmp(titlePixels, classPixels, 32 * 32 * 4) != 0);
    bool transparent = false;
    bool opaque = false;
    for (int index = 0; index < 32 * 32 * 4; index += 4)
    {
        unsigned char alpha = titlePixels[index + 3];
        transparent |= alpha == 0;
        opaque |= alpha == 255;
        assert(titlePixels[index] <= alpha);
        assert(titlePixels[index + 1] <= alpha);
        assert(titlePixels[index + 2] <= alpha);
    }

    assert(transparent && opaque);
    ICONINFO info = {};
    assert(GetIconInfo(titleIcon, &info));
    DeleteObject(info.hbmColor);
    DeleteObject(info.hbmMask);
    freeIcon(classPixels);
    freeIcon(titlePixels);
    unsigned char* invalid = nullptr;
    assert(FAILED(getIcon(nullptr, 32, &invalid, &width, &height)));
    assert(invalid == nullptr && width == 0 && height == 0);
    assert(FAILED(getIcon(window, 257, &invalid, &width, &height)));
    DestroyWindow(window);
    UnregisterClassW(windowClass.lpszClassName, windowClass.hInstance);
    FreeLibrary(library);
    puts("Window icon tests passed: title icon, class fallback, alpha, borrowed-handle lifetime, invalid input.");
    return 0;
}
