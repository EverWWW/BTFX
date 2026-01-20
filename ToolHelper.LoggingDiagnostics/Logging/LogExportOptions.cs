namespace ToolHelper.LoggingDiagnostics.Logging;

/// <summary>
/// 日志导出选项
/// </summary>
public class LogExportOptions
{
    /// <summary>
    /// 日志目录路径
    /// </summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>
    /// 日志文件匹配模式
    /// </summary>
    public string FilePattern { get; set; } = "*.txt";

    /// <summary>
    /// 默认编码
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// 日期格式
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// CSV 分隔符
    /// </summary>
    public string CsvDelimiter { get; set; } = ",";

    /// <summary>
    /// 是否包含标题行（CSV）
    /// </summary>
    public bool IncludeHeader { get; set; } = true;
}
