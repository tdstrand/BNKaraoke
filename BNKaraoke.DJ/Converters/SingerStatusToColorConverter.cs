using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Converters
{
    public class SingerStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Singer singer)
            {
                if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak)
                    return Brushes.Green; // Green: Logged in, joined, not on break
                if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak)
                    return Brushes.Yellow; // Yellow: Logged in, joined, on break
                if (singer.IsLoggedIn && !singer.IsJoined)
                    return Brushes.Orange; // Orange: Logged in, not joined
                return Brushes.Red; // Red: Not logged in
            }
            return Brushes.Red; // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}