namespace KeyFix.Windows;

public enum CorrectionStatus
{
    Success,
    Disabled,
    ExcludedApplication,
    PasswordField,
    NoSelection,
    NoChange,
    UndoUnavailable,
    Failed
}

public sealed record CorrectionOutcome(CorrectionStatus Status, string Message, string? CorrectedText = null)
{
    public bool IsSuccess => Status == CorrectionStatus.Success;
}
