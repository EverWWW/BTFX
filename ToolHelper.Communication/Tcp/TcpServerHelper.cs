using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Tcp;

/// <summary>
/// TCP 服务器帮助类
/// 提供多客户端管理、广播消息等功能
/// </summary>
public class TcpServerHelper : IServerConnection
{
    private readonly TcpServerOptions _options;
    private readonly ILogger<TcpServerHelper> _logger;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _acceptCts;
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
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
    /// 构造函数
    /// </summary>
    /// <param name="options">TCP 服务器配置</param>
    /// <param name="logger">日志记录器</param>
    public TcpServerHelper(IOptions<TcpServerOptions> options, ILogger<TcpServerHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（用于手动配置）
    /// </summary>
    /// <param name="port">监听端口</param>
    /// <param name="logger">日志记录器</param>
    public TcpServerHelper(int port, ILogger<TcpServerHelper> logger)
    {
        _options = new TcpServerOptions { Port = port };
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("服务器已在运行中");
            return;
        }

        try
        {
            var ipAddress = IPAddress.Parse(_options.Host);
            _tcpListener = new TcpListener(ipAddress, _options.Port);
            _tcpListener.Start(_options.MaxConnections);

            IsRunning = true;
            _logger.LogInformation("TCP 服务器已启动，监听 {Host}:{Port}", _options.Host, _options.Port);

            _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => AcceptClientsAsync(_acceptCts.Token), _acceptCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动 TCP 服务器失败");
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

            // 停止接受新连接
            _acceptCts?.Cancel();
            _tcpListener?.Stop();

            // 断开所有客户端
            var disconnectTasks = _clients.Keys.Select(clientId => DisconnectClientAsync(clientId, cancellationToken));
            await Task.WhenAll(disconnectTasks);

            _clients.Clear();

            _logger.LogInformation("TCP 服务器已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止 TCP 服务器时发生错误");
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
        if (!_clients.TryGetValue(clientId, out var session))
        {
            throw new InvalidOperationException($"客户端 {clientId} 未连接");
        }

        try
        {
            await session.SendLock.WaitAsync(cancellationToken);
            try
            {
                await session.NetworkStream.WriteAsync(data, cancellationToken);
                await session.NetworkStream.FlushAsync(cancellationToken);

                _logger.LogDebug("向客户端 {ClientId} 发送了 {Length} 字节数据", clientId, data.Length);
                return data.Length;
            }
            finally
            {
                session.SendLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "向客户端 {ClientId} 发送数据失败", clientId);
            
            // 发送失败，断开客户端
            await DisconnectClientAsync(clientId, cancellationToken);
            throw;
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
        int successCount = 0;
        var tasks = new List<Task>();

        foreach (var clientId in _clients.Keys)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await SendToClientAsync(clientId, data, cancellationToken);
                    Interlocked.Increment(ref successCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "广播到客户端 {ClientId} 失败", clientId);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);

        _logger.LogDebug("广播了 {Length} 字节数据到 {SuccessCount}/{TotalCount} 个客户端", 
            data.Length, successCount, _clients.Count);

        return successCount;
    }

    /// <inheritdoc/>
    public async Task DisconnectClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryRemove(clientId, out var session))
        {
            return;
        }

        try
        {
            session.Cts.Cancel();
            
            if (session.NetworkStream != null)
            {
                await session.NetworkStream.FlushAsync(cancellationToken);
                session.NetworkStream.Close();
            }

            session.TcpClient.Close();
            session.Dispose();

            _logger.LogInformation("客户端 {ClientId} 已断开连接", clientId);
            OnClientDisconnected(new ClientDisconnectedEventArgs(clientId, "服务器主动断开"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开客户端 {ClientId} 时发生错误", clientId);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetConnectedClients()
    {
        return _clients.Keys.ToList();
    }

    /// <summary>
    /// 接受客户端连接循环
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsRunning && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);

                // 检查是否超过最大连接数
                if (_clients.Count >= _options.MaxConnections)
                {
                    _logger.LogWarning("达到最大连接数 {MaxConnections}，拒绝新连接", _options.MaxConnections);
                    tcpClient.Close();
                    continue;
                }

                // 配置 TCP 客户端
                tcpClient.NoDelay = _options.NoDelay;
                tcpClient.ReceiveBufferSize = _options.ReceiveBufferSize;
                tcpClient.SendBufferSize = _options.SendBufferSize;
                tcpClient.ReceiveTimeout = _options.ReceiveTimeout;
                tcpClient.SendTimeout = _options.SendTimeout;

                if (_options.KeepAlive)
                {
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }

                var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                var clientId = Guid.NewGuid().ToString("N");

                var session = new ClientSession
                {
                    ClientId = clientId,
                    TcpClient = tcpClient,
                    NetworkStream = tcpClient.GetStream(),
                    RemoteAddress = remoteEndPoint,
                    ConnectedTime = DateTime.Now,
                    Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                };

                if (_clients.TryAdd(clientId, session))
                {
                    _logger.LogInformation("客户端 {ClientId} ({RemoteAddress}) 已连接", clientId, remoteEndPoint);
                    OnClientConnected(new ClientConnectedEventArgs(clientId, remoteEndPoint));

                    // 启动接收任务
                    _ = Task.Run(() => ReceiveFromClientAsync(session), session.Cts.Token);
                }
                else
                {
                    _logger.LogError("无法添加客户端 {ClientId} 到会话列表", clientId);
                    tcpClient.Close();
                }
            }
            catch (OperationCanceledException)
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
    /// 从客户端接收数据
    /// </summary>
    private async Task ReceiveFromClientAsync(ClientSession session)
    {
        var buffer = _bufferPool.Rent(_options.ReceiveBufferSize);
        var packetBuffer = new List<byte>();

        try
        {
            while (!session.Cts.Token.IsCancellationRequested && 
                   session.TcpClient.Connected && 
                   session.NetworkStream != null)
            {
                try
                {
                    var bytesRead = await session.NetworkStream.ReadAsync(
                        buffer.AsMemory(0, _options.ReceiveBufferSize), 
                        session.Cts.Token);

                    if (bytesRead == 0)
                    {
                        _logger.LogInformation("客户端 {ClientId} 关闭了连接", session.ClientId);
                        break;
                    }

                    _logger.LogDebug("从客户端 {ClientId} 接收到 {BytesRead} 字节数据", session.ClientId, bytesRead);

                    // 处理粘包
                    if (_options.EnablePacketProcessing)
                    {
                        ProcessPacket(session.ClientId, buffer, bytesRead, packetBuffer);
                    }
                    else
                    {
                        // 直接触发数据接收事件
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, 0, data, 0, bytesRead);
                        OnDataReceived(new ServerDataReceivedEventArgs(session.ClientId, data, bytesRead));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "从客户端 {ClientId} 接收数据时发生错误", session.ClientId);
                    break;
                }
            }
        }
        finally
        {
            _bufferPool.Return(buffer);
            
            // 移除客户端
            if (_clients.TryRemove(session.ClientId, out _))
            {
                OnClientDisconnected(new ClientDisconnectedEventArgs(session.ClientId, "连接已关闭"));
            }

            session.Dispose();
        }
    }

    /// <summary>
    /// 处理粘包
    /// </summary>
    private void ProcessPacket(string clientId, byte[] buffer, int bytesRead, List<byte> packetBuffer)
    {
        // 将新数据添加到缓冲区
        for (int i = 0; i < bytesRead; i++)
        {
            packetBuffer.Add(buffer[i]);

            // 检查是否超过最大包长度
            if (packetBuffer.Count > _options.MaxPacketLength)
            {
                _logger.LogWarning("客户端 {ClientId} 包长度超过最大值 {MaxLength}，清空缓冲区", 
                    clientId, _options.MaxPacketLength);
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
                        OnDataReceived(new ServerDataReceivedEventArgs(clientId, packet, packet.Length));
                    }
                }
            }
        }

        // 如果没有定义包尾，当缓冲区有数据时就触发事件
        if ((_options.PacketTail == null || _options.PacketTail.Length == 0) && packetBuffer.Count > 0)
        {
            var data = packetBuffer.ToArray();
            packetBuffer.Clear();
            OnDataReceived(new ServerDataReceivedEventArgs(clientId, data, data.Length));
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

        // 如果定义了包头，去除包头和包尾
        if (header != null && header.Length > 0 && packet.Length >= header.Length + tail.Length)
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
        _tcpListener?.Stop();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 客户端会话
    /// </summary>
    private class ClientSession : IDisposable
    {
        public string ClientId { get; init; } = string.Empty;
        public TcpClient TcpClient { get; init; } = null!;
        public NetworkStream NetworkStream { get; init; } = null!;
        public string RemoteAddress { get; init; } = string.Empty;
        public DateTime ConnectedTime { get; init; }
        public CancellationTokenSource Cts { get; init; } = null!;
        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public void Dispose()
        {
            Cts?.Dispose();
            SendLock?.Dispose();
            NetworkStream?.Dispose();
            TcpClient?.Dispose();
        }
    }
}
