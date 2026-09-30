namespace KeyFix.Core;

public sealed record CorrectionTransaction(
    string OriginalText,
    string CorrectedText,
    ConversionDirection Direction,
    DateTimeOffset Timestamp,
    nint WindowHandle);
