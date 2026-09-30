using System.Windows.Automation;

namespace KeyFix.Windows;

public sealed class PasswordFieldGuard
{
    public bool IsPasswordField()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            return focused is not null && focused.Current.IsPassword;
        }
        catch
        {
            return false;
        }
    }
}
