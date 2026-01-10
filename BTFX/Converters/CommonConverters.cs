using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BTFX.Converters;

/// <summary>
/// 空值转可见性转换器
/// </summary>
public class NullableToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

        // Check for null, empty string, or whitespace
        var isNullOrEmpty = value == null || 
                           (value is string str && string.IsNullOrWhiteSpace(str));

        if (isInverse)
        {
            return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>
    /// 反转换
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 布尔转可见性转换器
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
        var boolValue = value is bool b && b;

        if (isInverse)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 反转换
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
            var isVisible = visibility == Visibility.Visible;
            return isInverse ? !isVisible : isVisible;
        }
        return false;
    }
}

/// <summary>
/// 布尔取反转换器
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }

    /// <summary>
    /// 反转换
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : false;
    }
}

/// <summary>
/// 枚举转描述转换器
/// </summary>
public class EnumToDescriptionConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        var enumType = value.GetType();
        if (!enumType.IsEnum) return value.ToString() ?? string.Empty;

        var memberInfo = enumType.GetMember(value.ToString()!);
        if (memberInfo.Length > 0)
        {
            var attributes = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            if (attributes.Length > 0)
            {
                return ((System.ComponentModel.DescriptionAttribute)attributes[0]).Description;
            }
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 反转换
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 布尔转值转换器（用于根据布尔值选择不同的值）
/// </summary>
public class BooleanToValueConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramStr)
        {
            var values = paramStr.Split('|');
            if (values.Length == 2)
            {
                return boolValue ? values[0] : values[1];
            }
        }
        return value ?? string.Empty;
    }

    /// <summary>
    /// 反转换
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// Enum to ComboBox Index Converter
/// </summary>
public class EnumToIndexConverter : IValueConverter
{
    /// <summary>
    /// Convert enum value to index
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return 0;
        return (int)value;
    }

    /// <summary>
    /// Convert index back to enum value
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return 0;
        return Enum.ToObject(targetType, value);
    }
}

