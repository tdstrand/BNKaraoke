using BNKaraoke.DJ.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace BNKaraoke.DJ.Converters;

public class UpNextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is QueueEntry entry && entry.IsUpNext && !string.IsNullOrEmpty(entry.SongTitle) && !string.IsNullOrEmpty(entry.RequestorDisplayName))
        {
            return $"Up Next: {entry.SongTitle} by {entry.RequestorDisplayName}";
        }
        return "Up Next: None";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}