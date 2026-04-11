namespace BTFX.ViewModels.Measurement;

/// <summary>
/// 数据质量综合等级
/// </summary>
public enum QualityGrade
{
    /// <summary>
    /// A级（优秀）：所有指标均达标
    /// </summary>
    Excellent,

    /// <summary>
    /// B级（良好）：有指标需关注，但无严重问题
    /// </summary>
    Good,

    /// <summary>
    /// C级（较差）：有指标不达标，建议重新采集
    /// </summary>
    Poor
}

/// <summary>
/// 前置条件检查项
/// </summary>
public class PrerequisiteItem
{
    /// <summary>
    /// 条件名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否满足
    /// </summary>
    public bool IsMet { get; set; }

    /// <summary>
    /// 是否必选（必选项不满足会阻止分析）
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// MaterialDesign 图标名称
    /// </summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// 步态事件参数展示模型
/// </summary>
public class GaitEventParametersDisplay
{
    /// <summary>
    /// 步态周期时长
    /// </summary>
    public string GaitCycleDuration { get; set; } = "--";

    /// <summary>
    /// 站立相时长
    /// </summary>
    public string StanceTime { get; set; } = "--";

    /// <summary>
    /// 摆动相时长
    /// </summary>
    public string SwingTime { get; set; } = "--";

    /// <summary>
    /// 双支撑相时长
    /// </summary>
    public string DoubleSupportTime { get; set; } = "--";

    /// <summary>
    /// 单支撑相时长
    /// </summary>
    public string SingleSupportTime { get; set; } = "--";

    /// <summary>
    /// 步长
    /// </summary>
    public string StepLength { get; set; } = "--";

    /// <summary>
    /// 步幅
    /// </summary>
    public string StrideLength { get; set; } = "--";

    /// <summary>
    /// 步速
    /// </summary>
    public string GaitSpeed { get; set; } = "--";
}

/// <summary>
/// 运动学参数展示模型
/// </summary>
public class KinematicSummaryDisplay
{
    /// <summary>
    /// 髋关节活动范围
    /// </summary>
    public string HipRom { get; set; } = "--";

    /// <summary>
    /// 膝关节活动范围
    /// </summary>
    public string KneeRom { get; set; } = "--";

    /// <summary>
    /// 踝关节活动范围
    /// </summary>
    public string AnkleRom { get; set; } = "--";

    /// <summary>
    /// 骨盆活动范围
    /// </summary>
    public string PelvisRom { get; set; } = "--";
}

/// <summary>
/// 质量控制展示模型
/// </summary>
public class QualityControlDisplay
{
    /// <summary>
    /// 关键点置信度原始值
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 关键点置信度显示文本
    /// </summary>
    public string ConfidenceDisplay { get; set; } = "--";

    /// <summary>
    /// 关键点置信度是否达标
    /// </summary>
    public bool ConfidenceOk { get; set; }

    /// <summary>
    /// 有效帧比例原始值
    /// </summary>
    public double ValidFrameRatio { get; set; }

    /// <summary>
    /// 有效帧比例显示文本
    /// </summary>
    public string ValidFrameRatioDisplay { get; set; } = "--";

    /// <summary>
    /// 有效帧比例是否达标
    /// </summary>
    public bool ValidFrameRatioOk { get; set; }

    /// <summary>
    /// 是否有遮挡
    /// </summary>
    public bool HasOcclusion { get; set; }

    /// <summary>
    /// 是否有丢点
    /// </summary>
    public bool HasMissingPoints { get; set; }

    /// <summary>
    /// 综合质量等级
    /// </summary>
    public QualityGrade OverallGrade { get; set; }

    /// <summary>
    /// 等级显示文本（A/B/C）
    /// </summary>
    public string GradeDisplay { get; set; } = "--";

    /// <summary>
    /// 等级对应颜色（#4CAF50 绿 / #FF9800 橙 / #F44336 红）
    /// </summary>
    public string GradeColor { get; set; } = "#9E9E9E";

    /// <summary>
    /// 等级描述文本
    /// </summary>
    public string GradeDescription { get; set; } = string.Empty;

    /// <summary>
    /// 置信度等级（优秀/一般/差）
    /// </summary>
    public QualityGrade ConfidenceGrade { get; set; }

    /// <summary>
    /// 有效帧比例等级（优秀/一般/差）
    /// </summary>
    public QualityGrade ValidFrameRatioGrade { get; set; }
}
