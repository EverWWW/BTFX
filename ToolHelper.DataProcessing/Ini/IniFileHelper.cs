using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using ToolHelper.DataProcessing.Configuration;

namespace ToolHelper.DataProcessing.Ini;

/// <summary>
/// INI 配置文件读写帮助类
/// 支持分节配置、注释、线程安全操作
/// </summary>
public class IniFileHelper
{
    private readonly IniOptions _options;
    private readonly ILogger<IniFileHelper>? _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _data = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">INI配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public IniFileHelper(IOptions<IniOptions>? options = null, ILogger<IniFileHelper>? logger = null)
    {
        _options = options?.Value ?? new IniOptions();
        _logger = logger;
    }

    #region 读取操作

    /// <summary>
    /// 异步加载INI文件
    /// </summary>
    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始加载INI文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _data.Clear();
            var encoding = GetEncoding();
            var lines = await File.ReadAllLinesAsync(filePath, encoding, cancellationToken);

            string currentSection = string.Empty;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // 忽略空行和注释
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(_options.CommentChar))
                {
                    continue;
                }

                // 解析节
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (!_data.ContainsKey(GetSectionKey(currentSection)))
                    {
                        _data[GetSectionKey(currentSection)] = new ConcurrentDictionary<string, string>();
                    }
                    continue;
                }

                // 解析键值对
                var separatorIndex = trimmed.IndexOf(_options.Separator);
                if (separatorIndex > 0)
                {
                    var key = trimmed.Substring(0, separatorIndex).Trim();
                    var value = trimmed.Substring(separatorIndex + 1).Trim();

                    if (_options.TrimValues)
                    {
                        value = value.Trim('"', '\'');
                    }

                    var section = GetSectionKey(currentSection);
                    if (!_data.ContainsKey(section))
                    {
                        _data[section] = new ConcurrentDictionary<string, string>();
                    }

                    _data[section][GetKey(key)] = value;
                }
            }

            _logger?.LogInformation("INI文件加载完成，共 {SectionCount} 个节", _data.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 读取配置值
    /// </summary>
    public string? Read(string section, string key, string? defaultValue = null)
    {
        var sectionKey = GetSectionKey(section);
        var keyValue = GetKey(key);

        if (_data.TryGetValue(sectionKey, out var sectionData) &&
            sectionData.TryGetValue(keyValue, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    /// 读取配置值并转换为指定类型
    /// </summary>
    public T? Read<T>(string section, string key, T? defaultValue = default)
    {
        var value = Read(section, key);
        if (value == null)
        {
            return defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "转换配置值失败: {Section}.{Key}", section, key);
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取所有节名称
    /// </summary>
    public IEnumerable<string> GetSections()
    {
        return _data.Keys;
    }

    /// <summary>
    /// 获取指定节的所有键
    /// </summary>
    public IEnumerable<string> GetKeys(string section)
    {
        var sectionKey = GetSectionKey(section);
        if (_data.TryGetValue(sectionKey, out var sectionData))
        {
            return sectionData.Keys;
        }
        return Enumerable.Empty<string>();
    }

    #endregion

    #region 写入操作

    /// <summary>
    /// 写入配置值
    /// </summary>
    public void Write(string section, string key, string value)
    {
        var sectionKey = GetSectionKey(section);
        var keyValue = GetKey(key);

        var sectionData = _data.GetOrAdd(sectionKey, _ => new ConcurrentDictionary<string, string>());
        sectionData[keyValue] = value;
    }

    /// <summary>
    /// 写入配置值（泛型版本）
    /// </summary>
    public void Write<T>(string section, string key, T value)
    {
        Write(section, key, value?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// 异步保存到文件
    /// </summary>
    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始保存INI文件: {FilePath}", filePath);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder();
            var encoding = GetEncoding();

            foreach (var section in _data.OrderBy(s => s.Key))
            {
                // 写入节名
                if (!string.IsNullOrEmpty(section.Key))
                {
                    sb.AppendLine($"[{section.Key}]");
                }

                // 写入键值对
                foreach (var item in section.Value.OrderBy(kv => kv.Key))
                {
                    sb.AppendLine($"{item.Key}{_options.Separator}{item.Value}");
                }

                sb.AppendLine(); // 节之间空行
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), encoding, cancellationToken);

            _logger?.LogInformation("INI文件保存完成");
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region 删除操作

    /// <summary>
    /// 删除指定节
    /// </summary>
    public bool DeleteSection(string section)
    {
        var sectionKey = GetSectionKey(section);
        return _data.TryRemove(sectionKey, out _);
    }

    /// <summary>
    /// 删除指定键
    /// </summary>
    public bool DeleteKey(string section, string key)
    {
        var sectionKey = GetSectionKey(section);
        var keyValue = GetKey(key);

        if (_data.TryGetValue(sectionKey, out var sectionData))
        {
            return sectionData.TryRemove(keyValue, out _);
        }

        return false;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        _data.Clear();
    }

    #endregion

    #region 私有方法

    private string GetSectionKey(string section)
    {
        return _options.CaseSensitive ? section : section.ToLower();
    }

    private string GetKey(string key)
    {
        return _options.CaseSensitive ? key : key.ToLower();
    }

    private Encoding GetEncoding()
    {
        return _options.Encoding.ToUpper() switch
        {
            "UTF-8" => Encoding.UTF8,
            "UTF-16" => Encoding.Unicode,
            "GB2312" or "GBK" => Encoding.GetEncoding("GB2312"),
            _ => Encoding.UTF8
        };
    }

    #endregion
}
