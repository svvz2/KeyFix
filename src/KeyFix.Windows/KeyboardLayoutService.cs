using System.Globalization;
using KeyFix.Core;

namespace KeyFix.Windows;

public sealed class KeyboardLayoutService
{
    public string GetCurrentLayoutName(ForegroundWindowInfo window)
    {
        var layout = NativeMethods.GetKeyboardLayout(window.ThreadId);
        var languageId = unchecked((ushort)(long)layout);
        try
        {
            return CultureInfo.GetCultureInfo(languageId).DisplayName;
        }
        catch
        {
            return $"0x{languageId:X4}";
        }
    }

    public bool SwitchForDirection(ForegroundWindowInfo window, ConversionDirection direction)
    {
        var targetPrimaryLanguage = direction == ConversionDirection.EnglishToArabic ? 0x01 : 0x09;
        var count = NativeMethods.GetKeyboardLayoutList(0, null);
        if (count <= 0)
        {
            return false;
        }

        var layouts = new nint[count];
        NativeMethods.GetKeyboardLayoutList(layouts.Length, layouts);
        var target = layouts.FirstOrDefault(layout => GetPrimaryLanguage(layout) == targetPrimaryLanguage);
        return target != nint.Zero &&
               NativeMethods.PostMessage(window.Handle, NativeMethods.WmInputLanguageChangeRequest, nint.Zero, target);
    }

    private static int GetPrimaryLanguage(nint layout)
    {
        var languageId = unchecked((ushort)(long)layout);
        return languageId & 0x03FF;
    }
}
