using KeyFix.Core;

namespace KeyFix.Tests;

public sealed class TypingMistakeDetectorTests
{
    private readonly TypingMistakeDetector _detector = new(new Arabic101LayoutConverter());

    [Theory]
    [InlineData("hgsghl", "السلام", ConversionDirection.EnglishToArabic)]
    [InlineData("lvpfh", "مرحبا", ConversionDirection.EnglishToArabic)]
    [InlineData("اثممخ", "hello", ConversionDirection.ArabicToEnglish)]
    public void DetectsHighConfidenceLayoutMistakes(string input, string expected, ConversionDirection direction)
    {
        var suggestion = _detector.AnalyzeWord(input);

        Assert.NotNull(suggestion);
        Assert.Equal(expected, suggestion.SuggestedText);
        Assert.Equal(direction, suggestion.Direction);
        Assert.True(suggestion.Confidence >= 0.84);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("السلام")]
    [InlineData("abc")]
    [InlineData("test123")]
    [InlineData("عربيEnglish")]
    public void IgnoresNormalOrAmbiguousWords(string input) => Assert.Null(_detector.AnalyzeWord(input));
}
