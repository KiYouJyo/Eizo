using Eizo.Localization; using Eizo.Models; using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls;
namespace Eizo.Views;
public sealed partial class CacheView:UserControl
{
 private readonly AppLocalizationService _l=AppLocalizationService.Default;
 public CacheView(){InitializeComponent();ApplyText();CacheList.ItemsSource=new[]{new CacheItemModel("葬送的芙莉莲 · 第18话","WebDAV · NAS","1.8 GB",T("Common_Now")),new CacheItemModel("药屋少女的呢喃 · 第14话","OneDrive","1.2 GB","18 min"),new CacheItemModel("VIVANT · 第6话","WebDAV · NAS","2.4 GB","2 h")};}
 private string T(string k)=>_l.GetString(k);
 private void ApplyText(){PageTitle.Text=T("Nav_Cache");PageSubtitle.Text=T("Cache_Subtitle");OverviewTitle.Text=T("Cache_Overview");OverviewLimit.Text=T("Cache_OverviewLimit");ClearCacheButton.Content=T("Cache_Clear");PolicyTitle.Text=T("Cache_Policy");AutoCleanupTitle.Text=T("Cache_AutoCleanup");AutoCleanupDescription.Text=T("Cache_AutoCleanupDescription");PrecacheTitle.Text=T("Cache_Precache");PrecacheDescription.Text=T("Cache_PrecacheDescription");KeepOfflineTitle.Text=T("Cache_KeepOffline");KeepOfflineDescription.Text=T("Cache_KeepOfflineDescription");ContentsTitle.Text=T("Cache_Contents");}
 private void DeleteCacheItem_Click(object s,RoutedEventArgs e){}
}
