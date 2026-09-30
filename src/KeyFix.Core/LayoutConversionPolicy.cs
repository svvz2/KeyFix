namespace KeyFix.Core;

public static class LayoutConversionPolicy
{
    public static ConversionResult ConvertUserRequest(
        ILayoutConverter converter,
        string text,
        ConversionDirection preferredDirection)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(text);

        var result = converter.Convert(text, preferredDirection);
        if (result.Changed)
        {
            return result;
        }

        var oppositeDirection = result.Direction == ConversionDirection.ArabicToEnglish
            ? ConversionDirection.EnglishToArabic
            : ConversionDirection.ArabicToEnglish;
        return converter.Convert(text, oppositeDirection);
    }
}
