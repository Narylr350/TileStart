#include <windows.h>

#include <filesystem>
#include <format>
#include <iostream>
#include <string>
#include <string_view>

namespace
{
using OpenMenuFunction = BOOL(WINAPI*)();

void PrintUsage()
{
    std::wcout << L"Usage:\n"
                  L"  TileStart.Injector.exe --probe <ShellHook.dll>\n"
                  L"  TileStart.Injector.exe --inject <ShellHook.dll> <explorer-pid>\n";
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
    const SIZE_T byte_count = (absolute_path.size() + 1) * sizeof(wchar_t);
    const HANDLE process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE,
                                       FALSE,
                                       process_id);
    if (process == nullptr)
    {
        std::wcerr << std::format(L"Unable to open process {}: error={}\n", process_id, GetLastError());
        return 1;
    }

    const LPVOID remote_path = VirtualAllocEx(process, nullptr, byte_count, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remote_path == nullptr)
    {
        std::wcerr << std::format(L"Unable to allocate remote memory: error={}\n", GetLastError());
        CloseHandle(process);
        return 1;
    }

    SIZE_T written = 0;
    const BOOL copied = WriteProcessMemory(process, remote_path, absolute_path.c_str(), byte_count, &written);
    const HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    const auto load_library = reinterpret_cast<LPTHREAD_START_ROUTINE>(GetProcAddress(kernel32, "LoadLibraryW"));
    const HANDLE thread = copied && written == byte_count
                              ? CreateRemoteThread(process, nullptr, 0, load_library, remote_path, 0, nullptr)
                              : nullptr;
    if (thread == nullptr)
    {
        std::wcerr << std::format(L"Unable to create remote LoadLibraryW thread: error={}\n", GetLastError());
        VirtualFreeEx(process, remote_path, 0, MEM_RELEASE);
        CloseHandle(process);
        return 1;
    }

    const DWORD wait = WaitForSingleObject(thread, 3000);
    DWORD result = 0;
    GetExitCodeThread(thread, &result);
    CloseHandle(thread);
    VirtualFreeEx(process, remote_path, 0, MEM_RELEASE);
    CloseHandle(process);
    if (wait != WAIT_OBJECT_0 || result == 0)
    {
        std::wcerr << std::format(L"Remote LoadLibraryW failed: wait={}, result=0x{:X}.\n", wait, result);
        return 1;
    }

    std::wcout << std::format(L"Injected '{}' into PID {}.\n", absolute_path, process_id);
    return 0;
}
} // namespace

int wmain(int argc, wchar_t* argv[])
{
    if (argc == 3 && std::wstring_view(argv[1]) == L"--probe")
    {
        return Probe(argv[2]);
    }

    if (argc == 4 && std::wstring_view(argv[1]) == L"--inject")
    {
        try
        {
            return Inject(argv[2], std::stoul(argv[3]));
        }
        catch (...)
        {
            PrintUsage();
            return 2;
        }
    }

    PrintUsage();
    return 2;
}
