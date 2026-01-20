using System.IO.Compression;

namespace ToolHelper.DataProcessing.Compression;

/// <summary>
/// ZIP 压缩选项
/// </summary>
public class ZipOptions
{
    /// <summary>
    /// 压缩级别
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// 是否包含基目录
    /// </summary>
    public bool IncludeBaseDirectory { get; set; } = false;

    /// <summary>
    /// 默认编码
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// 是否覆盖已存在的文件
    /// </summary>
    public bool OverwriteExisting { get; set; } = true;

    /// <summary>
    /// 缓冲区大小（字节）
    /// </summary>
    public int BufferSize { get; set; } = 81920;
}
