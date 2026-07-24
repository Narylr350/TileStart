namespace TileStart.Host.Navigation;

public sealed class NavigationPreferences
{
    public bool ShowUser { get; set; } = true;

    public bool ShowDocuments { get; set; } = true;

    public bool ShowDownloads { get; set; }

    public bool ShowPictures { get; set; } = true;

    public bool ShowFileExplorer { get; set; }

    public bool ShowSettings { get; set; } = true;

    public bool ShowMusic { get; set; }

    public bool ShowVideos { get; set; }

    public bool ShowNetwork { get; set; }

    public bool IsVisible(string key) => key switch
    {
        nameof(ShowUser) => ShowUser,
        nameof(ShowDocuments) => ShowDocuments,
        nameof(ShowDownloads) => ShowDownloads,
        nameof(ShowPictures) => ShowPictures,
        nameof(ShowFileExplorer) => ShowFileExplorer,
        nameof(ShowSettings) => ShowSettings,
        nameof(ShowMusic) => ShowMusic,
        nameof(ShowVideos) => ShowVideos,
        nameof(ShowNetwork) => ShowNetwork,
        _ => false,
    };

    public void SetVisible(string key, bool value)
    {
        switch (key)
        {
            case nameof(ShowUser):
                ShowUser = value;
                break;
            case nameof(ShowDocuments):
                ShowDocuments = value;
                break;
            case nameof(ShowDownloads):
                ShowDownloads = value;
                break;
            case nameof(ShowPictures):
                ShowPictures = value;
                break;
            case nameof(ShowFileExplorer):
                ShowFileExplorer = value;
                break;
            case nameof(ShowSettings):
                ShowSettings = value;
                break;
            case nameof(ShowMusic):
                ShowMusic = value;
                break;
            case nameof(ShowVideos):
                ShowVideos = value;
                break;
            case nameof(ShowNetwork):
                ShowNetwork = value;
                break;
        }
    }
}