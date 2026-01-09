using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.WebSocket;

/// <summary>
/// WebSocket 服务端帮助类
/// 提供多客户端管理、广播消息、心跳检测等功能
/// </summary>
public class WebSocketServerHelper : IServerConnection
{
    private readonly WebSocketServerOptions _options;
    private readonly ILogger<WebSocketServerHelper> _logger;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _acceptCts;
    private CancellationTokenSource? _heartbeatCts;
    private readonly ConcurrentDictionary<string, WebSocketClientSession> _clients = new();
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private bool _isDisposed = false;

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <inheritdoc/>
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    /// <inheritdoc/>
    public event EventHandler<ServerDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 文本消息接收事件
    /// </summary>
    public event EventHandler<WebSocketTextMessageReceivedEventArgs>? TextMessageReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">WebSocket 服务端配置</param>
    /// <param name="logger">日志记录器</param>
    public WebSocketServerHelper(IOptions<WebSocketServerOptions> options, ILogger<WebSocketServerHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（支持手动配置）
    /// </summary>
    /// <param name="port">监听端口</param>
    /// <param name="logger">日志记录器</param>
    public WebSocketServerHelper(int port, ILogger<WebSocketServerHelper> logger)
    {
        _options = new WebSocketServerOptions { Port = port };
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("WebSocket 服务器已在运行中");
            return;
        }

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(_options.GetListenUrl());

            _httpListener.Start();
            IsRunning = true;

            _logger.LogInformation("WebSocket 服务器已启动，监听: {Url}", _options.GetListenUrl());
            _logger.LogInformation("客户端连接地址: {WsUrl}", _options.GetWebSocketUrl());

            _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // 启动接受连接任务
            _ = Task.Run(() => AcceptClientsAsync(_acceptCts.Token), _acceptCts.Token);

            // 启动心跳检测
            if (_options.EnableHeartbeat)
            {
                StartHeartbeat();
            }
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "启动 WebSocket 服务器失败。可能需要管理员权限或端口已被占用");
            IsRunning = false;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动 WebSocket 服务器失败");
            IsRunning = false;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            IsRunning = false;

            // 停止心跳
            StopHeartbeat();

            // 取消接受新连接
            _acceptCts?.Cancel();

            // 关闭所有客户端连接
            var closeTasks = new List<Task>();
            foreach (var client in _clients.Values)
            {
                closeTasks.Add(CloseClientAsync(client, "服务器关闭"));
            }

            if (closeTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(closeTasks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "关闭客户端连接时发生错误");
                }
            }

            _clients.Clear();

            // 停止监听器
            try
            {
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch { }

            _logger.LogInformation("WebSocket 服务器已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止 WebSocket 服务器时发生错误");
        }
    }

    /// <inheritdoc/>
    public async Task<int> SendToClientAsync(string clientId, byte[] data, CancellationToken cancellationToken = default)
    {
        return await SendToClientAsync(clientId, data.AsMemory(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SendToClientAsync(string clientId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(clientId, out var client))
        {
            _logger.LogWarning("客户端 {ClientId} 不存在", clientId);
            return 0;
        }

        if (client.WebSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("客户端 {ClientId} 连接已关闭", clientId);
            return 0;
        }

        try
        {
            await client.SendLock.WaitAsync(cancellationToken);
            try
            {
                await client.WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
                _logger.LogDebug("向客户端 {ClientId} 发送了 {Length} 字节数据", clientId, data.Length);
                return data.Length;
            }
            finally
            {
                client.SendLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向客户端 {ClientId} 发送数据失败", clientId);
            return 0;
        }
    }

    /// <summary>
    /// 向指定客户端发送文本消息
    /// </summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="text">文本消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> SendTextToClientAsync(string clientId, string text, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(clientId, out var client))
        {
            _logger.LogWarning("客户端 {ClientId} 不存在", clientId);
            return 0;
        }

        if (client.WebSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("客户端 {ClientId} 连接已关闭", clientId);
            return 0;
        }

        try
        {
            var data = Encoding.UTF8.GetBytes(text);
            await client.SendLock.WaitAsync(cancellationToken);
            try
            {
                await client.WebSocket.SendAsync(data.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
                _logger.LogDebug("向客户端 {ClientId} 发送了文本消息: {Text}", clientId, text.Length > 100 ? text[..100] + "..." : text);
                return data.Length;
            }
            finally
            {
                client.SendLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向客户端 {ClientId} 发送文本消息失败", clientId);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<int> BroadcastAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return await BroadcastAsync(data.AsMemory(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var successCount = 0;
        var tasks = new List<Task<bool>>();

        foreach (var client in _clients.Values)
        {
            if (client.WebSocket.State == WebSocketState.Open)
            {
                tasks.Add(SendToBinaryAsync(client, data, cancellationToken));
            }
        }

        var results = await Task.WhenAll(tasks);
        successCount = results.Count(r => r);

        _logger.LogDebug("广播了 {Length} 字节数据到 {Count} 个客户端", data.Length, successCount);
        return successCount;
    }

    /// <summary>
    /// 广播文本消息到所有客户端
    /// </summary>
    /// <param name="text">文本消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功发送的客户端数量</returns>
    public async Task<int> BroadcastTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var data = Encoding.UTF8.GetBytes(text);
        var successCount = 0;
        var tasks = new List<Task<bool>>();

        foreach (var client in _clients.Values)
        {
            if (client.WebSocket.State == WebSocketState.Open)
            {
                tasks.Add(SendToTextAsync(client, data, cancellationToken));
            }
        }

        var results = await Task.WhenAll(tasks);
        successCount = results.Count(r => r);

        _logger.LogDebug("广播了文本消息到 {Count} 个客户端", successCount);
        return successCount;
    }

    /// <inheritdoc/>
    public async Task DisconnectClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (_clients.TryRemove(clientId, out var client))
        {
            await CloseClientAsync(client, "服务器主动断开");
            OnClientDisconnected(new ClientDisconnectedEventArgs(clientId, "服务器主动断开"));
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetConnectedClients()
    {
        return _clients.Keys.ToList();
    }

    /// <summary>
    /// 获取连接的客户端数量
    /// </summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// 接受客户端连接循环
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsRunning && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                // 检查是否是 WebSocket 请求
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.StatusDescription = "WebSocket request expected";
                    context.Response.Close();
                    continue;
                }

                // 检查连接数限制
                if (_clients.Count >= _options.MaxConnections)
                {
                    _logger.LogWarning("已达到最大连接数 {MaxConnections}，拒绝新连接", _options.MaxConnections);
                    context.Response.StatusCode = 503;
                    context.Response.StatusDescription = "Server is full";
                    context.Response.Close();
                    continue;
                }

                // 异步处理新连接
                _ = Task.Run(() => HandleClientAsync(context, cancellationToken), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "接受客户端连接时发生错误");
            }
        }
    }

    /// <summary>
    /// 处理单个客户端连接
    /// </summary>
    private async Task HandleClientAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid().ToString("N");
        System.Net.WebSockets.WebSocket? webSocket = null;

        try
        {
            // 接受 WebSocket 连接
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            webSocket = wsContext.WebSocket;

            var remoteEndPoint = context.Request.RemoteEndPoint?.ToString() ?? "Unknown";
            var session = new WebSocketClientSession(clientId, webSocket, remoteEndPoint);

            _clients[clientId] = session;

            _logger.LogInformation("客户端 {ClientId} 已连接，地址: {RemoteAddress}，当前连接数: {Count}",
                clientId, remoteEndPoint, _clients.Count);

            OnClientConnected(new ClientConnectedEventArgs(clientId, remoteEndPoint));

            // 开始接收数据
            await ReceiveLoopAsync(session, cancellationToken);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "客户端 {ClientId} WebSocket 异常", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理客户端 {ClientId} 时发生错误", clientId);
        }
        finally
        {
            // 清理客户端
            if (_clients.TryRemove(clientId, out var removedClient))
            {
                await CloseClientAsync(removedClient, "连接关闭");
                OnClientDisconnected(new ClientDisconnectedEventArgs(clientId, "连接关闭"));
                _logger.LogInformation("客户端 {ClientId} 已断开，当前连接数: {Count}", clientId, _clients.Count);
            }
        }
    }

    /// <summary>
    /// 接收数据循环
    /// </summary>
    private async Task ReceiveLoopAsync(WebSocketClientSession session, CancellationToken cancellationToken)
    {
        var buffer = _bufferPool.Rent(_options.ReceiveBufferSize);
        var messageBuffer = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   session.WebSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;

                try
                {
                    result = await session.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogDebug("客户端 {ClientId} 连接被关闭", session.ClientId);
                    break;
                }

                // 更新最后活动时间
                session.LastActivityTime = DateTime.Now;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogDebug("客户端 {ClientId} 请求关闭连接: {Status} - {Description}",
                        session.ClientId, result.CloseStatus, result.CloseStatusDescription);
                    break;
                }

                // 累积消息片段
                messageBuffer.AddRange(buffer.Take(result.Count));

                // 如果消息完整
                if (result.EndOfMessage)
                {
                    var data = messageBuffer.ToArray();
                    messageBuffer.Clear();

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(data);
                        _logger.LogDebug("从客户端 {ClientId} 收到文本消息: {Length} 字符",
                            session.ClientId, text.Length);

                        OnTextMessageReceived(new WebSocketTextMessageReceivedEventArgs(
                            session.ClientId, text, data.Length));
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogDebug("从客户端 {ClientId} 收到二进制数据: {Length} 字节",
                            session.ClientId, data.Length);
                    }

                    // 触发数据接收事件
                    OnDataReceived(new ServerDataReceivedEventArgs(
                        session.ClientId, data, data.Length));
                }
            }
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    /// <summary>
    /// 关闭客户端连接
    /// </summary>
    private async Task CloseClientAsync(WebSocketClientSession client, string reason)
    {
        try
        {
            if (client.WebSocket.State == WebSocketState.Open ||
                client.WebSocket.State == WebSocketState.CloseReceived)
            {
                await client.WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    reason,
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "关闭客户端 {ClientId} 连接时发生错误", client.ClientId);
        }
        finally
        {
            try
            {
                client.WebSocket.Dispose();
                client.SendLock.Dispose();
            }
            catch { }
        }
    }

    /// <summary>
    /// 发送二进制数据到客户端
    /// </summary>
    private async Task<bool> SendToBinaryAsync(WebSocketClientSession client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            await client.SendLock.WaitAsync(cancellationToken);
            try
            {
                await client.WebSocket.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
                return true;
            }
            finally
            {
                client.SendLock.Release();
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 发送文本数据到客户端
    /// </summary>
    private async Task<bool> SendToTextAsync(WebSocketClientSession client, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            await client.SendLock.WaitAsync(cancellationToken);
            try
            {
                await client.WebSocket.SendAsync(data.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
                return true;
            }
            finally
            {
                client.SendLock.Release();
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 启动心跳检测
    /// </summary>
    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_heartbeatCts.Token.IsCancellationRequested && IsRunning)
            {
                try
                {
                    await Task.Delay(_options.HeartbeatInterval, _heartbeatCts.Token);

                    var now = DateTime.Now;
                    var timeoutThreshold = TimeSpan.FromMilliseconds(_options.HeartbeatTimeout);

                    foreach (var client in _clients.Values.ToList())
                    {
                        if (now - client.LastActivityTime > timeoutThreshold)
                        {
                            _logger.LogWarning("客户端 {ClientId} 心跳超时，断开连接", client.ClientId);
                            await DisconnectClientAsync(client.ClientId);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "心跳检测时发生错误");
                }
            }
        }, _heartbeatCts.Token);
    }

    /// <summary>
    /// 停止心跳检测
    /// </summary>
    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
    }

    /// <summary>
    /// 触发客户端连接事件
    /// </summary>
    private void OnClientConnected(ClientConnectedEventArgs e)
    {
        ClientConnected?.Invoke(this, e);
    }

    /// <summary>
    /// 触发客户端断开事件
    /// </summary>
    private void OnClientDisconnected(ClientDisconnectedEventArgs e)
    {
        ClientDisconnected?.Invoke(this, e);
    }

    /// <summary>
    /// 触发数据接收事件
    /// </summary>
    private void OnDataReceived(ServerDataReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }

    /// <summary>
    /// 触发文本消息接收事件
    /// </summary>
    private void OnTextMessageReceived(WebSocketTextMessageReceivedEventArgs e)
    {
        TextMessageReceived?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        StopAsync().GetAwaiter().GetResult();

        _acceptCts?.Dispose();
        _heartbeatCts?.Dispose();
        _httpListener?.Close();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// WebSocket 客户端会话
/// </summary>
internal class WebSocketClientSession
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// WebSocket 连接
    /// </summary>
    public System.Net.WebSockets.WebSocket WebSocket { get; }

    /// <summary>
    /// 远程地址
    /// </summary>
    public string RemoteAddress { get; }

    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectedTime { get; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivityTime { get; set; }

    /// <summary>
    /// 发送锁
    /// </summary>
    public SemaphoreSlim SendLock { get; } = new(1, 1);

    public WebSocketClientSession(string clientId, System.Net.WebSockets.WebSocket webSocket, string remoteAddress)
    {
        ClientId = clientId;
        WebSocket = webSocket;
        RemoteAddress = remoteAddress;
        ConnectedTime = DateTime.Now;
        LastActivityTime = DateTime.Now;
    }
}

/// <summary>
/// WebSocket 文本消息接收事件参数
/// </summary>
public class WebSocketTextMessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// 文本消息
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 消息长度（字节）
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedTime { get; }

    public WebSocketTextMessageReceivedEventArgs(string clientId, string text, int length)
    {
        ClientId = clientId;
        Text = text;
        Length = length;
        ReceivedTime = DateTime.Now;
    }
}
