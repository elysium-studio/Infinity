#include "NativeExports.h"
#include <knownfolders.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <algorithm>
#include <cstdlib>
#include <cwctype>
#include <string>
#include <vector>

namespace
{
    class ComInitialiser
    {
    public:
        ComInitialiser() : result(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED))
        {
        }

        ~ComInitialiser()
        {
            if (SUCCEEDED(result))
            {
                CoUninitialize();
            }
        }

    private:
        HRESULT result;
    };

    struct ApplicationEntry
    {
        std::wstring Id;
        std::wstring DisplayName;
    };

    bool CompareApplications(const ApplicationEntry& left, const ApplicationEntry& right)
    {
        return _wcsicmp(left.DisplayName.c_str(), right.DisplayName.c_str()) < 0;
    }
}

extern "C" __declspec(dllexport) int __stdcall ApplicationCatalog_Enumerate(wchar_t** buffer, int* characterCount)
{
    if (buffer == nullptr || characterCount == nullptr)
    {
        return E_INVALIDARG;
    }

    *buffer = nullptr;
    *characterCount = 0;
    ComInitialiser com;

    IShellItem* applicationsFolder = nullptr;
    HRESULT result = SHGetKnownFolderItem(FOLDERID_AppsFolder, KF_FLAG_DEFAULT, nullptr, IID_PPV_ARGS(&applicationsFolder));

    if (FAILED(result))
    {
        return result;
    }

    IEnumShellItems* enumerator = nullptr;
    result = applicationsFolder->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(&enumerator));
    applicationsFolder->Release();

    if (FAILED(result))
    {
        return result;
    }

    std::vector<ApplicationEntry> applications;
    IShellItem* item = nullptr;

    while (enumerator->Next(1, &item, nullptr) == S_OK)
    {
        PWSTR displayName = nullptr;
        PWSTR parsingName = nullptr;
        HRESULT displayResult = item->GetDisplayName(SIGDN_NORMALDISPLAY, &displayName);
        HRESULT parsingResult = item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, &parsingName);

        if (SUCCEEDED(displayResult) && SUCCEEDED(parsingResult) && displayName != nullptr && parsingName != nullptr && *displayName != L'\0' && *parsingName != L'\0')
        {
            applications.push_back({ std::wstring(L"shell:AppsFolder\\") + parsingName, displayName });
        }

        CoTaskMemFree(displayName);
        CoTaskMemFree(parsingName);
        item->Release();
        item = nullptr;
    }

    enumerator->Release();
    std::sort(applications.begin(), applications.end(), CompareApplications);

    size_t totalCharacters = 1;

    for (const ApplicationEntry& application : applications)
    {
        totalCharacters += application.Id.length() + application.DisplayName.length() + 2;
    }

    if (totalCharacters > static_cast<size_t>(INT_MAX))
    {
        return E_OUTOFMEMORY;
    }

    wchar_t* output = static_cast<wchar_t*>(CoTaskMemAlloc(totalCharacters * sizeof(wchar_t)));

    if (output == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    wchar_t* cursor = output;

    for (const ApplicationEntry& application : applications)
    {
        std::copy(application.Id.begin(), application.Id.end(), cursor);
        cursor += application.Id.length();
        *cursor++ = L'\0';
        std::copy(application.DisplayName.begin(), application.DisplayName.end(), cursor);
        cursor += application.DisplayName.length();
        *cursor++ = L'\0';
    }

    *cursor = L'\0';
    *buffer = output;
    *characterCount = static_cast<int>(totalCharacters);
    return S_OK;
}

extern "C" __declspec(dllexport) void __stdcall ApplicationCatalog_Free(wchar_t* buffer)
{
    CoTaskMemFree(buffer);
}

extern "C" __declspec(dllexport) int __stdcall ApplicationCatalog_GetIcon(const wchar_t* parsingName, int requestedSize, unsigned char** buffer, int* width, int* height)
{
    if (parsingName == nullptr || *parsingName == L'\0' || requestedSize <= 0 || buffer == nullptr || width == nullptr || height == nullptr)
    {
        return E_INVALIDARG;
    }

    *buffer = nullptr;
    *width = 0;
    *height = 0;
    ComInitialiser com;
    PIDLIST_ABSOLUTE itemId = nullptr;
    HRESULT result = SHParseDisplayName(parsingName, nullptr, &itemId, 0, nullptr);

    if (FAILED(result))
    {
        return result;
    }

    IShellItemImageFactory* imageFactory = nullptr;
    result = SHCreateItemFromIDList(itemId, IID_PPV_ARGS(&imageFactory));
    CoTaskMemFree(itemId);

    if (FAILED(result))
    {
        return result;
    }

    HBITMAP bitmap = nullptr;
    SIZE requested = { requestedSize, requestedSize };
    result = imageFactory->GetImage(requested, SIIGBF_ICONONLY, &bitmap);
    imageFactory->Release();

    if (FAILED(result))
    {
        return result;
    }

    BITMAP bitmapDetails = {};

    if (GetObjectW(bitmap, sizeof(bitmapDetails), &bitmapDetails) == 0)
    {
        result = HRESULT_FROM_WIN32(GetLastError());
        DeleteObject(bitmap);
        return result;
    }

    int bitmapWidth = bitmapDetails.bmWidth;
    int bitmapHeight = std::abs(bitmapDetails.bmHeight);

    if (bitmapWidth <= 0 || bitmapHeight <= 0 || bitmapWidth > INT_MAX / 4 || bitmapHeight > INT_MAX / (bitmapWidth * 4))
    {
        DeleteObject(bitmap);
        return E_UNEXPECTED;
    }

    int byteCount = bitmapWidth * bitmapHeight * 4;
    unsigned char* pixels = static_cast<unsigned char*>(CoTaskMemAlloc(byteCount));

    if (pixels == nullptr)
    {
        DeleteObject(bitmap);
        return E_OUTOFMEMORY;
    }

    BITMAPINFO bitmapInfo = {};
    bitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bitmapInfo.bmiHeader.biWidth = bitmapWidth;
    bitmapInfo.bmiHeader.biHeight = -bitmapHeight;
    bitmapInfo.bmiHeader.biPlanes = 1;
    bitmapInfo.bmiHeader.biBitCount = 32;
    bitmapInfo.bmiHeader.biCompression = BI_RGB;
    HDC deviceContext = GetDC(nullptr);
    int copiedRows = GetDIBits(deviceContext, bitmap, 0, bitmapHeight, pixels, &bitmapInfo, DIB_RGB_COLORS);
    ReleaseDC(nullptr, deviceContext);
    DeleteObject(bitmap);

    if (copiedRows != bitmapHeight)
    {
        DWORD error = GetLastError();
        CoTaskMemFree(pixels);
        return error == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(error);
    }

    bool hasAlpha = false;

    for (int index = 3; index < byteCount; index += 4)
    {
        if (pixels[index] != 0)
        {
            hasAlpha = true;
            break;
        }
    }

    if (!hasAlpha)
    {
        for (int index = 3; index < byteCount; index += 4)
        {
            pixels[index] = 255;
        }
    }
    else
    {
        // GetDIBits preserves the Shell bitmap's straight-alpha BGRA pixels,
        // while WinUI WriteableBitmap consumes premultiplied BGRA. Convert at
        // the platform boundary to avoid bright, jagged fringes around icons.
        for (int index = 0; index < byteCount; index += 4)
        {
            unsigned int alpha = pixels[index + 3];
            pixels[index] = static_cast<unsigned char>((pixels[index] * alpha + 127u) / 255u);
            pixels[index + 1] = static_cast<unsigned char>((pixels[index + 1] * alpha + 127u) / 255u);
            pixels[index + 2] = static_cast<unsigned char>((pixels[index + 2] * alpha + 127u) / 255u);
        }
    }

    *buffer = pixels;
    *width = bitmapWidth;
    *height = bitmapHeight;
    return S_OK;
}

extern "C" __declspec(dllexport) void __stdcall ApplicationCatalog_FreeIcon(unsigned char* buffer)
{
    CoTaskMemFree(buffer);
}

extern "C" __declspec(dllexport) int __stdcall ApplicationLauncher_Launch(const wchar_t* parsingName)
{
    if (parsingName == nullptr || *parsingName == L'\0')
    {
        return E_INVALIDARG;
    }

    ComInitialiser com;
    PIDLIST_ABSOLUTE itemId = nullptr;
    HRESULT result = SHParseDisplayName(parsingName, nullptr, &itemId, 0, nullptr);

    if (FAILED(result))
    {
        return result;
    }

    SHELLEXECUTEINFOW executeInfo{};
    executeInfo.cbSize = sizeof(executeInfo);
    executeInfo.fMask = SEE_MASK_IDLIST | SEE_MASK_FLAG_NO_UI;
    executeInfo.lpVerb = L"open";
    executeInfo.lpIDList = itemId;
    executeInfo.nShow = SW_SHOWNORMAL;

    BOOL launched = ShellExecuteExW(&executeInfo);
    result = launched ? S_OK : HRESULT_FROM_WIN32(GetLastError());
    CoTaskMemFree(itemId);
    return result;
}
