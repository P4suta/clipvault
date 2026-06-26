// ClipVault root launcher.
//
// The distributed bundle keeps the real application and its ~300 dependencies under an `app\`
// subfolder so the extracted folder is not a wall of DLLs. This tiny native stub is the one
// obvious entry point at the bundle root: it starts the sibling `app\ClipVault.App.exe`,
// forwards its own command-line arguments, and exits. The app is a tray-resident single
// instance, so the launcher does not wait on it.
//
// Built standalone with cl.exe (see build.ps1); it is not part of the .NET solution and pulls in
// no runtime, so a downloaded bundle runs with no install.

#include <windows.h>
#include <string>

namespace
{
    constexpr wchar_t kRelativeAppDir[] = L"app";
    constexpr wchar_t kAppExe[] = L"ClipVault.App.exe";

    // Full path of this launcher's own directory (without trailing separator).
    std::wstring LauncherDirectory()
    {
        std::wstring path(MAX_PATH, L'\0');
        for (;;)
        {
            const DWORD len = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
            if (len == 0)
            {
                return std::wstring{};
            }
            if (len < path.size())
            {
                path.resize(len);
                break;
            }
            path.resize(path.size() * 2); // buffer was too small (path truncated); grow and retry.
        }

        const size_t slash = path.find_last_of(L"\\/");
        return slash == std::wstring::npos ? std::wstring{} : path.substr(0, slash);
    }

    // The original command line minus argv[0] (this launcher), so the child receives the caller's
    // arguments verbatim. Handles both a quoted and an unquoted leading token.
    std::wstring ForwardedArguments()
    {
        const wchar_t* cmd = GetCommandLineW();
        if (cmd == nullptr)
        {
            return std::wstring{};
        }

        const wchar_t* p = cmd;
        if (*p == L'"')
        {
            for (++p; *p != L'\0' && *p != L'"'; ++p)
            {
            }
            if (*p == L'"')
            {
                ++p;
            }
        }
        else
        {
            for (; *p != L'\0' && *p != L' ' && *p != L'\t'; ++p)
            {
            }
        }

        while (*p == L' ' || *p == L'\t')
        {
            ++p;
        }
        return std::wstring(p);
    }

    void ReportError(const std::wstring& message)
    {
        MessageBoxW(nullptr, message.c_str(), L"ClipVault", MB_ICONERROR | MB_OK);
    }
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    const std::wstring dir = LauncherDirectory();
    if (dir.empty())
    {
        ReportError(L"Could not locate the ClipVault folder.");
        return 1;
    }

    const std::wstring appDir = dir + L"\\" + kRelativeAppDir;
    const std::wstring appExe = appDir + L"\\" + kAppExe;

    if (GetFileAttributesW(appExe.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        ReportError(L"Could not find app\\" + std::wstring(kAppExe) +
                    L". Extract the whole ClipVault folder before running.");
        return 1;
    }

    // CreateProcessW may write to lpCommandLine, so hand it a mutable, owned buffer whose first
    // token is the (quoted) target exe followed by the forwarded arguments.
    std::wstring commandLine = L"\"" + appExe + L"\"";
    const std::wstring args = ForwardedArguments();
    if (!args.empty())
    {
        commandLine += L" " + args;
    }
    std::wstring commandLineBuffer = commandLine;

    STARTUPINFOW startupInfo{};
    startupInfo.cb = sizeof(startupInfo);
    PROCESS_INFORMATION processInfo{};

    const BOOL started = CreateProcessW(
        appExe.c_str(),
        commandLineBuffer.data(),
        nullptr,
        nullptr,
        FALSE,
        0,
        nullptr,
        appDir.c_str(), // run with app\ as the working directory so relative lookups resolve there.
        &startupInfo,
        &processInfo);

    if (!started)
    {
        ReportError(L"Failed to start ClipVault (error " + std::to_wstring(GetLastError()) + L").");
        return 1;
    }

    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
    return 0;
}
