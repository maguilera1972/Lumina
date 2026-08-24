using System.Windows;
using Lumina.Core;

namespace Lumina.App;

public partial class PatternBrowserWindow : Window
{
    public event Action? DockRequested;
    public event Action? ImportRequested;
    public event Action<PatternItem>? PlaceRequested;
    public event Action<PatternItem>? PatternSelected;

    public PatternBrowserWindow()
    {
        InitializeComponent();
        Browser.DetachRequested += () => Close();
        Browser.ImportClicked += () => ImportRequested?.Invoke();
        Browser.PlaceRequested += pattern => PlaceRequested?.Invoke(pattern);
        Browser.PatternSelected += pattern => PatternSelected?.Invoke(pattern);
        Closed += (_, _) => DockRequested?.Invoke();
    }

    public void Bind(LaserShow? show, ColorConversionProfile? colors)
    {
        Browser.Bind(show, colors);
    }

    public void SetColors(ColorConversionProfile? colors) => Browser.SetColors(colors);

    public void SelectById(string? id) => Browser.SelectById(id);

    private void StayOnTop_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = StayOnTopBox.IsChecked == true;
    }

    private void Dock_Click(object sender, RoutedEventArgs e) => Close();
}
