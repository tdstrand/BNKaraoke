using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BNKaraoke.DJ.Converters
{
    public class PhoneNumberConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string phone && !string.IsNullOrEmpty(phone))
            {
                var digits = Regex.Replace(phone, @"\D", "");
                if (digits.Length == 10)
                {
                    return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
                }
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string phone)
            {
                return Regex.Replace(phone, @"\D", "");
            }
            return value;
        }
    }
}