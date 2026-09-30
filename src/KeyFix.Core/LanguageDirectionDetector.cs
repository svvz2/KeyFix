namespace KeyFix.Core;

public static class LanguageDirectionDetector
{
    public static ConversionDirection Detect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var latin = 0;
        var arabic = 0;

        foreach (var character in text)
        {
            if (IsArabic(character))
            {
                arabic++;
            }
            else if (IsLatin(character))
            {
                latin++;
            }
        }

        return arabic > latin
            ? ConversionDirection.ArabicToEnglish
            : ConversionDirection.EnglishToArabic;
    }

    public static bool IsArabic(char character) =>
        character is >= '\u0600' and <= '\u06FF' or
        >= '\u0750' and <= '\u077F' or
        >= '\u08A0' and <= '\u08FF' or
        >= '\uFB50' and <= '\uFDFF' or
        >= '\uFE70' and <= '\uFEFF';

    private static bool IsLatin(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
