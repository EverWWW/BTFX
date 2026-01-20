namespace ToolHelper.LoggingDiagnostics.Logging;

/// <summary>
/// 日志统计信息
/// </summary>
public class LogStatistics
{
    /// <summary>
    /// 统计开始日期
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// 统计结束日期
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// 日志文件数量
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// 总日志条数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 总文件大小（字节）
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 总文件大小（字节）- 兼容别名
    /// </summary>
    public long TotalSizeBytes => TotalSize;

    /// <summary>
    /// 格式化的总大小
    /// </summary>
    public string TotalSizeFormatted => FormatSize(TotalSize);

    /// <summary>
    /// Trace 级别日志数量
    /// </summary>
    public int TraceCount { get; set; }

    /// <summary>
    /// Debug 级别日志数量
    /// </summary>
    public int DebugCount { get; set; }

    /// <summary>
    /// Information 级别日志数量
    /// </summary>
    public int InformationCount { get; set; }

    /// <summary>
    /// Warning 级别日志数量
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Error 级别日志数量
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Critical 级别日志数量
    /// </summary>
    public int CriticalCount { get; set; }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// 日志目录信息
/// </summary>
public class LogDirectoryInfo
{
    /// <summary>
    /// 目录路径
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// 目录是否存在
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 文件数量
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// 总大小（字节）
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 格式化的总大小
    /// </summary>
    public string TotalSizeFormatted => FormatSize(TotalSize);

    /// <summary>
    /// 最旧文件的创建时间
    /// </summary>
    public DateTime? OldestFile { get; set; }

    /// <summary>
    /// 最新文件的创建时间
    /// </summary>
    public DateTime? NewestFile { get; set; }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
