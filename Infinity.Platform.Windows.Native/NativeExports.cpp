#include "NativeExports.h"
#include "DwmThumbnailVisual.h"
#include <knownfolders.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <algorithm>
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

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_IsAvailable()
{
	return Infinity::Platform::Windows::Native::DwmThumbnailVisual_IsAvailable();
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RenderBatch(HWND ownerWindowHandle, DwmThumbnailVisualItem* items, int count)
{
	return Infinity::Platform::Windows::Native::DwmThumbnailVisual_RenderBatch(ownerWindowHandle, items, count);
}

extern "C" __declspec(dllexport) int __stdcall DwmThumbnailVisual_RefreshSource(unsigned long long previewId)
{
	return Infinity::Platform::Windows::Native::DwmThumbnailVisual_RefreshSource(previewId);
}

extern "C" __declspec(dllexport) void __stdcall DwmThumbnailVisual_Clear()
{
	Infinity::Platform::Windows::Native::DwmThumbnailVisual_Clear();
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
