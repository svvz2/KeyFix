namespace KeyFix.Core;

public interface ILayoutConverter
{
    ConversionResult Convert(string text, ConversionDirection direction = ConversionDirection.Auto);
}
