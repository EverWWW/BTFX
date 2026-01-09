namespace ToolHelper.Communication.Abstractions;

/// <summary>
/// 连接基础接口
/// </summary>
public interface IConnection : IDisposable
{
    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接状态改变事件
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// 异步连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接是否成功</returns>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步断开连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    Task<int> SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步发送数据
    /// </summary>
    /// <param name="data">要发送的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送的字节数</returns>
    Task<int> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}

/// <summary>
/// 连接状态
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// 已断开
    /// </summary>
    Disconnected,

    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 断开中
    /// </summary>
    Disconnecting,

    /// <summary>
    /// 重连中
    /// </summary>
    Reconnecting
}

/// <summary>
/// 连接状态改变事件参数
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 旧状态
    /// </summary>
    public ConnectionState OldState { get; init; }

    /// <summary>
    /// 新状态
    /// </summary>
    public ConnectionState NewState { get; init; }

    /// <summary>
    /// 异常信息（如果有）
    /// </summary>
    public Exception? Exception { get; init; }

    public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState, Exception? exception = null)
    {
        OldState = oldState;
        NewState = newState;
        Exception = exception;
    }
}
