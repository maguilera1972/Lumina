using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Lumina.App;

public partial class SplashWindow : Window
{
    private readonly TaskCompletionSource _skip = new();

    public SplashWindow()
    {
        InitializeComponent();
        Opacity = 0;
        FitToScreen();
        SizeChanged += (_, _) =>
        {
            Chrome.Clip = new RectangleGeometry(
                new Rect(0, 0, ActualWidth, ActualHeight), 20, 20);
        };
    }

    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea;
        var side = Math.Min(780, Math.Min(area.Width, area.Height) * 0.78);
        Width = side;
        Height = side;
    }

    public async Task RunAsync()
    {
        Show();
        Activate();
        await AnimateOpacity(0, 1, 650);
        await Task.WhenAny(Task.Delay(2300), _skip.Task);
        await AnimateOpacity(1, 0, 700);
    }

    private void Skip() => _skip.TrySetResult();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.Space)
        {
            Skip();
            e.Handled = true;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Skip();

    private Task AnimateOpacity(double from, double to, int milliseconds)
    {
        var done = new TaskCompletionSource();
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        anim.Completed += (_, _) => done.TrySetResult();
        BeginAnimation(OpacityProperty, anim);
        return done.Task;
    }
}
