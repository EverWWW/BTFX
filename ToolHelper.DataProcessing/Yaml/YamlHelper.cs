using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using ToolHelper.DataProcessing.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ToolHelper.DataProcessing.Yaml;

/// <summary>
/// YAML 配置文件处理帮助类
/// 提供 YAML 文件的序列化、反序列化功能
/// 基于 YamlDotNet 实现
/// </summary>
public class YamlHelper
{
    private readonly YamlOptions _options;
    private readonly ILogger<YamlHelper>? _logger;
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">YAML配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public YamlHelper(IOptions<YamlOptions>? options = null, ILogger<YamlHelper>? logger = null)
    {
        _options = options?.Value ?? new YamlOptions();
        _logger = logger;

        (_serializer, _deserializer) = CreateSerializers();
    }

    #region 序列化

    /// <summary>
    /// 序列化对象为YAML字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>YAML字符串</returns>
    public string Serialize<T>(T obj)
    {
        try
        {
            _logger?.LogDebug("开始序列化对象为YAML");
            return _serializer.Serialize(obj);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "序列化对象为YAML失败");
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
        _logger?.LogInformation("开始序列化到YAML文件: {FilePath}", filePath);

        var yaml = Serialize(obj);
        var encoding = GetEncoding();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, yaml, encoding, cancellationToken);

        _logger?.LogInformation("YAML序列化完成");
    }

    #endregion

    #region 反序列化

    /// <summary>
    /// 反序列化YAML字符串为对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="yaml">YAML字符串</param>
    /// <returns>反序列化的对象</returns>
    public T? Deserialize<T>(string yaml)
    {
        try
        {
            _logger?.LogDebug("开始反序列化YAML字符串");
            return _deserializer.Deserialize<T>(yaml);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "反序列化YAML失败");
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
        _logger?.LogInformation("开始从YAML文件反序列化: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Deserialize<T>(yaml);
    }

    #endregion

    #region 验证和格式化

    /// <summary>
    /// 验证YAML字符串是否有效
    /// </summary>
    /// <param name="yaml">YAML字符串</param>
    /// <returns>是否有效</returns>
    public bool IsValid(string yaml)
    {
        try
        {
            _deserializer.Deserialize<object>(yaml);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 格式化YAML字符串
    /// </summary>
    /// <param name="yaml">原始YAML字符串</param>
    /// <returns>格式化后的YAML字符串</returns>
    public string Format(string yaml)
    {
        try
        {
            var obj = _deserializer.Deserialize<object>(yaml);
            return _serializer.Serialize(obj);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "格式化YAML失败");
            throw;
        }
    }

    #endregion

    #region 私有方法

    private (ISerializer, IDeserializer) CreateSerializers()
    {
        var serializerBuilder = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance);

        var deserializerBuilder = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance);

        if (_options.IgnoreAliases)
        {
            deserializerBuilder.IgnoreUnmatchedProperties();
        }

        return (serializerBuilder.Build(), deserializerBuilder.Build());
    }

    private Encoding GetEncoding()
    {
        return _options.Encoding.ToUpper() switch
        {
            "UTF-8" => Encoding.UTF8,
            "UTF-16" => Encoding.Unicode,
            _ => Encoding.UTF8
        };
    }

    #endregion
}
