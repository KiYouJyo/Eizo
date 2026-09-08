using Eizo.Localization; using Eizo.Models; using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls;
namespace Eizo.Views;
public sealed partial class DetailView:UserControl
{
 private readonly AppLocalizationService _l=AppLocalizationService.Default; public event EventHandler<string>? PlayRequested;
 public DetailView(string title){InitializeComponent();ApplyText();TitleText.Text=title;NativeTitleText.Text=title=="葬送的芙莉莲"?"葬送のフリーレン":title;EpisodeList.ItemsSource=new[]{new EpisodeItemModel("18","一级魔法使考试","一級魔法使試験","23:41","12:08",52),new EpisodeItemModel("19","周密的计划","入念な計画","24:03",T("Category_Unwatched")),new EpisodeItemModel("20","必要的杀戮","必要な殺し","23:58",T("Category_Unwatched")),new EpisodeItemModel("21","魔法的世界","魔法の世界","24:11",T("Category_Unwatched"))};}
 private string T(string k)=>_l.GetString(k);
 private void ApplyText(){OverviewText.Text=T("Detail_Overview");PlayButton.Content=T("Common_Continue");FavoriteButton.Content=T("Common_Favorite");EpisodesTitle.Text=T("Media_Episodes");CastTitle.Text=T("Media_Cast");StaffTitle.Text=T("Media_Staff");OriginalLabel.Text=T("Media_SourceMaterial");DirectorLabel.Text=T("Detail_Director");StudioLabel.Text=T("Media_Studio");SeriesCompositionLabel.Text=T("Detail_SeriesComposition");}
 private void PlayButton_Click(object s,RoutedEventArgs e)=>PlayRequested?.Invoke(this,"第18话");
}
