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
            return Microsoft.UI.Xaml.DependencyProperty.UnsetValue;
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

    /// <summary>
    /// Formats a float or double value according to parameter format string (e.g. "{0:F1} °C").
    /// </summary>
    public class FloatFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is float fVal && parameter is string fmt)
            {
                return string.Format(fmt, fVal);
            }
            if (value is double dVal && parameter is string fmtDouble)
            {
                return string.Format(fmtDouble, dVal);
            }
            if (value is int iVal && parameter is string fmtInt)
            {
                return string.Format(fmtInt, iVal);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
