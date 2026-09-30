using KeyFix.Core;

namespace KeyFix.Tests;

public sealed class Arabic101LayoutConverterTests
{
    private readonly Arabic101LayoutConverter _converter = new();

    [Theory]
    [InlineData("hgsghl", "السلام")]
    [InlineData("ugd;l", "عليكم")]
    [InlineData("lvpfh", "مرحبا")]
    [InlineData("HGSghl", "السلام")]
    [InlineData("hgsghl ugd;l", "السلام عليكم")]
    public void ConvertsEnglishToArabic(string input, string expected)
    {
        var result = _converter.Convert(input, ConversionDirection.EnglishToArabic);

        Assert.Equal(expected, result.ConvertedText);
        Assert.True(result.Changed);
    }

    [Theory]
    [InlineData("السلام", "hgsghl")]
    [InlineData("عليكم", "ugd;l")]
    [InlineData("مرحبا", "lvpfh")]
    public void ConvertsArabicToEnglish(string input, string expected)
    {
        var result = _converter.Convert(input, ConversionDirection.ArabicToEnglish);

        Assert.Equal(expected, result.ConvertedText);
    }

    [Theory]
    [InlineData("hello", ConversionDirection.EnglishToArabic)]
    [InlineData("مرحبا", ConversionDirection.ArabicToEnglish)]
    [InlineData("hello مرحبا بالعالم", ConversionDirection.ArabicToEnglish)]
    public void AutoDetectsDirection(string input, ConversionDirection expected)
    {
        var result = _converter.Convert(input);

        Assert.Equal(expected, result.Direction);
    }

    [Theory]
    [InlineData("hgsghl", "السلام")]
    [InlineData("السلام", "hgsghl")]
    [InlineData("lvpfh", "مرحبا")]
    [InlineData("مرحبا", "lvpfh")]
    public void AutoConvertsInBothDirections(string input, string expected)
    {
        var result = _converter.Convert(input, ConversionDirection.Auto);

        Assert.Equal(expected, result.ConvertedText);
    }

    [Fact]
    public void PreservesWhitespaceNumbersAndEmoji()
    {
        var result = _converter.Convert("lvpfh 123 👋", ConversionDirection.EnglishToArabic);

        Assert.Equal("مرحبا 123 👋", result.ConvertedText);
    }

    [Fact]
    public void RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => _converter.Convert(null!));
    }
}
