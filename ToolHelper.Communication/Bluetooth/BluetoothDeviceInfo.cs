namespace ToolHelper.Communication.Bluetooth;

/// <summary>
/// 蓝牙设备信息
/// </summary>
public class BluetoothDeviceInfo
{
    /// <summary>
    /// 设备名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 设备地址 (MAC地址，如 "00:11:22:33:44:55")
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 信号强度 (RSSI, dBm)
    /// </summary>
    public int SignalStrength { get; set; }

    /// <summary>
    /// 是否已配对
    /// </summary>
    public bool IsPaired { get; set; }

    /// <summary>
    /// 是否是 BLE (低功耗蓝牙) 设备
    /// </summary>
    public bool IsBleDevice { get; set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// 设备类型
    /// </summary>
    public BluetoothDeviceType DeviceType { get; set; } = BluetoothDeviceType.Unknown;

    /// <summary>
    /// 设备服务 UUID 列表
    /// </summary>
    public List<string> ServiceUuids { get; set; } = [];

    /// <summary>
    /// 制造商数据
    /// </summary>
    public byte[]? ManufacturerData { get; set; }

    /// <summary>
    /// 发现时间
    /// </summary>
    public DateTime DiscoveredTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 最后一次看到的时间
    /// </summary>
    public DateTime LastSeenTime { get; set; } = DateTime.Now;

    /// <inheritdoc/>
    public override string ToString()
    {
        var name = string.IsNullOrEmpty(Name) ? "(未知设备)" : Name;
        var type = IsBleDevice ? "BLE" : "Classic";
        var paired = IsPaired ? ", 已配对" : "";
        return $"{name} [{Address}] ({type}, {SignalStrength}dBm{paired})";
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is BluetoothDeviceInfo other)
        {
            return string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Address?.ToUpperInvariant().GetHashCode() ?? 0;
    }
}

/// <summary>
/// 蓝牙设备类型
/// </summary>
public enum BluetoothDeviceType
{
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 计算机
    /// </summary>
    Computer = 1,

    /// <summary>
    /// 手机
    /// </summary>
    Phone = 2,

    /// <summary>
    /// 音频设备（耳机、音箱等）
    /// </summary>
    Audio = 3,

    /// <summary>
    /// 键盘
    /// </summary>
    Keyboard = 4,

    /// <summary>
    /// 鼠标
    /// </summary>
    Mouse = 5,

    /// <summary>
    /// 游戏控制器
    /// </summary>
    GameController = 6,

    /// <summary>
    /// 健康设备
    /// </summary>
    Health = 7,

    /// <summary>
    /// 打印机
    /// </summary>
    Printer = 8,

    /// <summary>
    /// 成像设备
    /// </summary>
    Imaging = 9,

    /// <summary>
    /// 可穿戴设备
    /// </summary>
    Wearable = 10,

    /// <summary>
    /// 传感器
    /// </summary>
    Sensor = 11,

    /// <summary>
    /// 其他设备
    /// </summary>
    Other = 99
}
