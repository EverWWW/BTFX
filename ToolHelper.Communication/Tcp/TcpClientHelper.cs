using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Tcp;

/// <summary>
/// TCP 客户端帮助类
/// 提供心跳保活、断线重连、粘包处理等功能
/// </summary>
public class TcpClientHelper : IClientConnection
{
    private readonly TcpClientOptions _options;
    private readonly ILogger<TcpClientHelper> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _receiveCts;
    private CancellationTokenSource? _heartbeatCts;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
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
    /// <param name="options">TCP 客户端配置</param>
    /// <param name="logger">日志记录器</param>
    public TcpClientHelper(IOptions<TcpClientOptions> options, ILogger<TcpClientHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（用于手动配置）
    /// </summary>
    /// <param name="host">服务器地址</param>
    /// <param name="port">服务器端口</param>
    /// <param name="logger">日志记录器</param>
    public TcpClientHelper(string host, int port, ILogger<TcpClientHelper> logger)
    {
        _options = new TcpClientOptions { Host = host, Port = port };
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
                ReceiveTimeout = _options.ReceiveTimeout,
                SendTimeout = _options.SendTimeout
            };

            // 设置 Keep-Alive
            if (_options.KeepAlive)
            {
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }

            // 使用超时连接
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectTimeout);

            await _tcpClient.ConnectAsync(_options.Host, _options.Port, connectCts.Token);
            _networkStream = _tcpClient.GetStream();

            _reconnectAttempts = 0;
            ChangeState(ConnectionState.Connected);

            _logger.LogInformation("成功连接到 {Host}:{Port}", _options.Host, _options.Port);

            // 启动心跳
            if (_options.EnableHeartbeat)
            {
                StartHeartbeat();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到 {Host}:{Port} 失败", _options.Host, _options.Port);
            ChangeState(ConnectionState.Disconnected, ex);

            // 如果启用了自动重连，则尝试重连
            if (_options.EnableAutoReconnect)
            {
                _ = Task.Run(() => ReconnectAsync(cancellationToken), cancellationToken);
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

            // 如果正在重连中，只清理资源，不改变状态
            var isReconnecting = _state == ConnectionState.Reconnecting;

            try
            {
                if (!isReconnecting)
                {
                    ChangeState(ConnectionState.Disconnecting);
                }

                // 停止接收和心跳
                StopReceiving();
                StopHeartbeat();

                // 关闭网络流和 TCP 客户端
                await CleanupConnectionAsync();

                if (!isReconnecting)
                {
                    ChangeState(ConnectionState.Disconnected);
                    _logger.LogInformation("已断开连接");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "断开连接时发生错误");
                if (!isReconnecting)
                {
                    ChangeState(ConnectionState.Disconnected, ex);
                }
            }
        }

        /// <summary>
        /// 清理连接资源（内部使用，不改变状态）
        /// </summary>
        private async Task CleanupConnectionAsync()
        {
            try
            {
                if (_networkStream != null)
                {
                    try
                    {
                        await _networkStream.FlushAsync();
                    }
                    catch { }

                    _networkStream.Close();
                    _networkStream = null;
                }

                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "清理连接资源时发生异常");
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
        if (!IsConnected || _networkStream == null)
        {
            throw new InvalidOperationException("未连接到服务器");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _networkStream.WriteAsync(data, cancellationToken);
            await _networkStream.FlushAsync(cancellationToken);

            _logger.LogDebug("发送了 {Length} 字节数据", data.Length);
            return data.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送数据失败");

            // 发送失败可能意味着连接已断开
            if (_options.EnableAutoReconnect)
            {
                _ = Task.Run(() => ReconnectAsync(cancellationToken), cancellationToken);
            }

            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
    {
        if (_receiveCts != null)
        {
            _logger.LogWarning("接收任务已在运行中");
            return;
        }

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await ReceiveLoopAsync(_receiveCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接收循环发生错误");
        }
    }

    /// <inheritdoc/>
    public void StopReceiving()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;
    }

    /// <summary>
    /// 接收循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = _bufferPool.Rent(_options.ReceiveBufferSize);
        var packetBuffer = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected && _networkStream != null)
            {
                try
                {
                    var bytesRead = await _networkStream.ReadAsync(buffer.AsMemory(0, _options.ReceiveBufferSize), cancellationToken);

                    if (bytesRead == 0)
                    {
                        _logger.LogWarning("服务器关闭了连接");

                        if (_options.EnableAutoReconnect)
                        {
                            // ? 修复：使用新的 CancellationToken，不要使用已被取消的 token
                            _ = Task.Run(() => ReconnectAsync(CancellationToken.None));
                        }
                        break;
                    }

                    _logger.LogDebug("接收到 {BytesRead} 字节数据", bytesRead);

                    // 处理粘包
                    if (_options.EnablePacketProcessing)
                    {
                        ProcessPacket(buffer, bytesRead, packetBuffer);
                    }
                    else
                    {
                        // 直接触发数据接收事件
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, 0, data, 0, bytesRead);
                        OnDataReceived(new DataReceivedEventArgs(data, bytesRead));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    _logger.LogWarning("连接被远程主机关闭");

                    if (_options.EnableAutoReconnect)
                    {
                        // ? 修复：使用新的 CancellationToken
                        _ = Task.Run(() => ReconnectAsync(CancellationToken.None));
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接收数据时发生错误");

                    if (_options.EnableAutoReconnect && !cancellationToken.IsCancellationRequested)
                    {
                        // ? 修复：使用新的 CancellationToken
                        _ = Task.Run(() => ReconnectAsync(CancellationToken.None));
                    }
                    break;
                }
            }
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    /// <summary>
    /// 处理粘包
    /// </summary>
    private void ProcessPacket(byte[] buffer, int bytesRead, List<byte> packetBuffer)
    {
        // 将新数据添加到缓冲区
        for (int i = 0; i < bytesRead; i++)
        {
            packetBuffer.Add(buffer[i]);

            // 检查是否超过最大包长度
            if (packetBuffer.Count > _options.MaxPacketLength)
            {
                _logger.LogWarning("包长度超过最大值 {MaxLength}，清空缓冲区", _options.MaxPacketLength);
                packetBuffer.Clear();
                continue;
            }

            // 如果定义了包尾，检查是否匹配
            if (_options.PacketTail != null && _options.PacketTail.Length > 0)
            {
                if (IsPacketComplete(packetBuffer, _options.PacketHeader, _options.PacketTail))
                {
                    // 提取完整的包
                    var packet = ExtractPacket(packetBuffer, _options.PacketHeader, _options.PacketTail);
                    if (packet != null)
                    {
                        OnDataReceived(new DataReceivedEventArgs(packet, packet.Length));
                    }
                }
            }
        }

        // 如果没有定义包尾，当缓冲区有数据时就触发事件
        if ((_options.PacketTail == null || _options.PacketTail.Length == 0) && packetBuffer.Count > 0)
        {
            var data = packetBuffer.ToArray();
            packetBuffer.Clear();
            OnDataReceived(new DataReceivedEventArgs(data, data.Length));
        }
    }

    /// <summary>
    /// 检查是否为完整的包
    /// </summary>
    private bool IsPacketComplete(List<byte> buffer, byte[]? header, byte[]? tail)
    {
        if (tail == null || tail.Length == 0)
        {
            return false;
        }

        if (buffer.Count < tail.Length)
        {
            return false;
        }

        // 检查包尾
        for (int i = 0; i < tail.Length; i++)
        {
            if (buffer[buffer.Count - tail.Length + i] != tail[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 提取完整的包
    /// </summary>
    private byte[]? ExtractPacket(List<byte> buffer, byte[]? header, byte[]? tail)
    {
        if (tail == null || tail.Length == 0)
        {
            return null;
        }

        var packet = buffer.ToArray();
        buffer.Clear();

        // 如果定义了包头，去除包头
        if (header != null && header.Length > 0 && packet.Length >= header.Length)
        {
            var startIndex = 0;
            for (int i = 0; i <= packet.Length - header.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < header.Length; j++)
                {
                    if (packet[i + j] != header[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    startIndex = i + header.Length;
                    break;
                }
            }

            var endIndex = packet.Length - tail.Length;
            if (endIndex > startIndex)
            {
                var result = new byte[endIndex - startIndex];
                Array.Copy(packet, startIndex, result, 0, result.Length);
                return result;
            }
        }

        return packet;
    }

    /// <summary>
    /// 启动心跳
    /// </summary>
    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            var heartbeatData = _options.HeartbeatData ?? new byte[] { 0xFF };

            while (!_heartbeatCts.Token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    await Task.Delay(_options.HeartbeatInterval, _heartbeatCts.Token);

                    if (IsConnected)
                    {
                        await SendAsync(heartbeatData, _heartbeatCts.Token);
                        _logger.LogDebug("发送心跳包");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送心跳包失败");
                }
            }
        }, _heartbeatCts.Token);
    }

    /// <summary>
    /// 停止心跳
    /// </summary>
    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
    }

    /// <summary>
    /// 重连
    /// </summary>
    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        if (_state == ConnectionState.Reconnecting)
        {
            _logger.LogDebug("已经在重连中，跳过");
            return;
        }

        ChangeState(ConnectionState.Reconnecting);
        _logger.LogInformation("检测到连接断开，开始自动重连...");

        while ((_options.MaxReconnectAttempts == -1 || _reconnectAttempts < _options.MaxReconnectAttempts)
               && !cancellationToken.IsCancellationRequested && !_isDisposed)
        {
            _reconnectAttempts++;
            _logger.LogInformation("尝试重连 (第 {Attempt}/{MaxAttempts} 次)...", 
                _reconnectAttempts, 
                _options.MaxReconnectAttempts == -1 ? "∞" : _options.MaxReconnectAttempts.ToString());

            try
            {
                // 清理旧连接资源（不改变状态）
                StopReceiving();
                StopHeartbeat();
                await CleanupConnectionAsync();

                // 等待重连间隔
                _logger.LogDebug("等待 {Interval}ms 后重试...", _options.ReconnectInterval);
                await Task.Delay(_options.ReconnectInterval, cancellationToken);

                // 尝试重新连接
                if (await ConnectAsync(cancellationToken))
                {
                    _logger.LogInformation("? 重连成功！");

                    // 重新启动接收任务（不要 await，让它在后台运行）
                    _ = StartReceivingAsync(CancellationToken.None);

                    return;
                }
                else
                {
                    _logger.LogWarning("重连尝试失败，将继续重试...");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("重连被取消");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重连过程中发生错误");
            }
        }

        // 重连失败
        if (_options.MaxReconnectAttempts != -1 && _reconnectAttempts >= _options.MaxReconnectAttempts)
        {
            _logger.LogError("? 达到最大重连次数 {MaxAttempts}，停止重连", _options.MaxReconnectAttempts);
            ChangeState(ConnectionState.Disconnected);
        }
        else if (_isDisposed)
        {
            _logger.LogInformation("客户端已释放，停止重连");
        }
    }

    /// <summary>
    /// 改变连接状态
    /// </summary>
    private void ChangeState(ConnectionState newState, Exception? exception = null)
    {
        var oldState = _state;
        _state = newState;

        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState, exception));
    }

    /// <summary>
    /// 触发数据接收事件
    /// </summary>
    private void OnDataReceived(DataReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        StopReceiving();
        StopHeartbeat();

        _networkStream?.Dispose();
        _tcpClient?.Dispose();
        _receiveCts?.Dispose();
        _heartbeatCts?.Dispose();
        _sendLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
