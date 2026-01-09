namespace ToolHelper.Communication.Configuration;

/// <summary>
/// TCP 服务器配置
/// </summary>
public class TcpServerOptions
{
    /// <summary>
    /// 监听地址
    /// </summary>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>
    /// 监听端口
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// 最大连接数
    /// </summary>
    public int MaxConnections { get; set; } = 100;

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
