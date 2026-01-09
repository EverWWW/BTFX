namespace ToolHelper.Communication.Configuration;

/// <summary>
/// TCP 客户端配置
/// </summary>
public class TcpClientOptions
{
    /// <summary>
    /// 服务器地址
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// 服务器端口
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 接收超时时间（毫秒）
    /// </summary>
    public int ReceiveTimeout { get; set; } = 0;

    /// <summary>
    /// 发送超时时间（毫秒）
    /// </summary>
    public int SendTimeout { get; set; } = 5000;

    /// <summary>
    /// 接收缓冲区大小（字节）
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// 发送缓冲区大小（字节）
    /// </summary>
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 是否启用心跳保活
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;

    /// <summary>
    /// 心跳间隔（毫秒）
    /// </summary>
    public int HeartbeatInterval { get; set; } = 30000;

    /// <summary>
    /// 心跳数据
    /// </summary>
    public byte[]? HeartbeatData { get; set; }

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 重连间隔（毫秒）
    /// </summary>
    public int ReconnectInterval { get; set; } = 5000;

    /// <summary>
    /// 最大重连次数（-1表示无限重连）
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = -1;

    /// <summary>
    /// 是否启用粘包处理
    /// </summary>
    public bool EnablePacketProcessing { get; set; } = true;

    /// <summary>
    /// 包头标识
    /// </summary>
    public byte[]? PacketHeader { get; set; }

    /// <summary>
    /// 包尾标识
    /// </summary>
    public byte[]? PacketTail { get; set; }

    /// <summary>
    /// 最大包长度（字节）
    /// </summary>
    public int MaxPacketLength { get; set; } = 65536;

    /// <summary>
    /// 是否启用 Nagle 算法
    /// </summary>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// 是否启用 Keep-Alive
    /// </summary>
    public bool KeepAlive { get; set; } = true;
}
