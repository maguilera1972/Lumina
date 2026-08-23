using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Lumina.App;

public partial class App : Application
{
    public App()
    {
        DarkTitleBar.Hook();
        DispatcherUnhandledException += OnUnhandled;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var splash = new SplashWindow();
            await splash.RunAsync();
            splash.Close();
        }
        catch
        {
            /* splash optional */
        }

        var main = new MainWindow { Opacity = 0 };
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(550))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        main.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.GetBaseException().Message,
            "Lumina — erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
