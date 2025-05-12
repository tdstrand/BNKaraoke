using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BNKaraoke.DJ.Converters
{
    public class OpacityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int percentage)
            {
                return percentage / 100.0;
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}