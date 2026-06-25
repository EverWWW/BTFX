using BTFX.Common;

namespace BTFX.Models;

/// <summary>
/// 应用设置模型
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 应用设置
    /// </summary>
    public ApplicationSettings Application { get; set; } = new();

    /// <summary>
    /// 数据库设置
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <summary>
    /// 自动备份设置
    /// </summary>
    public AutoBackupSettings AutoBackup { get; set; } = new();

    /// <summary>
    /// 单位设置
    /// </summary>
    public UnitSettings Unit { get; set; } = new();

    /// <summary>
    /// 登录凭据设置
    /// </summary>
    public CredentialsSettings Credentials { get; set; } = new();

    /// <summary>
    /// 算法配置
    /// </summary>
    public AlgorithmSettings Algorithm { get; set; } = new();

    /// <summary>
    /// 在线更新配置
    /// </summary>
    public UpdateSettings Update { get; set; } = new();

    /// <summary>
    /// 产品显示信息
    /// </summary>
    public ProductInfoSettings ProductInfo { get; set; } = new();
}

/// <summary>
/// 应用程序设置
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// 语言
    /// </summary>
    public AppLanguage Language { get; set; } = AppLanguage.ChineseSimplified;

    /// <summary>
    /// 主题
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.Light;

    /// <summary>
    /// 主题色（十六进制颜色值，如 #FF009EDB）
    /// </summary>
    public string PrimaryColor { get; set; } = "#FF009EDB";
}

/// <summary>
/// 产品显示信息设置
/// </summary>
public class ProductInfoSettings
{
    /// <summary>
    /// 中文公司名称
    /// </summary>
    public string CompanyNameZh { get; set; } = "河南翔宇医疗设备股份有限公司";

    /// <summary>
    /// 英文公司名称
    /// </summary>
    public string CompanyNameEn { get; set; } = "Sunnyou Medical Co., Ltd.";

    /// <summary>
    /// 中文软件名称
    /// </summary>
    public string SoftwareNameZh { get; set; } = Constants.APP_DISPLAY_NAME;

    /// <summary>
    /// 英文软件名称
    /// </summary>
    public string SoftwareNameEn { get; set; } = Constants.APP_DISPLAY_NAME_EN;

    /// <summary>
    /// 设备型号
    /// </summary>
    public string EquipmentModel { get; set; } = Constants.ACTIVATION_PRODUCT_MODEL;

    /// <summary>
    /// 发布版本
    /// </summary>
    public string ReleaseVersion { get; set; } = Constants.VERSION_DISPLAY;

    /// <summary>
    /// 软件版本
    /// </summary>
    public string SoftwareVersion { get; set; } = Constants.VERSION_FULL;

    /// <summary>
    /// 内部版本
    /// </summary>
    public string InternalVersion { get; set; } = "V1.0.0.20260625_alpha01";

    /// <summary>
    /// 公司网址
    /// </summary>
    public string Website { get; set; } = "https://www.xyyl.com/";
}

/// <summary>
/// 在线更新设置
/// </summary>
public class UpdateSettings
{
    /// <summary>
    /// 是否启用在线更新检查
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 更新配置地址
    /// </summary>
    public string UpdateUrl { get; set; } = "http://192.168.110.201/update.xml";

    /// <summary>
    /// 检查间隔天数
    /// </summary>
    public int CheckIntervalDays { get; set; } = 7;

    /// <summary>
    /// 最近检查日期
    /// </summary>
    public string LastCheckDate { get; set; } = string.Empty;

    /// <summary>
    /// 有更新时是否显示更新内容
    /// </summary>
    public bool ShowDetail { get; set; } = true;
}

/// <summary>
/// 数据库设置
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string FilePath { get; set; } = "Data/Database/BTFX.db";
}

/// <summary>
/// 自动备份设置
/// </summary>
public class AutoBackupSettings
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 备份频率
    /// </summary>
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Daily;

    /// <summary>
    /// 备份时间
    /// </summary>
    public string Time { get; set; } = "02:00";

    /// <summary>
    /// 保留数量
    /// </summary>
    public int RetainCount { get; set; } = 7;
}

/// <summary>
/// 单位设置
/// </summary>
public class UnitSettings
{
    /// <summary>
    /// 单位名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Logo路径
    /// </summary>
    public string? LogoPath { get; set; }
}

/// <summary>
/// 登录凭据设置
/// </summary>
public class CredentialsSettings
{
    /// <summary>
    /// 是否记住密码
    /// </summary>
    public bool RememberPassword { get; set; }

    /// <summary>
    /// 账号
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希（加密存储）
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}

/// <summary>
/// 算法配置
/// </summary>
public class AlgorithmSettings
{
    /// <summary>
    /// 算法程序路径（默认 Algorithm/gait_analysis.exe）
    /// </summary>
    public string ExePath { get; set; } = System.IO.Path.Combine(Constants.ALGORITHM_DIRECTORY, Constants.ALGORITHM_EXE_FILENAME);

    /// <summary>
    /// 分析超时时间（分钟）
    /// </summary>
    public int TimeoutMinutes { get; set; } = Constants.ALGORITHM_DEFAULT_TIMEOUT_MINUTES;

    /// <summary>
    /// 算法版本号
    /// </summary>
    public string AlgorithmVersion { get; set; } = Constants.DEFAULT_ALGORITHM_VERSION;

    /// <summary>
    /// 模型版本号
    /// </summary>
    public string ModelVersion { get; set; } = Constants.DEFAULT_MODEL_VERSION;

    /// <summary>
    /// 侧向相机与步道距离（m），算法必填参数
    /// </summary>
    public double? SideCameraDistance { get; set; }
}
