using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace KeyFix.Windows;

public sealed class TextSelectionDetector
{
    public bool HasSelectedText(nint targetWindowHandle, int screenX, int screenY)
    {
        if (targetWindowHandle == nint.Zero)
        {
            return false;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(targetWindowHandle, out var targetProcessId);
            var pointedElement = AutomationElement.FromPoint(new System.Windows.Point(screenX, screenY));
            if (ElementOrAncestorHasSelectedText(pointedElement, targetProcessId))
            {
                return true;
            }

            var focused = AutomationElement.FocusedElement;
            return ElementOrAncestorHasSelectedText(focused, targetProcessId);
        }
        catch (ElementNotAvailableException)
        {
            // The target control disappeared while the right-click was being handled.
        }
        catch (InvalidOperationException)
        {
            // Some custom controls advertise TextPattern but cannot return a selection.
        }
        catch (UnauthorizedAccessException)
        {
            // Elevated applications cannot be inspected from a normal process.
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // A cross-process UI Automation provider stopped responding or went away.
        }

        return false;
    }

    private static bool ElementOrAncestorHasSelectedText(
        AutomationElement? element,
        uint targetProcessId)
    {
        for (var depth = 0; element is not null && depth < 8; depth++)
        {
            if (element.Current.ProcessId != targetProcessId)
            {
                return false;
            }

            if (element.Current.IsPassword)
            {
                return false;
            }

            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject) &&
                patternObject is TextPattern textPattern)
            {
                foreach (var range in textPattern.GetSelection())
                {
                    var lengthComparison = range.CompareEndpoints(
                        TextPatternRangeEndpoint.Start,
                        range,
                        TextPatternRangeEndpoint.End);
                    if (lengthComparison != 0 && range.GetText(1).Length > 0)
                    {
                        return true;
                    }
                }
            }

            element = TreeWalker.ControlViewWalker.GetParent(element);
        }

        return false;
    }
}
