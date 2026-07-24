using System.Runtime.InteropServices;

namespace TileStart.Host.Applications;

internal static class Win10AppGrouping
{
    private const string RuntimeClassName = "Windows.Globalization.Collation.CharacterGroupings";
    private static readonly object Sync = new();
    private static readonly Lazy<ICharacterGroupings?> CharacterGroupings = new(CreateCharacterGroupings);

    public static string GetGroupKey(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "#";
        }

        try
        {
            var groupings = CharacterGroupings.Value;
            if (groupings is not null)
            {
                string label;
                lock (Sync)
                {
                    label = Lookup(groupings, trimmed);
                }

                var normalizedLabel = label.Trim();
                if (normalizedLabel.Length == 1 && IsLatinLetter(normalizedLabel[0]))
                {
                    return char.ToUpperInvariant(normalizedLabel[0]).ToString();
                }

                if (normalizedLabel.StartsWith("拼音", StringComparison.OrdinalIgnoreCase)
                    || normalizedLabel.StartsWith("Pinyin", StringComparison.OrdinalIgnoreCase))
                {
                    var pinyinKey = normalizedLabel.LastOrDefault(IsLatinLetter);
                    if (pinyinKey != default)
                    {
                        return char.ToUpperInvariant(pinyinKey).ToString();
                    }
                }
            }
        }
        catch (COMException)
        {
        }

        var first = trimmed[0];
        return first is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
            ? char.ToUpperInvariant(first).ToString()
            : "#";
    }

    private static bool IsLatinLetter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static ICharacterGroupings? CreateCharacterGroupings()
    {
        _ = RoInitialize(1);
        WindowsCreateString(RuntimeClassName, RuntimeClassName.Length, out var classId);
        try
        {
            var result = RoActivateInstance(classId, out var instance);
            if (result < 0)
            {
                return null;
            }

            try
            {
                return (ICharacterGroupings)Marshal.GetObjectForIUnknown(instance);
            }
            finally
            {
                Marshal.Release(instance);
            }
        }
        finally
        {
            WindowsDeleteString(classId);
        }
    }

    private static string Lookup(ICharacterGroupings groupings, string value)
    {
        WindowsCreateString(value, value.Length, out var input);
        nint output = 0;
        try
        {
            groupings.Lookup(input, out output);
            var characters = WindowsGetStringRawBuffer(output, out var length);
            return Marshal.PtrToStringUni(characters, checked((int)length)) ?? string.Empty;
        }
        finally
        {
            WindowsDeleteString(input);
            if (output != 0)
            {
                WindowsDeleteString(output);
            }
        }
    }

    [ComImport]
    [Guid("B8D20A75-D4CF-4055-80E5-CE169C226496")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICharacterGroupings
    {
        void GetIids(out uint iidCount, out nint iids);
        void GetRuntimeClassName(out nint className);
        void GetTrustLevel(out int trustLevel);
        void Lookup(nint text, out nint result);
    }

    [DllImport("combase.dll")]
    private static extern int RoInitialize(int initType);

    [DllImport("combase.dll")]
    private static extern int RoActivateInstance(nint classId, out nint instance);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out nint value);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll")]
    private static extern nint WindowsGetStringRawBuffer(nint value, out uint length);
}