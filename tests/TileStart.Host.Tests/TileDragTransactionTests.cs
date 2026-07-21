using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileDragTransactionTests
{
    [Fact]
    public void PreviewSwapsOccupiedSlotsAndDisposeRollsBack()
    {
        var moving = Tile("moving", TileSize.Medium, 4, 0);
        var stationary = Tile("stationary", TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [stationary, moving] };
        var layout = new TileLayout { Groups = [group] };

        using (var transaction = new TileDragTransaction(layout, group, moving))
        {
            Assert.True(transaction.Preview(group, 0, 0));
            Assert.Equal((0, 0), (moving.Column, moving.Row));
            Assert.Equal((4, 0), (stationary.Column, stationary.Row));
        }

        Assert.Equal([stationary, moving], group.Tiles);
        Assert.Equal((0, 0), (stationary.Column, stationary.Row));
        Assert.Equal((4, 0), (moving.Column, moving.Row));
    }

    [Fact]
    public void SlightlyOffsetDropSwapsTheSingleOccupiedSlotWithoutCompactingTheGroup()
    {
        var moba = Tile("MobaXterm", TileSize.Medium, 0, 0);
        var visualStudio = Tile("Visual Studio", TileSize.Medium, 2, 0);
        var dataGrip = Tile("DataGrip", TileSize.Medium, 4, 0);
        var outlook = Tile("Outlook", TileSize.Medium, 6, 0);
        var intelliJ = Tile("IntelliJ", TileSize.Medium, 0, 2);
        var webStorm = Tile("WebStorm", TileSize.Medium, 2, 2);
        var pyCharm = Tile("PyCharm", TileSize.Medium, 4, 2);
        var calculator = Tile("Calculator", TileSize.Medium, 6, 2);
        var group = new TileGroup
        {
            Tiles = [moba, visualStudio, dataGrip, outlook, intelliJ, webStorm, pyCharm, calculator],
        };
        var layout = new TileLayout { Groups = [group] };

        using var transaction = new TileDragTransaction(layout, group, calculator);
        Assert.True(transaction.Preview(group, 5, 2));

        Assert.Equal((4, 2), (calculator.Column, calculator.Row));
        Assert.Equal((6, 2), (pyCharm.Column, pyCharm.Row));
        Assert.Equal((0, 0), (moba.Column, moba.Row));
        Assert.Equal((2, 0), (visualStudio.Column, visualStudio.Row));
        Assert.Equal((4, 0), (dataGrip.Column, dataGrip.Row));
        Assert.Equal((6, 0), (outlook.Column, outlook.Row));
        Assert.Equal((0, 2), (intelliJ.Column, intelliJ.Row));
        Assert.Equal((2, 2), (webStorm.Column, webStorm.Row));
        Assert.Equal(
            [moba, visualStudio, dataGrip, outlook, intelliJ, webStorm, pyCharm, calculator],
            group.Tiles);
    }

    [Fact]
    public void CommitKeepsCrossGroupPreview()
    {
        var moving = Tile("moving", TileSize.Wide, 0, 0);
        var source = new TileGroup { Tiles = [moving] };
        var stationary = Tile("stationary", TileSize.Medium, 0, 0);
        var target = new TileGroup { Tiles = [stationary] };
        var layout = new TileLayout { Groups = [source, target] };

        using var transaction = new TileDragTransaction(layout, source, moving);
        Assert.True(transaction.Preview(target, 0, 0));
        transaction.Commit();

        Assert.DoesNotContain(source, layout.Groups);
        Assert.Equal([moving, stationary], target.Tiles);
        Assert.Equal((0, 0), (moving.Column, moving.Row));
        Assert.Equal((4, 0), (stationary.Column, stationary.Row));
    }

    [Fact]
    public void PreviewNewGroupCanRollbackOrCommit()
    {
        var moving = Tile("moving", TileSize.Medium, 0, 0);
        var remaining = Tile("remaining", TileSize.Medium, 2, 0);
        var source = new TileGroup { Tiles = [moving, remaining] };
        var layout = new TileLayout { Groups = [source] };

        using (var transaction = new TileDragTransaction(layout, source, moving))
        {
            var preview = transaction.PreviewNewGroup();
            Assert.Equal(2, layout.Groups.Count);
            Assert.Same(moving, Assert.Single(preview.Tiles));
        }

        Assert.Same(source, Assert.Single(layout.Groups));
        Assert.Equal([moving, remaining], source.Tiles);

        using var committed = new TileDragTransaction(layout, source, moving);
        var created = committed.PreviewNewGroup();
        committed.Commit();

        Assert.Equal(2, layout.Groups.Count);
        Assert.Same(remaining, Assert.Single(source.Tiles));
        Assert.Same(moving, Assert.Single(created.Tiles));
    }

    [Fact]
    public void PreviewNewGroupShiftsOnlyItsOuterColumnAndPreservesTheTileCell()
    {
        var moving = Tile("moving", TileSize.Medium, 0, 0);
        var source = new TileGroup
        {
            GroupColumn = 0,
            GroupRow = 0,
            Tiles = [moving, Tile("remaining", TileSize.Medium, 2, 0)],
        };
        var following = new TileGroup
        {
            GroupColumn = 0,
            GroupRow = 1,
            Tiles = [Tile("following", TileSize.Medium, 0, 0)],
        };
        var layout = new TileLayout { Groups = [source, following] };

        using var transaction = new TileDragTransaction(layout, source, moving, groupColumns: 3);
        var created = transaction.PreviewNewGroup(new TileNewGroupDropTarget(0, 1, 6, 0));

        Assert.Equal(new TileGroupCell(0, 1), Win10GroupGridLayout.GetCell(created));
        Assert.Equal(new TileGroupCell(0, 2), Win10GroupGridLayout.GetCell(following));
        Assert.Equal((6, 0), (moving.Column, moving.Row));
    }

    [Fact]
    public void EmptyThirdOuterColumnCanReceiveAndArrangeANewGroup()
    {
        var moving = Tile("moving", TileSize.Medium, 0, 0);
        var source = new TileGroup
        {
            GroupColumn = 0,
            GroupRow = 0,
            Tiles = [moving, Tile("remaining", TileSize.Medium, 2, 0)],
        };
        var second = new TileGroup
        {
            GroupColumn = 1,
            GroupRow = 0,
            Tiles = [Tile("second", TileSize.Medium, 0, 0)],
        };
        var zones = new[]
        {
            new TileGroupDropZone(source.Id, 0, 0, 412, 204, GroupColumn: 0, GroupRow: 0),
            new TileGroupDropZone(
                second.Id,
                Win10TileMetrics.GroupPitch,
                0,
                412,
                204,
                GroupColumn: 1,
                GroupRow: 0),
        };
        var geometry = new TileDragHitGeometry(zones);
        var layout = new TileLayout { Groups = [source, second] };
        var target = geometry.FindNewGroupTarget(
            draggedLeft: Win10TileMetrics.GroupPitch * 2,
            draggedTop: 0,
            draggedHeight: Win10TileMetrics.Height(TileSize.Medium),
            columnSpan: TileSize.Medium.ColumnSpan(),
            groupColumns: 3);

        using var transaction = new TileDragTransaction(layout, source, moving, groupColumns: 3);
        var created = transaction.PreviewNewGroup(target);
        var slots = Win10GroupWrapPanel.CalculateSlots(
            layout.Groups
                .Select((group, index) => new Win10GroupPanelItem(index, group.GroupColumn, group.GroupRow, 232))
                .ToArray(),
            columns: 3);

        Assert.Equal(new TileNewGroupDropTarget(2, 0, 0, 0), target);
        Assert.Equal(new TileGroupCell(2, 0), Win10GroupGridLayout.GetCell(created));
        Assert.Equal(2, slots.Single(slot => slot.Index == layout.Groups.IndexOf(created)).Column);
    }

    [Fact]
    public void ProvisionalNewGroupCanMoveItsPlaceholderWithoutBeingRecreated()
    {
        var moving = Tile("moving", TileSize.Medium, 0, 0);
        var source = new TileGroup { Tiles = [moving] };
        var layout = new TileLayout { Groups = [source] };

        using var transaction = new TileDragTransaction(layout, source, moving, groupColumns: 3);
        var created = transaction.PreviewNewGroup(new TileNewGroupDropTarget(0, 1, 2, 0));

        Assert.True(transaction.Preview(created, 6, 0));
        Assert.Same(created, transaction.PreviewTarget);
        Assert.Equal((6, 0), (moving.Column, moving.Row));
        Assert.Equal(2, layout.Groups.Count);
    }

    [Fact]
    public void ResolvingTheProvisionalNewGroupOnMouseUpDoesNotDetachTheTile()
    {
        var moving = Tile("moving", TileSize.Medium, 0, 0);
        var source = new TileGroup { Tiles = [moving] };
        var layout = new TileLayout { Groups = [source] };

        using var transaction = new TileDragTransaction(layout, source, moving);
        var provisional = transaction.PreviewNewGroup();

        Assert.True(transaction.Preview(provisional, 0, 0));
        transaction.Commit();

        Assert.Contains(provisional, layout.Groups);
        Assert.Same(moving, Assert.Single(provisional.Tiles));
        Assert.DoesNotContain(source, layout.Groups);
    }

    [Fact]
    public void PreviewFolderCreatesDedicatedFolderAndRollbackRestoresTiles()
    {
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var target = Tile("target", TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [target, moving] };
        var layout = new TileLayout { Groups = [group] };

        using (var transaction = new TileDragTransaction(layout, group, moving))
        {
            Assert.True(transaction.PreviewFolder(group, target));
            Assert.Equal(TileDropIntent.CreateFolder, transaction.Intent);
            var folder = Assert.Single(group.Tiles);
            Assert.True(folder.IsTileFolder);
            Assert.Equal([target, moving], folder.FolderTiles);
        }

        Assert.Equal([target, moving], group.Tiles);
        Assert.False(target.IsTileFolder);
        Assert.Empty(target.FolderTiles);
    }

    [Fact]
    public void RepeatedDragOverOnCreatedFolderKeepsFolderPreviewStable()
    {
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var target = Tile("target", TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [target, moving] };
        var layout = new TileLayout { Groups = [group] };

        using var transaction = new TileDragTransaction(layout, group, moving);
        Assert.True(transaction.PreviewFolder(group, target));
        var previewFolder = Assert.Single(group.Tiles);

        Assert.True(transaction.PreviewFolder(group, previewFolder));
        Assert.Same(previewFolder, Assert.Single(group.Tiles));
        Assert.Equal([target, moving], previewFolder.FolderTiles);
        Assert.Equal(TileDropIntent.CreateFolder, transaction.Intent);
    }

    [Fact]
    public void RepositionAfterFolderPreviewRestoresOriginalTilesFirst()
    {
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var target = Tile("target", TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [target, moving] };
        var layout = new TileLayout { Groups = [group] };

        using var transaction = new TileDragTransaction(layout, group, moving);
        Assert.True(transaction.PreviewFolder(group, target));
        Assert.True(transaction.Preview(group, 0, 0));

        Assert.Equal(TileDropIntent.Reposition, transaction.Intent);
        Assert.Contains(target, group.Tiles);
        Assert.Contains(moving, group.Tiles);
        Assert.DoesNotContain(group.Tiles, tile => tile.IsTileFolder);
    }

    [Fact]
    public void PreviewFolderAddsTileToExistingFolderAndCommitPersistsIt()
    {
        var child = Tile("child", TileSize.Small, 0, 0);
        var folder = Tile("folder", TileSize.Medium, 0, 0);
        folder.IsTileFolder = true;
        folder.FolderTiles.Add(child);
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var source = new TileGroup { Tiles = [moving] };
        var target = new TileGroup { Tiles = [folder] };
        var layout = new TileLayout { Groups = [source, target] };

        using var transaction = new TileDragTransaction(layout, source, moving);
        Assert.True(transaction.PreviewFolder(target, folder));
        Assert.Equal(TileDropIntent.AddToFolder, transaction.Intent);
        transaction.Commit();

        Assert.DoesNotContain(source, layout.Groups);
        Assert.Equal([child, moving], folder.FolderTiles);
    }

    [Fact]
    public void RepeatedPreviewsAlwaysStartFromOriginalSnapshot()
    {
        var moving = Tile("moving", TileSize.Medium, 4, 0);
        var stationary = Tile("stationary", TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [stationary, moving] };
        var layout = new TileLayout { Groups = [group] };

        using var transaction = new TileDragTransaction(layout, group, moving);
        Assert.True(transaction.Preview(group, 0, 0));
        Assert.True(transaction.Preview(group, 6, 0));

        Assert.Equal((0, 0), (stationary.Column, stationary.Row));
        Assert.Equal((6, 0), (moving.Column, moving.Row));
    }

    [Fact]
    public void SwitchingPreviewTargetsDoesNotRebuildUnrelatedGroups()
    {
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var source = new TileGroup
        {
            GroupColumn = 0,
            GroupRow = 0,
            Tiles = [moving],
        };
        var target = new TileGroup
        {
            GroupColumn = 1,
            GroupRow = 0,
            Tiles = [Tile("target", TileSize.Medium, 0, 0)],
        };
        var unrelated = new TileGroup
        {
            GroupColumn = 2,
            GroupRow = 0,
            Tiles = [Tile("unrelated", TileSize.Medium, 0, 0)],
        };
        var layout = new TileLayout { Groups = [source, target, unrelated] };
        var unrelatedChanges = 0;
        unrelated.Tiles.CollectionChanged += (_, _) => unrelatedChanges++;

        using var transaction = new TileDragTransaction(layout, source, moving, groupColumns: 3);
        Assert.True(transaction.Preview(target, 2, 0));
        Assert.True(transaction.Preview(source, 4, 0));

        Assert.Equal(0, unrelatedChanges);
    }

    [Fact]
    public void FolderChildCanReorderWithinExpandedFolder()
    {
        var first = Tile("first", TileSize.Medium, 0, 0);
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var folder = Tile("folder", TileSize.Medium, 0, 0);
        folder.IsTileFolder = true;
        folder.IsFolderExpanded = true;
        folder.FolderTiles = [first, moving];
        var group = new TileGroup { Tiles = [folder] };
        var layout = new TileLayout { Groups = [group] };

        using var transaction = new TileDragTransaction(layout, group, folder, moving);
        Assert.True(transaction.PreviewInsideFolder(group, folder, 0, 0));
        transaction.Commit();

        Assert.Equal((0, 0), (moving.Column, moving.Row));
        Assert.Equal((2, 0), (first.Column, first.Row));
        Assert.Contains(folder, group.Tiles);
    }

    [Fact]
    public void FolderChildCanMoveBackToParentGroupWithoutChangingRemainingFolder()
    {
        var remaining = Tile("remaining", TileSize.Small, 0, 0);
        var moving = Tile("moving", TileSize.Medium, 1, 0);
        var folder = Tile("folder", TileSize.Medium, 0, 0);
        folder.IsTileFolder = true;
        folder.FolderTiles = [remaining, moving];
        var group = new TileGroup { Tiles = [folder] };
        var layout = new TileLayout { Groups = [group] };

        using var transaction = new TileDragTransaction(layout, group, folder, moving);
        Assert.True(transaction.Preview(group, 2, 0));
        transaction.Commit();

        Assert.Equal([remaining], folder.FolderTiles);
        Assert.Contains(folder, group.Tiles);
        Assert.Contains(moving, group.Tiles);
    }

    [Fact]
    public void MovingLastFolderChildDeletesEmptyFolderOnCommit()
    {
        var moving = Tile("moving", TileSize.Medium, 0, 0);
        var folder = Tile("folder", TileSize.Medium, 0, 0);
        folder.IsTileFolder = true;
        folder.FolderTiles = [moving];
        var source = new TileGroup { Tiles = [folder] };
        var target = new TileGroup();
        var layout = new TileLayout { Groups = [source, target] };

        using var transaction = new TileDragTransaction(layout, source, folder, moving);
        Assert.True(transaction.Preview(target, 0, 0));
        transaction.Commit();

        Assert.DoesNotContain(source, layout.Groups);
        Assert.Same(moving, Assert.Single(target.Tiles));
    }

    [Fact]
    public void FolderChildMoveRollbackRestoresNestedPositionsAndExpansion()
    {
        var moving = Tile("moving", TileSize.Medium, 2, 0);
        var folder = Tile("folder", TileSize.Medium, 0, 0);
        folder.IsTileFolder = true;
        folder.IsFolderExpanded = true;
        folder.FolderTiles = [moving];
        var group = new TileGroup { Tiles = [folder] };
        var layout = new TileLayout { Groups = [group] };

        using (var transaction = new TileDragTransaction(layout, group, folder, moving))
        {
            Assert.True(transaction.Preview(group, 4, 0));
        }

        Assert.True(folder.IsFolderExpanded);
        Assert.Same(moving, Assert.Single(folder.FolderTiles));
        Assert.Equal((2, 0), (moving.Column, moving.Row));
        Assert.Same(folder, Assert.Single(group.Tiles));
    }

    private static TileItem Tile(string name, TileSize size, int column, int row) => new()
    {
        Name = name,
        Size = size,
        Column = column,
        Row = row,
    };
}