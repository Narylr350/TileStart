using System.Runtime.InteropServices;

namespace TileStart.Host.Shell;

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

    public bool Start()
    {
        if (_hook == 0)
        {
            _hook = SetWindowsHookExW(WhKeyboardLl, _callback, GetModuleHandleW(null), 0);
        }

        return _hook != 0;
    }

    public static void OpenNativeStartMenu()
    {
        InjectKey((ushort)VkLwin, 0, KeyEventExtendedKey);
        InjectKey((ushort)VkLwin, 0, KeyEventExtendedKey | KeyEventKeyUp);
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

        _winKeyDown = false;
        _winKeyChord = false;
        _winVirtualKey = 0;
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

        var action = ProcessKey(
            key.VirtualKey,
            message is WmKeyDown or WmSysKeyDown,
            message is WmKeyUp or WmSysKeyUp);
        if (action.HasFlag(WinKeyAction.InjectWinDown))
        {
            InjectKey((ushort)_winVirtualKey, 0, KeyEventExtendedKey);
        }

        if (action.HasFlag(WinKeyAction.InjectCurrentKeyDown))
        {
            InjectKey((ushort)key.VirtualKey, (ushort)key.ScanCode,
                (key.Flags & LlkhfExtended) != 0 ? KeyEventExtendedKey : 0);
        }

        if (action.HasFlag(WinKeyAction.InjectWinUp))
        {
            InjectKey((ushort)key.VirtualKey, 0, KeyEventExtendedKey | KeyEventKeyUp);
        }

        if (action.HasFlag(WinKeyAction.OpenTileStart))
        {
            _onStandaloneWinKey();
        }

        return action.HasFlag(WinKeyAction.Suppress)
            ? 1
            : CallNextHookEx(_hook, code, message, data);
    }

    internal WinKeyAction ProcessKey(uint virtualKey, bool keyDown, bool keyUp)
    {
        var winKey = virtualKey is VkLwin or VkRwin;
        if (keyDown && winKey)
        {
            if (!_winKeyDown)
            {
                _winKeyDown = true;
                _winKeyChord = false;
                _winVirtualKey = virtualKey;
            }

            return WinKeyAction.Suppress;
        }

        if (keyDown && _winKeyDown && !_winKeyChord)
        {
            _winKeyChord = true;
            return WinKeyAction.Suppress | WinKeyAction.InjectWinDown | WinKeyAction.InjectCurrentKeyDown;
        }

        if (keyUp && winKey && _winKeyDown && virtualKey == _winVirtualKey)
        {
            var chord = _winKeyChord;
            _winKeyDown = false;
            _winKeyChord = false;
            _winVirtualKey = 0;
            return chord
                ? WinKeyAction.Suppress | WinKeyAction.InjectWinUp
                : WinKeyAction.Suppress | WinKeyAction.OpenTileStart;
        }

        return WinKeyAction.None;
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