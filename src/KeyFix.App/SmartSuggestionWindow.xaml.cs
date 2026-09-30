using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using KeyFix.Core;
using Forms = System.Windows.Forms;

namespace KeyFix.App;

public partial class SmartSuggestionWindow : Window
{
    private readonly DispatcherTimer _autoHideTimer;
    private SmartSuggestionActionEventArgs? _current;

    public SmartSuggestionWindow()
    {
        InitializeComponent();
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _autoHideTimer.Tick += (_, _) => Dismiss();
    }

    public event EventHandler<SmartSuggestionActionEventArgs>? Accepted;

    public event EventHandler? Dismissed;

    public void ShowSuggestion(TypingSuggestion suggestion, string delimiter, nint targetWindowHandle)
    {
        _current = new SmartSuggestionActionEventArgs(suggestion, delimiter, targetWindowHandle);
        SuggestedTextBlock.Text = suggestion.SuggestedText;

        var screen = Forms.Screen.FromHandle(targetWindowHandle).WorkingArea;
        var dpi = GetDpiForWindow(targetWindowHandle);
        var scale = dpi > 0 ? dpi / 96d : 1d;
        Left = screen.Right / scale - Width - 16;
        Top = screen.Bottom / scale - Height - 16;

        if (!IsVisible)
        {
            Show();
        }

        _autoHideTimer.Stop();
        _autoHideTimer.Start();
    }

    public void Dismiss(bool notify = true)
    {
        var hadSuggestion = _current is not null;
        _current = null;
        _autoHideTimer.Stop();
        Hide();
        if (notify && hadSuggestion)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClosePermanently()
    {
        _autoHideTimer.Stop();
        _current = null;
        Close();
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        var action = _current;
        Dismiss(notify: false);
        if (action is not null)
        {
            Accepted?.Invoke(this, action);
        }
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e) => Dismiss();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}

public sealed record SmartSuggestionActionEventArgs(
    TypingSuggestion Suggestion,
    string Delimiter,
    nint TargetWindowHandle);
