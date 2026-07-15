using System.Collections.Specialized;

namespace TileStart.Host.Tests;

public sealed class RangeObservableCollectionTests
{
    [Fact]
    public void AddRange_AddsItemsWithSingleResetNotification()
    {
        var collection = new RangeObservableCollection<int>();
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, args) => actions.Add(args.Action);

        collection.AddRange([1, 2, 3]);

        Assert.Equal([1, 2, 3], collection);
        Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
    }
}
