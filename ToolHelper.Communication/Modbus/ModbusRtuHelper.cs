using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.IO.Ports;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Modbus;

/// <summary>
/// Modbus RTU 协议帮助类
/// 支持串口通信的 Modbus RTU 协议, 提供读写线圈、离散输入、保持寄存器、输入寄存器等功能
/// </summary>
public class ModbusRtuHelper : IClientConnection
{
    private readonly ModbusRtuOptions _options;
    private readonly ILogger<ModbusRtuHelper> _logger;
    private System.IO.Ports.SerialPort? _serialPort;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private bool _isDisposed = false;
    private byte _currentSlaveId;

    /// <inheritdoc/>
    public bool IsConnected => _state == ConnectionState.Connected && _serialPort?.IsOpen == true;

    /// <summary>
    /// 当前从站地址
    /// </summary>
    public byte SlaveId
    {
        get => _currentSlaveId;
        set
        {
            if (value < 1 || value > 247)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "从站地址必须在 1-247 之间");
            }
            _currentSlaveId = value;
            _logger.LogDebug("从站地址已切换为 {SlaveId}", value);
        }
    }

    /// <inheritdoc/>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">Modbus RTU 配置选项</param>
    /// <param name="logger">日志记录器</param>
    public ModbusRtuHelper(IOptions<ModbusRtuOptions> options, ILogger<ModbusRtuHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
        _currentSlaveId = _options.SlaveId;
    }

    /// <summary>
    /// 构造函数（支持手动配置）
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <param name="baudRate">波特率</param>
    /// <param name="slaveId">从站地址</param>
    /// <param name="logger">日志记录器</param>
    public ModbusRtuHelper(string portName, int baudRate, byte slaveId, ILogger<ModbusRtuHelper> logger)
    {
        _options = new ModbusRtuOptions { PortName = portName, BaudRate = baudRate, SlaveId = slaveId };
        _logger = logger;
        _currentSlaveId = slaveId;
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

            // 检查串口是否存在
            var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
            if (!availablePorts.Contains(_options.PortName, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogError("串口 {PortName} 不存在, 可用串口: {AvailablePorts}", 
                    _options.PortName, string.Join(", ", availablePorts));
                ChangeState(ConnectionState.Disconnected);
                return false;
            }

                _serialPort = new System.IO.Ports.SerialPort
                {
                    PortName = _options.PortName,
                    BaudRate = _options.BaudRate,
                    DataBits = _options.DataBits,
                    StopBits = _options.StopBits,
                    Parity = _options.Parity,
                    ReadTimeout = _options.ReadTimeout,
                    WriteTimeout = _options.WriteTimeout,
                    ReadBufferSize = _options.ReceiveBufferSize,
                    WriteBufferSize = _options.SendBufferSize,
                    DtrEnable = true,
                    RtsEnable = true,
                    Handshake = System.IO.Ports.Handshake.None
                };

                // 直接在当前线程打开串口，避免 Task.Run 可能带来的异常传播复杂性
                // 注意: Open() 是阻塞操作，如果串口响应慢可能会阻塞当前线程
                try 
                {
                    _serialPort.Open();
                }
                catch
                {
                    // 如果打开失败，确保释放资源
                    _serialPort.Dispose();
                    _serialPort = null;
                    throw;
                }

                ChangeState(ConnectionState.Connected);
                _logger.LogInformation("成功打开 Modbus RTU 串口 {PortName}, 波特率: {BaudRate}", _options.PortName, _options.BaudRate);

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "串口 {PortName} 被占用或没有访问权限", _options.PortName);
                ChangeState(ConnectionState.Disconnected);
                return false;
            }
            catch (System.IO.IOException ex)
            {
                // 捕获所有 IO 异常（包括信号灯超时）
                _logger.LogError("串口 {PortName} 打开失败: {Message}. 可能原因: 端口被占用、驱动问题或设备未连接。", _options.PortName, ex.Message);
                ChangeState(ConnectionState.Disconnected);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "打开 Modbus RTU 串口失败: {Message}", ex.Message);
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

            if (_serialPort?.IsOpen == true)
            {
                await Task.Run(() => _serialPort.Close(), cancellationToken);
            }

            ChangeState(ConnectionState.Disconnected);
            _logger.LogInformation("Modbus RTU 串口已关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭 Modbus RTU 串口时出错: {Message}", ex.Message);
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
            throw new InvalidOperationException("Modbus RTU 串口未打开");
        }

        await _serialPort.BaseStream.WriteAsync(data, cancellationToken);
        await _serialPort.BaseStream.FlushAsync(cancellationToken);

        _logger.LogDebug("发送了 {Length} 字节 Modbus RTU 数据", data.Length);
        return data.Length;
    }

    /// <inheritdoc/>
    public Task StartReceivingAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Modbus RTU 使用请求-响应模式, 不需要主动接收");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void StopReceiving()
    {
        // Modbus RTU 使用请求-响应模式
    }

    #region Modbus 功能码实现

    /// <summary>
    /// 读取线圈 (功能码 0x01)
    /// </summary>
    /// <param name="startAddress">起始地址</param>
    /// <param name="quantity">数量 (1-2000)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>线圈状态数组</returns>
    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
    {
        if (quantity < 1 || quantity > 2000)
            throw new ArgumentException("数量必须在 1-2000 之间", nameof(quantity));

        var request = BuildModbusRequest(0x01, startAddress, quantity);
        var response = await SendRequestAndReceiveResponseAsync(request, cancellationToken);

        return ParseCoilsResponse(response, quantity);
    }

    /// <summary>
    /// 读取线圈 (功能码 0x01) - 指定从站地址
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="quantity">数量 (1-2000)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>线圈状态数组</returns>
    public async Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
    {
        SlaveId = slaveId;
        return await ReadCoilsAsync(startAddress, quantity, cancellationToken);
    }

    /// <summary>
    /// 读取离散输入 (功能码 0x02)
    /// </summary>
    /// <param name="startAddress">起始地址</param>
    /// <param name="quantity">数量 (1-2000)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>离散输入状态数组</returns>
    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
    {
        if (quantity < 1 || quantity > 2000)
            throw new ArgumentException("数量必须在 1-2000 之间", nameof(quantity));

        var request = BuildModbusRequest(0x02, startAddress, quantity);
        var response = await SendRequestAndReceiveResponseAsync(request, cancellationToken);

        return ParseCoilsResponse(response, quantity);
    }

    /// <summary>
    /// 读取离散输入 (功能码 0x02) - 指定从站地址
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="startAddress">起始地址</param>
    /// <param name="quantity">数量 (1-2000)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>离散输入状态数组</returns>
    public async Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
    {
            SlaveId = slaveId;
            return await ReadDiscreteInputsAsync(startAddress, quantity, cancellationToken);
        }

        /// <summary>
        /// 读取保持寄存器 (功能码 0x03)
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="quantity">数量 (1-125)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>寄存器值数组</returns>
        public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
        {
            if (quantity < 1 || quantity > 125)
                throw new ArgumentException("数量必须在 1-125 之间", nameof(quantity));

            var request = BuildModbusRequest(0x03, startAddress, quantity);
            var response = await SendRequestAndReceiveResponseAsync(request, cancellationToken);

            return ParseRegistersResponse(response, quantity);
        }

        /// <summary>
        /// 读取保持寄存器 (功能码 0x03) - 指定从站地址
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="quantity">数量 (1-125)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>寄存器值数组</returns>
        public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
        {
            SlaveId = slaveId;
            return await ReadHoldingRegistersAsync(startAddress, quantity, cancellationToken);
        }

        /// <summary>
        /// 读取输入寄存器 (功能码 0x04)
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="quantity">数量 (1-125)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>寄存器值数组</returns>
        public async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
        {
            if (quantity < 1 || quantity > 125)
                throw new ArgumentException("数量必须在 1-125 之间", nameof(quantity));

            var request = BuildModbusRequest(0x04, startAddress, quantity);
            var response = await SendRequestAndReceiveResponseAsync(request, cancellationToken);

            return ParseRegistersResponse(response, quantity);
        }

        /// <summary>
        /// 读取输入寄存器 (功能码 0x04) - 指定从站地址
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="quantity">数量 (1-125)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>寄存器值数组</returns>
        public async Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort quantity, CancellationToken cancellationToken = default)
        {
            SlaveId = slaveId;
            return await ReadInputRegistersAsync(startAddress, quantity, cancellationToken);
        }

        /// <summary>
    /// 写单个线圈 (功能码 0x05)
    /// </summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken cancellationToken = default)
    {
        ushort coilValue = value ? (ushort)0xFF00 : (ushort)0x0000;
        var request = BuildModbusRequest(0x05, address, coilValue);
        await SendRequestAndReceiveResponseAsync(request, cancellationToken);
    }

    /// <summary>
    /// 写单个寄存器 (功能码 0x06)
    /// </summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken = default)
    {
        var request = BuildModbusRequest(0x06, address, value);
        await SendRequestAndReceiveResponseAsync(request, cancellationToken);
    }

    /// <summary>
    /// 写多个线圈 (功能码 0x0F)
    /// </summary>
    /// <param name="startAddress">起始地址</param>
    /// <param name="values">线圈值数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, CancellationToken cancellationToken = default)
    {
        if (values.Length < 1 || values.Length > 1968)
            throw new ArgumentException("数量必须在 1-1968 之间", nameof(values));

        var request = BuildWriteMultipleCoilsRequest(startAddress, values);
        await SendRequestAndReceiveResponseAsync(request, cancellationToken);
    }

    /// <summary>
    /// 写多个寄存器 (功能码 0x10)
    /// </summary>
    /// <param name="startAddress">起始地址</param>
    /// <param name="values">寄存器值数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        if (values.Length < 1 || values.Length > 123)
            throw new ArgumentException("数量必须在 1-123 之间", nameof(values));

        var request = BuildWriteMultipleRegistersRequest(startAddress, values);
        await SendRequestAndReceiveResponseAsync(request, cancellationToken);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 构建 Modbus RTU 请求
    /// </summary>
    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort quantity)
    {
        var request = new byte[8];

        request[0] = _currentSlaveId;
        request[1] = functionCode;
        request[2] = (byte)(startAddress >> 8);
        request[3] = (byte)(startAddress & 0xFF);
        request[4] = (byte)(quantity >> 8);
        request[5] = (byte)(quantity & 0xFF);

        // 计算 CRC
        var crc = CalculateCrc(request, 0, 6);
        request[6] = (byte)(crc & 0xFF);
        request[7] = (byte)(crc >> 8);

        return request;
    }

    /// <summary>
    /// 构建写多个线圈请求
    /// </summary>
    private byte[] BuildWriteMultipleCoilsRequest(ushort startAddress, bool[] values)
    {
        var byteCount = (values.Length + 7) / 8;
        var request = new byte[9 + byteCount];

        request[0] = _currentSlaveId;
        request[1] = 0x0F;
        request[2] = (byte)(startAddress >> 8);
        request[3] = (byte)(startAddress & 0xFF);
        request[4] = (byte)(values.Length >> 8);
        request[5] = (byte)(values.Length & 0xFF);
        request[6] = (byte)byteCount;

        // 数据
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                request[7 + i / 8] |= (byte)(1 << (i % 8));
            }
        }

        // 计算 CRC
        var crc = CalculateCrc(request, 0, 7 + byteCount);
        request[7 + byteCount] = (byte)(crc & 0xFF);
        request[8 + byteCount] = (byte)(crc >> 8);

        return request;
    }

    /// <summary>
    /// 构建写多个寄存器请求
    /// </summary>
    private byte[] BuildWriteMultipleRegistersRequest(ushort startAddress, ushort[] values)
    {
        var byteCount = values.Length * 2;
        var request = new byte[9 + byteCount];

        request[0] = _currentSlaveId;
        request[1] = 0x10;
        request[2] = (byte)(startAddress >> 8);
        request[3] = (byte)(startAddress & 0xFF);
        request[4] = (byte)(values.Length >> 8);
        request[5] = (byte)(values.Length & 0xFF);
        request[6] = (byte)byteCount;

        // 数据
        for (int i = 0; i < values.Length; i++)
        {
            request[7 + i * 2] = (byte)(values[i] >> 8);
            request[8 + i * 2] = (byte)(values[i] & 0xFF);
        }

        // 计算 CRC
        var crc = CalculateCrc(request, 0, 7 + byteCount);
        request[7 + byteCount] = (byte)(crc & 0xFF);
        request[8 + byteCount] = (byte)(crc >> 8);

        return request;
    }

    /// <summary>
    /// 发送请求并接收响应
    /// </summary>
    private async Task<byte[]> SendRequestAndReceiveResponseAsync(byte[] request, CancellationToken cancellationToken)
    {
        await _transactionLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsConnected || _serialPort == null)
            {
                throw new InvalidOperationException("Modbus RTU 串口未打开");
            }

            // 重试机制
            for (int retry = 0; retry <= _options.MaxRetries; retry++)
            {
                try
                {
                    // 清空缓冲区
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();

                    // 发送请求
                    await _serialPort.BaseStream.WriteAsync(request, cancellationToken);
                    await _serialPort.BaseStream.FlushAsync(cancellationToken);

                    _logger.LogTrace("发送 Modbus RTU 请求: {Request}", BitConverter.ToString(request));

                    // 等待帧间隔
                    await Task.Delay(_options.FrameDelay, cancellationToken);

                    // 接收响应
                    var buffer = _bufferPool.Rent(256);
                    try
                    {
                        var totalBytesRead = 0;
                        var startTime = DateTime.Now;

                        while ((DateTime.Now - startTime).TotalMilliseconds < _options.ReadTimeout)
                        {
                            if (_serialPort.BytesToRead > 0)
                            {
                                var bytesRead = await _serialPort.BaseStream.ReadAsync(
                                    buffer.AsMemory(totalBytesRead, buffer.Length - totalBytesRead), cancellationToken);
                                totalBytesRead += bytesRead;

                                // 等待帧间隔以确保接收完整
                                await Task.Delay(_options.FrameDelay, cancellationToken);

                                // 如果没有更多数据, 认为接收完成
                                if (_serialPort.BytesToRead == 0)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                await Task.Delay(10, cancellationToken);
                            }
                        }

                        if (totalBytesRead == 0)
                        {
                            throw new TimeoutException("接收响应超时");
                        }

                        var response = new byte[totalBytesRead];
                        Array.Copy(buffer, 0, response, 0, totalBytesRead);

                        _logger.LogTrace("接收 Modbus RTU 响应: {Response}", BitConverter.ToString(response));

                        ValidateResponse(request, response);

                        return response;
                    }
                    finally
                    {
                        _bufferPool.Return(buffer);
                    }
                }
                catch (Exception ex) when (retry < _options.MaxRetries)
                {
                    _logger.LogWarning("Modbus RTU 通信失败 (第 {Retry}/{MaxRetries} 次重试): {Message}",
                        retry + 1, _options.MaxRetries, ex.Message);
                    await Task.Delay(_options.RetryInterval, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Modbus RTU 通信失败, 已重试 {_options.MaxRetries} 次");
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    /// <summary>
    /// 验证响应
    /// </summary>
    private void ValidateResponse(byte[] request, byte[] response)
    {
        if (response.Length < 4)
        {
            throw new InvalidOperationException("响应长度不足");
        }

        // 检查从站地址
        if (response[0] != request[0])
        {
            throw new InvalidOperationException("从站地址不匹配");
        }

        // 检查功能码 (异常响应)
        if ((response[1] & 0x80) != 0)
        {
            var exceptionCode = response[2];
            throw new ModbusException($"Modbus RTU 异常: 功能码 {response[1] & 0x7F}, 异常代码 {exceptionCode}");
        }

        // 检查 CRC
        if (_options.EnableCrcCheck)
        {
            var receivedCrc = (ushort)((response[response.Length - 1] << 8) | response[response.Length - 2]);
            var calculatedCrc = CalculateCrc(response, 0, response.Length - 2);

            if (receivedCrc != calculatedCrc)
            {
                throw new InvalidOperationException($"CRC 校验失败: 接收 {receivedCrc:X4}, 计算 {calculatedCrc:X4}");
            }
        }
    }

    /// <summary>
    /// 解析线圈响应
    /// </summary>
    private bool[] ParseCoilsResponse(byte[] response, ushort quantity)
    {
        var byteCount = response[2];
        var coils = new bool[quantity];

        for (int i = 0; i < quantity; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            coils[i] = (response[3 + byteIndex] & (1 << bitIndex)) != 0;
        }

        return coils;
    }

    /// <summary>
    /// 解析寄存器响应
    /// </summary>
    private ushort[] ParseRegistersResponse(byte[] response, ushort quantity)
    {
        var byteCount = response[2];
        var registers = new ushort[quantity];

        for (int i = 0; i < quantity; i++)
        {
            registers[i] = (ushort)((response[3 + i * 2] << 8) | response[4 + i * 2]);
        }

        return registers;
    }

    /// <summary>
    /// 计算 CRC-16 (Modbus)
    /// </summary>
    private ushort CalculateCrc(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];

            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return crc;
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

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_serialPort?.IsOpen == true)
        {
            _serialPort.Close();
        }

        _serialPort?.Dispose();
        _transactionLock?.Dispose();

        GC.SuppressFinalize(this);
    }
}
