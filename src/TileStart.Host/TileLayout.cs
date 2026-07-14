using System.Collections.ObjectModel;

namespace TileStart.Host;

public sealed class TileLayout
{
    public ObservableCollection<TileGroup> Groups { get; set; } = [];
}
