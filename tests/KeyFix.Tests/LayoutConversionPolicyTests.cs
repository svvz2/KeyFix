using KeyFix.Core;

namespace KeyFix.Tests;

public sealed class LayoutConversionPolicyTests
{
    private readonly Arabic101LayoutConverter _converter = new();

    [Fact]
    public void ConvertUserRequest_uses_requested_direction_when_it_matches()
    {
        var result = LayoutConversionPolicy.ConvertUserRequest(
            _converter,
            "hgsghl",
            ConversionDirection.EnglishToArabic);

        Assert.Equal("السلام", result.ConvertedText);
        Assert.Equal(ConversionDirection.EnglishToArabic, result.Direction);
    }

    [Fact]
    public void ConvertUserRequest_falls_back_when_saved_direction_does_not_match_selection()
    {
        var result = LayoutConversionPolicy.ConvertUserRequest(
            _converter,
            "hgsghl",
            ConversionDirection.ArabicToEnglish);

        Assert.Equal("السلام", result.ConvertedText);
        Assert.Equal(ConversionDirection.EnglishToArabic, result.Direction);
    }

    [Fact]
    public void ConvertUserRequest_falls_back_when_only_neutral_punctuation_matched()
    {
        var result = LayoutConversionPolicy.ConvertUserRequest(
            _converter,
            "hello?",
            ConversionDirection.ArabicToEnglish);

        Assert.Equal("اثممخ؟", result.ConvertedText);
        Assert.Equal(ConversionDirection.EnglishToArabic, result.Direction);
    }

    [Fact]
    public void ConvertUserRequest_preserves_unmapped_text_without_rejecting_the_command()
    {
        var result = LayoutConversionPolicy.ConvertUserRequest(
            _converter,
            "123 😀",
            ConversionDirection.Auto);

        Assert.Equal("123 😀", result.ConvertedText);
        Assert.Equal(0, result.ConvertedCharacters);
    }
}
