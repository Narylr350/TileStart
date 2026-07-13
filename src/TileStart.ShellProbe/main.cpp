#include <windows.h>
#include <tlhelp32.h>

#include <atomic>
#include <chrono>
#include <csignal>
#include <format>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>

namespace
{
constexpr wchar_t kTaskbarClass[] = L"Shell_TrayWnd";
constexpr wchar_t kStartButtonClass[] = L"Start";

std::atomic_bool g_running = true;
HHOOK g_keyboard_hook = nullptr;
HWINEVENTHOOK g_window_visibility_hook = nullptr;
HWINEVENTHOOK g_window_foreground_hook = nullptr;
bool g_win_key_down = false;
bool g_win_key_chord = false;

void Log(std::wstring_view message)
{
    SYSTEMTIME now{};
    GetLocalTime(&now);
    std::wcout << std::format(L"[{:02}:{:02}:{:02}] {}\n", now.wHour, now.wMinute, now.wSecond, message);
}

std::wstring WindowClass(HWND window)
{
    wchar_t name[256]{};
    GetClassNameW(window, name, static_cast<int>(std::size(name)));
    return name;
}

std::wstring ProcessName(DWORD process_id)
{
    const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE)
    {
        return L"<unavailable>";
    }

    PROCESSENTRY32W entry{.dwSize = sizeof(entry)};
    if (!Process32FirstW(snapshot, &entry))
    {
        CloseHandle(snapshot);
        return L"<unavailable>";
    }

    do
    {
        if (entry.th32ProcessID == process_id)
        {
            CloseHandle(snapshot);
            return entry.szExeFile;
        }
    } while (Process32NextW(snapshot, &entry));

    CloseHandle(snapshot);
    return L"<unavailable>";
}

std::optional<DWORD> FindShellExplorerProcessId()
{
    const HWND taskbar = FindWindowW(kTaskbarClass, nullptr);
    if (taskbar == nullptr)
    {
        return std::nullopt;
    }

    DWORD process_id = 0;
    GetWindowThreadProcessId(taskbar, &process_id);
    return process_id == 0 ? std::nullopt : std::optional{process_id};
}

void LogTaskbarSnapshot()
{
    const HWND taskbar = FindWindowW(kTaskbarClass, nullptr);
    if (taskbar == nullptr)
    {
        Log(L"Shell_TrayWnd was not found.");
        return;
    }

    const HWND start_button = FindWindowExW(taskbar, nullptr, kStartButtonClass, nullptr);
    RECT taskbar_rect{};
    GetWindowRect(taskbar, &taskbar_rect);
    Log(std::format(L"Taskbar hwnd=0x{:X}, rect=({}, {})-({}, {}).",
                    reinterpret_cast<std::uintptr_t>(taskbar),
                    taskbar_rect.left,
                    taskbar_rect.top,
                    taskbar_rect.right,
                    taskbar_rect.bottom));

    if (start_button == nullptr)
    {
        Log(L"The Win10 Start child window (class 'Start') was not found.");
        return;
    }

    RECT start_rect{};
    GetWindowRect(start_button, &start_rect);
    Log(std::format(L"Start button hwnd=0x{:X}, class='Start', rect=({}, {})-({}, {}).",
                    reinterpret_cast<std::uintptr_t>(start_button),
                    start_rect.left,
                    start_rect.top,
                    start_rect.right,
                    start_rect.bottom));
}

LRESULT CALLBACK KeyboardHook(int code, WPARAM message, LPARAM data)
{
    if (code != HC_ACTION)
    {
        return CallNextHookEx(g_keyboard_hook, code, message, data);
    }

    const auto* key = reinterpret_cast<const KBDLLHOOKSTRUCT*>(data);
    const bool key_down = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
    const bool key_up = message == WM_KEYUP || message == WM_SYSKEYUP;
    const bool win_key = key->vkCode == VK_LWIN || key->vkCode == VK_RWIN;

    if (key_down && win_key)
    {
        g_win_key_down = true;
        g_win_key_chord = false;
        Log(std::format(L"Win key down: vk=0x{:X}.", key->vkCode));
    }
    else if (key_down && g_win_key_down)
    {
        g_win_key_chord = true;
        Log(std::format(L"Win-key chord observed: vk=0x{:X}.", key->vkCode));
    }
    else if (key_up && win_key && g_win_key_down)
    {
        Log(g_win_key_chord
                ? L"Win key released after a chord; native shortcut remains untouched."
                : L"Standalone Win key released; probe does not suppress the native Start-menu request.");
        g_win_key_down = false;
    }

    return CallNextHookEx(g_keyboard_hook, code, message, data);
}

void CALLBACK WindowEvent(HWINEVENTHOOK, DWORD event, HWND window, LONG object_id, LONG child_id, DWORD, DWORD)
{
    if (window == nullptr || object_id != OBJID_WINDOW || child_id != CHILDID_SELF)
    {
        return;
    }

    if (event != EVENT_OBJECT_SHOW && event != EVENT_OBJECT_HIDE && event != EVENT_SYSTEM_FOREGROUND)
    {
        return;
    }

    DWORD process_id = 0;
    GetWindowThreadProcessId(window, &process_id);
    if (process_id == 0)
    {
        return;
    }

    const wchar_t* event_name = event == EVENT_OBJECT_SHOW ? L"show" : event == EVENT_OBJECT_HIDE ? L"hide" : L"foreground";
    Log(std::format(L"Window event={} pid={} process='{}' hwnd=0x{:X} class='{}'.",
                    event_name,
                    process_id,
                    ProcessName(process_id),
                    reinterpret_cast<std::uintptr_t>(window),
                    WindowClass(window)));
}

BOOL WINAPI ConsoleControl(DWORD control_type)
{
    if (control_type == CTRL_C_EVENT || control_type == CTRL_BREAK_EVENT || control_type == CTRL_CLOSE_EVENT)
    {
        g_running = false;
        return TRUE;
    }

    return FALSE;
}

std::optional<int> ParseDuration(int argc, wchar_t* argv[])
{
    if (argc != 3 || std::wstring_view(argv[1]) != L"--duration")
    {
        return std::nullopt;
    }

    try
    {
        return std::stoi(argv[2]);
    }
    catch (...)
    {
        return std::nullopt;
    }
}

void PrintUsage()
{
    std::wcout << L"Usage:\n"
                  L"  TileStart.ShellProbe.exe --snapshot\n"
                  L"  TileStart.ShellProbe.exe [--duration seconds]\n\n"
                  L"The probe only observes Win-key input, relevant window events, and Explorer PID changes.\n"
                  L"It does not inject into Explorer or suppress the native Start menu.\n";
}
} // namespace

int wmain(int argc, wchar_t* argv[])
{
    if (argc == 2 && std::wstring_view(argv[1]) == L"--help")
    {
        PrintUsage();
        return 0;
    }

    LogTaskbarSnapshot();
    const auto explorer_pid = FindShellExplorerProcessId();
    Log(explorer_pid ? std::format(L"Shell Explorer PID={}", *explorer_pid) : L"Explorer process was not found.");

    if (argc == 2 && std::wstring_view(argv[1]) == L"--snapshot")
    {
        return 0;
    }

    const auto duration_seconds = ParseDuration(argc, argv);
    if (argc > 1 && !duration_seconds)
    {
        PrintUsage();
        return 2;
    }

    SetConsoleCtrlHandler(ConsoleControl, TRUE);
    g_keyboard_hook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHook, GetModuleHandleW(nullptr), 0);
    const DWORD keyboard_error = g_keyboard_hook == nullptr ? GetLastError() : ERROR_SUCCESS;
    g_window_visibility_hook = SetWinEventHook(EVENT_OBJECT_SHOW,
                                                EVENT_OBJECT_HIDE,
                                                nullptr,
                                                WindowEvent,
                                                0,
                                                0,
                                                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    const DWORD visibility_error = g_window_visibility_hook == nullptr ? GetLastError() : ERROR_SUCCESS;
    g_window_foreground_hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND,
                                                EVENT_SYSTEM_FOREGROUND,
                                                nullptr,
                                                WindowEvent,
                                                0,
                                                0,
                                                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    const DWORD foreground_error = g_window_foreground_hook == nullptr ? GetLastError() : ERROR_SUCCESS;
    if (g_keyboard_hook == nullptr || g_window_visibility_hook == nullptr || g_window_foreground_hook == nullptr)
    {
        Log(std::format(L"Failed to install observer hooks: keyboard={}, visibility={}, foreground={}.",
                        keyboard_error,
                        visibility_error,
                        foreground_error));
        if (g_keyboard_hook != nullptr)
        {
            UnhookWindowsHookEx(g_keyboard_hook);
        }
        if (g_window_visibility_hook != nullptr)
        {
            UnhookWinEvent(g_window_visibility_hook);
        }
        if (g_window_foreground_hook != nullptr)
        {
            UnhookWinEvent(g_window_foreground_hook);
        }
        return 1;
    }

    Log(L"Observer hooks installed. Press Win or click Start; press Ctrl+C to stop.");
    const auto deadline = duration_seconds
                              ? std::optional{std::chrono::steady_clock::now() + std::chrono::seconds(*duration_seconds)}
                              : std::nullopt;
    auto last_explorer_pid = explorer_pid;

    MSG message{};
    while (g_running && (!deadline || std::chrono::steady_clock::now() < *deadline))
    {
        while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }

        const auto current_explorer_pid = FindShellExplorerProcessId();
        if (current_explorer_pid != last_explorer_pid)
        {
            const auto previous = last_explorer_pid ? std::to_wstring(*last_explorer_pid) : L"none";
            const auto current = current_explorer_pid ? std::to_wstring(*current_explorer_pid) : L"none";
            Log(std::format(L"Shell Explorer PID changed: {} -> {}. No input was blocked; native Start remains fail-open.", previous, current));
            last_explorer_pid = current_explorer_pid;
            LogTaskbarSnapshot();
        }

        Sleep(50);
    }

    UnhookWinEvent(g_window_foreground_hook);
    UnhookWinEvent(g_window_visibility_hook);
    UnhookWindowsHookEx(g_keyboard_hook);
    Log(L"Observer hooks removed.");
    return 0;
}
