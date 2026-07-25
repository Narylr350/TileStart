#include <windows.h>

#include <cstdint>
#include <cstdio>
#include <cstring>

namespace
{
    using ShellRegisterHotKeyFunction = BOOL(WINAPI*)(HWND, int, UINT, UINT, HWND);

    enum class ShellAdapter : std::uintptr_t
    {
        None = 0,
        Win10_19045 = 1,
        Win11_22631 = 2,
        Win11_26200 = 3,
    };

    constexpr wchar_t kPipeName[] = L"\\\\.\\pipe\\TileStart.Host";
    constexpr char kOpenCommand[] = "OPEN";
    constexpr DWORD kPipeTimeoutMilliseconds = 75;
    constexpr WORD kShellRegisterHotKeyOrdinal = 2671;
    constexpr int kShellStandaloneWinHotKeyId = 1;

    HMODULE g_module = nullptr;
    HANDLE g_worker_thread = nullptr;
    HANDLE g_stop_event = nullptr;
    HANDLE g_ready_event = nullptr;
    HHOOK g_mouse_hook = nullptr;
    HHOOK g_progman_hook = nullptr;
    HWND g_start_button = nullptr;
    ShellAdapter g_adapter = ShellAdapter::None;
    LONG g_started = 0;
    LONG g_install_succeeded = 0;
    ShellRegisterHotKeyFunction g_shell_register_hotkey = nullptr;
    void** g_shell_register_hotkey_slot = nullptr;

    void WriteShellLog(const char* message)
    {
        char local_app_data[MAX_PATH]{};
        const DWORD length = GetEnvironmentVariableA("LOCALAPPDATA", local_app_data, ARRAYSIZE(local_app_data));
        if (length == 0 || length >= ARRAYSIZE(local_app_data))
        {
            return;
        }

        char path[MAX_PATH]{};
        if (sprintf_s(path, "%s\\TileStart\\ShellHook.log", local_app_data) < 0)
        {
            return;
        }

        const HANDLE file = CreateFileA(path,
                                        FILE_APPEND_DATA,
                                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                                        nullptr,
                                        OPEN_ALWAYS,
                                        FILE_ATTRIBUTE_NORMAL,
                                        nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return;
        }

        SYSTEMTIME time{};
        GetLocalTime(&time);
        char line[512]{};
        const int line_length = sprintf_s(line,
                                          "[%04u-%02u-%02u %02u:%02u:%02u.%03u] %s\r\n",
                                          time.wYear,
                                          time.wMonth,
                                          time.wDay,
                                          time.wHour,
                                          time.wMinute,
                                          time.wSecond,
                                          time.wMilliseconds,
                                          message);
        if (line_length > 0)
        {
            DWORD written = 0;
            WriteFile(file, line, static_cast<DWORD>(line_length), &written, nullptr);
        }

        CloseHandle(file);
    }

    BOOL WINAPI ShellRegisterHotKeyHook(HWND window, int id, UINT modifiers, UINT virtual_key, HWND target)
    {
        if (modifiers == MOD_WIN && virtual_key == 0)
        {
            WriteShellLog("Blocked twinui ShellRegisterHotKey for standalone Win key.");
            return FALSE;
        }

        return g_shell_register_hotkey(window, id, modifiers, virtual_key, target);
    }

    BOOL PatchImportByAddress(HMODULE module, void* original, void* replacement, void*** patched_slot)
    {
        if (module == nullptr || original == nullptr || replacement == nullptr || patched_slot == nullptr)
        {
            return FALSE;
        }

        const auto* dos_header = reinterpret_cast<const IMAGE_DOS_HEADER*>(module);
        if (dos_header->e_magic != IMAGE_DOS_SIGNATURE)
        {
            return FALSE;
        }

        const auto* nt_headers = reinterpret_cast<const IMAGE_NT_HEADERS*>(
            reinterpret_cast<const BYTE*>(module) + dos_header->e_lfanew);
        if (nt_headers->Signature != IMAGE_NT_SIGNATURE)
        {
            return FALSE;
        }

        const auto& import_directory =
            nt_headers->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (import_directory.VirtualAddress == 0)
        {
            return FALSE;
        }

        const auto* imports = reinterpret_cast<const IMAGE_IMPORT_DESCRIPTOR*>(
            reinterpret_cast<const BYTE*>(module) + import_directory.VirtualAddress);
        for (; imports->Name != 0; ++imports)
        {
            const auto* imported_module = reinterpret_cast<const char*>(
                reinterpret_cast<const BYTE*>(module) + imports->Name);
            if (_stricmp(imported_module, "user32.dll") != 0)
            {
                continue;
            }

            auto* thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                reinterpret_cast<BYTE*>(module) + imports->FirstThunk);
            for (; thunk->u1.Function != 0; ++thunk)
            {
                auto** slot = reinterpret_cast<void**>(&thunk->u1.Function);
                if (*slot != original)
                {
                    continue;
                }

                DWORD old_protection = 0;
                if (!VirtualProtect(slot, sizeof(void*), PAGE_READWRITE, &old_protection))
                {
                    return FALSE;
                }

                InterlockedExchangePointer(slot, replacement);
                DWORD restored_protection = 0;
                VirtualProtect(slot, sizeof(void*), old_protection, &restored_protection);
                FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
                *patched_slot = slot;
                return TRUE;
            }
        }

        return FALSE;
    }

    void RestoreShellRegisterHotKeyImport()
    {
        if (g_shell_register_hotkey_slot == nullptr || g_shell_register_hotkey == nullptr)
        {
            return;
        }

        DWORD old_protection = 0;
        if (VirtualProtect(g_shell_register_hotkey_slot, sizeof(void*), PAGE_READWRITE, &old_protection))
        {
            InterlockedExchangePointer(g_shell_register_hotkey_slot,
                                       reinterpret_cast<void*>(g_shell_register_hotkey));
            DWORD restored_protection = 0;
            VirtualProtect(g_shell_register_hotkey_slot,
                           sizeof(void*),
                           old_protection,
                           &restored_protection);
        }

        g_shell_register_hotkey_slot = nullptr;
        g_shell_register_hotkey = nullptr;
    }

    void NTAPI UnregisterShellWinHotKey(ULONG_PTR)
    {
        WriteShellLog(UnregisterHotKey(nullptr, kShellStandaloneWinHotKeyId)
                          ? "Unregistered existing Shell standalone Win hotkey (ID 1)."
                          : "Unable to unregister existing Shell standalone Win hotkey (ID 1)."
        );
    }

    BOOL InstallShellWinHotKeyInterception()
    {
        const HMODULE user32 = GetModuleHandleW(L"user32.dll");
        const HMODULE twinui = GetModuleHandleW(L"twinui.dll");
        g_shell_register_hotkey = user32 == nullptr
                                      ? nullptr
                                      : reinterpret_cast<ShellRegisterHotKeyFunction>(
                                          GetProcAddress(user32, MAKEINTRESOURCEA(kShellRegisterHotKeyOrdinal)));
        if (!PatchImportByAddress(twinui,
                                  reinterpret_cast<void*>(g_shell_register_hotkey),
                                  reinterpret_cast<void*>(ShellRegisterHotKeyHook),
                                  &g_shell_register_hotkey_slot))
        {
            g_shell_register_hotkey = nullptr;
            WriteShellLog("Unable to patch twinui ShellRegisterHotKey import.");
            return FALSE;
        }

        WriteShellLog("Patched twinui ShellRegisterHotKey import.");
        const HWND application_manager = FindWindowW(L"ApplicationManager_ImmersiveShellWindow", nullptr);
        const DWORD application_manager_thread = application_manager == nullptr
                                                     ? 0
                                                     : GetWindowThreadProcessId(application_manager, nullptr);
        const HANDLE thread = application_manager_thread == 0
                                  ? nullptr
                                  : OpenThread(THREAD_SET_CONTEXT, FALSE, application_manager_thread);
        if (thread == nullptr || QueueUserAPC(UnregisterShellWinHotKey, thread, 0) == 0)
        {
            if (thread != nullptr)
            {
                CloseHandle(thread);
            }
            WriteShellLog("Unable to queue Shell standalone Win hotkey unregister APC.");
            return TRUE;
        }

        CloseHandle(thread);
        WriteShellLog("Queued Shell standalone Win hotkey unregister APC.");
        return TRUE;
    }

    BOOL RequestHostOpen()
    {
        if (!WaitNamedPipeW(kPipeName, kPipeTimeoutMilliseconds))
        {
            return FALSE;
        }

        const HANDLE pipe = CreateFileW(kPipeName,
                                        GENERIC_READ | GENERIC_WRITE,
                                        0,
                                        nullptr,
                                        OPEN_EXISTING,
                                        0,
                                        nullptr);
        if (pipe == INVALID_HANDLE_VALUE)
        {
            return FALSE;
        }

        ULONG server_process = 0;
        if (GetNamedPipeServerProcessId(pipe, &server_process) && server_process != 0)
        {
            AllowSetForegroundWindow(server_process);
        }

        BYTE response = 0;
        DWORD written = 0;
        DWORD response_size = 0;
        const BOOL delivered = WriteFile(pipe,
                                         kOpenCommand,
                                         sizeof(kOpenCommand) - 1,
                                         &written,
                                         nullptr)
            && written == sizeof(kOpenCommand) - 1
            && ReadFile(pipe,
                        &response,
                        sizeof(response),
                        &response_size,
                        nullptr);
        CloseHandle(pipe);
        return delivered && response_size == sizeof(response) && response == 1;
    }

    HWND FindWin10StartButton(HWND taskbar)
    {
        return FindWindowExW(taskbar, nullptr, L"Start", nullptr);
    }

    HWND FindWin11StartButton(HWND taskbar)
    {
        return FindWindowExW(taskbar, nullptr, L"Start", nullptr);
    }

    void RefreshStartButton()
    {
        const HWND taskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
        if (taskbar == nullptr)
        {
            g_start_button = nullptr;
            return;
        }

        switch (g_adapter)
        {
        case ShellAdapter::Win10_19045:
            g_start_button = FindWin10StartButton(taskbar);
            break;
        case ShellAdapter::Win11_22631:
        case ShellAdapter::Win11_26200:
            g_start_button = FindWin11StartButton(taskbar);
            break;
        default:
            g_start_button = nullptr;
            break;
        }
    }

    LRESULT CALLBACK MouseHook(int code, WPARAM message, LPARAM data)
    {
        if (code == HC_ACTION && message == WM_LBUTTONDOWN && g_start_button != nullptr)
        {
            const auto* mouse = reinterpret_cast<const MSLLHOOKSTRUCT*>(data);
            RECT start_button_rect{};
            if (GetWindowRect(g_start_button, &start_button_rect) && PtInRect(&start_button_rect, mouse->pt) &&
                RequestHostOpen())
            {
                return 1;
            }
        }

        return CallNextHookEx(g_mouse_hook, code, message, data);
    }

    LRESULT CALLBACK ProgmanMessageHook(int code, WPARAM remove_message, LPARAM data)
    {
        if (code == HC_ACTION && data != 0)
        {
            auto* message = reinterpret_cast<MSG*>(data);
            if (message->message == WM_SYSCOMMAND && (message->wParam & 0xFFF0) == SC_TASKLIST)
            {
                const BOOL delivered = RequestHostOpen();
                char diagnostic[160]{};
                sprintf_s(diagnostic,
                          "SC_TASKLIST observed: remove=%llu, hwnd=0x%p, lParam=0x%llX, delivered=%d.",
                          static_cast<unsigned long long>(remove_message),
                          message->hwnd,
                          static_cast<unsigned long long>(message->lParam),
                          delivered);
                WriteShellLog(diagnostic);
                if (delivered)
                {
                    message->message = WM_NULL;
                }
            }
        }

        return CallNextHookEx(g_progman_hook, code, remove_message, data);
    }

    DWORD WINAPI WorkerThread(LPVOID)
    {
        MSG message{};
        PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
        RefreshStartButton();
        g_mouse_hook = SetWindowsHookExW(WH_MOUSE_LL, MouseHook, g_module, 0);
        const HWND progman = FindWindowW(L"Progman", nullptr);
        const DWORD progman_thread = progman == nullptr ? 0 : GetWindowThreadProcessId(progman, nullptr);
        g_progman_hook = progman_thread == 0
                            ? nullptr
                            : SetWindowsHookExW(WH_GETMESSAGE, ProgmanMessageHook, g_module, progman_thread);
        WriteShellLog(g_progman_hook == nullptr
                          ? "Progman SC_TASKLIST interception installation failed."
                          : "Progman SC_TASKLIST interception installed.");
        if (g_adapter == ShellAdapter::Win11_26200)
        {
            InstallShellWinHotKeyInterception();
        }
        if (g_mouse_hook != nullptr && g_start_button != nullptr)
        {
            InterlockedExchange(&g_install_succeeded, 1);
        }
        SetEvent(g_ready_event);

        const HANDLE stop_handle[] = {g_stop_event};
        while (MsgWaitForMultipleObjects(1, stop_handle, FALSE, 250, QS_ALLINPUT) != WAIT_OBJECT_0)
        {
            while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
            {
                TranslateMessage(&message);
                DispatchMessageW(&message);
            }

            RefreshStartButton();
        }

        if (g_mouse_hook != nullptr)
        {
            UnhookWindowsHookEx(g_mouse_hook);
            g_mouse_hook = nullptr;
        }

        if (g_progman_hook != nullptr)
        {
            UnhookWindowsHookEx(g_progman_hook);
            g_progman_hook = nullptr;
        }

        RestoreShellRegisterHotKeyImport();

        return 0;
    }
} // namespace

extern "C" __declspec(dllexport) BOOL TileStartTryOpenMenu()
{
    return RequestHostOpen();
}

extern "C" __declspec(dllexport) DWORD WINAPI TileStartInstallHook(LPVOID parameter)
{
    const auto adapter = static_cast<ShellAdapter>(reinterpret_cast<std::uintptr_t>(parameter));
    if (adapter != ShellAdapter::Win10_19045 && adapter != ShellAdapter::Win11_22631 && adapter != ShellAdapter::Win11_26200)
    {
        return FALSE;
    }

    if (InterlockedCompareExchange(&g_started, 1, 0) != 0)
    {
        return g_adapter == adapter && InterlockedCompareExchange(&g_install_succeeded, 0, 0) != 0;
    }

    g_adapter = adapter;

    g_stop_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    g_ready_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (g_stop_event == nullptr || g_ready_event == nullptr)
    {
        InterlockedExchange(&g_started, 0);
        return FALSE;
    }

    g_worker_thread = CreateThread(nullptr, 0, WorkerThread, nullptr, 0, nullptr);
    if (g_worker_thread == nullptr || WaitForSingleObject(g_ready_event, 1000) != WAIT_OBJECT_0)
    {
        return FALSE;
    }

    return InterlockedCompareExchange(&g_install_succeeded, 0, 0) != 0;
}

extern "C" __declspec(dllexport) DWORD WINAPI TileStartStopHook(LPVOID)
{
    if (InterlockedCompareExchange(&g_started, 0, 0) == 0)
    {
        return TRUE;
    }

    SetEvent(g_stop_event);
    const DWORD stopped = WaitForSingleObject(g_worker_thread, 1500) == WAIT_OBJECT_0;
    if (g_worker_thread != nullptr)
    {
        CloseHandle(g_worker_thread);
        g_worker_thread = nullptr;
    }
    if (g_stop_event != nullptr)
    {
        CloseHandle(g_stop_event);
        g_stop_event = nullptr;
    }
    if (g_ready_event != nullptr)
    {
        CloseHandle(g_ready_event);
        g_ready_event = nullptr;
    }

    g_start_button = nullptr;
    g_adapter = ShellAdapter::None;
    g_install_succeeded = 0;
    g_started = 0;
    return stopped;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}
