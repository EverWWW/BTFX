using BTFX.Common;
using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 测量记录模型
/// </summary>
[SugarTable("MeasurementRecords")]
public class MeasurementRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 患者ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int PatientId { get; set; }

    /// <summary>
    /// 患者信息（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public Patient? Patient { get; set; }

    /// <summary>
    /// 操作员ID（对应数据库 UserId 字段）
    /// </summary>
    [SugarColumn(ColumnName = "UserId", IsNullable = false)]
    public int OperatorId { get; set; }

    /// <summary>
    /// 操作员信息（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public User? Operator { get; set; }

    /// <summary>
    /// 测量日期时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime MeasurementDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 测量状态
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public MeasurementStatus Status { get; set; } = MeasurementStatus.Pending;

    /// <summary>
    /// 视频文件路径（对应数据库 VideoPath 字段）
    /// </summary>
    [SugarColumn(ColumnName = "VideoPath", Length = 500, IsNullable = true)]
    public string? VideoFilePath { get; set; }

    /// <summary>
    /// 测量持续时间 (秒)（对应数据库 Duration 字段）
    /// </summary>
    [SugarColumn(ColumnName = "Duration", IsNullable = true)]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// 是否为游客数据
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsGuestData { get; set; } = false;

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 步态参数ID（忽略，不存在于数据库）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public int? GaitParametersId { get; set; }

    /// <summary>
    /// 步态参数（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public GaitParameters? GaitParameters { get; set; }

    #region 测量评估模块扩展字段

    /// <summary>
    /// 测量名称
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? MeasurementName { get; set; }

    /// <summary>
    /// 测量类型
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public MeasurementType MeasurementType { get; set; } = MeasurementType.NormalWalk;

    /// <summary>
    /// 正面视频路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? FrontVideoPath { get; set; }

    /// <summary>
    /// 侧面视频路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? SideVideoPath { get; set; }

    /// <summary>
    /// 视频规格
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public VideoSpec VideoSpec { get; set; } = VideoSpec.P1080_30fps;

    /// <summary>
    /// 步道长度 (米)
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public double WalkwayLength { get; set; } = 6.0;

    /// <summary>
    /// 导入策略（复制/引用）
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public ImportStrategy ImportStrategy { get; set; } = ImportStrategy.CopyToFolder;

    /// <summary>
    /// 视频导入模式（导入/采集）
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public VideoImportMode VideoImportMode { get; set; } = VideoImportMode.Import;

    /// <summary>
    /// 当前分析阶段
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public AnalysisStage CurrentAnalysisStage { get; set; } = AnalysisStage.None;

    /// <summary>
    /// 关键点分析完成
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool KeypointsCompleted { get; set; } = false;

    /// <summary>
    /// 事件分析完成
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool EventsCompleted { get; set; } = false;

    /// <summary>
    /// 运动学分析完成
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool KinematicsCompleted { get; set; } = false;

    /// <summary>
    /// 测量目录路径（相对路径）
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? MeasurementFolderPath { get; set; }

    #endregion

    #region 辅助属性（非数据库字段）

    /// <summary>
    /// 是否有正面视频
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool HasFrontVideo => !string.IsNullOrEmpty(FrontVideoPath);

    /// <summary>
    /// 是否有侧面视频
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool HasSideVideo => !string.IsNullOrEmpty(SideVideoPath);

    /// <summary>
    /// 是否有双视频（可进行分析）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool HasDualVideo => HasFrontVideo && HasSideVideo;

    #endregion
}
