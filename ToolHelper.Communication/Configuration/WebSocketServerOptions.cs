namespace ToolHelper.Communication.Configuration;

/// <summary>
/// WebSocket 服务端配置选项
/// </summary>
public class WebSocketServerOptions
{
    /// <summary>
    /// 监听地址（默认为 localhost）
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// 监听端口（默认为 8080）
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 请求路径（默认为 /ws）
    /// </summary>
    public string Path { get; set; } = "/ws";

    /// <summary>
    /// 最大连接数（默认为 100）
    /// </summary>
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    /// 接收缓冲区大小（默认为 8192 字节）
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// 发送缓冲区大小（默认为 8192 字节）
    /// </summary>
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 是否启用心跳检测（默认为 false）
    /// </summary>
    public bool EnableHeartbeat { get; set; } = false;

    /// <summary>
    /// 心跳间隔（毫秒，默认为 30000）
    /// </summary>
    public int HeartbeatInterval { get; set; } = 30000;

    /// <summary>
    /// 心跳超时时间（毫秒，默认为 60000）
    /// </summary>
    public int HeartbeatTimeout { get; set; } = 60000;

    /// <summary>
    /// 是否使用 HTTPS/WSS（默认为 false）
    /// 注意：启用 HTTPS 需要配置证书
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// SSL 证书路径（启用 HTTPS 时需要）
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// SSL 证书密码
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// 支持的子协议列表
    /// </summary>
    public List<string> SubProtocols { get; set; } = new();

    /// <summary>
    /// 空闲连接超时时间（毫秒，默认为 300000，即 5 分钟）
    /// 设置为 0 表示不超时
    /// </summary>
    public int IdleTimeout { get; set; } = 300000;

    /// <summary>
    /// 获取完整的监听 URL
    /// </summary>
    public string GetListenUrl()
    {
        var scheme = UseHttps ? "https" : "http";
        var path = Path.StartsWith("/") ? Path : "/" + Path;
        if (!path.EndsWith("/"))
        {
            path += "/";
        }
        return $"{scheme}://{Host}:{Port}{path}";
    }

    /// <summary>
    /// 获取 WebSocket URL（供客户端连接使用）
    /// </summary>
    public string GetWebSocketUrl()
    {
        var scheme = UseHttps ? "wss" : "ws";
        var path = Path.StartsWith("/") ? Path : "/" + Path;
        return $"{scheme}://{Host}:{Port}{path}";
    }
}
