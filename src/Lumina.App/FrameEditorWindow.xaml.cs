using System.Windows;
using Lumina.Core;

namespace Lumina.App;

public partial class FrameEditorWindow : Window
{
    public event Action<PatternItem>? PatternSaved;

    public FrameEditorWindow()
    {
        InitializeComponent();
        Editor.SaveRequested += OnSaveRequested;
        Closed += (_, _) => Editor.SaveRequested -= OnSaveRequested;
    }

    public void NewSession(string firstFrameName = "Frame 1") =>
        Editor.NewSession(firstFrameName);

    public void LoadPattern(PatternItem pattern) =>
        Editor.LoadPattern(pattern);

    private void OnSaveRequested(PatternItem pattern)
    {
        PatternSaved?.Invoke(pattern);
    }

    private void StayOnTop_Changed(object sender, RoutedEventArgs e) =>
        Topmost = StayOnTopBox.IsChecked == true;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
