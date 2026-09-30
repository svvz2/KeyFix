namespace KeyFix.Core;

public sealed record ConversionResult(
    string OriginalText,
    string ConvertedText,
    ConversionDirection Direction,
    int ConvertedCharacters,
    int PreservedCharacters)
{
    public bool Changed => !string.Equals(OriginalText, ConvertedText, StringComparison.Ordinal);
}
