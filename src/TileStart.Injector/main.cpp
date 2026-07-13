#include <windows.h>
#include <tlhelp32.h>

#include <filesystem>
#include <format>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>

namespace
{
using OpenMenuFunction = BOOL(WINAPI*)();

void PrintUsage()
{
    std::wcout << L"Usage:\n"
                  L"  TileStart.Injector.exe --probe <ShellHook.dll>\n"
                  L"  TileStart.Injector.exe --inject <ShellHook.dll> <explorer-pid>\n"
                  L"  TileStart.Injector.exe --stop <ShellHook.dll> <explorer-pid>\n";
}

std::optional<std::uintptr_t> FindRemoteModuleBase(DWORD process_id, const std::wstring& dll_path)
{
    const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, process_id);
    if (snapshot == INVALID_HANDLE_VALUE)
    {
        return std::nullopt;
    }

    MODULEENTRY32W module{.dwSize = sizeof(module)};
    if (!Module32FirstW(snapshot, &module))
    {
        CloseHandle(snapshot);
        return std::nullopt;
    }

    do
    {
        if (_wcsicmp(module.szExePath, dll_path.c_str()) == 0)
        {
            CloseHandle(snapshot);
            return reinterpret_cast<std::uintptr_t>(module.modBaseAddr);
        }
    } while (Module32NextW(snapshot, &module));

    CloseHandle(snapshot);
    return std::nullopt;
}

std::optional<std::uintptr_t> FindRemoteExport(DWORD process_id, const std::wstring& dll_path, const char* export_name)
{
    const HMODULE local_module = LoadLibraryW(dll_path.c_str());
    if (local_module == nullptr)
    {
        return std::nullopt;
    }

    const auto local_export = reinterpret_cast<std::uintptr_t>(GetProcAddress(local_module, export_name));
    const auto local_base = reinterpret_cast<std::uintptr_t>(local_module);
    const auto remote_base = FindRemoteModuleBase(process_id, dll_path);
    FreeLibrary(local_module);
    if (local_export == 0 || !remote_base)
    {
        return std::nullopt;
    }

    return *remote_base + (local_export - local_base);
}

std::optional<DWORD> CallRemoteExport(HANDLE process, std::uintptr_t address, LPVOID parameter = nullptr)
{
    const HANDLE thread = CreateRemoteThread(process,
                                             nullptr,
                                             0,
                                             reinterpret_cast<LPTHREAD_START_ROUTINE>(address),
                                             parameter,
                                             0,
                                             nullptr);
    if (thread == nullptr)
    {
        return std::nullopt;
    }

    const DWORD wait = WaitForSingleObject(thread, 3000);
    DWORD result = 0;
    GetExitCodeThread(thread, &result);
    CloseHandle(thread);
    return wait == WAIT_OBJECT_0 ? std::optional{result} : std::nullopt;
}

bool LoadRemoteLibrary(HANDLE process, const std::wstring& dll_path)
{
    const SIZE_T byte_count = (dll_path.size() + 1) * sizeof(wchar_t);
    const LPVOID remote_path = VirtualAllocEx(process, nullptr, byte_count, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remote_path == nullptr)
    {
        return false;
    }

    SIZE_T written = 0;
    const BOOL copied = WriteProcessMemory(process, remote_path, dll_path.c_str(), byte_count, &written);
    const HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    const auto load_library = reinterpret_cast<LPTHREAD_START_ROUTINE>(GetProcAddress(kernel32, "LoadLibraryW"));
    const auto result = copied && written == byte_count ? CallRemoteExport(process, reinterpret_cast<std::uintptr_t>(load_library), remote_path) : std::nullopt;
    VirtualFreeEx(process, remote_path, 0, MEM_RELEASE);
    return result && *result != 0;
}

int Probe(const std::filesystem::path& dll_path)
{
    const HMODULE module = LoadLibraryW(dll_path.c_str());
    if (module == nullptr)
    {
        std::wcerr << std::format(L"Unable to load '{}': error={}\n", dll_path.wstring(), GetLastError());
        return 1;
    }

    const auto open_menu = reinterpret_cast<OpenMenuFunction>(GetProcAddress(module, "TileStartTryOpenMenu"));
    if (open_menu == nullptr)
    {
        std::wcerr << L"TileStartTryOpenMenu export is missing.\n";
        FreeLibrary(module);
        return 1;
    }

    SetLastError(ERROR_SUCCESS);
    const BOOL acknowledged = open_menu();
    const DWORD error = GetLastError();
    FreeLibrary(module);
    std::wcout << (acknowledged
                       ? L"Host acknowledged the open request.\n"
                       : std::format(L"Host unavailable or timed out (error={}); native Start must remain allowed.\n", error));
    return acknowledged ? 0 : 3;
}

int Inject(const std::filesystem::path& dll_path, DWORD process_id)
{
    const std::wstring absolute_path = std::filesystem::absolute(dll_path).wstring();
    const HANDLE process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE,
                                       FALSE,
                                       process_id);
    if (process == nullptr)
    {
        std::wcerr << std::format(L"Unable to open process {}: error={}\n", process_id, GetLastError());
        return 1;
    }

    if (!LoadRemoteLibrary(process, absolute_path))
    {
        std::wcerr << std::format(L"Remote LoadLibraryW failed: error={}\n", GetLastError());
        CloseHandle(process);
        return 1;
    }

    const auto install_export = FindRemoteExport(process_id, absolute_path, "TileStartInstallHook");
    const auto result = install_export ? CallRemoteExport(process, *install_export) : std::nullopt;
    CloseHandle(process);
    if (!result || *result == 0)
    {
        std::wcerr << L"ShellHook loaded but could not install its Explorer hooks.\n";
        return 1;
    }

    std::wcout << std::format(L"Injected and started '{}' in PID {}.\n", absolute_path, process_id);
    return 0;
}

int Stop(const std::filesystem::path& dll_path, DWORD process_id)
{
    const std::wstring absolute_path = std::filesystem::absolute(dll_path).wstring();
    const HANDLE process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_READ,
                                       FALSE,
                                       process_id);
    if (process == nullptr)
    {
        std::wcerr << std::format(L"Unable to open process {}: error={}\n", process_id, GetLastError());
        return 1;
    }

    const auto stop_export = FindRemoteExport(process_id, absolute_path, "TileStartStopHook");
    const auto result = stop_export ? CallRemoteExport(process, *stop_export) : std::nullopt;
    CloseHandle(process);
    if (!result || *result == 0)
    {
        std::wcerr << L"ShellHook was not running or could not be stopped.\n";
        return 1;
    }

    std::wcout << std::format(L"Stopped ShellHook in PID {}.\n", process_id);
    return 0;
}

std::optional<DWORD> ParseProcessId(const wchar_t* value)
{
    try
    {
        return std::stoul(value);
    }
    catch (...)
    {
        return std::nullopt;
    }
}
} // namespace

int wmain(int argc, wchar_t* argv[])
{
    if (argc == 3 && std::wstring_view(argv[1]) == L"--probe")
    {
        return Probe(argv[2]);
    }

    if (argc == 4)
    {
        const auto process_id = ParseProcessId(argv[3]);
        if (!process_id)
        {
            PrintUsage();
            return 2;
        }

        if (std::wstring_view(argv[1]) == L"--inject")
        {
            return Inject(argv[2], *process_id);
        }

        if (std::wstring_view(argv[1]) == L"--stop")
        {
            return Stop(argv[2], *process_id);
        }
    }

    PrintUsage();
    return 2;
}
