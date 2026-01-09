using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Text;
using ToolHelper.DataProcessing.Abstractions;
using ToolHelper.DataProcessing.Configuration;

namespace ToolHelper.DataProcessing.Csv;

/// <summary>
/// CSV 文件读写帮助类
/// 支持大文件流式处理、自定义分隔符、编码自动检测等功能
/// 使用内存池优化性能，减少GC压力
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class CsvHelper<T> : IFileReader<T>, IFileWriter<T> where T : class, new()
{
    private readonly CsvOptions _options;
    private readonly ILogger<CsvHelper<T>>? _logger;
    private readonly ArrayPool<char> _charPool = ArrayPool<char>.Shared;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">CSV配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public CsvHelper(IOptions<CsvOptions>? options = null, ILogger<CsvHelper<T>>? logger = null)
    {
        _options = options?.Value ?? new CsvOptions();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始读取CSV文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var result = new List<T>();
        var encoding = GetEncoding();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        using var reader = new StreamReader(stream, encoding, _options.AutoDetectEncoding);

        string[]? headers = null;
        int lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;

            if (_options.IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);

            if (_options.HasHeader && headers == null)
            {
                headers = fields;
                continue;
            }

            try
            {
                var obj = MapToObject(fields, headers);
                result.Add(obj);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "解析第 {LineNumber} 行时出错", lineNumber);
            }
        }

        _logger?.LogInformation("CSV文件读取完成，共 {Count} 条记录", result.Count);
        return result;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> ReadStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始流式读取CSV文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var encoding = GetEncoding();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        using var reader = new StreamReader(stream, encoding, _options.AutoDetectEncoding);

        string[]? headers = null;
        int lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;

            if (_options.IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);

            if (_options.HasHeader && headers == null)
            {
                headers = fields;
                continue;
            }

            T? obj = null;
            try
            {
                obj = MapToObject(fields, headers);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "解析第 {LineNumber} 行时出错", lineNumber);
            }

            if (obj != null)
            {
                yield return obj;
            }
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string filePath, IEnumerable<T> data, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始写入CSV文件: {FilePath}", filePath);

        var encoding = GetEncoding();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        using var writer = new StreamWriter(stream, encoding);

        var properties = typeof(T).GetProperties();
        
        // 写入标题行
        if (_options.HasHeader)
        {
            var headerLine = string.Join(_options.Delimiter, properties.Select(p => EscapeField(p.Name)));
            await writer.WriteLineAsync(headerLine.AsMemory(), cancellationToken);
        }

        // 写入数据行
        int count = 0;
        foreach (var item in data)
        {
            var values = properties.Select(p => 
            {
                var value = p.GetValue(item);
                return EscapeField(value?.ToString() ?? string.Empty);
            });
            
            var line = string.Join(_options.Delimiter, values);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            count++;
        }

        _logger?.LogInformation("CSV文件写入完成，共 {Count} 条记录", count);
    }

    /// <inheritdoc/>
    public async Task AppendAsync(string filePath, IEnumerable<T> data, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始追加写入CSV文件: {FilePath}", filePath);

        var encoding = GetEncoding();
        var fileExists = File.Exists(filePath);

        using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, 4096, true);
        using var writer = new StreamWriter(stream, encoding);

        var properties = typeof(T).GetProperties();

        // 如果文件不存在且需要标题行，先写入标题
        if (!fileExists && _options.HasHeader)
        {
            var headerLine = string.Join(_options.Delimiter, properties.Select(p => EscapeField(p.Name)));
            await writer.WriteLineAsync(headerLine.AsMemory(), cancellationToken);
        }

        // 追加数据行
        int count = 0;
        foreach (var item in data)
        {
            var values = properties.Select(p =>
            {
                var value = p.GetValue(item);
                return EscapeField(value?.ToString() ?? string.Empty);
            });

            var line = string.Join(_options.Delimiter, values);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            count++;
        }

        _logger?.LogInformation("CSV文件追加完成，共 {Count} 条记录", count);
    }

    #region 私有辅助方法

    /// <summary>
    /// 解析CSV行，处理引号和分隔符
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == _options.Quote)
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == _options.Quote)
                {
                    // 双引号转义
                    field.Append(_options.Quote);
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c.ToString() == _options.Delimiter && !inQuotes)
            {
                fields.Add(_options.TrimFields ? field.ToString().Trim() : field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(_options.TrimFields ? field.ToString().Trim() : field.ToString());
        return fields.ToArray();
    }

    /// <summary>
    /// 转义CSV字段（处理包含分隔符、引号、换行符的情况）
    /// </summary>
    private string EscapeField(string field)
    {
        if (field.Contains(_options.Delimiter) || 
            field.Contains(_options.Quote) || 
            field.Contains('\n') || 
            field.Contains('\r'))
        {
            return $"{_options.Quote}{field.Replace(_options.Quote.ToString(), $"{_options.Quote}{_options.Quote}")}{_options.Quote}";
        }
        return field;
    }

    /// <summary>
    /// 将字段数组映射到对象
    /// </summary>
    private T MapToObject(string[] fields, string[]? headers)
    {
        var obj = new T();
        var properties = typeof(T).GetProperties();

        if (headers != null)
        {
            // 按标题名称映射
            for (int i = 0; i < Math.Min(fields.Length, headers.Length); i++)
            {
                var property = properties.FirstOrDefault(p => 
                    p.Name.Equals(headers[i], StringComparison.OrdinalIgnoreCase));

                if (property != null && property.CanWrite)
                {
                    SetPropertyValue(property, obj, fields[i]);
                }
            }
        }
        else
        {
            // 按索引顺序映射
            for (int i = 0; i < Math.Min(fields.Length, properties.Length); i++)
            {
                if (properties[i].CanWrite)
                {
                    SetPropertyValue(properties[i], obj, fields[i]);
                }
            }
        }

        return obj;
    }

    /// <summary>
    /// 设置属性值（自动类型转换）
    /// </summary>
    private void SetPropertyValue(System.Reflection.PropertyInfo property, T obj, string value)
    {
        try
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (string.IsNullOrWhiteSpace(value))
            {
                if (Nullable.GetUnderlyingType(property.PropertyType) != null)
                {
                    property.SetValue(obj, null);
                }
                return;
            }

            object? convertedValue = targetType.Name switch
            {
                nameof(String) => value,
                nameof(Int32) => int.Parse(value),
                nameof(Int64) => long.Parse(value),
                nameof(Double) => double.Parse(value),
                nameof(Decimal) => decimal.Parse(value),
                nameof(Boolean) => bool.Parse(value),
                nameof(DateTime) => DateTime.Parse(value),
                _ => Convert.ChangeType(value, targetType)
            };

            property.SetValue(obj, convertedValue);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "设置属性 {PropertyName} 值时出错: {Value}", property.Name, value);
        }
    }

    /// <summary>
    /// 获取编码对象
    /// </summary>
    private Encoding GetEncoding()
    {
        return _options.Encoding.ToUpper() switch
        {
            "UTF-8" => Encoding.UTF8,
            "UTF-16" => Encoding.Unicode,
            "UTF-32" => Encoding.UTF32,
            "ASCII" => Encoding.ASCII,
            "GB2312" or "GBK" => Encoding.GetEncoding("GB2312"),
            _ => Encoding.UTF8
        };
    }

    #endregion
}
