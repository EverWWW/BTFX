using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToolHelper.LoggingDiagnostics.Logging;

/// <summary>
/// 日志导出助手
/// 提供日志导出、统计、清理等功能
/// </summary>
/// <example>
/// <code>
/// // 直接创建
/// var exportHelper = new LogExportHelper("logs");
/// 
/// // 导出到文本文件
/// var count = await exportHelper.ExportLogsAsync("export.txt", startDate, endDate);
/// 
/// // 导出到CSV
/// var count = await exportHelper.ExportLogsToCsvAsync("export.csv", startDate, endDate);
/// 
/// // 获取统计信息
/// var stats = await exportHelper.GetStatisticsAsync(startDate, endDate);
/// </code>
/// </example>
public partial class LogExportHelper
{
    private readonly LogExportOptions _options;
    private readonly ILogger<LogExportHelper>? _logger;
    private readonly string _logDirectory;

    // 日志级别正则表达式
    [GeneratedRegex(@"\[(TRACE|DEBUG|INFO|INFORMATION|WARNING|WARN|ERROR|CRITICAL|FATAL)\]", RegexOptions.IgnoreCase)]
    private static partial Regex LogLevelRegex();

    /// <summary>
    /// 创建 LogExportHelper 实例
    /// </summary>
    /// <param name="logDirectory">日志目录路径</param>
    public LogExportHelper(string logDirectory) : this(Options.Create(new LogExportOptions { LogDirectory = logDirectory }), null)
    {
    }

    /// <summary>
    /// 创建 LogExportHelper 实例
    /// </summary>
    /// <param name="options">导出选项</param>
    /// <param name="logger">日志记录器</param>
    public LogExportHelper(IOptions<LogExportOptions> options, ILogger<LogExportHelper>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
        _logDirectory = _options.LogDirectory;
    }

    /// <summary>
    /// 导出日志到文本文件
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导出的日志条数</returns>
    public async Task<int> ExportLogsAsync(
        string outputPath,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        EnsureDirectoryExists(outputPath);

        var logFiles = GetLogFiles(startDate, endDate);
        var totalCount = 0;
        var encoding = Encoding.GetEncoding(_options.Encoding);

        await using var writer = new StreamWriter(outputPath, false, encoding);

        foreach (var file in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lines = await File.ReadAllLinesAsync(file, encoding, cancellationToken);
                foreach (var line in lines)
                {
                    await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                    totalCount++;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "读取日志文件失败: {FilePath}", file);
            }
        }

        _logger?.LogInformation("已导出 {Count} 条日志到: {OutputPath}", totalCount, outputPath);
        return totalCount;
    }

    /// <summary>
    /// 导出日志到CSV文件
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导出的日志条数</returns>
    public async Task<int> ExportLogsToCsvAsync(
        string outputPath,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        EnsureDirectoryExists(outputPath);

        var logFiles = GetLogFiles(startDate, endDate);
        var totalCount = 0;
        var encoding = Encoding.GetEncoding(_options.Encoding);
        var delimiter = _options.CsvDelimiter;

        await using var writer = new StreamWriter(outputPath, false, encoding);

        // 写入标题行
        if (_options.IncludeHeader)
        {
            await writer.WriteLineAsync($"Timestamp{delimiter}Level{delimiter}Category{delimiter}Message");
        }

        foreach (var file in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lines = await File.ReadAllLinesAsync(file, encoding, cancellationToken);
                foreach (var line in lines)
                {
                    var parsedLine = ParseLogLine(line);
                    if (parsedLine != null)
                    {
                        var csvLine = $"\"{EscapeCsv(parsedLine.Value.Timestamp)}\"{delimiter}" +
                                      $"\"{EscapeCsv(parsedLine.Value.Level)}\"{delimiter}" +
                                      $"\"{EscapeCsv(parsedLine.Value.Category)}\"{delimiter}" +
                                      $"\"{EscapeCsv(parsedLine.Value.Message)}\"";
                        await writer.WriteLineAsync(csvLine.AsMemory(), cancellationToken);
                        totalCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "读取日志文件失败: {FilePath}", file);
            }
        }

        _logger?.LogInformation("已导出 {Count} 条日志到CSV: {OutputPath}", totalCount, outputPath);
        return totalCount;
    }

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计信息</returns>
    public async Task<LogStatistics> GetStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var stats = new LogStatistics
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var logFiles = GetLogFiles(startDate, endDate);
        stats.FileCount = logFiles.Count;

        var encoding = Encoding.GetEncoding(_options.Encoding);

        foreach (var file in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileInfo = new FileInfo(file);
                stats.TotalSize += fileInfo.Length;

                var lines = await File.ReadAllLinesAsync(file, encoding, cancellationToken);
                foreach (var line in lines)
                {
                    stats.TotalCount++;

                    var match = LogLevelRegex().Match(line);
                    if (match.Success)
                    {
                        var level = match.Groups[1].Value.ToUpperInvariant();
                        switch (level)
                        {
                            case "TRACE":
                                stats.TraceCount++;
                                break;
                            case "DEBUG":
                                stats.DebugCount++;
                                break;
                            case "INFO":
                            case "INFORMATION":
                                stats.InformationCount++;
                                break;
                            case "WARNING":
                            case "WARN":
                                stats.WarningCount++;
                                break;
                            case "ERROR":
                                stats.ErrorCount++;
                                break;
                            case "CRITICAL":
                            case "FATAL":
                                stats.CriticalCount++;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "读取日志文件失败: {FilePath}", file);
            }
        }

        return stats;
    }

    /// <summary>
    /// 获取日志目录信息
    /// </summary>
    /// <returns>目录信息</returns>
    public LogDirectoryInfo GetDirectoryInfo()
    {
        var info = new LogDirectoryInfo
        {
            DirectoryPath = Path.GetFullPath(_logDirectory)
        };

        if (!Directory.Exists(_logDirectory))
        {
            info.Exists = false;
            return info;
        }

        info.Exists = true;

        var files = Directory.GetFiles(_logDirectory, _options.FilePattern, SearchOption.AllDirectories);
        info.FileCount = files.Length;

        DateTime? oldest = null;
        DateTime? newest = null;

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            info.TotalSize += fileInfo.Length;

            if (!oldest.HasValue || fileInfo.CreationTime < oldest.Value)
            {
                oldest = fileInfo.CreationTime;
            }

            if (!newest.HasValue || fileInfo.CreationTime > newest.Value)
            {
                newest = fileInfo.CreationTime;
            }
        }

        info.OldestFile = oldest;
        info.NewestFile = newest;

        return info;
    }

    /// <summary>
    /// 清理过期日志文件
    /// </summary>
    /// <param name="retentionDays">保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除的文件数量</returns>
    public async Task<int> CleanupOldLogsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_logDirectory))
        {
            return 0;
        }

        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        var files = Directory.GetFiles(_logDirectory, _options.FilePattern, SearchOption.AllDirectories);
        var deletedCount = 0;

        await Task.Run(() =>
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger?.LogInformation("已删除过期日志: {FilePath}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "删除日志文件失败: {FilePath}", file);
                }
            }
        }, cancellationToken);

        _logger?.LogInformation("已清理 {Count} 个过期日志文件", deletedCount);
        return deletedCount;
    }

    private List<string> GetLogFiles(DateTime startDate, DateTime endDate)
    {
        if (!Directory.Exists(_logDirectory))
        {
            return [];
        }

        var files = Directory.GetFiles(_logDirectory, _options.FilePattern, SearchOption.AllDirectories);

        return files
            .Where(f =>
            {
                var fileInfo = new FileInfo(f);
                return fileInfo.CreationTime.Date >= startDate.Date &&
                       fileInfo.CreationTime.Date <= endDate.Date;
            })
            .OrderBy(f => new FileInfo(f).CreationTime)
            .ToList();
    }

    private static (string Timestamp, string Level, string Category, string Message)? ParseLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // 尝试解析格式: [2025-01-10 09:00:00] [INFO] [Category] Message
        var pattern = @"^\[([^\]]+)\]\s*\[([^\]]+)\]\s*\[([^\]]+)\]\s*(.*)$";
        var match = Regex.Match(line, pattern);

        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
        }

        // 如果无法解析，返回整行作为消息
        return (string.Empty, string.Empty, string.Empty, line);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.Replace("\"", "\"\"");
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
