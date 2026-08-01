using System.Drawing;
using System.IO;
using System.Xml.Linq;
using TileStart.Host.Themes;

namespace TileStart.Host.Tests;

public sealed class DarkThemeVisualTests
{
    [Fact]
    public void MainWindowUsesTheSharedPrimaryTextBrushForTextBlocks()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute("TargetType") == "TextBlock"
                && element.Attribute(x + "Key") is null);

        Assert.Contains(
            style.Elements(presentation + "Setter"),
            setter =>
                (string?)setter.Attribute("Property") == "Foreground"
                && (string?)setter.Attribute("Value") == "{StaticResource TileStartTextPrimaryBrush}");

        Assert.Equal("#FFFFFFFF", ReadThemeBrushColor("TileStartTextPrimaryBrush"));
    }

    [Theory]
    [InlineData("Win10Theme.xaml", "TileStartTextPrimaryBrush", "#FFFFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartTextSecondaryBrush", "#CCFFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartTextTertiaryBrush", "#99FFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartTextDisabledBrush", "#33FFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartControlHoverBrush", "#19FFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartControlPressedBrush", "#33FFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartNavigationOverlayBrush", "#802C2C2C")]
    [InlineData("Win10LightTheme.xaml", "TileStartTextPrimaryBrush", "#FF000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartTextSecondaryBrush", "#CC000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartTextTertiaryBrush", "#99000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartTextDisabledBrush", "#33000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartControlHoverBrush", "#19000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartControlPressedBrush", "#33000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartNavigationOverlayBrush", "#80E9E9E9")]
    public void Windows10PaletteMatchesExtractedStartUiThemeResources(
        string theme,
        string key,
        string expected)
    {
        Assert.Equal(expected, ReadThemeBrushColor(theme, key));
    }

    [Theory]
    [InlineData("Win10Theme.xaml", "TileStartPopupBackgroundBrush", "#FF2B2B2B")]
    [InlineData("Win10Theme.xaml", "TileStartPopupStrokeBrush", "#5C000000")]
    [InlineData("Win10Theme.xaml", "TileStartContextMenuHighlightBrush", "#19FFFFFF")]
    [InlineData("Win10Theme.xaml", "TileStartMenuSeparatorBrush", "#FF7A7A7A")]
    [InlineData("Win10LightTheme.xaml", "TileStartPopupBackgroundBrush", "#FFF2F2F2")]
    [InlineData("Win10LightTheme.xaml", "TileStartPopupStrokeBrush", "#24000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartContextMenuHighlightBrush", "#19000000")]
    [InlineData("Win10LightTheme.xaml", "TileStartMenuSeparatorBrush", "#FF7A7A7A")]
    public void Windows10ContextMenuUsesNativeFallbackPalette(string theme, string key, string expected)
    {
        Assert.Equal(expected, ReadThemeBrushColor(theme, key));
    }

    [Theory]
    [InlineData("Win10Theme.xaml", "0.6")]
    [InlineData("Win10LightTheme.xaml", "0.4")]
    public void Windows10SubmenuOpenedBackgroundUsesNativeAccentLowOpacity(
        string theme,
        string expectedOpacity)
    {
        const string key = "TileStartContextMenuSubmenuOpenedBrush";
        Assert.Equal(
            "{x:Static local:Win10Theme.AccentColor}",
            ReadThemeBrushColor(theme, key));
        Assert.Equal(expectedOpacity, ReadThemeBrushAttribute(theme, key, "Opacity"));
    }

    [Theory]
    [InlineData("Win10Theme.xaml", "0.9")]
    [InlineData("Win10LightTheme.xaml", "0.7")]
    public void Windows10MenuPressedBackgroundUsesNativeAccentHighOpacity(
        string theme,
        string expectedOpacity)
    {
        const string key = "TileStartContextMenuPressedBrush";
        Assert.Equal(
            "{x:Static local:Win10Theme.AccentColor}",
            ReadThemeBrushColor(theme, key));
        Assert.Equal(expectedOpacity, ReadThemeBrushAttribute(theme, key, "Opacity"));
    }

    [Fact]
    public void ApplicationNamesUseTheNativeSingleLineTextStyle()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var appName = document.Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding Name}"
                && (string?)element.Attribute("Grid.Column") == "1"
                && (string?)element.Attribute("Margin") == "8,0,0,0");

        Assert.Equal("NoWrap", (string?)appName.Attribute("TextWrapping"));
        Assert.Equal("Normal", (string?)appName.Attribute("FontWeight"));
        Assert.Equal("14", (string?)appName.Attribute("FontSize"));
        Assert.Null(appName.Attribute("MaxHeight"));
        Assert.Null(appName.Attribute("LineHeight"));

        var folderChevron = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{Binding FolderChevron}");
        Assert.Equal("14", (string?)folderChevron.Attribute("FontSize"));

        var navigationText = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{TemplateBinding Tag}");
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.NavigationTextFontSize}",
            (string?)navigationText.Attribute("FontSize"));
        Assert.Equal("0", (string?)navigationText.Attribute("Margin"));

        var userPicture = document.Descendants(presentation + "Grid")
            .Single(element => (string?)element.Attribute(x + "Name") == "UserPictureVisual");
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.NavigationUserPictureSize}",
            (string?)userPicture.Attribute("Width"));
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.NavigationUserPictureSize}",
            (string?)userPicture.Attribute("Height"));
    }

    [Fact]
    public void ApplicationListAppliesCompiledPaddingInsideScrollableContent()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var appsList = document.Descendants(presentation + "ListBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "AppsList");
        var itemsPanel = appsList.Descendants(presentation + "VirtualizingStackPanel")
            .Single(element =>
                (string?)element.Attribute("Margin") ==
                "{x:Static local:Win10VisualMetrics.AllAppsListPadding}");

        Assert.Null(appsList.Attribute("Margin"));
        Assert.Null(appsList.Attribute("Padding"));
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.AllAppsListPadding}",
            (string?)itemsPanel.Attribute("Margin"));
        Assert.Equal(54, Win10VisualMetrics.AllAppsListPadding.Bottom);
    }

    [Fact]
    public void ApplicationFolderChevronUsesContentSizedTrailingColumn()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var template = document.Descendants(presentation + "DataTemplate")
            .Single(element => (string?)element.Attribute(x + "Key") == "AppEntryTemplate");
        var chevron = template.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "FolderChevron");
        var columns = chevron.Parent!
            .Element(presentation + "Grid.ColumnDefinitions")!
            .Elements(presentation + "ColumnDefinition")
            .ToArray();

        Assert.Equal(
            "{x:Static icons:Win10IconMetrics.ClassicAppLogoGridLength}",
            (string?)columns[0].Attribute("Width"));
        Assert.Equal("Auto", (string?)columns[^1].Attribute("Width"));
    }

    [Fact]
    public void ExpandedNavigationUsesASeparateBackdropAndShadowLayer()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var backdrop = document.Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "NavigationBackdrop");
        Assert.Equal("{x:Static local:Win10VisualMetrics.NavigationBackdropMargin}",
            (string?)backdrop.Attribute("Margin"));
        Assert.Equal("{DynamicResource TileStartNavigationOverlayBrush}",
            (string?)backdrop.Attribute("Background"));
        Assert.Equal("0", (string?)backdrop.Attribute("Opacity"));
        Assert.Equal("False", (string?)backdrop.Attribute("IsHitTestVisible"));

        var shadow = backdrop.Descendants(presentation + "DropShadowEffect").Single();
        Assert.Equal("{x:Static local:Win10VisualMetrics.NavigationShadowBlurRadius}",
            (string?)shadow.Attribute("BlurRadius"));
        Assert.Equal("{x:Static local:Win10VisualMetrics.NavigationShadowOpacity}",
            (string?)shadow.Attribute("Opacity"));
        Assert.Equal("{x:Static local:Win10VisualMetrics.NavigationShadowDepth}",
            (string?)shadow.Attribute("ShadowDepth"));
        Assert.Equal("{x:Static local:Win10VisualMetrics.NavigationShadowDirection}",
            (string?)shadow.Attribute("Direction"));
    }

    [Fact]
    public void TrayMenuColorTableUsesDarkSurfaceAndAccentSelection()
    {
        var highlight = Color.FromArgb(12, 34, 56);
        var palette = TileStartTrayRenderer.GetPalette(AppThemeStyle.Windows11);
        var colors = new TileStartTrayColorTable(highlight, palette);

        Assert.Equal(palette.Background, colors.ToolStripDropDownBackground);
        Assert.Equal(palette.Border, colors.MenuBorder);
        Assert.Equal(palette.Separator, colors.SeparatorDark);
        Assert.Equal(highlight, colors.MenuItemSelected);
    }

    [Fact]
    public void LightShellKeepsTheSameDefaultTileFaceAsItsMatchingStyle()
    {
        const string key = "TileStartDefaultTileBackgroundBrush";

        Assert.Equal(
            ReadThemeBrushColor("Win10Theme.xaml", key),
            ReadThemeBrushColor("Win10LightTheme.xaml", key));
        Assert.Equal(
            ReadThemeBrushColor("Win11Theme.xaml", key),
            ReadThemeBrushColor("Win11LightTheme.xaml", key));
        Assert.Equal(
            "{x:Static local:Win10Theme.AccentColor}",
            ReadThemeBrushColor("Win10Theme.xaml", key));
    }

    [Fact]
    public void TileButtonsUseDedicatedSquareGeometry()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStyle");
        var border = style.Descendants(XName.Get("Win10InteractionBorder", "clr-namespace:TileStart.Host"))
            .Single(element => (string?)element.Attribute(x + "Name") == "TileBorder");

        Assert.Equal("{StaticResource TileStartTileCornerRadius}", (string?)border.Attribute("CornerRadius"));
        Assert.Equal("0.5", (string?)border.Attribute("RevealBorderOpacity"));
        Assert.Equal("0", ReadThemeResourceValue("Win11Theme.xaml", "CornerRadius", "TileStartTileCornerRadius"));
        Assert.Equal("0", ReadThemeResourceValue("Win10Theme.xaml", "CornerRadius", "TileStartTileCornerRadius"));
    }

    [Fact]
    public void TileTitlesUseTheNativeCaptionWeightAndFolderVisualMetrics()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var title = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "TileTitle");
        Assert.Equal("12", (string?)title.Attribute("FontSize"));
        Assert.Equal("Normal", (string?)title.Attribute("FontWeight"));

        var chevron = document.Descendants(presentation + "Grid")
            .Single(element => (string?)element.Attribute(x + "Name") == "FolderCollapseGlyph")
            .Descendants(presentation + "TextBlock")
            .Single();
        Assert.Equal("16", (string?)chevron.Attribute("FontSize"));
        Assert.Equal("{DynamicResource TileStartTextPrimaryBrush}",
            (string?)chevron.Attribute("Foreground"));
    }

    [Theory]
    [InlineData("AppRowStyle")]
    [InlineData("LetterHeaderStyle")]
    [InlineData("AlphabetButtonStyle")]
    public void AllAppsInteractionsUseNativeListFallbackOpacities(string styleKey)
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace local = "clr-namespace:TileStart.Host";

        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == styleKey);
        var border = style.Descendants(local + "Win10InteractionBorder").Single();

        Assert.Equal("0.10", (string?)border.Attribute("HoverFillOpacity"));
        Assert.Equal("0.20", (string?)border.Attribute("PressedFillOpacity"));
    }

    [Fact]
    public void AllAppsHeadersUseTheNativeStackGeometry()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var letterStyle = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "LetterHeaderStyle");
        Assert.Contains(letterStyle.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Focusable"
            && (string?)setter.Attribute("Value") == "False");
        Assert.Contains(letterStyle.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "IsTabStop"
            && (string?)setter.Attribute("Value") == "False");

        var recentHeader = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "RecentApplicationsHeader");
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.AllAppsGroupHeaderPadding}",
            (string?)recentHeader.Attribute("Margin"));
        Assert.Equal("Bottom", (string?)recentHeader.Attribute("VerticalAlignment"));
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.AllAppsGroupHeaderFontSize}",
            (string?)recentHeader.Attribute("FontSize"));

        var expandCaret = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "{Binding ExpandGlyph}");
        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.AllAppsExpandCollapseCaretFontSize}",
            (string?)expandCaret.Attribute("FontSize"));
    }

    [Fact]
    public void SharedExpanderTemplatePropagatesPrimaryForegroundIntoHeaderAndChevron()
    {
        var document = LoadXaml("SharedStyles.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStartDarkExpanderStyle");
        Assert.Contains(style.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground"
            && (string?)setter.Attribute("Value") == "{DynamicResource TileStartTextPrimaryBrush}");

        var toggle = style.Descendants(presentation + "ToggleButton").Single();
        Assert.Equal("{TemplateBinding Foreground}", (string?)toggle.Attribute("Foreground"));
        var header = style.Descendants(presentation + "ContentPresenter")
            .Single(element => element.Attribute("Content") is not null);
        Assert.Equal("{TemplateBinding Foreground}", (string?)header.Attribute("TextElement.Foreground"));
        var chevron = style.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "Chevron");
        Assert.Equal("{TemplateBinding Foreground}", (string?)chevron.Attribute("Foreground"));
    }

    [Theory]
    [InlineData("TileSettingsWindow.xaml", "SettingsExpanderStyle")]
    [InlineData("BackupRestoreWindow.xaml", "BackupExpanderStyle")]
    public void DarkWindowsUseTheSharedExpanderTemplate(string fileName, string styleKey)
    {
        var document = LoadXaml(fileName);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == styleKey);

        Assert.Equal("{StaticResource TileStartDarkExpanderStyle}", (string?)style.Attribute("BasedOn"));
    }

    [Fact]
    public void TileStartSettingsContainsConsolidatedTrayActions()
    {
        var document = LoadXaml("SettingsWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.NotNull(document.Descendants(presentation + "CheckBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "StartupBox"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "Windows10Choice"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "Windows11Choice"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "SystemColorChoice"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "LightColorChoice"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "DarkColorChoice"));
        Assert.DoesNotContain(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "检查更新");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "备份与恢复");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "关于 TileStart");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "诊断日志");
        Assert.Contains(document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Click") == "ExportDiagnostics_Click");

        var settingsScrollBarStyle = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute("TargetType") == "ScrollBar"
                               && element.Attribute(x + "Key") is null);
        Assert.Equal("{StaticResource TileStartDialogScrollBarStyle}",
            (string?)settingsScrollBarStyle.Attribute("BasedOn"));

        var about = LoadXaml("AboutWindow.xaml");
        Assert.Contains(about.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Content") == "检查更新");
        Assert.Contains(about.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Content") == "返回设置");
        Assert.Contains(about.Descendants(presentation + "RowDefinition"),
            element => (string?)element.Attribute("Height") == "60");
    }

    [Fact]
    public void TileSettingsSeparatesApplyFromSaveAndDoesNotClipDefaultButton()
    {
        var document = LoadXaml("TileSettingsWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var apply = document.Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "ApplyButton");
        Assert.Equal("应用", (string?)apply.Attribute("Content"));
        Assert.Equal("Apply_Click", (string?)apply.Attribute("Click"));
        Assert.Contains(document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Content") == "保存并关闭");

        var useDefault = document.Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute("Content") == "使用默认");
        Assert.Equal("88", (string?)useDefault.Attribute("Width"));
    }

    [Fact]
    public void TilePaneContextMenusUseTheSharedPopupAndItemStyles()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var menus = document.Descendants(presentation + "ContextMenu")
            .Where(menu => menu.Descendants(presentation + "MenuItem").Any(item =>
                (string?)item.Attribute("Header") is "新建自定义磁贴…" or "删除组"))
            .ToArray();

        Assert.NotEmpty(menus);
        Assert.All(menus, menu =>
        {
            Assert.Null(menu.Attribute("Style"));
            Assert.All(menu.Descendants(presentation + "MenuItem"), item =>
                Assert.Null(item.Attribute("Style")));
        });

        var sharedStyles = LoadXaml("SharedStyles.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var implicitContextMenuStyle = sharedStyles.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute("TargetType") == "ContextMenu"
                && element.Attribute(x + "Key") is null);
        var implicitMenuItemStyle = sharedStyles.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute("TargetType") == "MenuItem"
                && element.Attribute(x + "Key") is null);
        Assert.Equal("{StaticResource TileStartContextMenuStyle}",
            (string?)implicitContextMenuStyle.Attribute("BasedOn"));
        Assert.Equal("{StaticResource TileStartMenuItemStyle}",
            (string?)implicitMenuItemStyle.Attribute("BasedOn"));

        var menuItemStyle = sharedStyles.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStartMenuItemStyle");
        var pressedEvents = menuItemStyle.Elements(presentation + "EventSetter")
            .Select(setter => (
                Event: (string?)setter.Attribute("Event"),
                Handler: (string?)setter.Attribute("Handler")))
            .ToArray();
        Assert.Contains(("PreviewMouseLeftButtonDown", "MenuItem_PreviewMouseLeftButtonDown"), pressedEvents);
        Assert.Contains(("PreviewMouseLeftButtonUp", "MenuItem_ClearPressedState"), pressedEvents);
        Assert.Contains(("MouseLeave", "MenuItem_ClearPressedState"), pressedEvents);
        Assert.Contains(("LostMouseCapture", "MenuItem_ClearPressedState"), pressedEvents);
        var activeTriggers = menuItemStyle.Descendants(presentation + "Trigger")
            .Where(trigger => (string?)trigger.Attribute("Property") is "IsHighlighted" or "IsSubmenuOpen")
            .ToArray();
        Assert.Equal(2, activeTriggers.Length);
        Assert.All(activeTriggers, trigger =>
        {
            var activeForegroundSetters = trigger.Elements(presentation + "Setter")
                .Where(setter =>
                (string?)setter.Attribute("Property") == "Foreground"
                && (string?)setter.Attribute("TargetName") is "SubmenuArrow" or "CheckMark")
                .ToArray();
            Assert.Equal(2, activeForegroundSetters.Length);
            Assert.All(activeForegroundSetters, setter =>
                Assert.Equal(
                    "{DynamicResource TileStartTextPrimaryBrush}",
                    (string?)setter.Attribute("Value")));
        });
        var submenuOpenedTrigger = activeTriggers.Single(trigger =>
            (string?)trigger.Attribute("Property") == "IsSubmenuOpen");
        Assert.Contains(submenuOpenedTrigger.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("TargetName") == "MenuItemRoot"
            && (string?)setter.Attribute("Property") == "Background"
            && (string?)setter.Attribute("Value") ==
            "{DynamicResource TileStartContextMenuSubmenuOpenedBrush}");

        var submenuArrow = sharedStyles.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "SubmenuArrow");
        Assert.Equal("{x:Static local:Win10VisualMetrics.ContextMenuSubmenuArrowFontSize}",
            (string?)submenuArrow.Attribute("FontSize"));
        Assert.Equal("{x:Static local:Win10VisualMetrics.ContextMenuSubmenuArrowGlyph}",
            (string?)submenuArrow.Attribute("Text"));
        Assert.Equal("{DynamicResource TileStartTextSecondaryBrush}",
            (string?)submenuArrow.Attribute("Foreground"));

        var checkGlyph = sharedStyles.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "CheckMark");
        Assert.Equal("{x:Static local:Win10VisualMetrics.ContextMenuCheckGlyphFontSize}",
            (string?)checkGlyph.Attribute("FontSize"));
        Assert.Equal("{x:Static local:Win10VisualMetrics.ContextMenuCheckGlyph}",
            (string?)checkGlyph.Attribute("Text"));
        Assert.Equal("{DynamicResource TileStartTextSecondaryBrush}",
            (string?)checkGlyph.Attribute("Foreground"));
    }

    [Fact]
    public void Windows11ContextMenusUseAccentColorHighlightWithInsetPadding()
    {
        Assert.Equal("{x:Static local:Win10Theme.AccentColor}",
            ReadThemeBrushColor("TileStartContextMenuHighlightBrush"));
        Assert.Equal("4", ReadThemeResourceValue(
            "Win11Theme.xaml",
            "Thickness",
            "TileStartContextMenuPresenterPadding"));

        var document = LoadXaml("SharedStyles.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var contextMenuStyle = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStartContextMenuStyle");
        Assert.Contains(contextMenuStyle.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Padding"
            && (string?)setter.Attribute("Value") == "{DynamicResource TileStartContextMenuPresenterPadding}");
    }

    [Fact]
    public void FolderTilesUseAStableThreeByThreePreviewAndSpecificSettingsLabel()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var preview = document.Descendants(presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute(x + "Name") == "FolderPreview");
        Assert.Equal("{Binding FolderPreviewTiles}", (string?)preview.Attribute("ItemsSource"));
        var previewPanel = preview.Descendants(presentation + "UniformGrid").Single();
        Assert.Equal("3", (string?)previewPanel.Attribute("Rows"));
        Assert.Equal("3", (string?)previewPanel.Attribute("Columns"));

        var settingsItem = document.Descendants(presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Click") == "TileSettings_Click");
        Assert.Equal("{Binding SettingsMenuHeader}", (string?)settingsItem.Attribute("Header"));
        var tileMenu = settingsItem.Ancestors(presentation + "ContextMenu").Single();
        Assert.Equal("TileContextMenu", (string?)tileMenu.Attribute(x + "Key"));
        Assert.Equal("{Binding PlacementTarget.Tag, RelativeSource={RelativeSource Self}}",
            (string?)tileMenu.Attribute("DataContext"));

        var menuUsers = document.Descendants(presentation + "Button")
            .Where(button => (string?)button.Attribute("ContextMenu") == "{StaticResource TileContextMenu}")
            .ToArray();
        Assert.Equal(2, menuUsers.Length);
    }

    private static XDocument LoadMainWindow()
    {
        return LoadXaml("MainWindow.xaml");
    }

    private static XDocument LoadXaml(string fileName) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName));

    private static string? ReadThemeBrushColor(string key) =>
        ReadThemeBrushColor("Win11Theme.xaml", key);

    private static string? ReadThemeBrushColor(string fileName, string key)
        => ReadThemeBrushAttribute(fileName, key, "Color");

    private static string? ReadThemeBrushAttribute(string fileName, string key, string attribute)
    {
        var document = LoadXaml(fileName);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Descendants(presentation + "SolidColorBrush")
            .Single(element => (string?)element.Attribute(x + "Key") == key)
            .Attribute(attribute)?.Value;
    }

    private static string? ReadThemeResourceValue(string fileName, string elementName, string key)
    {
        var document = LoadXaml(fileName);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Descendants(presentation + elementName)
            .Single(element => (string?)element.Attribute(x + "Key") == key)
            .Value;
    }
}