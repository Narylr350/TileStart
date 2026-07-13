using System.Runtime.InteropServices;

namespace TileStart.Host;

public sealed class WinKeyHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int HcAction = 0;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint VkLwin = 0x5B;
    private const uint VkRwin = 0x5C;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint LlkhfExtended = 0x00000001;
    private const uint LlkhfInjected = 0x00000010;
    private readonly Action _onStandaloneWinKey;
    private readonly HookProcedure _callback;
    private nint _hook;
    private bool _winKeyDown;
    private bool _winKeyChord;
    private uint _winVirtualKey;

    public WinKeyHook(Action onStandaloneWinKey)
    {
        _onStandaloneWinKey = onStandaloneWinKey;
        _callback = Callback;
    }

    public void Start()
    {
        _hook = SetWindowsHookExW(WhKeyboardLl, _callback, GetModuleHandleW(null), 0);
    }

    public void Dispose()
    {
        if (_winKeyChord)
        {
            InjectKey((ushort)_winVirtualKey, 0, KeyEventExtendedKey | KeyEventKeyUp);
        }

        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }

    private nint Callback(int code, nint message, nint data)
    {
        if (code != HcAction)
        {
            return CallNextHookEx(_hook, code, message, data);
        }

        var key = Marshal.PtrToStructure<KeyboardData>(data);
        if ((key.Flags & LlkhfInjected) != 0)
        {
            return CallNextHookEx(_hook, code, message, data);
        }

        var keyDown = message is WmKeyDown or WmSysKeyDown;
        var keyUp = message is WmKeyUp or WmSysKeyUp;
        var winKey = key.VirtualKey is VkLwin or VkRwin;

        if (keyDown && winKey)
        {
            if (!_winKeyDown)
            {
                _winKeyDown = true;
                _winKeyChord = false;
                _winVirtualKey = key.VirtualKey;
            }

            return 1;
        }

        if (keyDown && _winKeyDown && !_winKeyChord)
        {
            _winKeyChord = true;
            InjectKey((ushort)_winVirtualKey, 0, KeyEventExtendedKey);
            InjectKey((ushort)key.VirtualKey, (ushort)key.ScanCode,
                (key.Flags & LlkhfExtended) != 0 ? KeyEventExtendedKey : 0);
            return 1;
        }

        if (keyUp && winKey && _winKeyDown && key.VirtualKey == _winVirtualKey)
        {
            var chord = _winKeyChord;
            var winVirtualKey = _winVirtualKey;
            _winKeyDown = false;
            _winKeyChord = false;
            _winVirtualKey = 0;

            if (chord)
            {
                InjectKey((ushort)winVirtualKey, 0, KeyEventExtendedKey | KeyEventKeyUp);
            }
            else
            {
                _onStandaloneWinKey();
            }

            return 1;
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private static void InjectKey(ushort virtualKey, ushort scanCode, uint flags)
    {
        keybd_event((byte)virtualKey, (byte)scanCode, flags, 0);
    }

    private delegate nint HookProcedure(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetWindowsHookExW(int hookType, HookProcedure procedure, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);
}
