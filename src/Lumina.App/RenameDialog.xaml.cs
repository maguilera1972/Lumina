using System.Windows;
using System.Windows.Input;

namespace Lumina.App;

public partial class RenameDialog : Window
{
    public string ResultName { get; private set; } = "";

    public RenameDialog(string initialName = "")
    {
        InitializeComponent();
        NameBox.Text = initialName;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultName = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ResultName)) return;
        DialogResult = true;
    }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) Ok_Click(sender, new RoutedEventArgs());
    }
}
