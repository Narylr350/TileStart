using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TileStart.Host.Applications;
using TileStart.Host.Navigation;

namespace TileStart.Host;

public partial class MainWindow
{
    private void NavigationToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationHoverTimer.Stop();
        _navigationPinnedOpen = !_navigationPinnedOpen;
        SetNavigationExpanded(_navigationPinnedOpen);
    }

    private void NavigationPane_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_navigationPinnedOpen && !_navigationExpanded)
        {
            _navigationHoverTimer.Stop();
            _navigationHoverTimer.Start();
        }
    }

    private void NavigationHoverTimer_Tick(object? sender, EventArgs e)
    {
        _navigationHoverTimer.Stop();
        if (!_navigationPinnedOpen && NavigationPane.IsMouseOver)
        {
            SetNavigationExpanded(true);
        }
    }

    private void NavigationPane_MouseLeave(object sender, MouseEventArgs e)
    {
        _navigationHoverTimer.Stop();
        if (!_navigationPinnedOpen && _openContextMenuCount == 0)
        {
            SetNavigationExpanded(false);
        }
    }

    private void SetNavigationExpanded(bool expanded)
    {
        if (_navigationExpanded == expanded)
        {
            return;
        }

        _navigationExpanded = expanded;
        var targetWidth = expanded
            ? Win10VisualMetrics.ExpandedNavigationWidth
            : Win10VisualMetrics.CollapsedNavigationWidth;
        NavigationToggleButton.ToolTip = expanded ? "收起" : "展开";
        if (expanded)
        {
            NavigationPane.Background = (System.Windows.Media.Brush)FindResource("ExpandedNavigationBackground");
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            NavigationPane.Width = targetWidth;
            if (!expanded)
            {
                NavigationPane.Background = System.Windows.Media.Brushes.Transparent;
            }

            return;
        }

        var animation = new DoubleAnimation
        {
            From = NavigationPane.ActualWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            },
        };
        animation.Completed += (_, _) =>
        {
            NavigationPane.BeginAnimation(WidthProperty, null);
            NavigationPane.Width = targetWidth;
            if (!expanded && !_navigationExpanded)
            {
                NavigationPane.Background = System.Windows.Media.Brushes.Transparent;
            }
        };
        NavigationPane.BeginAnimation(WidthProperty, animation);
    }

    private void NavigationPreferencesMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is string key)
            {
                item.IsChecked = _navigationPreferences.IsVisible(key);
            }
        }
    }

    private void NavigationPreference_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string key } item)
        {
            return;
        }

        _navigationPreferences.SetVisible(key, item.IsChecked);
        NavigationPreferencesStore.Save(_navigationPreferences);
        ApplyNavigationPreferences();
    }

    private void ApplyNavigationPreferences()
    {
        UserNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowUser));
        DocumentsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowDocuments));
        DownloadsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowDownloads));
        PicturesNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowPictures));
        MusicNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowMusic));
        VideosNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowVideos));
        FileExplorerNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowFileExplorer));
        NetworkNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowNetwork));
        SettingsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowSettings));
    }

    private Visibility PreferenceVisibility(string key) =>
        _navigationPreferences.IsVisible(key) ? Visibility.Visible : Visibility.Collapsed;

    private void UserNavigationButton_Click(object sender, RoutedEventArgs e) =>
        OpenButtonContextMenu(UserNavigationButton);

    private void PowerNavigationButton_Click(object sender, RoutedEventArgs e) =>
        OpenButtonContextMenu(PowerNavigationButton);

    private static void OpenButtonContextMenu(Button button)
    {
        if (button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
        button.ContextMenu.IsOpen = true;
    }

    private void DocumentsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

    private void DownloadsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("下载", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

    private void PicturesNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("图片", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

    private void MusicNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("音乐", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

    private void VideosNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("视频", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

    private void FileExplorerNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("文件资源管理器", "explorer.exe");

    private void NetworkNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("网络", "shell:NetworkPlacesFolder");

    private void SettingsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("设置", "ms-settings:");

    private void AccountSettings_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("账户设置", "ms-settings:yourinfo");

    private void LockSession_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        LockWorkStation();
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchProcess("注销", "shutdown.exe", "/l");
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        SetSuspendState(false, false, false);
    }

    private void ShutDown_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchProcess("关机", "shutdown.exe", "/s /t 0");
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchProcess("重启", "shutdown.exe", "/r /t 0");
    }

    private void LaunchNavigationTarget(string name, string target)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchShellTarget(name, target);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ShowSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_isInternalAppDrag)
            {
                EndInternalAppDrag(commit: false, Mouse.GetPosition(MainSurface));
            }
            else if (_isInternalTileDrag)
            {
                EndInternalTileDrag(commit: false);
            }
            else if (LetterIndexPanel.Visibility == Visibility.Visible)
            {
                HideLetterIndex();
            }
            else if (SearchPanel.Visibility == Visibility.Visible)
            {
                ClearSearch();
            }
            else
            {
                DismissWindow();
            }

            e.Handled = true;
        }
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.OriginalSource == SearchBox || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        HideLetterIndex(animate: false);
        ShowSearch();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        AppsView.Filter = item => item is AppEntry app && MatchesApp(app, query);
        if (query.Length > 0)
        {
            ExpandMatchingFolders(_apps, query);
        }

        RecentPanel.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppsView.Refresh();
    }

    private void ShowSearch()
    {
        HideLetterIndex(animate: false);
        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
    }

    private void LetterHeader_Click(object sender, RoutedEventArgs e)
    {
        if (SearchPanel.Visibility == Visibility.Visible)
        {
            return;
        }

        if (_isLetterIndexActive)
        {
            return;
        }

        ResetSemanticZoomVisuals();
        _isLetterIndexActive = true;
        LetterIndexPanel.Visibility = Visibility.Visible;
        AppsScrollViewer.IsHitTestVisible = false;
        LetterIndexPanel.IsHitTestVisible = false;
        BeginSemanticZoomTransition(
            zoomedInViewActive: false,
            animate: true,
            () => LetterIndexPanel.IsHitTestVisible = true);
    }

    private void AlphabetLetter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AlphabetIndexEntry { IsAvailable: true } entry })
        {
            return;
        }

        if (entry.IsRecent)
        {
            AppsScrollViewer.ScrollToTop();
            RecentPanel.BringIntoView();
            HideLetterIndex();
            return;
        }

        var group = AppsView.Groups?
            .OfType<CollectionViewGroup>()
            .FirstOrDefault(candidate =>
                candidate.Name?.ToString()?.Equals(entry.TargetLetter, StringComparison.OrdinalIgnoreCase) == true);
        if (group is null)
        {
            return;
        }

        AlignAppGroupToTop(group);
        HideLetterIndex(focusGroup: group);
    }

    private void AlignAppGroupToTop(CollectionViewGroup group)
    {
        AppsScrollViewer.UpdateLayout();
        if (AppsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
        {
            var groupTop = container.TranslatePoint(new System.Windows.Point(), AppsScrollViewer).Y;
            AppsScrollViewer.ScrollToVerticalOffset(Math.Max(0, AppsScrollViewer.VerticalOffset + groupTop));
            AppsScrollViewer.UpdateLayout();
        }
    }

    private void HideLetterIndex(bool animate = true, CollectionViewGroup? focusGroup = null)
    {
        _isLetterIndexActive = false;
        AppsScrollViewer.IsHitTestVisible = false;
        LetterIndexPanel.IsHitTestVisible = false;

        if (LetterIndexPanel.Visibility != Visibility.Visible)
        {
            ResetSemanticZoomVisuals();
            AppsScrollViewer.IsHitTestVisible = true;
            FocusAppGroup(focusGroup);
            return;
        }

        BeginSemanticZoomTransition(
            zoomedInViewActive: true,
            animate,
            () =>
            {
                LetterIndexPanel.Visibility = Visibility.Collapsed;
                AppsScrollViewer.IsHitTestVisible = true;
                FocusAppGroup(focusGroup);
            });
    }

    private void FocusAppGroup(CollectionViewGroup? group)
    {
        if (group is null)
        {
            return;
        }

        AppsScrollViewer.UpdateLayout();
        if (AppsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
        {
            container.Focus();
        }
    }

    private void SemanticZoomViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isSemanticZoomAnimating)
        {
            ResetSemanticZoomVisuals();
        }
    }

    private void BeginSemanticZoomTransition(bool zoomedInViewActive, bool animate, Action? completed = null)
    {
        var viewport = new System.Windows.Size(SemanticZoomViewport.ActualWidth, SemanticZoomViewport.ActualHeight);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            ResetSemanticZoomVisuals();
            completed?.Invoke();
            return;
        }

        var generation = ++_semanticZoomAnimationGeneration;
        var animationsEnabled = animate && SystemParameters.ClientAreaAnimation;
        _isSemanticZoomAnimating = animationsEnabled;
        SemanticZoomMotion.Animate(
            viewport,
            zoomedInViewActive,
            SemanticZoomSharedScale,
            SemanticZoomSharedTranslate,
            SemanticZoomedInScale,
            SemanticZoomedInTranslate,
            ZoomedInPresenter,
            LetterIndexPanel,
            animationsEnabled,
            () =>
            {
                if (generation != _semanticZoomAnimationGeneration)
                {
                    return;
                }

                _isSemanticZoomAnimating = false;
                ResetSemanticZoomVisuals();
                completed?.Invoke();
            });
    }

    private void ResetSemanticZoomVisuals()
    {
        var viewport = new System.Windows.Size(SemanticZoomViewport.ActualWidth, SemanticZoomViewport.ActualHeight);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        SemanticZoomMotion.Snap(
            viewport,
            !_isLetterIndexActive,
            SemanticZoomSharedScale,
            SemanticZoomSharedTranslate,
            SemanticZoomedInScale,
            SemanticZoomedInTranslate,
            ZoomedInPresenter,
            LetterIndexPanel);
    }

    private void ClearSearch()
    {
        SearchBox.Clear();
        SearchPanel.Visibility = Visibility.Collapsed;
        HideLetterIndex(animate: false);
        RecentPanel.Visibility = Visibility.Visible;
        AppsView.Filter = null;
        AppsView.Refresh();
    }

    private static bool MatchesApp(AppEntry app, string query) =>
        query.Length == 0
        || app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || app.Children.Any(child => MatchesApp(child, query));

    private static bool ExpandMatchingFolders(IEnumerable<AppEntry> entries, string query)
    {
        var anyMatch = false;
        foreach (var entry in entries)
        {
            var childMatch = entry.IsFolder && ExpandMatchingFolders(entry.Children, query);
            if (entry.IsFolder)
            {
                entry.IsExpanded = childMatch;
            }

            anyMatch |= childMatch || entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        return anyMatch;
    }
}
