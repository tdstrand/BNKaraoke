using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BNKaraoke.DJ.Converters
{
    public class MultiConditionBrushConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count != 3)
                return Brushes.Transparent;

            bool isCurrentlyPlaying = values[0] is bool current && current;
            bool isNext = values[1] is bool next && next;
            bool hasSpecialSinger = values[2] is bool special && special;

            if (isCurrentlyPlaying)
                return Brushes.Red;
            if (isNext)
                return Brushes.Yellow;
            if (hasSpecialSinger)
                return Brushes.Purple;

            return Brushes.Transparent;
        }
    }
}