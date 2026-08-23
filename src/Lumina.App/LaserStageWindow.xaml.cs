using System.Windows;
using System.Windows.Input;
using Lumina.Core;

namespace Lumina.App;

public partial class LaserStageWindow : Window
{
    public event Action? DockRequested;

    public LaserStageWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => DockRequested?.Invoke();
    }

    public void ShowFrame(LaserFrame? frame) => StagePreview.ShowFrame(frame);

    public void Clear() => StagePreview.Clear(showHint: true);

    private void StayOnTop_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = StayOnTopBox.IsChecked == true;
    }

    private void Dock_Click(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (Application.Current.MainWindow is MainWindow main)
        {
            main.RequestEmergencyStop();
            e.Handled = true;
        }
    }
}
