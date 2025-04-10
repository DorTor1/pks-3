using System;
using System.Globalization;
using System.Windows.Data;

namespace HttpMonitoringSystem.Converters
{
    public class MethodToEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string method)
            {
                return method == "POST";
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 