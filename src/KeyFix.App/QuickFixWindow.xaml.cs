using System.Windows;
using System.Windows.Threading;

namespace KeyFix.App;

public partial class QuickFixWindow : Window
{
    private readonly DispatcherTimer _autoHideTimer;
    private nint _targetWindowHandle;

    public QuickFixWindow()
    {
        InitializeComponent();
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _autoHideTimer.Tick += (_, _) => HideAction();
    }

    public event Action<nint>? CorrectionRequested;

    public void ShowAt(int screenX, int screenY, nint targetWindowHandle)
    {
        _targetWindowHandle = targetWindowHandle;

        var workArea = SystemParameters.WorkArea;
        var proposedLeft = screenX + 12;
        var proposedTop = screenY - Height - 10;
        if (proposedTop < workArea.Top + 8)
        {
            proposedTop = screenY + 14;
        }
        Left = Math.Max(workArea.Left + 8, Math.Min(proposedLeft, workArea.Right - Width - 8));
        Top = Math.Max(workArea.Top + 8, Math.Min(proposedTop, workArea.Bottom - Height - 8));

        if (!IsVisible)
        {
            Show();
        }

        _autoHideTimer.Stop();
        _autoHideTimer.Start();
    }

    public void HideAction()
    {
        _autoHideTimer.Stop();
        Hide();
    }

    public void ClosePermanently()
    {
        _autoHideTimer.Stop();
        Close();
    }

    private void FixLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        var target = _targetWindowHandle;
        HideAction();
        CorrectionRequested?.Invoke(target);
    }
}
