using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Eizo;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Eizo";
        AppWindow.Resize(new SizeInt32(900, 600));
    }
}
