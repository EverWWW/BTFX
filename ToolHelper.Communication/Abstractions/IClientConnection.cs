namespace ToolHelper.Communication.Abstractions;

/// <summary>
/// 客户端连接接口
/// </summary>
public interface IClientConnection : IConnection
{
    /// <summary>
    /// 数据接收事件
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// 启动异步接收数据
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartReceivingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止接收数据
    /// </summary>
    void StopReceiving();
}

/// <summary>
/// 数据接收事件参数
/// </summary>
public class DataReceivedEventArgs : EventArgs
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
    /// 接收时间
    /// </summary>
    public DateTime ReceivedTime { get; init; }

    public DataReceivedEventArgs(byte[] data, int length)
    {
        Data = data;
        Length = length;
        ReceivedTime = DateTime.Now;
    }
}
