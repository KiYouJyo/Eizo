using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class PlayerView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;

    public PlayerView(string title, string episode)
    {
        InitializeComponent();

        NowPlayingTitle.Text = title + " · " + episode;
        NowPlayingEpisode.Text = "一级魔法使考试";

        QueueList.ItemsSource = new[]
        {
            new EpisodeItemModel("18", "一级魔法使考试", "一級魔法使試験", "23:41", T("Playback_Playing")),
            new EpisodeItemModel("19", "周密的计划", "入念な計画", "24:03", T("Category_Unwatched")),
            new EpisodeItemModel("20", "必要的杀戮", "必要な殺し", "23:58", T("Category_Unwatched")),
            new EpisodeItemModel("21", "魔法的世界", "魔法の世界", "24:11", T("Category_Unwatched"))
        };

        ApplyText();
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        SubtitleQuickButton.Content = T("Playback_SubtitleTrack");
        AudioQuickButton.Content = T("Playback_AudioTrack");
        QueueTitle.Text = T("Playback_Queue");
        QueueSubtitle.Text = T("Section_Anime");
        PlaybackInfoTitle.Text = T("Playback_Info");

        PlayerSectionList.ItemsSource = new[]
        {
            T("Playback_Queue"),
            T("Playback_Tracks")
        };
    }
}
