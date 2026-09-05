using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Lumina.App;

public partial class IldaContentFolderDialog : Window
{
    public string? ResultFolder { get; private set; }

    public IldaContentFolderDialog(string? initialFolder)
    {
        InitializeComponent();
        FolderBox.Text = initialFolder ?? "";
        Loaded += (_, _) =>
        {
            FolderBox.Focus();
            FolderBox.SelectAll();
        };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var initDir = FolderBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(initDir) && !Directory.Exists(initDir))
            initDir = null;

        var dlg = new OpenFileDialog
        {
            Title = "Choisir un fichier .ild / .fra (pour déduire le dossier)",
            Filter = "ILDA / FRA|*.ild;*.fra|Tous|*.*"
        };
        if (!string.IsNullOrWhiteSpace(initDir))
            dlg.InitialDirectory = initDir;

        if (dlg.ShowDialog(this) != true) return;
        FolderBox.Text = Path.GetDirectoryName(dlg.FileName) ?? "";
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => FolderBox.Text = "";

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            ResultFolder = null;
            DialogResult = true;
            return;
        }

        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Le dossier indiqué n'existe pas.", "Dossier ILDA",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultFolder = folder;
        DialogResult = true;
    }
}

