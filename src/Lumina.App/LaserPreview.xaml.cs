using System.Windows.Controls;
using Lumina.Core;

namespace Lumina.App;

public partial class LaserPreview : UserControl
{
    private LaserFrame? _frame;
    private bool _showHint = true;

    public LaserPreview()
    {
        InitializeComponent();
        UpdateHint();
    }

    public void ShowFrame(LaserFrame? frame)
    {
        _frame = frame;
        Stage.Frame = frame;
        UpdateHint();
    }

    public void Clear(bool showHint = true)
    {
        _frame = null;
        _showHint = showHint;
        Stage.Frame = null;
        UpdateHint();
    }

    public void SetHintVisible(bool visible)
    {
        _showHint = visible;
        UpdateHint();
    }

    private void UpdateHint() =>
        HintText.Visibility = _showHint && _frame is null
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
}
