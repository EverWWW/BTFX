namespace ToolHelper.Communication.Configuration;

/// <summary>
/// UDP 通信配置
/// </summary>
public class UdpOptions
{
    /// <summary>
    /// 本地监听地址
    /// </summary>
    public string LocalHost { get; set; } = "0.0.0.0";

    /// <summary>
    /// 本地监听端口
    /// </summary>
    public int LocalPort { get; set; } = 0;

    /// <summary>
    /// 远程主机地址（用于发送）
    /// </summary>
    public string? RemoteHost { get; set; }

    /// <summary>
    /// 远程端口（用于发送）
    /// </summary>
    public int RemotePort { get; set; } = 8080;

    /// <summary>
    /// 接收缓冲区大小（字节）
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// 发送缓冲区大小（字节）
    /// </summary>
    public int SendBufferSize { get; set; } = 8192;

    /// <summary>
    /// 接收超时时间（毫秒，0表示无超时）
    /// </summary>
    public int ReceiveTimeout { get; set; } = 0;

    /// <summary>
    /// 发送超时时间（毫秒）
    /// </summary>
    public int SendTimeout { get; set; } = 5000;

    /// <summary>
    /// 是否允许广播
    /// </summary>
    public bool EnableBroadcast { get; set; } = false;

    /// <summary>
    /// 组播地址（用于组播通信）
    /// </summary>
    public string? MulticastAddress { get; set; }

    /// <summary>
    /// 组播 TTL（生存时间）
    /// </summary>
    public int MulticastTtl { get; set; } = 1;

    /// <summary>
    /// 是否允许地址重用
    /// </summary>
    public bool ReuseAddress { get; set; } = false;
}
