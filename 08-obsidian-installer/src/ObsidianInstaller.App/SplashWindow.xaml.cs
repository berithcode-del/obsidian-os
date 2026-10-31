using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ObsidianInstaller.App;

/// <summary>
/// Splash do instalador usando a mesma arte da splash do console
/// (gameman-assets/library/opening.png) com tipografia em serifa.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LoadPlate();
            FadeIn();
        };
    }

    /// <summary>Mensagem exibida na barra inferior.</summary>
    public void SetStatus(string text) => Dispatcher.Invoke(() => Status.Text = text);

    private void LoadPlate()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "splash.png");
            if (!File.Exists(path))
                return;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            Plate.Source = image;
        }
        catch (Exception ex) when (ex is IOException or UriFormatException or NotSupportedException)
        {
        }
    }

    private void FadeIn()
    {
        var animation = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260));
        BeginAnimation(OpacityProperty, animation);
    }
}
