namespace ToolHelper.DataProcessing.Compression;

/// <summary>
/// ZIP 文件条目信息
/// </summary>
public class ZipEntryInfo
{
    /// <summary>
    /// 条目完整名称（包含路径）
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 条目名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// 原始大小（字节）
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// 压缩后大小（字节）
    /// </summary>
    public long CompressedLength { get; set; }

    /// <summary>
    /// 压缩比（百分比）
    /// </summary>
    public double CompressionRatio
    {
        get
        {
            if (Length == 0) return 0;
            return (1.0 - (double)CompressedLength / Length) * 100;
        }
    }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset LastWriteTime { get; set; }

    /// <summary>
    /// CRC32 校验值
    /// </summary>
    public uint Crc32 { get; set; }
}

/// <summary>
/// ZIP 文件信息
/// </summary>
public class ZipInfo
{
    /// <summary>
    /// ZIP 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// ZIP 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 条目数量
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// 总压缩前大小
    /// </summary>
    public long TotalUncompressedSize { get; set; }

    /// <summary>
    /// 总压缩后大小
    /// </summary>
    public long TotalCompressedSize { get; set; }

    /// <summary>
    /// 压缩比（百分比）
    /// </summary>
    public double CompressionRatio
    {
        get
        {
            if (TotalUncompressedSize == 0) return 0;
            return (1.0 - (double)TotalCompressedSize / TotalUncompressedSize) * 100;
        }
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
