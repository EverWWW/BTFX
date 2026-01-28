namespace ToolHelper.Communication.Bluetooth;

/// <summary>
/// 设备发现事件参数
/// </summary>
public class DeviceDiscoveredEventArgs : EventArgs
{
    /// <summary>
    /// 发现的设备信息
    /// </summary>
    public BluetoothDeviceInfo Device { get; }

    /// <summary>
    /// 是否是新设备（首次发现）
    /// </summary>
    public bool IsNewDevice { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="device">设备信息</param>
    /// <param name="isNewDevice">是否是新设备</param>
    public DeviceDiscoveredEventArgs(BluetoothDeviceInfo device, bool isNewDevice = true)
    {
        Device = device;
        IsNewDevice = isNewDevice;
    }
}

/// <summary>
/// 扫描完成事件参数
/// </summary>
public class ScanCompletedEventArgs : EventArgs
{
    /// <summary>
    /// 发现的所有设备列表
    /// </summary>
    public IReadOnlyList<BluetoothDeviceInfo> Devices { get; }

    /// <summary>
    /// 扫描是否因超时而完成
    /// </summary>
    public bool IsTimeout { get; }

    /// <summary>
    /// 扫描是否被取消
    /// </summary>
    public bool IsCancelled { get; }

    /// <summary>
    /// 扫描耗时
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="devices">设备列表</param>
    /// <param name="duration">扫描耗时</param>
    /// <param name="isTimeout">是否超时</param>
    /// <param name="isCancelled">是否取消</param>
    public ScanCompletedEventArgs(
        IReadOnlyList<BluetoothDeviceInfo> devices,
        TimeSpan duration,
        bool isTimeout = false,
        bool isCancelled = false)
    {
        Devices = devices;
        Duration = duration;
        IsTimeout = isTimeout;
        IsCancelled = isCancelled;
    }
}

/// <summary>
/// 蓝牙数据接收事件参数
/// </summary>
public class BluetoothDataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 接收到的数据
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// 数据长度
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedTime { get; }

    /// <summary>
    /// 来源设备
    /// </summary>
    public BluetoothDeviceInfo? SourceDevice { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="data">接收的数据</param>
    /// <param name="length">数据长度</param>
    /// <param name="sourceDevice">来源设备</param>
    public BluetoothDataReceivedEventArgs(byte[] data, int length, BluetoothDeviceInfo? sourceDevice = null)
    {
        Data = data;
        Length = length;
        ReceivedTime = DateTime.Now;
        SourceDevice = sourceDevice;
    }
}

/// <summary>
/// 蓝牙错误事件参数
/// </summary>
public class BluetoothErrorEventArgs : EventArgs
{
    /// <summary>
    /// 错误信息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public BluetoothErrorType ErrorType { get; }

    /// <summary>
    /// 发生时间
    /// </summary>
    public DateTime OccurredTime { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误信息</param>
    /// <param name="errorType">错误类型</param>
    /// <param name="exception">异常对象</param>
    public BluetoothErrorEventArgs(string message, BluetoothErrorType errorType, Exception? exception = null)
    {
        Message = message;
        ErrorType = errorType;
        Exception = exception;
        OccurredTime = DateTime.Now;
    }
}

/// <summary>
/// 蓝牙错误类型
/// </summary>
public enum BluetoothErrorType
{
    /// <summary>
    /// 未知错误
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 扫描错误
    /// </summary>
    ScanError,

    /// <summary>
    /// 连接错误
    /// </summary>
    ConnectionError,

    /// <summary>
    /// 断开连接错误
    /// </summary>
    DisconnectionError,

    /// <summary>
    /// 发送数据错误
    /// </summary>
    SendError,

    /// <summary>
    /// 接收数据错误
    /// </summary>
    ReceiveError,

    /// <summary>
    /// 配对错误
    /// </summary>
    PairingError,

    /// <summary>
    /// 超时错误
    /// </summary>
    TimeoutError,

    /// <summary>
    /// 权限错误
    /// </summary>
    PermissionError,

    /// <summary>
    /// 蓝牙适配器不可用
    /// </summary>
    AdapterUnavailable
}
