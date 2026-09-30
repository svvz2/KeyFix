using System.Collections.ObjectModel;
using System.Text;

namespace KeyFix.Core;

/// <summary>
/// Converts text as if the same physical keys were entered on English US or Arabic 101.
/// Uppercase Latin letters intentionally use the base Arabic letter to make recovery from
/// accidental Caps Lock predictable. Unsupported characters are preserved.
/// </summary>
public sealed class Arabic101LayoutConverter : ILayoutConverter
{
    private static readonly IReadOnlyDictionary<char, string> EnglishToArabic =
        new ReadOnlyDictionary<char, string>(CreateEnglishToArabicMap());

    private static readonly IReadOnlyDictionary<char, string> ArabicToEnglish =
        new ReadOnlyDictionary<char, string>(CreateArabicToEnglishMap());

    public ConversionResult Convert(string text, ConversionDirection direction = ConversionDirection.Auto)
    {
        ArgumentNullException.ThrowIfNull(text);

        var resolvedDirection = direction == ConversionDirection.Auto
            ? LanguageDirectionDetector.Detect(text)
            : direction;

        var map = resolvedDirection == ConversionDirection.ArabicToEnglish
            ? ArabicToEnglish
            : EnglishToArabic;

        var converted = 0;
        var preserved = 0;
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (map.TryGetValue(character, out var replacement))
            {
                builder.Append(replacement);
                converted++;
            }
            else
            {
                builder.Append(character);
                preserved++;
            }
        }

        return new ConversionResult(text, builder.ToString(), resolvedDirection, converted, preserved);
    }

    private static Dictionary<char, string> CreateEnglishToArabicMap()
    {
        const string english = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./";
        string[] arabic = ["ذ", "ض", "ص", "ث", "ق", "ف", "غ", "ع", "ه", "خ", "ح", "ج", "د", "ش", "س", "ي", "ب", "ل", "ا", "ت", "ن", "م", "ك", "ط", "ئ", "ء", "ؤ", "ر", "لا", "ى", "ة", "و", "ز", "ظ"];

        var result = new Dictionary<char, string>();
        for (var index = 0; index < english.Length; index++)
        {
            result[english[index]] = arabic[index];
            if (char.IsLetter(english[index]))
            {
                result[char.ToUpperInvariant(english[index])] = arabic[index];
            }
        }

        result['?'] = "؟";
        return result;
    }

    private static Dictionary<char, string> CreateArabicToEnglishMap() => new()
    {
        ['ذ'] = "`", ['ض'] = "q", ['ص'] = "w", ['ث'] = "e", ['ق'] = "r",
        ['ف'] = "t", ['غ'] = "y", ['ع'] = "u", ['ه'] = "i", ['خ'] = "o",
        ['ح'] = "p", ['ج'] = "[", ['د'] = "]", ['ش'] = "a", ['س'] = "s",
        ['ي'] = "d", ['ب'] = "f", ['ل'] = "g", ['ا'] = "h", ['ت'] = "j",
        ['ن'] = "k", ['م'] = "l", ['ك'] = ";", ['ط'] = "'", ['ئ'] = "z",
        ['ء'] = "x", ['ؤ'] = "c", ['ر'] = "v", ['ى'] = "n", ['ة'] = "m",
        ['و'] = ",", ['ز'] = ".", ['ظ'] = "/", ['؟'] = "?",
        ['أ'] = "h", ['إ'] = "h", ['آ'] = "h"
    };
}
