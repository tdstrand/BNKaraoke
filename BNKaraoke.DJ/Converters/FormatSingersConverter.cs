using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BNKaraoke.DJ.Converters
{
    public class FormatSingersConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string[] singers && singers != null)
            {
                return string.Join(", ", singers);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}