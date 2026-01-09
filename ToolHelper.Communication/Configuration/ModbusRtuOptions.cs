namespace ToolHelper.Communication.Configuration;

/// <summary>
/// Modbus RTU 协议配置选项
/// </summary>
public class ModbusRtuOptions
{
    /// <summary>
    /// 串口名称 (如 COM1, COM2)
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// 波特率
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 数据位 (5-8)
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 停止位
    /// </summary>
    public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;

    /// <summary>
    /// 奇偶校验位
    /// </summary>
    public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;

    /// <summary>
    /// 从站地址 (Slave ID)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// 读取超时 (毫秒)
    /// </summary>
    public int ReadTimeout { get; set; } = 1000;

    /// <summary>
    /// 写入超时 (毫秒)
    /// </summary>
    public int WriteTimeout { get; set; } = 1000;

    /// <summary>
    /// 接收缓冲区大小
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// 发送缓冲区大小
    /// </summary>
    public int SendBufferSize { get; set; } = 4096;

    /// <summary>
    /// 帧间隔时间 (毫秒)
    /// 根据 Modbus RTU 标准, 应为 3.5 个字符时间
    /// </summary>
    public int FrameDelay { get; set; } = 10;

    /// <summary>
    /// 是否启用 CRC 校验
    /// </summary>
    public bool EnableCrcCheck { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 重试间隔 (毫秒)
    /// </summary>
    public int RetryInterval { get; set; } = 100;
}
