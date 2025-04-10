using System;
using System.Globalization;
using System.Windows.Data;

namespace HttpMonitoringSystem.Converters
{
    public class BoolToDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncoming)
            {
                return isIncoming ? "Входящий" : "Исходящий";
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string direction)
            {
                return direction == "Входящий";
            }
            return false;
        }
    }
} 