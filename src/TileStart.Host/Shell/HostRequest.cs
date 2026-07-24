using System.Text;

namespace TileStart.Host.Shell;

public enum HostRequestKind
{
    Open,
    Exit,
    AddToAppList,
    PinTile,
}

public readonly record struct HostRequest(HostRequestKind Kind, string Path = "")
{
    private const string AddAppPrefix = "ADD_APP\n";
    private const string PinTilePrefix = "PIN_TILE\n";

    public static HostRequest FromArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Contains("--shutdown", StringComparer.OrdinalIgnoreCase))
        {
            return new HostRequest(HostRequestKind.Exit);
        }

        return TryReadPathArgument(arguments, "--add-app-list", HostRequestKind.AddToAppList)
               ?? TryReadPathArgument(arguments, "--pin-tile", HostRequestKind.PinTile)
               ?? new HostRequest(HostRequestKind.Open);
    }

    public byte[] Encode() => Kind switch
    {
        HostRequestKind.Open => "OPEN"u8.ToArray(),
        HostRequestKind.Exit => "EXIT"u8.ToArray(),
        HostRequestKind.AddToAppList => EncodePath(AddAppPrefix, Path),
        HostRequestKind.PinTile => EncodePath(PinTilePrefix, Path),
        _ => "OPEN"u8.ToArray(),
    };

    public static bool TryDecode(ReadOnlySpan<byte> message, out HostRequest request)
    {
        if (message.SequenceEqual("OPEN"u8))
        {
            request = new HostRequest(HostRequestKind.Open);
            return true;
        }

        if (message.SequenceEqual("EXIT"u8))
        {
            request = new HostRequest(HostRequestKind.Exit);
            return true;
        }

        var text = Encoding.UTF8.GetString(message);
        if (TryDecodePath(text, AddAppPrefix, HostRequestKind.AddToAppList, out request)
            || TryDecodePath(text, PinTilePrefix, HostRequestKind.PinTile, out request))
        {
            return true;
        }

        request = default;
        return false;
    }

    private static HostRequest? TryReadPathArgument(
        IReadOnlyList<string> arguments,
        string option,
        HostRequestKind kind)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                return new HostRequest(kind, arguments[index + 1]);
            }
        }

        return null;
    }

    private static byte[] EncodePath(string prefix, string path) =>
        Encoding.UTF8.GetBytes(prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(path)));

    private static bool TryDecodePath(string message, string prefix, HostRequestKind kind, out HostRequest request)
    {
        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            request = default;
            return false;
        }

        try
        {
            var path = Encoding.UTF8.GetString(Convert.FromBase64String(message[prefix.Length..]));
            request = new HostRequest(kind, path);
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (FormatException)
        {
            request = default;
            return false;
        }
    }
}