using System.Windows.Media;

namespace TileStart.Host;

public sealed class AppEntry
{
    public required string Name { get; init; }
    public required string LaunchTarget { get; init; }
    public required string SortLetter { get; init; }
    public required string Initial { get; init; }
    public required DateTime AddedAt { get; init; }
    public ImageSource? Icon { get; init; }
}
