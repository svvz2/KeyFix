namespace KeyFix.Core;

/// <summary>
/// Conservatively detects words that become substantially more plausible after
/// interpreting their physical keys through the opposite keyboard layout.
/// All analysis is local and callers should keep input only in short-lived memory.
/// </summary>
public sealed class TypingMistakeDetector(ILayoutConverter converter)
{
    private const double SuggestionThreshold = 0.84;

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "hello", "hi", "the", "and", "you", "your", "this", "that", "these", "those",
        "is", "are", "was", "were", "have", "has", "can", "could", "should", "would",
        "want", "need", "please", "thanks", "thank", "good", "great", "morning", "evening",
        "today", "tomorrow", "now", "later", "before", "after", "with", "from", "for",
        "not", "yes", "no", "how", "what", "where", "why", "when", "who", "which",
        "program", "language", "keyboard", "text", "user", "windows", "correct", "correction",
        "word", "words", "problem", "work", "working", "open", "close", "save", "file",
        "new", "settings", "start", "stop", "right", "wrong", "application", "computer"
    };

    private static readonly HashSet<string> ArabicWords = new(StringComparer.Ordinal)
    {
        "السلام", "عليكم", "مرحبا", "الله", "انا", "أنت", "انت", "هو", "هي", "هذا", "هذه",
        "ذلك", "الذي", "التي", "كيف", "شلون", "اريد", "أريد", "تريد", "نريد", "ممكن",
        "شكرا", "تمام", "نعم", "لا", "الى", "إلى", "على", "في", "من", "مع", "عن",
        "كان", "يكون", "عندي", "عندك", "وين", "شنو", "ليش", "هسه", "اليوم", "بكرة",
        "بعد", "قبل", "برنامج", "لغة", "عربي", "انكليزي", "إنكليزي", "كتابة", "صحيح",
        "تصحيح", "كلمة", "كلمات", "نص", "المستخدم", "الكمبيوتر", "ويندوز", "عمل", "يعمل",
        "مشكلة", "اكو", "أكو", "خلي", "نكمل", "سوي", "هاي", "اول", "أول", "واحد", "جديد",
        "جيد", "ممتاز", "تلقائي", "اعدادات", "إعدادات", "افتح", "اغلق", "أغلق", "حفظ"
    };

    private static readonly string[] EnglishBigrams =
    [
        "th", "he", "in", "er", "an", "re", "on", "at", "en", "nd", "ti", "es", "or",
        "te", "of", "ed", "is", "it", "al", "ar", "st", "to", "nt", "ng", "se", "ha",
        "as", "ou", "io", "le", "ve", "co", "me", "de", "hi", "ri", "ro", "ic", "ne",
        "ea", "ra", "ce", "li", "ch", "ll", "be", "ma", "si", "om", "ur"
    ];

    private static readonly string[] ArabicBigrams =
    [
        "ال", "لا", "لم", "من", "في", "عل", "لي", "ري", "ان", "ين", "ون", "ما", "ها",
        "ية", "ات", "را", "سل", "مر", "حب", "با", "لك", "كم", "شك", "كر", "بر", "نا",
        "هو", "هي", "تع", "تص", "حي", "صح", "كت", "تا", "اب", "ام", "وا", "ني", "دة"
    ];

    public TypingSuggestion? AnalyzeWord(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var word = text.Trim();
        if (word.Length is < 3 or > 24 || word.Any(character => !char.IsLetter(character)))
        {
            return null;
        }

        var arabicCount = word.Count(LanguageDirectionDetector.IsArabic);
        var latinCount = word.Count(IsLatin);
        if (arabicCount > 0 && latinCount > 0 || arabicCount + latinCount != word.Length)
        {
            return null;
        }

        var sourceIsArabic = arabicCount == word.Length;
        if (IsKnownWord(word, sourceIsArabic))
        {
            return null;
        }

        var result = converter.Convert(word, sourceIsArabic
            ? ConversionDirection.ArabicToEnglish
            : ConversionDirection.EnglishToArabic);
        if (!result.Changed || result.ConvertedCharacters != word.Length)
        {
            return null;
        }

        var candidate = result.ConvertedText;
        var confidence = ScoreCandidate(candidate, !sourceIsArabic);
        if (confidence < SuggestionThreshold)
        {
            return null;
        }

        return new TypingSuggestion(word, candidate, result.Direction, confidence);
    }

    private static bool IsKnownWord(string word, bool isArabic) => isArabic
        ? ArabicWords.Contains(word)
        : EnglishWords.Contains(word);

    private static double ScoreCandidate(string candidate, bool isArabic)
    {
        if (IsKnownWord(candidate, isArabic))
        {
            return 0.98;
        }

        if (candidate.Length < 4 || candidate.Any(character => isArabic
                ? !LanguageDirectionDetector.IsArabic(character)
                : !IsLatin(character)))
        {
            return 0;
        }

        var normalized = candidate.ToLowerInvariant();
        var bigrams = isArabic ? ArabicBigrams : EnglishBigrams;
        var matches = bigrams.Count(normalized.Contains);
        var score = isArabic ? 0.40 : 0.34;
        score += Math.Min(0.34, matches * 0.085);

        if (isArabic && normalized.StartsWith("ال", StringComparison.Ordinal))
        {
            score += 0.16;
        }
        else if (!isArabic && normalized.Any(character => "aeiouy".Contains(character)))
        {
            score += 0.16;
        }

        if (!isArabic && HasLongConsonantRun(normalized))
        {
            score -= 0.25;
        }

        return Math.Clamp(score, 0, 0.94);
    }

    private static bool HasLongConsonantRun(string word)
    {
        var run = 0;
        foreach (var character in word)
        {
            run = "aeiouy".Contains(character) ? 0 : run + 1;
            if (run >= 4)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLatin(char character) => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
