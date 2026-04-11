namespace BTFX.Models.Analysis;

/// <summary>
/// 关节角度帧（从 CSV 文件读取的单行数据）
/// </summary>
public record JointAngleFrame
{
    /// <summary>
    /// 帧索引
    /// </summary>
    public int FrameIndex { get; init; }

    /// <summary>
    /// 时间（秒）
    /// </summary>
    public double TimeS { get; init; }

    /// <summary>
    /// 髋关节角度（°）
    /// </summary>
    public double HipAngleDeg { get; init; }

    /// <summary>
    /// 膝关节角度（°）
    /// </summary>
    public double KneeAngleDeg { get; init; }

    /// <summary>
    /// 踝关节角度（°）
    /// </summary>
    public double AnkleAngleDeg { get; init; }

    /// <summary>
    /// 骨盆角度（°）
    /// </summary>
    public double PelvisAngleDeg { get; init; }
}

/// <summary>
/// 关键点轨迹帧
/// </summary>
public record KeypointTrajectoryFrame
{
    /// <summary>
    /// 帧索引
    /// </summary>
    public int FrameIndex { get; init; }

    /// <summary>
    /// 时间（秒）
    /// </summary>
    public double TimeS { get; init; }

    /// <summary>
    /// 关键点名称
    /// </summary>
    public string KeypointName { get; init; } = string.Empty;

    /// <summary>
    /// X 坐标
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Y 坐标
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Z 坐标
    /// </summary>
    public double Z { get; init; }
}

/// <summary>
/// 关键点速度帧
/// </summary>
public record KeypointVelocityFrame
{
    /// <summary>
    /// 帧索引
    /// </summary>
    public int FrameIndex { get; init; }

    /// <summary>
    /// 时间（秒）
    /// </summary>
    public double TimeS { get; init; }

    /// <summary>
    /// 关键点名称
    /// </summary>
    public string KeypointName { get; init; } = string.Empty;

    /// <summary>
    /// 速度（m/s）
    /// </summary>
    public double VelocityMPerS { get; init; }
}

/// <summary>
/// 关节角速度帧
/// </summary>
public record JointAngularVelocityFrame
{
    /// <summary>
    /// 帧索引
    /// </summary>
    public int FrameIndex { get; init; }

    /// <summary>
    /// 时间（秒）
    /// </summary>
    public double TimeS { get; init; }

    /// <summary>
    /// 关节名称
    /// </summary>
    public string JointName { get; init; } = string.Empty;

    /// <summary>
    /// 角速度（°/s）
    /// </summary>
    public double AngularVelocityDegPerS { get; init; }
}
