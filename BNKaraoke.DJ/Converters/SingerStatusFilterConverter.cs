using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Converters
{
    public class SingerStatusFilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ObservableCollection<Singer> singers && parameter is string status)
            {
                return status switch
                {
                    "Green" => singers.Where(s => s.IsLoggedIn && s.IsJoined && !s.IsOnBreak && !s.UserId.StartsWith("dummy")).ToList(),
                    "Yellow" => singers.Where(s => s.IsLoggedIn && s.IsJoined && s.IsOnBreak && !s.UserId.StartsWith("dummy")).ToList(),
                    "Orange" => singers.Where(s => s.IsLoggedIn && !s.IsJoined && !s.IsOnBreak && !s.UserId.StartsWith("dummy")).ToList(),
                    "Red" => singers.Where(s => !s.IsLoggedIn && !s.IsJoined && !s.IsOnBreak && !s.UserId.StartsWith("dummy")).ToList(),
                    _ => singers
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}