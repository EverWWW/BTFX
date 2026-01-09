using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using ToolHelper.DataProcessing.Configuration;

namespace ToolHelper.DataProcessing.Xml;

/// <summary>
/// XML 处理帮助类
/// 提供XML序列化/反序列化、XPath查询、格式化等功能
/// </summary>
public class XmlHelper
{
    private readonly XmlOptions _options;
    private readonly ILogger<XmlHelper>? _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">XML配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public XmlHelper(IOptions<XmlOptions>? options = null, ILogger<XmlHelper>? logger = null)
    {
        _options = options?.Value ?? new XmlOptions();
        _logger = logger;
    }

    #region 序列化

    /// <summary>
    /// 序列化对象为XML字符串
    /// </summary>
    public string Serialize<T>(T obj)
    {
        try
        {
            var serializer = new XmlSerializer(typeof(T));
            var settings = CreateWriterSettings();

            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, settings);
            
            serializer.Serialize(xmlWriter, obj);
            return stringWriter.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "序列化对象为XML失败");
            throw;
        }
    }

    /// <summary>
    /// 异步序列化对象到文件
    /// </summary>
    public async Task SerializeToFileAsync<T>(string filePath, T obj, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始序列化到XML文件: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var xml = Serialize(obj);
        await File.WriteAllTextAsync(filePath, xml, GetEncoding(), cancellationToken);

        _logger?.LogInformation("XML序列化完成");
    }

    #endregion

    #region 反序列化

    /// <summary>
    /// 反序列化XML字符串为对象
    /// </summary>
    public T? Deserialize<T>(string xml)
    {
        try
        {
            var serializer = new XmlSerializer(typeof(T));
            using var reader = new StringReader(xml);
            return (T?)serializer.Deserialize(reader);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "反序列化XML失败");
            throw;
        }
    }

    /// <summary>
    /// 异步从文件反序列化对象
    /// </summary>
    public async Task<T?> DeserializeFromFileAsync<T>(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始从XML文件反序列化: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var xml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Deserialize<T>(xml);
    }

    #endregion

    #region XPath查询

    /// <summary>
    /// 使用XPath查询单个节点的值
    /// </summary>
    public string? SelectSingleNode(string xml, string xpath)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var element = doc.XPathSelectElement(xpath);
            return element?.Value;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "XPath查询失败: {XPath}", xpath);
            return null;
        }
    }

    /// <summary>
    /// 使用XPath查询多个节点
    /// </summary>
    public IEnumerable<string> SelectNodes(string xml, string xpath)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var elements = doc.XPathSelectElements(xpath);
            return elements.Select(e => e.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "XPath查询失败: {XPath}", xpath);
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// 从文件使用XPath查询
    /// </summary>
    public async Task<string?> SelectSingleNodeFromFileAsync(string filePath, string xpath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var xml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return SelectSingleNode(xml, xpath);
    }

    #endregion

    #region 格式化和验证

    /// <summary>
    /// 格式化XML字符串
    /// </summary>
    public string Format(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var settings = CreateWriterSettings();

            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, settings);
            
            doc.Save(xmlWriter);
            return stringWriter.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "格式化XML失败");
            throw;
        }
    }

    /// <summary>
    /// 验证XML是否有效
    /// </summary>
    public bool IsValid(string xml)
    {
        try
        {
            XDocument.Parse(xml);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 私有方法

    private XmlWriterSettings CreateWriterSettings()
    {
        return new XmlWriterSettings
        {
            Indent = _options.Indent,
            IndentChars = _options.IndentChars,
            Encoding = GetEncoding(),
            OmitXmlDeclaration = _options.OmitXmlDeclaration,
            Async = true
        };
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
