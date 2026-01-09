namespace ToolHelper.Communication.Configuration;

/// <summary>
/// HTTP 请求配置
/// </summary>
public class HttpOptions
{
    /// <summary>
    /// 基础地址
    /// </summary>
    public string? BaseAddress { get; set; }

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int Timeout { get; set; } = 30000;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryInterval { get; set; } = 1000;

    /// <summary>
    /// User-Agent
    /// </summary>
    public string UserAgent { get; set; } = "ToolHelper.Communication/1.0";

    /// <summary>
    /// 是否自动处理压缩
    /// </summary>
    public bool AutomaticDecompression { get; set; } = true;

    /// <summary>
    /// 是否允许自动重定向
    /// </summary>
    public bool AllowAutoRedirect { get; set; } = true;

    /// <summary>
    /// 最大重定向次数
    /// </summary>
    public int MaxAutomaticRedirections { get; set; } = 50;

    /// <summary>
    /// 连接池最大连接数
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// 是否使用代理
    /// </summary>
    public bool UseProxy { get; set; } = false;

    /// <summary>
    /// 代理地址
    /// </summary>
    public string? ProxyAddress { get; set; }

    /// <summary>
    /// 默认请求头
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}
