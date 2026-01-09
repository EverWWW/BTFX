namespace ToolHelper.Communication.Configuration;

/// <summary>
/// 串口通信配置选项
/// </summary>
public class SerialPortOptions
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
    /// 握手协议
    /// </summary>
    public System.IO.Ports.Handshake Handshake { get; set; } = System.IO.Ports.Handshake.None;

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
    /// 是否启用 DTR (数据终端就绪)
    /// </summary>
    public bool DtrEnable { get; set; } = false;

    /// <summary>
    /// 是否启用 RTS (请求发送)
    /// </summary>
    public bool RtsEnable { get; set; } = false;

    /// <summary>
    /// 是否启用自动波特率检测
    /// </summary>
    public bool AutoBaudRate { get; set; } = false;

    /// <summary>
    /// 自动波特率检测时尝试的波特率列表
    /// </summary>
    public int[] BaudRatesToTry { get; set; } = new[] { 9600, 19200, 38400, 57600, 115200 };

    /// <summary>
    /// 是否启用自动端口识别
    /// </summary>
    public bool AutoDetectPort { get; set; } = false;

    /// <summary>
    /// 自动检测时的测试数据
    /// </summary>
    public byte[]? TestData { get; set; }

    /// <summary>
    /// 自动检测时的预期响应数据
    /// </summary>
    public byte[]? ExpectedResponse { get; set; }

    /// <summary>
    /// 自动检测超时 (毫秒)
    /// </summary>
    public int AutoDetectTimeout { get; set; } = 1000;
}
