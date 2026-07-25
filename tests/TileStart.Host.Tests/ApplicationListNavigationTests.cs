using System.Collections;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class ApplicationListNavigationTests
{
    [Fact]
    public void AlphabetJumpAlignsRealizedGroupHeaderToViewportTop()
    {
        RunOnSta(() =>
        {
            var items = new ArrayList();
            foreach (var letter in new[] { "A", "B", "C", "D", "E", "F" })
            {
                for (var index = 0; index < 8; index++)
                {
                    items.Add(AppEntry.Application($"{letter} app {index}", $"{letter}{index}", DateTime.MinValue));
                }
            }

            var view = new ListCollectionView(items);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppEntry.SortLetter)));
            view.SortDescriptions.Add(new SortDescription(nameof(AppEntry.SortLetter), ListSortDirection.Ascending));

            var list = new ListBox
            {
                Width = 320,
                Height = 180,
                ItemsSource = view,
                ItemTemplate = FixedHeightTemplate(24),
            };
            ScrollViewer.SetCanContentScroll(list, true);
            VirtualizingPanel.SetIsVirtualizing(list, true);
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(list, true);
            VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
            list.GroupStyle.Add(new GroupStyle { HeaderTemplate = FixedHeightTemplate(24) });

            var window = new Window
            {
                Width = 320,
                Height = 180,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = list,
            };

            try
            {
                window.Show();
                list.UpdateLayout();

                var group = view.Groups!.OfType<CollectionViewGroup>().Single(candidate => Equals(candidate.Name, "D"));
                list.ScrollIntoView(group.Items[0]);
                list.UpdateLayout();

                Assert.True(NavigationController.AlignRealizedGroupToTop(list, group));

                var container = Assert.IsAssignableFrom<FrameworkElement>(
                    list.ItemContainerGenerator.ContainerFromItem(group));
                var viewer = Assert.IsType<ScrollViewer>(FindVisualDescendant<ScrollViewer>(list));
                Assert.InRange(Math.Abs(container.TranslatePoint(new Point(), viewer).Y), 0, 0.5);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static DataTemplate FixedHeightTemplate(double height)
    {
        var template = new DataTemplate();
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetValue(FrameworkElement.HeightProperty, height);
        template.VisualTree = root;
        return template;
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(error);
    }
}
