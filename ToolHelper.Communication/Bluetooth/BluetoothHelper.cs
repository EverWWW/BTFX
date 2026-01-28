using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Bluetooth;

/// <summary>
/// 蓝牙通讯帮助类
/// 提供蓝牙设备扫描、连接、数据收发等功能
/// </summary>
/// <remarks>
/// 注意：此类在 Windows 平台上使用 Windows.Devices.Bluetooth API，
/// 需要在项目中添加 Windows 兼容性支持。
/// 对于跨平台场景，建议使用平台特定的蓝牙库。
/// 
/// 当前实现提供了接口定义和模拟实现，实际使用时需要根据目标平台
/// 集成相应的蓝牙库（如 InTheHand.Net.Bluetooth 或 Plugin.BLE）。
/// </remarks>
/// <example>
/// <code>
/// // 使用依赖注入
/// services.AddBluetooth(options => {
///     options.ScanTimeout = 10000;
///     options.DeviceAddress = "00:11:22:33:44:55";
/// });
/// 
/// // 手动创建
/// var bluetooth = new BluetoothHelper(options, logger);
/// 
/// // 扫描设备
/// var devices = await bluetooth.ScanDevicesAsync();
/// 
/// // 连接设备
/// if (await bluetooth.ConnectAsync())
/// {
///     await bluetooth.SendTextAsync("Hello");
/// }
/// </code>
/// </example>
public class BluetoothHelper : IClientConnection
{
    private readonly BluetoothOptions _options;
    private readonly ILogger<BluetoothHelper>? _logger;
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly List<BluetoothDeviceInfo> _discoveredDevices = [];

    private ConnectionState _state = ConnectionState.Disconnected;
    private BluetoothDeviceInfo? _connectedDevice;
    private CancellationTokenSource? _receiveCts;
    private CancellationTokenSource? _scanCts;
    private Stream? _bluetoothStream;
    private int _reconnectAttempts;
    private bool _isDisposed;

    /// <inheritdoc/>
    public bool IsConnected => _state == ConnectionState.Connected;

    /// <summary>
    /// 当前连接的设备
    /// </summary>
    public BluetoothDeviceInfo? ConnectedDevice => _connectedDevice;

    /// <summary>
    /// 已发现的设备列表
    /// </summary>
    public IReadOnlyList<BluetoothDeviceInfo> DiscoveredDevices => _discoveredDevices.AsReadOnly();

    /// <inheritdoc/>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 设备发现事件
    /// </summary>
    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

    /// <summary>
    /// 扫描完成事件
    /// </summary>
    public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;

    /// <summary>
    /// 蓝牙错误事件
    /// </summary>
    public event EventHandler<BluetoothErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">蓝牙配置选项</param>
    /// <param name="logger">日志记录器</param>
    public BluetoothHelper(IOptions<BluetoothOptions> options, ILogger<BluetoothHelper>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（手动配置）
    /// </summary>
    /// <param name="deviceAddress">设备地址</param>
    /// <param name="logger">日志记录器</param>
    public BluetoothHelper(string deviceAddress, ILogger<BluetoothHelper>? logger = null)
        : this(Options.Create(new BluetoothOptions { DeviceAddress = deviceAddress }), logger)
    {
    }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public BluetoothHelper() : this(Options.Create(new BluetoothOptions()), null)
    {
    }

    /// <summary>
    /// 扫描蓝牙设备
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发现的设备列表</returns>
    public async Task<IReadOnlyList<BluetoothDeviceInfo>> ScanDevicesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _discoveredDevices.Clear();
        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var startTime = DateTime.Now;

        try
        {
            _logger?.LogInformation("开始扫描蓝牙设备，超时时间: {Timeout}ms", _options.ScanTimeout);

            // 注意：实际实现需要使用平台特定的蓝牙 API
            // 这里提供模拟实现作为示例
            await ScanDevicesInternalAsync(_scanCts.Token);

            var duration = DateTime.Now - startTime;
            OnScanCompleted(new ScanCompletedEventArgs(_discoveredDevices, duration, isTimeout: true));

            _logger?.LogInformation("扫描完成，发现 {Count} 个设备", _discoveredDevices.Count);
            return _discoveredDevices.AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.Now - startTime;
            OnScanCompleted(new ScanCompletedEventArgs(_discoveredDevices, duration, isCancelled: true));
            _logger?.LogInformation("扫描被取消，已发现 {Count} 个设备", _discoveredDevices.Count);
            return _discoveredDevices.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "扫描蓝牙设备时发生错误");
            OnErrorOccurred(new BluetoothErrorEventArgs("扫描设备失败", BluetoothErrorType.ScanError, ex));
            throw;
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    /// <summary>
    /// 停止扫描
    /// </summary>
    public void StopScan()
    {
        _scanCts?.Cancel();
        _logger?.LogInformation("已请求停止扫描");
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsConnected)
        {
            _logger?.LogWarning("设备已处于连接状态");
            return true;
        }

        if (string.IsNullOrEmpty(_options.DeviceAddress) && string.IsNullOrEmpty(_options.DeviceName))
        {
            _logger?.LogError("未指定设备地址或设备名称");
            return false;
        }

        try
        {
            ChangeState(ConnectionState.Connecting);
            _logger?.LogInformation("正在连接蓝牙设备: {Address}", _options.DeviceAddress);

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectionTimeout);

            // 注意：实际实现需要使用平台特定的蓝牙 API
            var connected = await ConnectInternalAsync(connectCts.Token);

            if (connected)
            {
                _reconnectAttempts = 0;
                _connectedDevice = new BluetoothDeviceInfo
                {
                    Address = _options.DeviceAddress,
                    Name = _options.DeviceName,
                    IsConnected = true
                };

                ChangeState(ConnectionState.Connected);
                _logger?.LogInformation("蓝牙设备连接成功: {Address}", _options.DeviceAddress);
                return true;
            }
            else
            {
                ChangeState(ConnectionState.Disconnected);
                _logger?.LogWarning("蓝牙设备连接失败");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            ChangeState(ConnectionState.Disconnected);
            OnErrorOccurred(new BluetoothErrorEventArgs("连接超时", BluetoothErrorType.TimeoutError));
            _logger?.LogWarning("连接蓝牙设备超时");
            return false;
        }
        catch (Exception ex)
        {
            ChangeState(ConnectionState.Disconnected, ex);
            OnErrorOccurred(new BluetoothErrorEventArgs("连接失败", BluetoothErrorType.ConnectionError, ex));
            _logger?.LogError(ex, "连接蓝牙设备失败");

            if (_options.AutoReconnect && _reconnectAttempts < _options.MaxReconnectAttempts)
            {
                _ = Task.Run(() => ReconnectAsync(cancellationToken), cancellationToken);
            }

            return false;
        }
    }

    /// <summary>
    /// 按设备信息连接
    /// </summary>
    /// <param name="device">设备信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否连接成功</returns>
    public Task<bool> ConnectAsync(BluetoothDeviceInfo device, CancellationToken cancellationToken = default)
    {
        _options.DeviceAddress = device.Address;
        _options.DeviceName = device.Name;
        return ConnectAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_state == ConnectionState.Disconnected)
        {
            return;
        }

        try
        {
            ChangeState(ConnectionState.Disconnecting);
            _logger?.LogInformation("正在断开蓝牙连接");

            StopReceiving();

            await DisconnectInternalAsync(cancellationToken);

            _connectedDevice = null;
            ChangeState(ConnectionState.Disconnected);

            _logger?.LogInformation("蓝牙连接已断开");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "断开蓝牙连接时发生错误");
            OnErrorOccurred(new BluetoothErrorEventArgs("断开连接失败", BluetoothErrorType.DisconnectionError, ex));
            ChangeState(ConnectionState.Disconnected, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<int> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return await SendAsync(data.AsMemory(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            throw new InvalidOperationException("蓝牙设备未连接");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            _logger?.LogDebug("发送数据: {Length} 字节", data.Length);

            // 注意：实际实现需要写入蓝牙流
            if (_bluetoothStream != null)
            {
                await _bluetoothStream.WriteAsync(data, cancellationToken);
                await _bluetoothStream.FlushAsync(cancellationToken);
            }

            return data.Length;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "发送数据失败");
            OnErrorOccurred(new BluetoothErrorEventArgs("发送数据失败", BluetoothErrorType.SendError, ex));
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 发送文本数据
    /// </summary>
    /// <param name="text">要发送的文本</param>
    /// <param name="encoding">文本编码，默认 UTF-8</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> SendTextAsync(string text, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
        return await SendAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// 启动数据接收
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            throw new InvalidOperationException("蓝牙设备未连接");
        }

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger?.LogInformation("开始接收蓝牙数据");

        await Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), cancellationToken);
    }

    /// <summary>
    /// 停止数据接收
    /// </summary>
    public void StopReceiving()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;
        _logger?.LogInformation("已停止接收蓝牙数据");
    }

    /// <summary>
    /// 获取可用的蓝牙适配器信息
    /// </summary>
    /// <returns>适配器是否可用</returns>
    public Task<bool> IsAdapterAvailableAsync()
    {
        // 注意：实际实现需要检查蓝牙适配器状态
        // 这里返回模拟结果
        _logger?.LogDebug("检查蓝牙适配器状态");
        return Task.FromResult(true);
    }

    /// <summary>
    /// 配对设备
    /// </summary>
    /// <param name="device">要配对的设备</param>
    /// <param name="pin">配对 PIN 码（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否配对成功</returns>
    public async Task<bool> PairDeviceAsync(BluetoothDeviceInfo device, string? pin = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _logger?.LogInformation("正在配对设备: {Name} ({Address})", device.Name, device.Address);

            // 注意：实际实现需要使用平台特定的配对 API
            await Task.Delay(1000, cancellationToken); // 模拟配对过程

            device.IsPaired = true;
            _logger?.LogInformation("设备配对成功: {Address}", device.Address);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "设备配对失败: {Address}", device.Address);
            OnErrorOccurred(new BluetoothErrorEventArgs("配对失败", BluetoothErrorType.PairingError, ex));
            return false;
        }
    }

    /// <summary>
    /// 取消配对
    /// </summary>
    /// <param name="device">要取消配对的设备</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UnpairDeviceAsync(BluetoothDeviceInfo device, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _logger?.LogInformation("正在取消配对设备: {Address}", device.Address);

            // 注意：实际实现需要使用平台特定的取消配对 API
            await Task.Delay(500, cancellationToken);

            device.IsPaired = false;
            _logger?.LogInformation("设备取消配对成功: {Address}", device.Address);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "取消配对失败: {Address}", device.Address);
            return false;
        }
    }

    #region Internal Methods

    private async Task ScanDevicesInternalAsync(CancellationToken cancellationToken)
    {
        // 注意：这是模拟实现，实际应用中需要使用平台特定的蓝牙 API
        // 
        // Windows 平台可以使用：
        // - Windows.Devices.Bluetooth (UWP/WinRT)
        // - InTheHand.Net.Bluetooth (32feet.NET)
        //
        // 跨平台可以使用：
        // - Plugin.BLE (Xamarin/MAUI)

        _logger?.LogDebug("执行设备扫描（模拟实现）");

        // 模拟扫描过程
        var scanDuration = TimeSpan.FromMilliseconds(_options.ScanTimeout);
        var endTime = DateTime.Now.Add(scanDuration);

        while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken);

            // 模拟发现设备
            // 实际实现中，这里应该从蓝牙适配器获取设备
        }
    }

    private async Task<bool> ConnectInternalAsync(CancellationToken cancellationToken)
    {
        // 注意：这是模拟实现，实际应用中需要使用平台特定的蓝牙 API
        //
        // 经典蓝牙 (SPP) 连接示例：
        // var client = new BluetoothClient();
        // var device = BluetoothAddress.Parse(_options.DeviceAddress);
        // var endpoint = new BluetoothEndPoint(device, BluetoothService.SerialPort);
        // await client.ConnectAsync(endpoint);
        // _bluetoothStream = client.GetStream();
        //
        // BLE 连接示例：
        // var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        // var services = await device.GetGattServicesAsync();
        // var characteristic = await service.GetCharacteristicsAsync();

        _logger?.LogDebug("执行设备连接（模拟实现）");

        // 模拟连接过程
        await Task.Delay(1000, cancellationToken);

        // 模拟成功连接
        return true;
    }

    private async Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        // 关闭蓝牙流
        if (_bluetoothStream != null)
        {
            await _bluetoothStream.DisposeAsync();
            _bluetoothStream = null;
        }

        await Task.Delay(100, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = _bufferPool.Rent(_options.ReceiveBufferSize);

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    if (_bluetoothStream != null)
                    {
                        var bytesRead = await _bluetoothStream.ReadAsync(buffer, cancellationToken);
                        if (bytesRead > 0)
                        {
                            var data = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);

                            OnDataReceived(new DataReceivedEventArgs(data, bytesRead));
                            _logger?.LogDebug("接收到数据: {Length} 字节", bytesRead);
                        }
                    }
                    else
                    {
                        // 没有流时，等待一小段时间
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "接收数据时发生错误");
                    OnErrorOccurred(new BluetoothErrorEventArgs("接收数据失败", BluetoothErrorType.ReceiveError, ex));

                    if (!IsConnected)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        while (_reconnectAttempts < _options.MaxReconnectAttempts && !cancellationToken.IsCancellationRequested)
        {
            _reconnectAttempts++;
            ChangeState(ConnectionState.Reconnecting);

            _logger?.LogInformation("尝试重新连接 ({Attempt}/{Max})",
                _reconnectAttempts, _options.MaxReconnectAttempts);

            await Task.Delay(_options.ReconnectInterval, cancellationToken);

            if (await ConnectAsync(cancellationToken))
            {
                _logger?.LogInformation("重新连接成功");
                return;
            }
        }

        _logger?.LogWarning("重新连接失败，已达到最大重试次数");
        ChangeState(ConnectionState.Disconnected);
    }

    #endregion

    #region Event Handlers

    private void ChangeState(ConnectionState newState, Exception? exception = null)
    {
        var oldState = _state;
        _state = newState;

        if (oldState != newState)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState, exception));
        }
    }

    private void OnDataReceived(DataReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }

    private void OnDeviceDiscovered(DeviceDiscoveredEventArgs e)
    {
        DeviceDiscovered?.Invoke(this, e);
    }

    private void OnScanCompleted(ScanCompletedEventArgs e)
    {
        ScanCompleted?.Invoke(this, e);
    }

    private void OnErrorOccurred(BluetoothErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            StopReceiving();
            StopScan();

            _bluetoothStream?.Dispose();
            _sendLock.Dispose();
            _scanCts?.Dispose();
            _receiveCts?.Dispose();

            _discoveredDevices.Clear();
        }

        _isDisposed = true;
    }

    #endregion
}
