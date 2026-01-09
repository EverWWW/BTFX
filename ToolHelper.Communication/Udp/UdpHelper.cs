using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Udp;

/// <summary>
/// UDP 通信帮助类
/// 支持单播、组播、广播通信
/// </summary>
public class UdpHelper : IDisposable
{
    private readonly UdpOptions _options;
    private readonly ILogger<UdpHelper> _logger;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _receiveCts;
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;
    private bool _isDisposed = false;
    private IPEndPoint? _multicastEndPoint;

    /// <summary>
    /// 是否正在监听
    /// </summary>
    public bool IsListening { get; private set; }

    /// <summary>
    /// 数据接收事件
    /// </summary>
    public event EventHandler<UdpDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">UDP 配置</param>
    /// <param name="logger">日志记录器</param>
    public UdpHelper(IOptions<UdpOptions> options, ILogger<UdpHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 构造函数（用于手动配置）
    /// </summary>
    /// <param name="localPort">本地监听端口</param>
    /// <param name="logger">日志记录器</param>
    public UdpHelper(int localPort, ILogger<UdpHelper> logger)
    {
        _options = new UdpOptions { LocalPort = localPort };
        _logger = logger;
    }

    /// <summary>
    /// 初始化 UDP 客户端
    /// </summary>
    public void Initialize()
    {
        if (_udpClient != null)
        {
            _logger.LogWarning("UDP 客户端已初始化");
            return;
        }

        try
        {
            // 创建 UDP 客户端
            if (_options.LocalPort > 0)
            {
                _udpClient = new UdpClient(_options.LocalPort);
            }
            else
            {
                _udpClient = new UdpClient();
            }

            // 配置选项
            _udpClient.Client.ReceiveBufferSize = _options.ReceiveBufferSize;
            _udpClient.Client.SendBufferSize = _options.SendBufferSize;
            _udpClient.Client.ReceiveTimeout = _options.ReceiveTimeout;
            _udpClient.Client.SendTimeout = _options.SendTimeout;

            // 启用广播
            if (_options.EnableBroadcast)
            {
                _udpClient.EnableBroadcast = true;
            }

            // 允许地址重用
            if (_options.ReuseAddress)
            {
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            // 加入组播组
            if (!string.IsNullOrEmpty(_options.MulticastAddress))
            {
                var multicastAddress = IPAddress.Parse(_options.MulticastAddress);
                _udpClient.JoinMulticastGroup(multicastAddress, _options.MulticastTtl);
                _multicastEndPoint = new IPEndPoint(multicastAddress, _options.RemotePort);
                _logger.LogInformation("已加入组播组 {MulticastAddress}", _options.MulticastAddress);
            }

            _logger.LogInformation("UDP 客户端已初始化，本地端口: {LocalPort}", 
                (_udpClient.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化 UDP 客户端失败");
            throw;
        }
    }

    /// <summary>
    /// 开始监听（接收数据）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
        {
            Initialize();
        }

        if (IsListening)
        {
            _logger.LogWarning("UDP 客户端已在监听中");
            return;
        }

        IsListening = true;
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _logger.LogInformation("开始监听 UDP 数据");

        try
        {
            await ReceiveLoopAsync(_receiveCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP 接收循环发生错误");
        }
    }

    /// <summary>
    /// 停止监听
    /// </summary>
    public void StopListening()
    {
        if (!IsListening)
        {
            return;
        }

        IsListening = false;
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;

        _logger.LogInformation("已停止监听 UDP 数据");
    }

    /// <summary>
    /// 发送数据（单播）
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="remoteHost">远程主机地址</param>
    /// <param name="remotePort">远程端口</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> SendAsync(byte[] data, string remoteHost, int remotePort, CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
        {
            Initialize();
        }

        try
        {
            var bytesSent = await _udpClient!.SendAsync(data, data.Length, remoteHost, remotePort);
            _logger.LogDebug("向 {RemoteHost}:{RemotePort} 发送了 {BytesSent} 字节数据", 
                remoteHost, remotePort, bytesSent);
            return bytesSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 UDP 数据到 {RemoteHost}:{RemotePort} 失败", remoteHost, remotePort);
            throw;
        }
    }

    /// <summary>
    /// 发送数据（单播，使用配置的远程地址）
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.RemoteHost))
        {
            throw new InvalidOperationException("未配置远程主机地址");
        }

        return await SendAsync(data, _options.RemoteHost, _options.RemotePort, cancellationToken);
    }

    /// <summary>
    /// 发送数据（单播）
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="remoteEndPoint">远程终结点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> SendAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
        {
            Initialize();
        }

        try
        {
            var bytesSent = await _udpClient!.SendAsync(data, data.Length, remoteEndPoint);
            _logger.LogDebug("向 {RemoteEndPoint} 发送了 {BytesSent} 字节数据", 
                remoteEndPoint, bytesSent);
            return bytesSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 UDP 数据到 {RemoteEndPoint} 失败", remoteEndPoint);
            throw;
        }
    }

    /// <summary>
    /// 广播数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="port">广播端口</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> BroadcastAsync(byte[] data, int port, CancellationToken cancellationToken = default)
    {
        if (_udpClient == null)
        {
            Initialize();
        }

        if (!_options.EnableBroadcast)
        {
            throw new InvalidOperationException("未启用广播功能");
        }

        try
        {
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);
            var bytesSent = await _udpClient!.SendAsync(data, data.Length, broadcastEndPoint);
            
            _logger.LogDebug("广播了 {BytesSent} 字节数据到端口 {Port}", bytesSent, port);
            return bytesSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "广播 UDP 数据失败");
            throw;
        }
    }

    /// <summary>
    /// 发送组播数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    public async Task<int> MulticastAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_multicastEndPoint == null)
        {
            throw new InvalidOperationException("未配置组播地址");
        }

        if (_udpClient == null)
        {
            Initialize();
        }

        try
        {
            var bytesSent = await _udpClient!.SendAsync(data, data.Length, _multicastEndPoint);
            _logger.LogDebug("发送了 {BytesSent} 字节组播数据到 {MulticastEndPoint}", 
                bytesSent, _multicastEndPoint);
            return bytesSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送组播数据失败");
            throw;
        }
    }

    /// <summary>
    /// 退出组播组
    /// </summary>
    public void LeaveMulticastGroup()
    {
        if (_udpClient == null || string.IsNullOrEmpty(_options.MulticastAddress))
        {
            return;
        }

        try
        {
            var multicastAddress = IPAddress.Parse(_options.MulticastAddress);
            _udpClient.DropMulticastGroup(multicastAddress);
            _multicastEndPoint = null;
            
            _logger.LogInformation("已退出组播组 {MulticastAddress}", _options.MulticastAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退出组播组失败");
        }
    }

    /// <summary>
    /// 接收循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsListening && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                
                _logger.LogDebug("从 {RemoteEndPoint} 接收到 {BytesReceived} 字节数据", 
                    result.RemoteEndPoint, result.Buffer.Length);

                // 触发数据接收事件
                OnDataReceived(new UdpDataReceivedEventArgs(
                    result.Buffer, 
                    result.Buffer.Length, 
                    result.RemoteEndPoint));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "接收 UDP 数据时发生错误");
            }
        }
    }

    /// <summary>
    /// 触发数据接收事件
    /// </summary>
    private void OnDataReceived(UdpDataReceivedEventArgs e)
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

        StopListening();

        // 退出组播组
        if (!string.IsNullOrEmpty(_options.MulticastAddress))
        {
            LeaveMulticastGroup();
        }

        _udpClient?.Close();
        _udpClient?.Dispose();
        _receiveCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// UDP 数据接收事件参数
/// </summary>
public class UdpDataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 接收到的数据
    /// </summary>
    public byte[] Data { get; init; }

    /// <summary>
    /// 数据长度
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// 远程终结点
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; init; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedTime { get; init; }

    public UdpDataReceivedEventArgs(byte[] data, int length, IPEndPoint remoteEndPoint)
    {
        Data = data;
        Length = length;
        RemoteEndPoint = remoteEndPoint;
        ReceivedTime = DateTime.Now;
    }
}
