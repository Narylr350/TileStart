using System.Windows;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace TileStart.Host;

public partial class MainWindow
{
    private void NavigationToggleButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.NavigationToggleButtonClick();

    private void NavigationPane_MouseEnter(object sender, MouseEventArgs e) =>
        _navigationController.NavigationPaneMouseEnter();

    private void NavigationPane_MouseLeave(object sender, MouseEventArgs e) =>
        _navigationController.NavigationPaneMouseLeave();

    private void NavigationPreferencesMenu_Opened(object sender, RoutedEventArgs e) =>
        _navigationController.NavigationPreferencesMenuOpened(sender, e);

    private void NavigationPreference_Click(object sender, RoutedEventArgs e) =>
        _navigationController.NavigationPreferenceClick(sender, e);

    private void UserNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.UserNavigationButtonClick();

    private void PowerNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.PowerNavigationButtonClick();

    private void DocumentsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.DocumentsNavigationButtonClick();

    private void DownloadsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.DownloadsNavigationButtonClick();

    private void PicturesNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.PicturesNavigationButtonClick();

    private void MusicNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.MusicNavigationButtonClick();

    private void VideosNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.VideosNavigationButtonClick();

    private void FileExplorerNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.FileExplorerNavigationButtonClick();

    private void NetworkNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.NetworkNavigationButtonClick();

    private void SettingsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        _navigationController.SettingsNavigationButtonClick();

    private void AccountSettings_Click(object sender, RoutedEventArgs e) =>
        _navigationController.AccountSettingsClick();

    private void LockSession_Click(object sender, RoutedEventArgs e) =>
        _navigationController.LockSessionClick();

    private void SignOut_Click(object sender, RoutedEventArgs e) =>
        _navigationController.SignOutClick();

    private void Sleep_Click(object sender, RoutedEventArgs e) =>
        _navigationController.SleepClick();

    private void ShutDown_Click(object sender, RoutedEventArgs e) =>
        _navigationController.ShutDownClick();

    private void Restart_Click(object sender, RoutedEventArgs e) =>
        _navigationController.RestartClick();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) =>
        _navigationController.WindowPreviewKeyDown(sender, e);

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        _navigationController.WindowPreviewTextInput(sender, e);

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        _navigationController.SearchBoxTextChanged(sender, e);

    private void LetterHeader_Click(object sender, RoutedEventArgs e) =>
        _navigationController.LetterHeaderClick(sender, e);

    private void AlphabetLetter_Click(object sender, RoutedEventArgs e) =>
        _navigationController.AlphabetLetterClick(sender, e);
}