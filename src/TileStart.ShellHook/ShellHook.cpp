#include <windows.h>

namespace
{
constexpr wchar_t kPipeName[] = L"\\\\.\\pipe\\TileStart.Host";
constexpr char kOpenCommand[] = "OPEN";
constexpr DWORD kPipeTimeoutMilliseconds = 75;

HMODULE g_module = nullptr;
HANDLE g_worker_thread = nullptr;
HANDLE g_stop_event = nullptr;
HANDLE g_ready_event = nullptr;
HHOOK g_mouse_hook = nullptr;
HWND g_start_button = nullptr;
LONG g_started = 0;
LONG g_install_succeeded = 0;

BOOL RequestHostOpen()
{
    BYTE response = 0;
    DWORD response_size = 0;
    const BOOL delivered = CallNamedPipeW(kPipeName,
                                          const_cast<char*>(kOpenCommand),
                                          sizeof(kOpenCommand) - 1,
                                          &response,
                                          sizeof(response),
                                          &response_size,
                                          kPipeTimeoutMilliseconds);
    return delivered && response_size == sizeof(response) && response == 1;
}

void RefreshStartButton()
{
    const HWND taskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
    g_start_button = taskbar == nullptr ? nullptr : FindWindowExW(taskbar, nullptr, L"Start", nullptr);
}

LRESULT CALLBACK MouseHook(int code, WPARAM message, LPARAM data)
{
    if (code == HC_ACTION && message == WM_LBUTTONDOWN && g_start_button != nullptr)
    {
        const auto* mouse = reinterpret_cast<const MSLLHOOKSTRUCT*>(data);
        RECT start_button_rect{};
        if (GetWindowRect(g_start_button, &start_button_rect) && PtInRect(&start_button_rect, mouse->pt) && RequestHostOpen())
        {
            return 1;
        }
    }

    return CallNextHookEx(g_mouse_hook, code, message, data);
}

DWORD WINAPI WorkerThread(LPVOID)
{
    MSG message{};
    PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
    RefreshStartButton();
    g_mouse_hook = SetWindowsHookExW(WH_MOUSE_LL, MouseHook, g_module, 0);
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

    return 0;
}
} // namespace

extern "C" __declspec(dllexport) BOOL TileStartTryOpenMenu()
{
    return RequestHostOpen();
}

extern "C" __declspec(dllexport) DWORD WINAPI TileStartInstallHook(LPVOID)
{
    if (InterlockedCompareExchange(&g_started, 1, 0) != 0)
    {
        return InterlockedCompareExchange(&g_install_succeeded, 0, 0) != 0;
    }

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
