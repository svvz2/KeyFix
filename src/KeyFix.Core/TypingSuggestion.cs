namespace KeyFix.Core;

public sealed record TypingSuggestion(
    string OriginalText,
    string SuggestedText,
    ConversionDirection Direction,
    double Confidence);
