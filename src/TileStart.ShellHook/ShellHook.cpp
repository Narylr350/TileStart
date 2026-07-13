#include <windows.h>

namespace
{
constexpr wchar_t kPipeName[] = L"\\\\.\\pipe\\TileStart.Host";
constexpr char kOpenCommand[] = "OPEN";
constexpr DWORD kPipeTimeoutMilliseconds = 75;
}

extern "C" __declspec(dllexport) BOOL TileStartTryOpenMenu()
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

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID)
{
    return TRUE;
}
