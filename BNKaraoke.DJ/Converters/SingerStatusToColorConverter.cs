using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
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
                try
                {
                    Log.Information("[COLOR CONVERTER] Converting for UserId={UserId}, DisplayName={DisplayName}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}, ThreadId={ThreadId}, BindingParameter={Parameter}, DataContextType={DataContextType}",
                        singer.UserId, singer.DisplayName, singer.IsLoggedIn, singer.IsJoined, singer.IsOnBreak, Thread.CurrentThread.ManagedThreadId, parameter?.ToString() ?? "None", value.GetType().Name);

                    // Fallback for invalid Singer state
                    if (string.IsNullOrEmpty(singer.UserId))
                    {
                        Log.Warning("[COLOR CONVERTER] Invalid Singer state: UserId is empty, returning White (#FFFFFF)");
                        return new SolidColorBrush(Color.FromRgb(255, 255, 255)); // Neutral White
                    }

                    if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak)
                    {
                        Log.Information("[COLOR CONVERTER] Returning Green (#008000) for UserId={UserId}", singer.UserId);
                        return Brushes.Green; // Green (#008000): Logged in, joined, not on break
                    }
                    if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak)
                    {
                        Log.Information("[COLOR CONVERTER] Returning DarkGoldenrod (#B8860B) for UserId={UserId}", singer.UserId);
                        return new SolidColorBrush(Color.FromRgb(184, 134, 11)); // DarkGoldenrod (#B8860B): Logged in, joined, on break
                    }
                    if (singer.IsLoggedIn && !singer.IsJoined)
                    {
                        Log.Information("[COLOR CONVERTER] Returning Darker Orange (#FF8C00) for UserId={UserId}", singer.UserId);
                        return new SolidColorBrush(Color.FromRgb(255, 140, 0)); // Darker Orange (#FF8C00): Logged in, not joined
                    }
                    Log.Information("[COLOR CONVERTER] Returning Bright Red (#FF0000) for UserId={UserId}", singer.UserId);
                    return new SolidColorBrush(Color.FromRgb(255, 0, 0)); // Bright Red (#FF0000): Not logged in
                }
                catch (Exception ex)
                {
                    Log.Error("[COLOR CONVERTER] Failed to convert for UserId={UserId}: {Message}, StackTrace={StackTrace}",
                        singer.UserId, ex.Message, ex.StackTrace);
                    return new SolidColorBrush(Color.FromRgb(255, 255, 255)); // Neutral White
                }
            }
            Log.Warning("[COLOR CONVERTER] Invalid value type: {Type}, returning White (#FFFFFF)", value?.GetType().Name);
            return new SolidColorBrush(Color.FromRgb(255, 255, 255)); // Neutral White
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}