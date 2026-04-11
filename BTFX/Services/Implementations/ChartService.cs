using System.Globalization;
using System.IO;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 图表生成服务实现
/// 基于 OxyPlot 生成步态分析报告中的各类图表
/// </summary>
public class ChartService : IChartService
{
    /// <summary>
    /// 图表主题蓝色
    /// </summary>
    private static readonly OxyColor ThemeBlue = OxyColor.Parse("#009EDB");

    /// <summary>
    /// 数据线宽度
    /// </summary>
    private const double LineStrokeThickness = 2.0;

    /// <summary>
    /// 字体
    /// </summary>
    private const string FontFamily = "Microsoft YaHei UI";

    private readonly ILogHelper? _logHelper;

    public ChartService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    // ============ 图表创建 ============

    /// <inheritdoc/>
    public PlotModel CreateJointAnglePlot(List<JointAngleFrame> data, string jointName, string title)
    {
        var model = CreateBasePlotModel(title, "时间 (s)", "角度 (°)");

        if (data.Count == 0) return model;

        var series = new LineSeries
        {
            Color = ThemeBlue,
            StrokeThickness = LineStrokeThickness,
            Title = title
        };

        Func<JointAngleFrame, double> selector = jointName.ToLowerInvariant() switch
        {
            "hip" => f => f.HipAngleDeg,
            "knee" => f => f.KneeAngleDeg,
            "ankle" => f => f.AnkleAngleDeg,
            "pelvis" => f => f.PelvisAngleDeg,
            _ => f => f.HipAngleDeg
        };

        foreach (var frame in data)
        {
            series.Points.Add(new DataPoint(frame.TimeS, selector(frame)));
        }

        model.Series.Add(series);
        return model;
    }

    /// <inheritdoc/>
    public PlotModel CreateVelocityPlot(List<KeypointVelocityFrame> data, string keypointName, string title)
    {
        var model = CreateBasePlotModel(title, "时间 (s)", "速度 (m/s)");

        if (data.Count == 0) return model;

        var filtered = string.IsNullOrEmpty(keypointName)
            ? data
            : data.Where(f => string.Equals(f.KeypointName, keypointName, StringComparison.OrdinalIgnoreCase)).ToList();

        var series = new LineSeries
        {
            Color = ThemeBlue,
            StrokeThickness = LineStrokeThickness,
            Title = title
        };

        foreach (var frame in filtered)
        {
            series.Points.Add(new DataPoint(frame.TimeS, frame.VelocityMPerS));
        }

        model.Series.Add(series);
        return model;
    }

    /// <inheritdoc/>
    public PlotModel CreateAngularVelocityPlot(List<JointAngularVelocityFrame> data, string jointName, string title)
    {
        var model = CreateBasePlotModel(title, "时间 (s)", "角速度 (°/s)");

        if (data.Count == 0) return model;

        var filtered = string.IsNullOrEmpty(jointName)
            ? data
            : data.Where(f => string.Equals(f.JointName, jointName, StringComparison.OrdinalIgnoreCase)).ToList();

        var series = new LineSeries
        {
            Color = ThemeBlue,
            StrokeThickness = LineStrokeThickness,
            Title = title
        };

        foreach (var frame in filtered)
        {
            series.Points.Add(new DataPoint(frame.TimeS, frame.AngularVelocityDegPerS));
        }

        model.Series.Add(series);
        return model;
    }

    /// <inheritdoc/>
    public PlotModel CreateTrajectoryPlot(List<KeypointTrajectoryFrame> data, string keypointName, string title)
    {
        var model = CreateBasePlotModel(title, "X", "Y");

        if (data.Count == 0) return model;

        var filtered = string.IsNullOrEmpty(keypointName)
            ? data
            : data.Where(f => string.Equals(f.KeypointName, keypointName, StringComparison.OrdinalIgnoreCase)).ToList();

        var series = new LineSeries
        {
            Color = ThemeBlue,
            StrokeThickness = LineStrokeThickness,
            Title = title
        };

        foreach (var frame in filtered)
        {
            series.Points.Add(new DataPoint(frame.X, frame.Y));
        }

        model.Series.Add(series);
        return model;
    }

    // ============ 导出 ============

    /// <inheritdoc/>
    public byte[] ExportPlotToPng(PlotModel model, int width = 480, int height = 240)
    {
        var exporter = new OxyPlot.Wpf.PngExporter
        {
            Width = width,
            Height = height,
            Resolution = 150
        };

        using var stream = new MemoryStream();
        exporter.Export(model, stream);
        return stream.ToArray();
    }

    // ============ CSV 读取 ============

    /// <inheritdoc/>
    public List<JointAngleFrame> ReadJointAngleCsv(string csvPath)
    {
        var result = new List<JointAngleFrame>();

        try
        {
            if (!File.Exists(csvPath)) return result;

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return result;

            // 解析表头以确定列索引
            var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            var colMap = BuildColumnMap(headers);

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 2) continue;

                result.Add(new JointAngleFrame
                {
                    FrameIndex = GetIntValue(fields, colMap, "frame_index", i - 1),
                    TimeS = GetDoubleValue(fields, colMap, "time_s"),
                    HipAngleDeg = GetDoubleValue(fields, colMap, "hip_angle_deg"),
                    KneeAngleDeg = GetDoubleValue(fields, colMap, "knee_angle_deg"),
                    AnkleAngleDeg = GetDoubleValue(fields, colMap, "ankle_angle_deg"),
                    PelvisAngleDeg = GetDoubleValue(fields, colMap, "pelvis_angle_deg")
                });
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"读取关节角度 CSV 失败: {csvPath}", ex);
        }

        return result;
    }

    /// <inheritdoc/>
    public List<KeypointTrajectoryFrame> ReadKeypointTrajectoryCsv(string csvPath)
    {
        var result = new List<KeypointTrajectoryFrame>();

        try
        {
            if (!File.Exists(csvPath)) return result;

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return result;

            var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            var colMap = BuildColumnMap(headers);

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 2) continue;

                result.Add(new KeypointTrajectoryFrame
                {
                    FrameIndex = GetIntValue(fields, colMap, "frame_index", i - 1),
                    TimeS = GetDoubleValue(fields, colMap, "time_s"),
                    KeypointName = GetStringValue(fields, colMap, "keypoint_name"),
                    X = GetDoubleValue(fields, colMap, "x"),
                    Y = GetDoubleValue(fields, colMap, "y"),
                    Z = GetDoubleValue(fields, colMap, "z")
                });
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"读取关键点轨迹 CSV 失败: {csvPath}", ex);
        }

        return result;
    }

    /// <inheritdoc/>
    public List<KeypointVelocityFrame> ReadKeypointVelocityCsv(string csvPath)
    {
        var result = new List<KeypointVelocityFrame>();

        try
        {
            if (!File.Exists(csvPath)) return result;

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return result;

            var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            var colMap = BuildColumnMap(headers);

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 2) continue;

                result.Add(new KeypointVelocityFrame
                {
                    FrameIndex = GetIntValue(fields, colMap, "frame_index", i - 1),
                    TimeS = GetDoubleValue(fields, colMap, "time_s"),
                    KeypointName = GetStringValue(fields, colMap, "keypoint_name"),
                    VelocityMPerS = GetDoubleValue(fields, colMap, "velocity_m_per_s")
                });
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"读取关键点速度 CSV 失败: {csvPath}", ex);
        }

        return result;
    }

    /// <inheritdoc/>
    public List<JointAngularVelocityFrame> ReadJointAngularVelocityCsv(string csvPath)
    {
        var result = new List<JointAngularVelocityFrame>();

        try
        {
            if (!File.Exists(csvPath)) return result;

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return result;

            var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
            var colMap = BuildColumnMap(headers);

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                if (fields.Length < 2) continue;

                result.Add(new JointAngularVelocityFrame
                {
                    FrameIndex = GetIntValue(fields, colMap, "frame_index", i - 1),
                    TimeS = GetDoubleValue(fields, colMap, "time_s"),
                    JointName = GetStringValue(fields, colMap, "joint_name"),
                    AngularVelocityDegPerS = GetDoubleValue(fields, colMap, "angular_velocity_deg_per_s")
                });
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"读取关节角速度 CSV 失败: {csvPath}", ex);
        }

        return result;
    }

    // ============ 私有辅助 ============

    /// <summary>
    /// 创建基础 PlotModel（统一样式）
    /// </summary>
    private static PlotModel CreateBasePlotModel(string title, string xAxisTitle, string yAxisTitle)
    {
        var model = new PlotModel
        {
            Title = title,
            TitleFontSize = 12,
            TitleFontWeight = OxyPlot.FontWeights.Bold,
            DefaultFont = FontFamily,
            PlotAreaBorderColor = OxyColors.LightGray,
            Background = OxyColors.White
        };

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xAxisTitle,
            TitleFontSize = 10,
            FontSize = 9,
            MajorGridlineStyle = LineStyle.Dash,
            MajorGridlineColor = OxyColor.FromArgb(40, 128, 128, 128),
            MinorGridlineStyle = LineStyle.None
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = yAxisTitle,
            TitleFontSize = 10,
            FontSize = 9,
            MajorGridlineStyle = LineStyle.Dash,
            MajorGridlineColor = OxyColor.FromArgb(40, 128, 128, 128),
            MinorGridlineStyle = LineStyle.None
        });

        return model;
    }

    /// <summary>
    /// 构建列名 → 索引映射
    /// </summary>
    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            map[headers[i]] = i;
        }
        return map;
    }

    /// <summary>
    /// 从字段数组中获取 double 值
    /// </summary>
    private static double GetDoubleValue(string[] fields, Dictionary<string, int> colMap, string columnName, double defaultValue = 0)
    {
        if (!colMap.TryGetValue(columnName, out var idx) || idx >= fields.Length) return defaultValue;
        return double.TryParse(fields[idx].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : defaultValue;
    }

    /// <summary>
    /// 从字段数组中获取 int 值
    /// </summary>
    private static int GetIntValue(string[] fields, Dictionary<string, int> colMap, string columnName, int defaultValue = 0)
    {
        if (!colMap.TryGetValue(columnName, out var idx) || idx >= fields.Length) return defaultValue;
        return int.TryParse(fields[idx].Trim(), out var val) ? val : defaultValue;
    }

    /// <summary>
    /// 从字段数组中获取 string 值
    /// </summary>
    private static string GetStringValue(string[] fields, Dictionary<string, int> colMap, string columnName, string defaultValue = "")
    {
        if (!colMap.TryGetValue(columnName, out var idx) || idx >= fields.Length) return defaultValue;
        return fields[idx].Trim();
    }
}
