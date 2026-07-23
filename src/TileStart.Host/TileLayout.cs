using System.Collections.ObjectModel;

namespace TileStart.Host;

public sealed class TileLayout
{
    public int Version { get; set; }

    public ObservableCollection<TileGroup> Groups { get; set; } = [];
}
