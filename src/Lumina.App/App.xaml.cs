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
        var msg = e.Exception.GetBaseException().Message;
        // Cette erreur venait souvent d'anciens sidecars / faux .ild : ne pas laisser planter la session.
        if (msg.Contains("en-tête ILDA manquant", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Un fichier a été pris pour de l’ILDA alors qu’il ne l’est pas.\n\n" +
                "Fermez Lumina, relancez la version à jour, puis rouvrez le show (.mrs).\n" +
                "Si le problème continue, supprimez le fichier « *.lumina.json » à côté du show.",
                "Lumina — fichier non ILDA",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Handled = true;
            return;
        }

        MessageBox.Show(
            msg,
            "Lumina — erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
