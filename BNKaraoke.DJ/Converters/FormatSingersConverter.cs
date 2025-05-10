using Avalonia.Data.Converters;
using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.ViewModels;
using System;
using System.Globalization;

namespace BNKaraoke.DJ.Converters
{
    public class FormatSingersConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is EventQueueDto queueItem && queueItem.Singers != null)
            {
                var viewModel = new MainWindowViewModel();
                return viewModel.FormatSingers(queueItem.Singers);
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}