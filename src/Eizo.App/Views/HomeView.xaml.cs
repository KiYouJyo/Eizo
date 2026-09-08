using Eizo.Localization; using Eizo.Models; using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls;
namespace Eizo.Views;
public sealed partial class HomeView:UserControl
{
 private readonly AppLocalizationService _l=AppLocalizationService.Default; public event EventHandler<string>? DetailRequested; public event EventHandler<string>? PlayRequested;
 public HomeView(){InitializeComponent();ApplyText();ContinueRepeater.ItemsSource=new[]{new MediaCardModel("葬送的芙莉莲","葬送のフリーレン","18 / 28",52),new MediaCardModel("药屋少女的呢喃","薬屋のひとりごと","14 / 24",36),new MediaCardModel("VIVANT","VIVANT","6 / 10",64),new MediaCardModel("非自然死亡","アンナチュラル","5 / 10",44)};SeasonRepeater.ItemsSource=new[]{new MediaCardModel("胆大党 第3期","ダンダダン","2026 秋"),new MediaCardModel("间谍过家家 第4期","SPY×FAMILY","2026 秋"),new MediaCardModel("链锯人","チェンソーマン","更新中"),new MediaCardModel("Re:从零开始","Re:ゼロから始める異世界生活","更新中"),new MediaCardModel("蓝色监狱","ブルーロック","更新中")};}
 private string T(string k)=>_l.GetString(k);
 private void ApplyText(){PageTitle.Text=T("Nav_Home");PageSubtitle.Text=T("Home_Subtitle");SearchBox.PlaceholderText=T("Search_Placeholder");FeaturedEyebrow.Text=T("Home_Featured");FeaturedDescription.Text=T("Home_FeaturedDescription");FeaturedPlayText.Text=T("Common_Play");FeaturedDetailsButton.Content=T("Common_ViewDetails");ContinueTitle.Text=T("Section_ContinueWatching");SeasonTitle.Text=T("Section_CurrentSeason");ContinueAllButton.Content=T("Common_ViewAll");SeasonAllButton.Content=T("Common_ViewAll");}
 private void FeaturedButton_Click(object s,RoutedEventArgs e)=>DetailRequested?.Invoke(this,"葬送的芙莉莲");
 private void FeaturedPlayButton_Click(object s,RoutedEventArgs e){PlayRequested?.Invoke(this,"葬送的芙莉莲");}
 private void MediaCard_Click(object s,RoutedEventArgs e){if(s is Button{Tag:string title})DetailRequested?.Invoke(this,title);}
}
