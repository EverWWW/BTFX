namespace ToolHelper.DataProcessing.Abstractions;

/// <summary>
/// 文件读取接口
/// 定义统一的文件读取操作标准
/// </summary>
/// <typeparam name="T">读取的数据类型</typeparam>
public interface IFileReader<T>
{
    /// <summary>
    /// 异步读取文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>读取的数据集合</returns>
    Task<IEnumerable<T>> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式读取文件（适用于大文件，逐行处理，减少内存占用）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步枚举器</returns>
    IAsyncEnumerable<T> ReadStreamAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 文件写入接口
/// 定义统一的文件写入操作标准
/// </summary>
/// <typeparam name="T">写入的数据类型</typeparam>
public interface IFileWriter<T>
{
    /// <summary>
    /// 异步写入文件（覆盖模式）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要写入的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(string filePath, IEnumerable<T> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步追加写入文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要追加的数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AppendAsync(string filePath, IEnumerable<T> data, CancellationToken cancellationToken = default);
}
