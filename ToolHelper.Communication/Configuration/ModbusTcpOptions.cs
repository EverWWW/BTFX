namespace ToolHelper.Communication.Configuration;

/// <summary>
/// Modbus TCP 协议配置选项
/// </summary>
public class ModbusTcpOptions
{
    /// <summary>
    /// 服务器地址
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// 服务器端口
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// 从站地址 (Unit ID)
    /// </summary>
    public byte UnitId { get; set; } = 1;

    /// <summary>
    /// 连接超时 (毫秒)
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 读取超时 (毫秒)
    /// </summary>
    public int ReadTimeout { get; set; } = 3000;

    /// <summary>
    /// 写入超时 (毫秒)
    /// </summary>
    public int WriteTimeout { get; set; } = 3000;

    /// <summary>
    /// 接收缓冲区大小
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// 发送缓冲区大小
    /// </summary>
    public int SendBufferSize { get; set; } = 4096;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 最大重连次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 重连间隔 (毫秒)
    /// </summary>
    public int ReconnectInterval { get; set; } = 2000;

    /// <summary>
    /// 是否启用 TCP NoDelay (禁用 Nagle 算法)
    /// </summary>
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// 是否启用 Keep-Alive
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// 事务超时 (毫秒)
    /// </summary>
    public int TransactionTimeout { get; set; } = 5000;

    /// <summary>
    /// 最大并发事务数
    /// </summary>
    public int MaxConcurrentTransactions { get; set; } = 10;
}
