#pragma once

#include <windows.h>

extern "C" __declspec(dllexport) int __stdcall ContentDragProbe_Create(void** result);
extern "C" __declspec(dllexport) unsigned int __stdcall ContentDragProbe_Poll(void* context);
extern "C" __declspec(dllexport) void __stdcall ContentDragProbe_Destroy(void* context);

extern "C" __declspec(dllexport) int __stdcall ApplicationCatalog_Enumerate(wchar_t** buffer, int* characterCount);

extern "C" __declspec(dllexport) void __stdcall ApplicationCatalog_Free(wchar_t* buffer);

extern "C" __declspec(dllexport) int __stdcall ApplicationCatalog_GetIcon(const wchar_t* parsingName, int requestedSize, unsigned char** buffer, int* width, int* height);

extern "C" __declspec(dllexport) int __stdcall WindowIcon_GetIcon(HWND window, int requestedSize, unsigned char** buffer, int* width, int* height);

extern "C" __declspec(dllexport) void __stdcall ApplicationCatalog_FreeIcon(unsigned char* buffer);

extern "C" __declspec(dllexport) int __stdcall ApplicationLauncher_Launch(const wchar_t* parsingName);
