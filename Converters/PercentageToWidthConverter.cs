using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DonateForLife.Converters
{
    public class PercentageToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                // Convert percentage (0-100) to a proportion (0-1)
                return percentage / 100.0;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}