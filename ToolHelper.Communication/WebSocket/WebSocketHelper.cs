using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.WebSocket;

/// <summary>
/// WebSocket 通信帮助类
/// 提供实时双向通信、自动重连、心跳保活等功能
/// </summary>
public class WebSocketHelper : IClientConnection
{
    private readonly WebSocketOptions _options;
    private readonly ILogger<WebSocketHelper> _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private CancellationTokenSource? _heartbeatCts;
    private ConnectionState _state = ConnectionState.Disconnected;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private int _reconnectAttempts = 0;
    private bool _isDisposed = false;
    private DateTime _lastHeartbeatTime = DateTime.Now;

    /// <inheritdoc/>
    public bool IsConnected => _state == ConnectionState.Connected && 
                               _webSocket?.State == WebSocketState.Open;

    /// <inheritdoc/>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 文本消息接收事件
    /// </summary>
    public event EventHandler<TextMessageReceivedEventArgs>? TextMessageReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">WebSocket 配置选项</param>
    /// <param name="logger">日志记录器</param>
    public WebSocketHelper(IOptions<WebSocketOptions> options, ILogger<WebSocketHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（支持手动配置）
    /// </summary>
    /// <param name="uri">服务器地址</param>
    /// <param name="logger">日志记录器</param>
    public WebSocketHelper(string uri, ILogger<WebSocketHelper> logger)
    {
        _options = new WebSocketOptions { Uri = uri };
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger.LogWarning("WebSocket 已经处于连接状态");
            return true;
        }

        try
        {
            ChangeState(ConnectionState.Connecting);

            _webSocket = new ClientWebSocket();
            
            // 配置选项
            _webSocket.Options.SetBuffer(_options.ReceiveBufferSize, _options.SendBufferSize);

            // 添加子协议
            foreach (var subProtocol in _options.SubProtocols)
            {
                _webSocket.Options.AddSubProtocol(subProtocol);
            }

            // 添加请求头
            foreach (var header in _options.Headers)
            {
                _webSocket.Options.SetRequestHeader(header.Key, header.Value);
            }

            // 设置 Cookie
            if (_options.Cookies != null)
            {
                _webSocket.Options.Cookies = _options.Cookies;
            }

            // 使用超时控制
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectTimeout);

            await _webSocket.ConnectAsync(new Uri(_options.Uri), connectCts.Token);

            _reconnectAttempts = 0;
            ChangeState(ConnectionState.Connected);
            _lastHeartbeatTime = DateTime.Now;

            _logger.LogInformation("成功连接到 WebSocket 服务器: {Uri}", _options.Uri);

            // 启动心跳
            if (_options.HeartbeatInterval > 0)
            {
                StartHeartbeat();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接 WebSocket 服务器失败: {Message}", ex.Message);
            ChangeState(ConnectionState.Disconnected);

            // 尝试重连
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

        try
        {
            ChangeState(ConnectionState.Disconnecting);

            StopReceiving();
            StopHeartbeat();

            if (_webSocket?.State == WebSocketState.Open || _webSocket?.State == WebSocketState.CloseReceived)
            {
                using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                closeCts.CancelAfter(_options.CloseTimeout);

                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常关闭", closeCts.Token);
            }

            ChangeState(ConnectionState.Disconnected);
            _logger.LogInformation("WebSocket 连接已关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭 WebSocket 连接时出错: {Message}", ex.Message);
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
        if (!IsConnected || _webSocket == null)
        {
            throw new InvalidOperationException("WebSocket 未连接");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCts.CancelAfter(_options.SendTimeout);

            await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, sendCts.Token);

            _logger.LogDebug("发送了 {Length} 字节二进制数据", data.Length);
            return data.Length;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _webSocket == null)
        {
            throw new InvalidOperationException("WebSocket 未连接");
        }

        var data = Encoding.UTF8.GetBytes(text);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCts.CancelAfter(_options.SendTimeout);

            await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, sendCts.Token);

            _logger.LogDebug("发送了文本消息: {Text}", text.Length > 100 ? text.Substring(0, 100) + "..." : text);
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
        if (!IsConnected || _webSocket == null)
        {
            throw new InvalidOperationException("WebSocket 未连接");
        }

        StopReceiving();

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

        _logger.LogDebug("开始接收 WebSocket 消息");
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void StopReceiving()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;
        _logger.LogDebug("停止接收 WebSocket 消息");
    }

    /// <summary>
    /// 启动心跳
    /// </summary>
    private void StartHeartbeat()
    {
        StopHeartbeat();

        _heartbeatCts = new CancellationTokenSource();
        _ = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token), _heartbeatCts.Token);

        _logger.LogDebug("启动心跳, 间隔: {Interval}ms", _options.HeartbeatInterval);
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
    /// 心跳循环
    /// </summary>
    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(_options.HeartbeatInterval, cancellationToken);

                if (IsConnected && _webSocket != null)
                {
                    // 发送 Ping 消息
                    var pingData = new byte[0];
                    await _webSocket.SendAsync(pingData, WebSocketMessageType.Text, true, cancellationToken);

                    _logger.LogTrace("发送心跳");

                    // 检查心跳超时
                    if ((DateTime.Now - _lastHeartbeatTime).TotalMilliseconds > _options.HeartbeatTimeout)
                    {
                        _logger.LogWarning("心跳超时, 尝试重连");
                        await DisconnectAsync(cancellationToken);

                        if (_options.EnableAutoReconnect)
                        {
                            _ = Task.Run(() => ReconnectAsync(cancellationToken), cancellationToken);
                        }
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳发送失败: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 重连循环
    /// </summary>
    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && 
               (_options.MaxReconnectAttempts == -1 || _reconnectAttempts < _options.MaxReconnectAttempts))
        {
            _reconnectAttempts++;
            _logger.LogInformation("尝试重连 (第 {Attempt} 次)...", _reconnectAttempts);

            await Task.Delay(_options.ReconnectInterval, cancellationToken);

            if (await ConnectAsync(cancellationToken))
            {
                _logger.LogInformation("重连成功");
                return;
            }
        }

        _logger.LogError("达到最大重连次数 ({MaxAttempts}), 停止重连", _options.MaxReconnectAttempts);
    }

    /// <summary>
    /// 数据接收循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = _bufferPool.Rent(_options.ReceiveBufferSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected && _webSocket != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    _lastHeartbeatTime = DateTime.Now;

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("服务器关闭了连接: {Status} - {Description}",
                            result.CloseStatus, result.CloseStatusDescription);

                        await DisconnectAsync(cancellationToken);

                        if (_options.EnableAutoReconnect)
                        {
                            _ = Task.Run(() => ReconnectAsync(cancellationToken), cancellationToken);
                        }
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var data = ms.ToArray();
                        var text = Encoding.UTF8.GetString(data);

                        _logger.LogDebug("接收到文本消息: {Length} 字节", data.Length);

                        TextMessageReceived?.Invoke(this, new TextMessageReceivedEventArgs(text, data, data.Length));
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(data, data.Length));
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var data = ms.ToArray();

                        _logger.LogDebug("接收到二进制消息: {Length} 字节", data.Length);

                        DataReceived?.Invoke(this, new DataReceivedEventArgs(data, data.Length));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    _logger.LogError(ex, "接收 WebSocket 消息时出错: {Message}", ex.Message);

                    if (!IsConnected)
                    {
                        if (_options.EnableAutoReconnect)
                        {
                            _ = Task.Run(() => ReconnectAsync(cancellationToken), cancellationToken);
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接收数据时出错: {Message}", ex.Message);
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        StopReceiving();
        StopHeartbeat();

        _webSocket?.Dispose();
        _sendLock?.Dispose();
        _receiveCts?.Dispose();
        _heartbeatCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 文本消息接收事件参数
/// </summary>
public class TextMessageReceivedEventArgs : DataReceivedEventArgs
{
    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; init; }

    public TextMessageReceivedEventArgs(string text, byte[] data, int length)
        : base(data, length)
    {
        Text = text;
    }
}
