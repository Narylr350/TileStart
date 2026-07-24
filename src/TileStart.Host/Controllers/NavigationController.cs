using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using TileStart.Host.Applications;
using TileStart.Host.Navigation;
using TileStart.Host.Utilities;

namespace TileStart.Host.Controllers;

internal sealed class NavigationController
{
    private readonly System.Windows.Threading.DispatcherTimer _navigationHoverTimer = new()
    {
        Interval = SystemParameters.MouseHoverTime,
    };

    private bool _navigationExpanded;
    private bool _navigationPinnedOpen;
    private readonly NavigationPreferences _navigationPreferences = NavigationPreferencesStore.Load();
    private int _semanticZoomAnimationGeneration;
    private bool _isLetterIndexActive;
    private bool _isSemanticZoomAnimating;

    private readonly Grid _navigationPane;
    private readonly Button _navigationToggleButton;
    private readonly Button _userNavigationButton;
    private readonly Button _documentsNavigationButton;
    private readonly Button _downloadsNavigationButton;
    private readonly Button _picturesNavigationButton;
    private readonly Button _musicNavigationButton;
    private readonly Button _videosNavigationButton;
    private readonly Button _fileExplorerNavigationButton;
    private readonly Button _networkNavigationButton;
    private readonly Button _settingsNavigationButton;
    private readonly Button _powerNavigationButton;
    private readonly Border _letterIndexPanel;
    private readonly Border _searchPanel;
    private readonly TextBox _searchBox;
    private readonly ICollectionView _appsView;
    private readonly ListBox _appsList;
    private readonly RecentApplicationsSection _recentSection;
    private readonly Grid _semanticZoomViewport;
    private readonly ScaleTransform _semanticZoomSharedScale;
    private readonly TranslateTransform _semanticZoomSharedTranslate;
    private readonly ScaleTransform _semanticZoomedInScale;
    private readonly TranslateTransform _semanticZoomedInTranslate;
    private readonly Grid _zoomedInPresenter;

    private readonly Action<bool> _dismissWindow;
    private readonly Func<bool> _cancelCurrentDrag;
    private readonly Func<IList<AppEntry>> _getAllApps;
    private readonly Func<int> _getOpenContextMenuCount;
    private readonly Func<bool> _lockWorkStation;
    private readonly Func<bool, bool, bool, bool> _setSuspendState;

    public NavigationController(
        Grid navigationPane,
        Button navigationToggleButton,
        Button userNavigationButton,
        Button documentsNavigationButton,
        Button downloadsNavigationButton,
        Button picturesNavigationButton,
        Button musicNavigationButton,
        Button videosNavigationButton,
        Button fileExplorerNavigationButton,
        Button networkNavigationButton,
        Button settingsNavigationButton,
        Button powerNavigationButton,
        Border letterIndexPanel,
        Border searchPanel,
        TextBox searchBox,
        ICollectionView appsView,
        ListBox appsList,
        RecentApplicationsSection recentSection,
        Grid semanticZoomViewport,
        ScaleTransform semanticZoomSharedScale,
        TranslateTransform semanticZoomSharedTranslate,
        ScaleTransform semanticZoomedInScale,
        TranslateTransform semanticZoomedInTranslate,
        Grid zoomedInPresenter,
        Action<bool> dismissWindow,
        Func<bool> cancelCurrentDrag,
        Func<IList<AppEntry>> getAllApps,
        Func<int> getOpenContextMenuCount,
        Func<bool> lockWorkStation,
        Func<bool, bool, bool, bool> setSuspendState)
    {
        _navigationPane = navigationPane;
        _navigationToggleButton = navigationToggleButton;
        _userNavigationButton = userNavigationButton;
        _documentsNavigationButton = documentsNavigationButton;
        _downloadsNavigationButton = downloadsNavigationButton;
        _picturesNavigationButton = picturesNavigationButton;
        _musicNavigationButton = musicNavigationButton;
        _videosNavigationButton = videosNavigationButton;
        _fileExplorerNavigationButton = fileExplorerNavigationButton;
        _networkNavigationButton = networkNavigationButton;
        _settingsNavigationButton = settingsNavigationButton;
        _powerNavigationButton = powerNavigationButton;
        _letterIndexPanel = letterIndexPanel;
        _searchPanel = searchPanel;
        _searchBox = searchBox;
        _appsView = appsView;
        _appsList = appsList;
        _recentSection = recentSection;
        _semanticZoomViewport = semanticZoomViewport;
        _semanticZoomSharedScale = semanticZoomSharedScale;
        _semanticZoomSharedTranslate = semanticZoomSharedTranslate;
        _semanticZoomedInScale = semanticZoomedInScale;
        _semanticZoomedInTranslate = semanticZoomedInTranslate;
        _zoomedInPresenter = zoomedInPresenter;
        _dismissWindow = dismissWindow;
        _cancelCurrentDrag = cancelCurrentDrag;
        _getAllApps = getAllApps;
        _getOpenContextMenuCount = getOpenContextMenuCount;
        _lockWorkStation = lockWorkStation;
        _setSuspendState = setSuspendState;

        _navigationHoverTimer.Tick += NavigationHoverTimer_Tick;
        _semanticZoomViewport.SizeChanged += SemanticZoomViewport_SizeChanged;
    }

    public bool IsNavigationPinnedOpen => _navigationPinnedOpen;

    public void StopHoverTimer() => _navigationHoverTimer.Stop();

    public void NavigationToggleButtonClick()
    {
        _navigationHoverTimer.Stop();
        _navigationPinnedOpen = !_navigationPinnedOpen;
        SetNavigationExpanded(_navigationPinnedOpen);
    }

    public void NavigationPaneMouseEnter()
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
        if (!_navigationPinnedOpen && _navigationPane.IsMouseOver)
        {
            SetNavigationExpanded(true);
        }
    }

    public void NavigationPaneMouseLeave()
    {
        _navigationHoverTimer.Stop();
        if (!_navigationPinnedOpen && _getOpenContextMenuCount() == 0)
        {
            SetNavigationExpanded(false);
        }
    }

    public void SetNavigationExpanded(bool expanded)
    {
        if (_navigationExpanded == expanded)
        {
            return;
        }

        _navigationExpanded = expanded;
        var targetWidth = expanded
            ? Win10VisualMetrics.ExpandedNavigationWidth
            : Win10VisualMetrics.CollapsedNavigationWidth;
        _navigationToggleButton.ToolTip = expanded ? "收起" : "展开";
        if (expanded)
        {
            _navigationPane.Background =
                (System.Windows.Media.Brush)_navigationPane.FindResource("ExpandedNavigationBackground");
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            _navigationPane.Width = targetWidth;
            if (!expanded)
            {
                _navigationPane.Background = System.Windows.Media.Brushes.Transparent;
            }

            return;
        }

        var animation = new DoubleAnimation
        {
            From = _navigationPane.ActualWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            },
        };
        animation.Completed += (_, _) =>
        {
            _navigationPane.BeginAnimation(FrameworkElement.WidthProperty, null);
            _navigationPane.Width = targetWidth;
            if (!expanded && !_navigationExpanded)
            {
                _navigationPane.Background = System.Windows.Media.Brushes.Transparent;
            }
        };
        _navigationPane.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }

    public void NavigationPreferencesMenuOpened(object sender, RoutedEventArgs e)
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

    public void NavigationPreferenceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string key } item)
        {
            return;
        }

        _navigationPreferences.SetVisible(key, item.IsChecked);
        NavigationPreferencesStore.Save(_navigationPreferences);
        ApplyNavigationPreferences();
    }

    public void ApplyNavigationPreferences()
    {
        _userNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowUser));
        _documentsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowDocuments));
        _downloadsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowDownloads));
        _picturesNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowPictures));
        _musicNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowMusic));
        _videosNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowVideos));
        _fileExplorerNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowFileExplorer));
        _networkNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowNetwork));
        _settingsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowSettings));
    }

    private Visibility PreferenceVisibility(string key) =>
        _navigationPreferences.IsVisible(key) ? Visibility.Visible : Visibility.Collapsed;

    public void UserNavigationButtonClick() =>
        OpenButtonContextMenu(_userNavigationButton);

    public void PowerNavigationButtonClick() =>
        OpenButtonContextMenu(_powerNavigationButton);

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

    public void DocumentsNavigationButtonClick() =>
        LaunchNavigationTarget("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

    public void DownloadsNavigationButtonClick() =>
        LaunchNavigationTarget("下载",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

    public void PicturesNavigationButtonClick() =>
        LaunchNavigationTarget("图片", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

    public void MusicNavigationButtonClick() =>
        LaunchNavigationTarget("音乐", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

    public void VideosNavigationButtonClick() =>
        LaunchNavigationTarget("视频", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

    public void FileExplorerNavigationButtonClick() =>
        LaunchNavigationTarget("文件资源管理器", "explorer.exe");

    public void NetworkNavigationButtonClick() =>
        LaunchNavigationTarget("网络", "shell:NetworkPlacesFolder");

    public void SettingsNavigationButtonClick() =>
        LaunchNavigationTarget("设置", "ms-settings:");

    public void AccountSettingsClick() =>
        LaunchNavigationTarget("账户设置", "ms-settings:yourinfo");

    public void LockSessionClick()
    {
        _dismissWindow(true);
        _lockWorkStation();
    }

    public void SignOutClick()
    {
        _dismissWindow(true);
        AppLauncher.LaunchProcess("注销", "shutdown.exe", "/l");
    }

    public void SleepClick()
    {
        _dismissWindow(true);
        _setSuspendState(false, false, false);
    }

    public void ShutDownClick()
    {
        _dismissWindow(true);
        AppLauncher.LaunchProcess("关机", "shutdown.exe", "/s /t 0");
    }

    public void RestartClick()
    {
        _dismissWindow(true);
        AppLauncher.LaunchProcess("重启", "shutdown.exe", "/r /t 0");
    }

    private void LaunchNavigationTarget(string name, string target)
    {
        _dismissWindow(true);
        AppLauncher.LaunchShellTarget(name, target);
    }

    public void WindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ShowSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_cancelCurrentDrag() == true)
            {
                e.Handled = true;
                return;
            }

            if (_letterIndexPanel.Visibility == Visibility.Visible)
            {
                HideLetterIndex();
            }
            else if (_searchPanel.Visibility == Visibility.Visible)
            {
                ClearSearch();
            }
            else
            {
                _dismissWindow(false);
            }

            e.Handled = true;
        }
    }

    public void WindowPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.OriginalSource == _searchBox || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        HideLetterIndex(animate: false);
        ShowSearch();
        _searchBox.Text += e.Text;
        _searchBox.CaretIndex = _searchBox.Text.Length;
        e.Handled = true;
    }

    public void SearchBoxTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = _searchBox.Text.Trim();
        _appsView.Filter = item => item switch
        {
            RecentApplicationsSection => query.Length == 0,
            AppEntry app => MatchesApp(app, query),
            _ => false,
        };
        if (query.Length > 0)
        {
            ExpandMatchingFolders(_getAllApps(), query);
        }

        _appsView.Refresh();
    }

    private void ShowSearch()
    {
        HideLetterIndex(animate: false);
        _searchPanel.Visibility = Visibility.Visible;
        _searchBox.Focus();
    }

    public void LetterHeaderClick(object sender, RoutedEventArgs e)
    {
        if (_searchPanel.Visibility == Visibility.Visible)
        {
            return;
        }

        if (_isLetterIndexActive)
        {
            return;
        }

        ResetSemanticZoomVisuals();
        _isLetterIndexActive = true;
        _letterIndexPanel.Visibility = Visibility.Visible;
        _appsList.IsHitTestVisible = false;
        _letterIndexPanel.IsHitTestVisible = false;
        BeginSemanticZoomTransition(
            zoomedInViewActive: false,
            animate: true,
            () => _letterIndexPanel.IsHitTestVisible = true);
    }

    public void AlphabetLetterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AlphabetIndexEntry { IsAvailable: true } entry })
        {
            return;
        }

        if (entry.IsRecent)
        {
            SmoothScroll.Cancel(_appsList);
            _appsList.ScrollIntoView(_recentSection);
            HideLetterIndex();
            return;
        }

        var group = _appsView.Groups?
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
        var firstItem = group.Items.Cast<object>().FirstOrDefault();
        if (firstItem is null)
        {
            return;
        }

        SmoothScroll.Cancel(_appsList);
        _appsList.ScrollIntoView(firstItem);
        _appsList.UpdateLayout();
        if (_appsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement groupContainer)
        {
            groupContainer.BringIntoView();
        }
    }

    private void HideLetterIndex(bool animate = true, CollectionViewGroup? focusGroup = null)
    {
        _isLetterIndexActive = false;
        _appsList.IsHitTestVisible = false;
        _letterIndexPanel.IsHitTestVisible = false;

        if (_letterIndexPanel.Visibility != Visibility.Visible)
        {
            ResetSemanticZoomVisuals();
            _appsList.IsHitTestVisible = true;
            FocusAppGroup(focusGroup);
            return;
        }

        BeginSemanticZoomTransition(
            zoomedInViewActive: true,
            animate,
            () =>
            {
                _letterIndexPanel.Visibility = Visibility.Collapsed;
                _appsList.IsHitTestVisible = true;
                FocusAppGroup(focusGroup);
            });
    }

    private void FocusAppGroup(CollectionViewGroup? group)
    {
        if (group is null)
        {
            return;
        }

        _appsList.UpdateLayout();
        if (_appsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
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
        var viewport = new System.Windows.Size(_semanticZoomViewport.ActualWidth, _semanticZoomViewport.ActualHeight);
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
            _semanticZoomSharedScale,
            _semanticZoomSharedTranslate,
            _semanticZoomedInScale,
            _semanticZoomedInTranslate,
            _zoomedInPresenter,
            _letterIndexPanel,
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
        var viewport = new System.Windows.Size(_semanticZoomViewport.ActualWidth, _semanticZoomViewport.ActualHeight);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        SemanticZoomMotion.Snap(
            viewport,
            !_isLetterIndexActive,
            _semanticZoomSharedScale,
            _semanticZoomSharedTranslate,
            _semanticZoomedInScale,
            _semanticZoomedInTranslate,
            _zoomedInPresenter,
            _letterIndexPanel);
    }

    public void ClearSearch()
    {
        _searchBox.Clear();
        _searchPanel.Visibility = Visibility.Collapsed;
        HideLetterIndex(animate: false);
        _appsView.Filter = null;
        _appsView.Refresh();
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