using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.Sockets;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Modbus;

/// <summary>
/// Modbus TCP 协议帮助类
/// 支持主站/从站模式, 提供读写线圈、离散输入、保持寄存器、输入寄存器等功能
/// </summary>
public class ModbusTcpHelper : IClientConnection
{
    private readonly ModbusTcpOptions _options;
    private readonly ILogger<ModbusTcpHelper> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private ushort _transactionId = 0;
    private int _reconnectAttempts = 0;
    private bool _isDisposed = false;

    /// <inheritdoc/>
    public bool IsConnected => _state == ConnectionState.Connected && _tcpClient?.Connected == true;

    /// <inheritdoc/>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">Modbus TCP 配置选项</param>
    /// <param name="logger">日志记录器</param>
    public ModbusTcpHelper(IOptions<ModbusTcpOptions> options, ILogger<ModbusTcpHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（支持手动配置）
    /// </summary>
    /// <param name="host">服务器地址</param>
    /// <param name="port">服务器端口</param>
    /// <param name="unitId">从站地址</param>
    /// <param name="logger">日志记录器</param>
    public ModbusTcpHelper(string host, int port, byte unitId, ILogger<ModbusTcpHelper> logger)
    {
        _options = new ModbusTcpOptions { Host = host, Port = port, UnitId = unitId };
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger.LogWarning("已经处于连接状态");
            return true;
        }

        try
        {
            ChangeState(ConnectionState.Connecting);

            _tcpClient = new TcpClient
            {
                NoDelay = _options.NoDelay,
                ReceiveBufferSize = _options.ReceiveBufferSize,
                SendBufferSize = _options.SendBufferSize,
                ReceiveTimeout = _options.ReadTimeout,
                SendTimeout = _options.WriteTimeout
            };

            if (_options.KeepAlive)
            {
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectTimeout);

            await _tcpClient.ConnectAsync(_options.Host, _options.Port, connectCts.Token);
            _networkStream = _tcpClient.GetStream();

            _reconnectAttempts = 0;
            ChangeState(ConnectionState.Connected);

            _logger.LogInformation("成功连接到 Modbus TCP 服务器 {Host}:{Port}", _options.Host, _options.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接 Modbus TCP 服务器失败: {Message}", ex.Message);
            ChangeState(ConnectionState.Disconnected);

            if (_options.EnableAutoReconnect && _reconnectAttempts < _options.MaxReconnectAttempts)
            {
                _reconnectAttempts++;
                await Task.Delay(_options.ReconnectInterval, cancellationToken);
                return await ConnectAsync(cancellationToken);
            }

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

            _networkStream?.Close();
            _tcpClient?.Close();

            ChangeState(ConnectionState.Disconnected);
            _logger.LogInformation("Modbus TCP 连接已关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭 Modbus TCP 连接时出错: {Message}", ex.Message);
        }
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<int> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return await SendAsync(data.AsMemory(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _networkStream == null)
        {
            throw new InvalidOperationException("Modbus TCP 未连接");
        }

        await _networkStream.WriteAsync(data, cancellationToken);
        await _networkStream.FlushAsync(cancellationToken);

        _logger.LogDebug("发送了 {Length} 字节 Modbus 数据", data.Length);
        return data.Length;
    }

    /// <inheritdoc/>
    public Task StartReceivingAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Modbus TCP 使用请求-响应模式, 不需要主动接收");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void StopReceiving()
    {
        // Modbus TCP 使用请求-响应模式
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
    /// 构建 Modbus 请求
    /// </summary>
    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort quantity)
    {
        var transactionId = GetNextTransactionId();
        var request = new byte[12];

        // MBAP 头部
        request[0] = (byte)(transactionId >> 8);
        request[1] = (byte)(transactionId & 0xFF);
        request[2] = 0x00; // 协议标识符
        request[3] = 0x00;
        request[4] = 0x00; // 长度
        request[5] = 0x06;
        request[6] = _options.UnitId;

        // PDU
        request[7] = functionCode;
        request[8] = (byte)(startAddress >> 8);
        request[9] = (byte)(startAddress & 0xFF);
        request[10] = (byte)(quantity >> 8);
        request[11] = (byte)(quantity & 0xFF);

        return request;
    }

    /// <summary>
    /// 构建写多个线圈请求
    /// </summary>
    private byte[] BuildWriteMultipleCoilsRequest(ushort startAddress, bool[] values)
    {
        var transactionId = GetNextTransactionId();
        var byteCount = (values.Length + 7) / 8;
        var request = new byte[13 + byteCount];

        // MBAP 头部
        request[0] = (byte)(transactionId >> 8);
        request[1] = (byte)(transactionId & 0xFF);
        request[2] = 0x00;
        request[3] = 0x00;
        var length = (ushort)(7 + byteCount);
        request[4] = (byte)(length >> 8);
        request[5] = (byte)(length & 0xFF);
        request[6] = _options.UnitId;

        // PDU
        request[7] = 0x0F;
        request[8] = (byte)(startAddress >> 8);
        request[9] = (byte)(startAddress & 0xFF);
        request[10] = (byte)(values.Length >> 8);
        request[11] = (byte)(values.Length & 0xFF);
        request[12] = (byte)byteCount;

        // 数据
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                request[13 + i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return request;
    }

    /// <summary>
    /// 构建写多个寄存器请求
    /// </summary>
    private byte[] BuildWriteMultipleRegistersRequest(ushort startAddress, ushort[] values)
    {
        var transactionId = GetNextTransactionId();
        var byteCount = values.Length * 2;
        var request = new byte[13 + byteCount];

        // MBAP 头部
        request[0] = (byte)(transactionId >> 8);
        request[1] = (byte)(transactionId & 0xFF);
        request[2] = 0x00;
        request[3] = 0x00;
        var length = (ushort)(7 + byteCount);
        request[4] = (byte)(length >> 8);
        request[5] = (byte)(length & 0xFF);
        request[6] = _options.UnitId;

        // PDU
        request[7] = 0x10;
        request[8] = (byte)(startAddress >> 8);
        request[9] = (byte)(startAddress & 0xFF);
        request[10] = (byte)(values.Length >> 8);
        request[11] = (byte)(values.Length & 0xFF);
        request[12] = (byte)byteCount;

        // 数据
        for (int i = 0; i < values.Length; i++)
        {
            request[13 + i * 2] = (byte)(values[i] >> 8);
            request[14 + i * 2] = (byte)(values[i] & 0xFF);
        }

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
            if (!IsConnected || _networkStream == null)
            {
                throw new InvalidOperationException("Modbus TCP 未连接");
            }

            // 发送请求
            await _networkStream.WriteAsync(request, cancellationToken);
            await _networkStream.FlushAsync(cancellationToken);

            _logger.LogTrace("发送 Modbus 请求: {Request}", BitConverter.ToString(request));

            // 接收响应
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.TransactionTimeout);

            var buffer = _bufferPool.Rent(256);
            try
            {
                var bytesRead = await _networkStream.ReadAsync(buffer, timeoutCts.Token);
                var response = new byte[bytesRead];
                Array.Copy(buffer, 0, response, 0, bytesRead);

                _logger.LogTrace("接收 Modbus 响应: {Response}", BitConverter.ToString(response));

                ValidateResponse(request, response);

                return response;
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
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
        if (response.Length < 8)
        {
            throw new InvalidOperationException("响应长度不足");
        }

        // 检查事务 ID
        if (response[0] != request[0] || response[1] != request[1])
        {
            throw new InvalidOperationException("事务 ID 不匹配");
        }

        // 检查单元 ID
        if (response[6] != request[6])
        {
            throw new InvalidOperationException("单元 ID 不匹配");
        }

        // 检查功能码 (异常响应)
        if ((response[7] & 0x80) != 0)
        {
            var exceptionCode = response[8];
            throw new ModbusException($"Modbus 异常: 功能码 {response[7] & 0x7F}, 异常代码 {exceptionCode}");
        }
    }

    /// <summary>
    /// 解析线圈响应
    /// </summary>
    private bool[] ParseCoilsResponse(byte[] response, ushort quantity)
    {
        var byteCount = response[8];
        var coils = new bool[quantity];

        for (int i = 0; i < quantity; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            coils[i] = (response[9 + byteIndex] & (1 << bitIndex)) != 0;
        }

        return coils;
    }

    /// <summary>
    /// 解析寄存器响应
    /// </summary>
    private ushort[] ParseRegistersResponse(byte[] response, ushort quantity)
    {
        var byteCount = response[8];
        var registers = new ushort[quantity];

        for (int i = 0; i < quantity; i++)
        {
            registers[i] = (ushort)((response[9 + i * 2] << 8) | response[10 + i * 2]);
        }

        return registers;
    }

    /// <summary>
    /// 获取下一个事务 ID
    /// </summary>
    private ushort GetNextTransactionId()
    {
        return ++_transactionId;
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

        _networkStream?.Dispose();
        _tcpClient?.Dispose();
        _transactionLock?.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Modbus 异常
/// </summary>
public class ModbusException : Exception
{
    public ModbusException(string message) : base(message) { }
}
