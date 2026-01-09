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
