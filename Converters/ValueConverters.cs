using System;
using Microsoft.UI.Xaml.Data;

namespace SmartFanCooling.Converters
{
    /// <summary>
    /// Converts boolean connection status to text ("Ngắt kết nối" / "Kết nối").
    /// </summary>
    public class ConnectTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool isConnected && isConnected) ? "Ngắt kết nối" : "Kết nối";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts a boolean value.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b ? !b : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is bool b ? !b : false;
        }
    }

    /// <summary>
    /// Checks if a string matches the ConverterParameter for RadioButton grouping.
    /// </summary>
    public class StringMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string currentStr && parameter is string targetStr)
            {
                return string.Equals(currentStr, targetStr, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isChecked && isChecked && parameter is string targetStr)
            {
                return targetStr;
            }
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Returns true if value is non-null.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
