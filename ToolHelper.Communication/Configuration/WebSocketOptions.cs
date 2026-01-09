namespace ToolHelper.Communication.Configuration;

/// <summary>
/// WebSocket 通信配置选项
/// </summary>
public class WebSocketOptions
{
    /// <summary>
    /// 服务器地址 (如 ws://localhost:8080 或 wss://example.com)
    /// </summary>
    public string Uri { get; set; } = "ws://localhost:8080";

    /// <summary>
    /// 子协议列表
    /// </summary>
    public List<string> SubProtocols { get; set; } = new();

    /// <summary>
    /// 连接超时 (毫秒)
    /// </summary>
    public int ConnectTimeout { get; set; } = 30000;

    /// <summary>
    /// 心跳间隔 (毫秒), 0 表示禁用心跳
    /// </summary>
    public int HeartbeatInterval { get; set; } = 30000;

    /// <summary>
    /// 心跳超时 (毫秒)
    /// </summary>
    public int HeartbeatTimeout { get; set; } = 10000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 最大重连次数, -1 表示无限重连
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = -1;

    /// <summary>
    /// 重连间隔 (毫秒)
    /// </summary>
    public int ReconnectInterval { get; set; } = 5000;

    /// <summary>
    /// 接收缓冲区大小
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// 发送缓冲区大小
    /// </summary>
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 消息发送超时 (毫秒)
    /// </summary>
    public int SendTimeout { get; set; } = 5000;

    /// <summary>
    /// 是否自动处理 Ping/Pong
    /// </summary>
    public bool AutoPong { get; set; } = true;

    /// <summary>
    /// HTTP 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Cookie 容器
    /// </summary>
    public System.Net.CookieContainer? Cookies { get; set; }

    /// <summary>
    /// 是否使用压缩 (Sec-WebSocket-Extensions: permessage-deflate)
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// 关闭超时 (毫秒)
    /// </summary>
    public int CloseTimeout { get; set; } = 5000;
}
