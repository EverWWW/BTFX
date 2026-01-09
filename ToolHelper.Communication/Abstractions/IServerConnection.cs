namespace ToolHelper.Communication.Abstractions;

/// <summary>
/// 服务端连接接口
/// </summary>
public interface IServerConnection : IDisposable
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 客户端连接事件
    /// </summary>
    event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <summary>
    /// 客户端断开事件
    /// </summary>
    event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    /// <summary>
    /// 接收到客户端数据事件
    /// </summary>
    event EventHandler<ServerDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止服务器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 向指定客户端发送数据
    /// </summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    Task<int> SendToClientAsync(string clientId, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向指定客户端发送数据
    /// </summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    Task<int> SendToClientAsync(string clientId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 广播数据到所有客户端
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功发送的客户端数量</returns>
    Task<int> BroadcastAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 广播数据到所有客户端
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功发送的客户端数量</returns>
    Task<int> BroadcastAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开指定客户端
    /// </summary>
    /// <param name="clientId">客户端标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DisconnectClientAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有已连接的客户端标识
    /// </summary>
    /// <returns>客户端标识列表</returns>
    IReadOnlyList<string> GetConnectedClients();
}

/// <summary>
/// 客户端连接事件参数
/// </summary>
public class ClientConnectedEventArgs : EventArgs
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// 客户端地址
    /// </summary>
    public string RemoteAddress { get; init; }

    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectedTime { get; init; }

    public ClientConnectedEventArgs(string clientId, string remoteAddress)
    {
        ClientId = clientId;
        RemoteAddress = remoteAddress;
        ConnectedTime = DateTime.Now;
    }
}

/// <summary>
/// 客户端断开事件参数
/// </summary>
public class ClientDisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// 断开原因
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 断开时间
    /// </summary>
    public DateTime DisconnectedTime { get; init; }

    public ClientDisconnectedEventArgs(string clientId, string? reason = null)
    {
        ClientId = clientId;
        Reason = reason;
        DisconnectedTime = DateTime.Now;
    }
}

/// <summary>
/// 服务端数据接收事件参数
/// </summary>
public class ServerDataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 客户端标识
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// 接收到的数据
    /// </summary>
    public byte[] Data { get; init; }

    /// <summary>
    /// 数据长度
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedTime { get; init; }

    public ServerDataReceivedEventArgs(string clientId, byte[] data, int length)
    {
        ClientId = clientId;
        Data = data;
        Length = length;
        ReceivedTime = DateTime.Now;
    }
}
