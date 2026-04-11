using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BTFX.Common;

namespace BTFX.Converters;

/// <summary>
/// 分析状态转可见性转换器
/// 当 AnalysisState 与 ConverterParameter 匹配时可见
/// 用法: Visibility="{Binding AnalysisState, Converter={StaticResource AnalysisStateToVisibilityConverter}, ConverterParameter=Ready}"
/// </summary>
public class AnalysisStateToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AnalysisState state && parameter is string targetState)
        {
            return state.ToString().Equals(targetState, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
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
/// 分析进度转阶段状态转换器
/// 用于阶段进度列表中标识已完成(✓)/进行中(●)/待完成(○)
/// ConverterParameter 为阶段对应的起始百分比（如 "35"）
/// </summary>
public class ProgressToStageStatusConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int progress && parameter is string thresholdStr
            && int.TryParse(thresholdStr, out var threshold))
        {
            if (progress >= threshold)
                return "✓";
            if (progress >= threshold - 25)
                return "●";
            return "○";
        }
        return "○";
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
/// 布尔值转前置条件图标颜色转换器
/// true → 绿色，false → 红色
/// </summary>
public class PrerequisiteStatusToColorConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isMet = value is bool b && b;
        return isMet ? "#4CAF50" : "#F44336";
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
/// 布尔值转前置条件状态图标转换器
/// true → CheckCircle，false → CloseCircle
/// </summary>
public class PrerequisiteStatusToIconConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isMet = value is bool b && b;
        return isMet ? "CheckCircle" : "CloseCircle";
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
/// 质量指标布尔值转状态文本转换器
/// true → ✅ 达标，false → ⚠️ 不达标
/// </summary>
public class QualityBoolToStatusConverter : IValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? "✅" : "⚠️";
    }

    /// <summary>
    /// 反转换
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
