using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.IO.Ports;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.SerialPort;

/// <summary>
/// 串口通信帮助类
/// 提供串口自动识别、波特率自适应、数据收发等功能
/// </summary>
public class SerialPortHelper : IClientConnection
{
    private readonly SerialPortOptions _options;
    private readonly ILogger<SerialPortHelper> _logger;
    private System.IO.Ports.SerialPort? _serialPort;
    private CancellationTokenSource? _receiveCts;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private bool _isDisposed = false;

    /// <inheritdoc/>
    public bool IsConnected => _state == ConnectionState.Connected && _serialPort?.IsOpen == true;

    /// <inheritdoc/>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">串口配置选项</param>
    /// <param name="logger">日志记录器</param>
    public SerialPortHelper(IOptions<SerialPortOptions> options, ILogger<SerialPortHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（支持手动配置）
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <param name="baudRate">波特率</param>
    /// <param name="logger">日志记录器</param>
    public SerialPortHelper(string portName, int baudRate, ILogger<SerialPortHelper> logger)
    {
        _options = new SerialPortOptions { PortName = portName, BaudRate = baudRate };
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger.LogWarning("串口已经处于打开状态");
            return true;
        }

        try
        {
            ChangeState(ConnectionState.Connecting);

            // 自动识别串口
            if (_options.AutoDetectPort)
            {
                var detectedPort = await AutoDetectPortAsync(cancellationToken);
                if (string.IsNullOrEmpty(detectedPort))
                {
                    _logger.LogError("无法自动识别串口");
                    ChangeState(ConnectionState.Disconnected);
                    return false;
                }
                _options.PortName = detectedPort;
            }

            // 自动波特率适配
            if (_options.AutoBaudRate)
            {
                var detectedBaudRate = await AutoDetectBaudRateAsync(_options.PortName, cancellationToken);
                if (detectedBaudRate > 0)
                {
                    _options.BaudRate = detectedBaudRate;
                }
            }

            // 创建并配置串口
            _serialPort = new System.IO.Ports.SerialPort
            {
                PortName = _options.PortName,
                BaudRate = _options.BaudRate,
                DataBits = _options.DataBits,
                StopBits = _options.StopBits,
                Parity = _options.Parity,
                Handshake = _options.Handshake,
                ReadTimeout = _options.ReadTimeout,
                WriteTimeout = _options.WriteTimeout,
                ReadBufferSize = _options.ReceiveBufferSize,
                WriteBufferSize = _options.SendBufferSize,
                DtrEnable = _options.DtrEnable,
                RtsEnable = _options.RtsEnable
            };

            // 打开串口
            await Task.Run(() => _serialPort.Open(), cancellationToken);

            ChangeState(ConnectionState.Connected);
            _logger.LogInformation("成功打开串口 {PortName}, 波特率: {BaudRate}", _options.PortName, _options.BaudRate);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开串口失败: {Message}", ex.Message);
            ChangeState(ConnectionState.Disconnected);
            return false;
        }
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

            StopReceiving();

            if (_serialPort?.IsOpen == true)
            {
                await Task.Run(() => _serialPort.Close(), cancellationToken);
            }

            ChangeState(ConnectionState.Disconnected);
            _logger.LogInformation("串口已关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭串口时出错: {Message}", ex.Message);
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
        if (!IsConnected || _serialPort == null)
        {
            throw new InvalidOperationException("串口未打开");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _serialPort.BaseStream.WriteAsync(data, cancellationToken);
            await _serialPort.BaseStream.FlushAsync(cancellationToken);

            _logger.LogDebug("发送了 {Length} 字节数据", data.Length);
            return data.Length;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _serialPort == null)
        {
            throw new InvalidOperationException("串口未打开");
        }

        StopReceiving();

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

        _logger.LogDebug("开始接收数据");
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void StopReceiving()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;
        _logger.LogDebug("停止接收数据");
    }

    /// <summary>
    /// 自动检测串口
    /// </summary>
    private async Task<string?> AutoDetectPortAsync(CancellationToken cancellationToken)
    {
        var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
        _logger.LogInformation("检测到 {Count} 个可用串口: {Ports}", availablePorts.Length, string.Join(", ", availablePorts));

        if (_options.TestData == null || _options.ExpectedResponse == null)
        {
            _logger.LogWarning("自动检测需要配置测试数据和预期响应");
            return availablePorts.FirstOrDefault();
        }

        foreach (var portName in availablePorts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                using var testPort = new System.IO.Ports.SerialPort(portName, _options.BaudRate);
                testPort.Open();

                // 发送测试数据
                await testPort.BaseStream.WriteAsync(_options.TestData, cancellationToken);
                await testPort.BaseStream.FlushAsync(cancellationToken);

                // 等待响应
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.AutoDetectTimeout);

                var buffer = new byte[_options.ExpectedResponse.Length];
                var bytesRead = await testPort.BaseStream.ReadAsync(buffer, timeoutCts.Token);

                if (bytesRead == _options.ExpectedResponse.Length &&
                    buffer.SequenceEqual(_options.ExpectedResponse))
                {
                    _logger.LogInformation("自动检测到有效串口: {PortName}", portName);
                    return portName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("测试串口 {PortName} 失败: {Message}", portName, ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// 自动检测波特率
    /// </summary>
    private async Task<int> AutoDetectBaudRateAsync(string portName, CancellationToken cancellationToken)
    {
        if (_options.TestData == null || _options.ExpectedResponse == null)
        {
            _logger.LogWarning("自动波特率检测需要配置测试数据和预期响应");
            return 0;
        }

        foreach (var baudRate in _options.BaudRatesToTry)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                using var testPort = new System.IO.Ports.SerialPort(portName, baudRate);
                testPort.Open();

                // 发送测试数据
                await testPort.BaseStream.WriteAsync(_options.TestData, cancellationToken);
                await testPort.BaseStream.FlushAsync(cancellationToken);

                // 等待响应
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.AutoDetectTimeout);

                var buffer = new byte[_options.ExpectedResponse.Length];
                var bytesRead = await testPort.BaseStream.ReadAsync(buffer, timeoutCts.Token);

                if (bytesRead == _options.ExpectedResponse.Length &&
                    buffer.SequenceEqual(_options.ExpectedResponse))
                {
                    _logger.LogInformation("自动检测到有效波特率: {BaudRate}", baudRate);
                    return baudRate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("测试波特率 {BaudRate} 失败: {Message}", baudRate, ex.Message);
            }
        }

        return 0;
    }

    /// <summary>
    /// 数据接收循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = _bufferPool.Rent(_options.ReceiveBufferSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected && _serialPort != null)
            {
                try
                {
                    var bytesRead = await _serialPort.BaseStream.ReadAsync(buffer.AsMemory(0, _options.ReceiveBufferSize), cancellationToken);

                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, 0, data, 0, bytesRead);

                        _logger.LogDebug("接收到 {Length} 字节数据", bytesRead);

                        DataReceived?.Invoke(this, new DataReceivedEventArgs(data, bytesRead));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接收数据时出错: {Message}", ex.Message);

                    if (!IsConnected)
                    {
                        break;
                    }

                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    /// <summary>
    /// 更改连接状态
    /// </summary>
    private void ChangeState(ConnectionState newState)
    {
        if (_state != newState)
        {
            var oldState = _state;
            _state = newState;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
            _logger.LogDebug("连接状态从 {OldState} 变更为 {NewState}", oldState, newState);
        }
    }

    /// <summary>
    /// 获取可用串口列表
    /// </summary>
    /// <returns>可用串口名称列表</returns>
    public static string[] GetAvailablePorts()
    {
        return System.IO.Ports.SerialPort.GetPortNames();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        StopReceiving();

        if (_serialPort?.IsOpen == true)
        {
            _serialPort.Close();
        }

        _serialPort?.Dispose();
        _sendLock?.Dispose();
        _receiveCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
