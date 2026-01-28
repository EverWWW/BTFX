namespace ToolHelper.Communication.Configuration;

/// <summary>
/// 蓝牙通讯配置选项
/// </summary>
public class BluetoothOptions
{
    /// <summary>
    /// 目标设备地址 (如 "00:11:22:33:44:55")
    /// </summary>
    public string DeviceAddress { get; set; } = string.Empty;

    /// <summary>
    /// 目标设备名称（用于按名称连接）
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// 扫描超时时间（毫秒）
    /// </summary>
    public int ScanTimeout { get; set; } = 10000;

    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 10000;

    /// <summary>
    /// 读取超时时间（毫秒）
    /// </summary>
    public int ReadTimeout { get; set; } = 5000;

    /// <summary>
    /// 写入超时时间（毫秒）
    /// </summary>
    public int WriteTimeout { get; set; } = 5000;

    /// <summary>
    /// 接收缓冲区大小（字节）
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// 发送缓冲区大小（字节）
    /// </summary>
    public int SendBufferSize { get; set; } = 4096;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 最大重连尝试次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 重连间隔时间（毫秒）
    /// </summary>
    public int ReconnectInterval { get; set; } = 2000;

    /// <summary>
    /// 是否仅扫描 BLE 设备
    /// </summary>
    public bool ScanBleOnly { get; set; } = false;

    /// <summary>
    /// 是否仅扫描已配对设备
    /// </summary>
    public bool ScanPairedOnly { get; set; } = false;

    /// <summary>
    /// 蓝牙服务 UUID（用于 BLE 连接）
    /// </summary>
    public string? ServiceUuid { get; set; }

    /// <summary>
    /// 蓝牙特征 UUID（用于 BLE 读写）
    /// </summary>
    public string? CharacteristicUuid { get; set; }

    /// <summary>
    /// 串口蓝牙 SPP 服务 UUID（经典蓝牙）
    /// </summary>
    public string SppServiceUuid { get; set; } = "00001101-0000-1000-8000-00805F9B34FB";
}
