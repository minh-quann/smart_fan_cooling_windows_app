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

    /// <summary>
    /// Converts int tab index to Visibility (Visible if matches ConverterParameter, Collapsed otherwise).
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int currentTab && parameter != null && int.TryParse(parameter.ToString(), out int targetTab))
            {
                return currentTab == targetTab ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            return Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts bool to Visibility (Visible if true, Collapsed if false). Supports parameter "Inverse".
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isTrue = value is bool b && b;
            if (parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                isTrue = !isTrue;
            }
            return isTrue ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Microsoft.UI.Xaml.Visibility vis)
            {
                bool isTrue = vis == Microsoft.UI.Xaml.Visibility.Visible;
                if (parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                {
                    isTrue = !isTrue;
                }
                return isTrue;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts string matching parameter to Visibility (Visible if matches, Collapsed otherwise).
    /// </summary>
    public class StringMatchToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string strVal && parameter is string targetStr)
            {
                bool matches = string.Equals(strVal, targetStr, StringComparison.OrdinalIgnoreCase);
                return matches ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            return Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a percentage value (0 to 100) to a ScaleX double (0.0 to 1.0) for GPU-accelerated progress bars.
    /// Anchored strictly at RenderTransformOrigin="0,0.5" (left origin x=0) with zero jerk or animation lag.
    /// </summary>
    public class PercentToScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = 0.0;
            if (value is float f) val = f;
            else if (value is double d) val = d;
            else if (value is int i) val = i;

            val = Math.Clamp(val, 0.0, 100.0);
            return val / 100.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts bool to a thicker border (2px for active state, 1px for inactive).
    /// </summary>
    public class BoolToAccentBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isTrue = value is bool b && b;
            return isTrue ? new Microsoft.UI.Xaml.Thickness(2) : new Microsoft.UI.Xaml.Thickness(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
