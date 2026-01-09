using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToolHelper.DataProcessing.Configuration;

namespace ToolHelper.DataProcessing.Json;

/// <summary>
/// JSON 处理帮助类
/// 提供序列化、反序列化、美化、压缩等功能
/// 基于 System.Text.Json 实现高性能处理
/// </summary>
public class JsonHelper
{
    private readonly JsonOptions _options;
    private readonly ILogger<JsonHelper>? _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">JSON配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public JsonHelper(IOptions<JsonOptions>? options = null, ILogger<JsonHelper>? logger = null)
    {
        _options = options?.Value ?? new JsonOptions();
        _logger = logger;
        _serializerOptions = CreateSerializerOptions();
    }

    #region 序列化

    /// <summary>
    /// 序列化对象为JSON字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>JSON字符串</returns>
    public string Serialize<T>(T obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, _serializerOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "序列化对象为JSON失败");
            throw;
        }
    }

    /// <summary>
    /// 异步序列化对象到流
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="stream">目标流</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SerializeAsync<T>(Stream stream, T obj, CancellationToken cancellationToken = default)
    {
        try
        {
            await JsonSerializer.SerializeAsync(stream, obj, _serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "异步序列化对象到流失败");
            throw;
        }
    }

    /// <summary>
    /// 序列化对象到文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SerializeToFileAsync<T>(string filePath, T obj, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始序列化到文件: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await SerializeAsync(stream, obj, cancellationToken);

        _logger?.LogInformation("序列化到文件完成");
    }

    #endregion

    #region 反序列化

    /// <summary>
    /// 反序列化JSON字符串为对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON字符串</param>
    /// <returns>反序列化的对象</returns>
    public T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, _serializerOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "反序列化JSON失败");
            throw;
        }
    }

    /// <summary>
    /// 异步从流反序列化对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="stream">源流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "异步从流反序列化失败");
            throw;
        }
    }

    /// <summary>
    /// 从文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public async Task<T?> DeserializeFromFileAsync<T>(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始从文件反序列化: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var result = await DeserializeAsync<T>(stream, cancellationToken);

        _logger?.LogInformation("从文件反序列化完成");
        return result;
    }

    #endregion

    #region 格式化和压缩

    /// <summary>
    /// 美化JSON字符串（格式化并缩进）
    /// </summary>
    /// <param name="json">原始JSON字符串</param>
    /// <returns>美化后的JSON字符串</returns>
    public string Beautify(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(document.RootElement, options);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "美化JSON失败");
            throw;
        }
    }

    /// <summary>
    /// 压缩JSON字符串（移除空白字符）
    /// </summary>
    /// <param name="json">原始JSON字符串</param>
    /// <returns>压缩后的JSON字符串</returns>
    public string Minify(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            return JsonSerializer.Serialize(document.RootElement, options);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "压缩JSON失败");
            throw;
        }
    }

    #endregion

    #region 验证和查询

    /// <summary>
    /// 验证JSON字符串是否有效
    /// </summary>
    /// <param name="json">JSON字符串</param>
    /// <returns>是否有效</returns>
    public bool IsValid(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取JSON路径的值
    /// </summary>
    /// <param name="json">JSON字符串</param>
    /// <param name="path">JSON路径（如：$.user.name）</param>
    /// <returns>值（如果存在）</returns>
    public string? GetValue(string json, string path)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;

            var parts = path.TrimStart('$', '.').Split('.');
            foreach (var part in parts)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(part, out var property))
                {
                    element = property;
                }
                else
                {
                    return null;
                }
            }

            return element.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "获取JSON路径值失败: {Path}", path);
            return null;
        }
    }

    #endregion

    #region 转换

    /// <summary>
    /// 对象深拷贝（通过JSON序列化/反序列化）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">源对象</param>
    /// <returns>深拷贝的新对象</returns>
    public T? DeepClone<T>(T obj)
    {
        try
        {
            var json = Serialize(obj);
            return Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "对象深拷贝失败");
            throw;
        }
    }

    /// <summary>
    /// 合并两个JSON对象
    /// </summary>
    /// <param name="json1">第一个JSON字符串</param>
    /// <param name="json2">第二个JSON字符串</param>
    /// <returns>合并后的JSON字符串</returns>
    public string Merge(string json1, string json2)
    {
        try
        {
            using var doc1 = JsonDocument.Parse(json1);
            using var doc2 = JsonDocument.Parse(json2);

            var merged = new Dictionary<string, object?>();

            foreach (var prop in doc1.RootElement.EnumerateObject())
            {
                merged[prop.Name] = prop.Value.Clone();
            }

            foreach (var prop in doc2.RootElement.EnumerateObject())
            {
                merged[prop.Name] = prop.Value.Clone();
            }

            return Serialize(merged);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "合并JSON失败");
            throw;
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 创建序列化选项
    /// </summary>
    private JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = _options.Indented,
            DefaultIgnoreCondition = _options.IgnoreNullValues 
                ? JsonIgnoreCondition.WhenWritingNull 
                : JsonIgnoreCondition.Never,
            ReadCommentHandling = _options.AllowComments 
                ? JsonCommentHandling.Skip 
                : JsonCommentHandling.Disallow,
            MaxDepth = _options.MaxDepth,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 属性命名策略
        options.PropertyNamingPolicy = _options.PropertyNamingPolicy.ToLower() switch
        {
            "camelcase" => JsonNamingPolicy.CamelCase,
            "snakecase" => JsonNamingPolicy.SnakeCaseLower,
            _ => null
        };

        // 字符串枚举转换器
        if (_options.UseStringEnumConverter)
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        return options;
    }

    #endregion
}
