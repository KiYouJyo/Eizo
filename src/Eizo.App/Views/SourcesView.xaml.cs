using Eizo.Localization; using Eizo.Models; using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls;
namespace Eizo.Views;
public sealed partial class SourcesView:UserControl
{
 private readonly AppLocalizationService _l=AppLocalizationService.Default;
 public SourcesView(){InitializeComponent();ApplyText();SourceList.ItemsSource=new[]{new SourceItemModel(T("Source_Local")+" · Anime","1,248 · 2.8 TB","local"),new SourceItemModel("WebDAV · NAS","3,612 · 8.4 TB","webdav"),new SourceItemModel("OneDrive · Personal","824 · 1.2 TB","onedrive"),new SourceItemModel("Google Drive","—","gdrive")};}
 private string T(string k)=>_l.GetString(k);
 private void ApplyText(){PageTitle.Text=T("Nav_Sources");PageSubtitle.Text=T("Sources_Subtitle");AddSourceButton.Content=T("Source_Add");SourcesTab.Content=T("Sources_TabSources");ScanTab.Content=T("Sources_TabScan");}
 private void SourceList_ContainerContentChanging(ListViewBase sender,ContainerContentChangingEventArgs args){if(args.ItemContainer is not ListViewItem container||args.Item is not SourceItemModel item)return;var f=new MenuFlyout();Add(f,T("Common_Edit"),"\uE70F");Add(f,T("Source_ScanNow"),"\uE72C");Add(f,T("Source_ScanHistory"),"\uE81C");f.Items.Add(new MenuFlyoutSeparator());Add(f,T("Source_Disconnect"),"\uE8CD");Add(f,T("Common_Remove"),"\uE711");container.ContextFlyout=f;container.Tag=item;}
 private static void Add(MenuFlyout f,string text,string glyph)=>f.Items.Add(new MenuFlyoutItem{Text=text,Icon=new FontIcon{Glyph=glyph}});
 private async void AddSourceButton_Click(object s,RoutedEventArgs e){var provider=new ComboBox{Header=T("Sources_Provider"),HorizontalAlignment=HorizontalAlignment.Stretch,ItemsSource=new[]{T("Source_Local"),"WebDAV","OneDrive","Google Drive"},SelectedIndex=1};var stack=new StackPanel{Spacing=12,MinWidth=420};stack.Children.Add(provider);stack.Children.Add(new TextBox{Header=T("Sources_DisplayName"),PlaceholderText="NAS"});stack.Children.Add(new TextBox{Header=T("Sources_Address"),PlaceholderText="https://"});stack.Children.Add(new TextBox{Header=T("Sources_Username")});stack.Children.Add(new PasswordBox{Header=T("Sources_Password")});await new ContentDialog{XamlRoot=XamlRoot,Title=T("Source_Add"),PrimaryButtonText=T("Common_Add"),CloseButtonText=T("Common_Cancel"),DefaultButton=ContentDialogButton.Primary,Content=stack}.ShowAsync();}
}
