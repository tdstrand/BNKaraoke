using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BNKaraoke.DJ.Models;
using Serilog;

namespace BNKaraoke.DJ.Converters
{
    public class SingerStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Singer singer)
            {
                string colorHex;
                if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak)
                {
                    colorHex = "#008000"; // Green (Available)
                    Log.Information("[COLOR CONVERTER] Returning Green ({ColorHex}) for UserId={UserId}, DisplayName={DisplayName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                        colorHex, singer.UserId, singer.DisplayName, singer.IsLoggedIn, singer.IsJoined, singer.IsOnBreak);
                    return Brushes.Green;
                }
                if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak)
                {
                    colorHex = "#0000FF"; // Blue (On Break)
                    Log.Information("[COLOR CONVERTER] Returning Blue ({ColorHex}) for UserId={UserId}, DisplayName={DisplayName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                        colorHex, singer.UserId, singer.DisplayName, singer.IsLoggedIn, singer.IsJoined, singer.IsOnBreak);
                    return Brushes.Blue;
                }
                if (singer.IsLoggedIn && !singer.IsJoined)
                {
                    colorHex = "#FFD700"; // Gold (Logged In but Not Joined)
                    Log.Information("[COLOR CONVERTER] Returning Gold ({ColorHex}) for UserId={UserId}, DisplayName={DisplayName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                        colorHex, singer.UserId, singer.DisplayName, singer.IsLoggedIn, singer.IsJoined, singer.IsOnBreak);
                    return new SolidColorBrush(Color.FromRgb(255, 215, 0));
                }
                colorHex = "#FF0000"; // Red (Not Logged In)
                Log.Information("[COLOR CONVERTER] Returning Red ({ColorHex}) for UserId={UserId}, DisplayName={DisplayName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                    colorHex, singer.UserId, singer.DisplayName, singer.IsLoggedIn, singer.IsJoined, singer.IsOnBreak);
                return Brushes.Red;
            }
            Log.Warning("[COLOR CONVERTER] Invalid value type, returning default Red (#FF0000)");
            return Brushes.Red; // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}