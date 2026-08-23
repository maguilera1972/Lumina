using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Lumina.App;

internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Hook()
    {
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window) Apply(window);
    }

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var dark = 1;
        Set(hwnd, DwmwaUseImmersiveDarkMode, dark);
        Set(hwnd, DwmwaUseImmersiveDarkModeLegacy, dark);
        Set(hwnd, DwmwaCaptionColor, 0x00120B07);
        Set(hwnd, DwmwaTextColor, 0x00FFFBF5);
        Set(hwnd, DwmwaBorderColor, 0x00786A1E);
    }

    private static void Set(IntPtr hwnd, int attribute, int value) =>
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
}
