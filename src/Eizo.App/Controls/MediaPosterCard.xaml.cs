using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Eizo.Controls;

public sealed partial class MediaPosterCard : UserControl
{
    public static readonly DependencyProperty ArtworkProperty =
        DependencyProperty.Register(
            nameof(Artwork),
            typeof(ImageSource),
            typeof(MediaPosterCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph),
            typeof(string),
            typeof(MediaPosterCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(
            nameof(BadgeText),
            typeof(string),
            typeof(MediaPosterCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(MediaPosterCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(MediaPosterCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MetaLineProperty =
        DependencyProperty.Register(
            nameof(MetaLine),
            typeof(string),
            typeof(MediaPosterCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FooterLineProperty =
        DependencyProperty.Register(
            nameof(FooterLine),
            typeof(string),
            typeof(MediaPosterCard),
            new PropertyMetadata(
                string.Empty,
                static (dependencyObject, _) =>
                {
                    if (dependencyObject is MediaPosterCard card)
                        card.UpdateFooterVisibility();
                }));

    public MediaPosterCard()
    {
        InitializeComponent();
        UpdateFooterVisibility();
    }

    public ImageSource? Artwork
    {
        get => (ImageSource?)GetValue(ArtworkProperty);
        set => SetValue(ArtworkProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string MetaLine
    {
        get => (string)GetValue(MetaLineProperty);
        set => SetValue(MetaLineProperty, value);
    }

    public string FooterLine
    {
        get => (string)GetValue(FooterLineProperty);
        set => SetValue(FooterLineProperty, value);
    }

    private void UpdateFooterVisibility()
    {
        if (FooterTextBlock is null)
            return;

        FooterTextBlock.Visibility =
            string.IsNullOrWhiteSpace(FooterLine)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }
}
