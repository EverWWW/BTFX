namespace BTFX.Models.Analysis;

/// <summary>
/// 分析请求 DTO（ViewModel → Service 的传递对象）
/// </summary>
public class AnalysisRequest
{
    /// <summary>
    /// 测量记录
    /// </summary>
    public required MeasurementRecord Record { get; init; }

    /// <summary>
    /// 患者信息
    /// </summary>
    public required Patient Patient { get; init; }

    /// <summary>
    /// 分析选项
    /// </summary>
    public AnalysisOptions Options { get; init; } = new();

    /// <summary>
    /// 输出目录路径
    /// </summary>
    public required string OutputDirectory { get; init; }
}

/// <summary>
/// 分析选项
/// </summary>
public class AnalysisOptions
{
    /// <summary>
    /// 计算步态事件参数
    /// </summary>
    public bool CalculateGaitEvents { get; set; } = true;

    /// <summary>
    /// 计算运动学参数
    /// </summary>
    public bool CalculateKinematics { get; set; } = true;

    /// <summary>
    /// 导出 CSV 文件
    /// </summary>
    public bool ExportCsv { get; set; } = true;

    /// <summary>
    /// 曲线平滑处理
    /// </summary>
    public bool SmoothCurve { get; set; } = true;
}
