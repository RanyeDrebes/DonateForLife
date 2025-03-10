using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DonateForLife.Models;

namespace DonateForLife.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TransplantationStatus status)
            {
                return status switch
                {
                    TransplantationStatus.Scheduled => new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                    TransplantationStatus.InProgress => new SolidColorBrush(Color.Parse("#FFF5F5F5")),
                    TransplantationStatus.Completed => new SolidColorBrush(Color.Parse("#E8F5E9")),
                    TransplantationStatus.Cancelled => new SolidColorBrush(Color.Parse("#FFEBEE")),
                    TransplantationStatus.Delayed => new SolidColorBrush(Color.Parse("#FFF8E1")),
                    TransplantationStatus.Failed => new SolidColorBrush(Color.Parse("#FFEBEE")),
                    _ => new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                };
            }

            return new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPositive)
            {
                return isPositive
                    ? new SolidColorBrush(Color.Parse("#E8F5E9"))
                    : new SolidColorBrush(Color.Parse("#FFEBEE"));
            }

            return new SolidColorBrush(Color.Parse("#FFFFFF"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPositive)
            {
                return isPositive
                    ? new SolidColorBrush(Color.Parse("#2E7D32"))
                    : new SolidColorBrush(Color.Parse("#C62828"));
            }

            return new SolidColorBrush(Color.Parse("#000000"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToResultConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPositive)
            {
                return isPositive ? "Successful" : "Unsuccessful";
            }

            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringNotEmptyToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}