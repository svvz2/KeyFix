using KeyFix.Core;
using System.IO;

namespace KeyFix.Windows;

public sealed class TextCorrectionService(
    ILayoutConverter converter,
    ForegroundWindowService foregroundWindows,
    KeyboardInputService keyboardInput,
    ClipboardTextService clipboard,
    KeyboardLayoutService keyboardLayouts,
    PasswordFieldGuard passwordGuard)
{
    private CorrectionTransaction? _lastTransaction;

    public async Task<CorrectionOutcome> FixSelectionAsync(
        AppSettings settings,
        bool selectPreviousWord = false,
        ConversionDirection? directionOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsEnabled)
        {
            return new CorrectionOutcome(CorrectionStatus.Disabled, "KeyFix متوقف مؤقتاً");
        }

        var window = foregroundWindows.GetCurrent();
        if (window is null)
        {
            return new CorrectionOutcome(CorrectionStatus.Failed, "لا توجد نافذة نشطة");
        }

        if (IsExcluded(settings, window.ProcessName))
        {
            return new CorrectionOutcome(CorrectionStatus.ExcludedApplication, $"التصحيح متوقف داخل {window.ProcessName}");
        }

        if (passwordGuard.IsPasswordField())
        {
            return new CorrectionOutcome(CorrectionStatus.PasswordField, "لن يتم التصحيح داخل حقول كلمات المرور");
        }

        try
        {
            if (selectPreviousWord)
            {
                keyboardInput.SelectPreviousWord();
                await Task.Delay(35, cancellationToken);
            }

            using var selected = await clipboard.ReadSelectionAsync(cancellationToken);
            if (string.IsNullOrEmpty(selected.Text))
            {
                if (selectPreviousWord)
                {
                    keyboardInput.CollapseSelectionToEnd();
                }

                return new CorrectionOutcome(CorrectionStatus.NoSelection, "حدد نصاً أولاً، أو ضع المؤشر بعد الكلمة");
            }

            var result = converter.Convert(selected.Text, directionOverride ?? settings.PreferredDirection);
            if (!result.Changed || result.ConvertedCharacters == 0)
            {
                keyboardInput.CollapseSelectionToEnd();
                return new CorrectionOutcome(CorrectionStatus.NoChange, "النص لا يحتاج إلى تحويل");
            }

            keyboardInput.SendUnicodeText(result.ConvertedText);
            _lastTransaction = new CorrectionTransaction(
                selected.Text,
                result.ConvertedText,
                result.Direction,
                DateTimeOffset.UtcNow,
                window.Handle);

            if (settings.SwitchLayoutAfterCorrection)
            {
                keyboardLayouts.SwitchForDirection(window, result.Direction);
            }

            await Task.Delay(40, cancellationToken);
            return new CorrectionOutcome(CorrectionStatus.Success, "تم تصحيح النص", result.ConvertedText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CorrectionOutcome(CorrectionStatus.Failed, exception.Message);
        }
    }

    public CorrectionOutcome Undo()
    {
        var transaction = _lastTransaction;
        var currentWindow = foregroundWindows.GetCurrent();
        if (transaction is null || currentWindow is null ||
            currentWindow.Handle != transaction.WindowHandle ||
            DateTimeOffset.UtcNow - transaction.Timestamp > TimeSpan.FromMinutes(2))
        {
            return new CorrectionOutcome(CorrectionStatus.UndoUnavailable, "لا توجد عملية حديثة يمكن التراجع عنها");
        }

        try
        {
            keyboardInput.SelectPreviousCharacters(transaction.CorrectedText.Length);
            keyboardInput.SendUnicodeText(transaction.OriginalText);
            _lastTransaction = null;
            return new CorrectionOutcome(CorrectionStatus.Success, "تم التراجع عن التصحيح", transaction.OriginalText);
        }
        catch (Exception exception)
        {
            return new CorrectionOutcome(CorrectionStatus.Failed, exception.Message);
        }
    }

    public bool CanOfferRightClickAction(AppSettings settings, nint targetWindowHandle)
    {
        if (!settings.IsEnabled || !settings.ShowRightClickAction || targetWindowHandle == nint.Zero)
        {
            return false;
        }

        var window = foregroundWindows.Get(targetWindowHandle);
        return window is not null &&
               !window.ProcessName.StartsWith("KeyFix", StringComparison.OrdinalIgnoreCase) &&
               !IsExcluded(settings, window.ProcessName) &&
               !passwordGuard.IsPasswordField();
    }

    public bool ActivateWindow(nint windowHandle) => foregroundWindows.Activate(windowHandle);

    public bool CanMonitorTyping(AppSettings settings, nint targetWindowHandle)
    {
        if (!settings.IsEnabled || !settings.EnableSmartSuggestions || targetWindowHandle == nint.Zero)
        {
            return false;
        }

        var window = foregroundWindows.Get(targetWindowHandle);
        return window is not null &&
               !window.ProcessName.StartsWith("KeyFix", StringComparison.OrdinalIgnoreCase) &&
               !IsExcluded(settings, window.ProcessName) &&
               !passwordGuard.IsPasswordField();
    }

    public async Task<CorrectionOutcome> FixRecentTextAsync(
        AppSettings settings,
        nint targetWindowHandle,
        string originalText,
        string replacementText,
        string delimiter,
        ConversionDirection direction,
        CancellationToken cancellationToken = default)
    {
        if (!CanMonitorTyping(settings, targetWindowHandle))
        {
            return new CorrectionOutcome(CorrectionStatus.Failed, "لم يعد موضع الكتابة متاحاً");
        }

        try
        {
            if (!foregroundWindows.Activate(targetWindowHandle))
            {
                return new CorrectionOutcome(CorrectionStatus.Failed, "تعذر الرجوع إلى التطبيق الأصلي");
            }

            await Task.Delay(120, cancellationToken);
            if (foregroundWindows.GetCurrent()?.Handle != targetWindowHandle || passwordGuard.IsPasswordField())
            {
                return new CorrectionOutcome(CorrectionStatus.Failed, "تغيّر موضع الكتابة قبل التصحيح");
            }

            var original = originalText + delimiter;
            var corrected = replacementText + delimiter;
            keyboardInput.DeletePreviousCharacters(original.Length);
            keyboardInput.SendUnicodeText(corrected);
            _lastTransaction = new CorrectionTransaction(
                original,
                corrected,
                direction,
                DateTimeOffset.UtcNow,
                targetWindowHandle);

            if (settings.SwitchLayoutAfterCorrection)
            {
                var window = foregroundWindows.Get(targetWindowHandle);
                if (window is not null)
                {
                    keyboardLayouts.SwitchForDirection(window, direction);
                }
            }

            return new CorrectionOutcome(CorrectionStatus.Success, "تم تطبيق الاقتراح", replacementText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CorrectionOutcome(CorrectionStatus.Failed, exception.Message);
        }
    }

    private static bool IsExcluded(AppSettings settings, string processName) =>
        settings.ExcludedApplications.Any(name =>
            string.Equals(Path.GetFileNameWithoutExtension(name), processName, StringComparison.OrdinalIgnoreCase));
}
