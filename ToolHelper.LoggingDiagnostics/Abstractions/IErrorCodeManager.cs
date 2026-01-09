using System.Globalization;

namespace ToolHelper.LoggingDiagnostics.Abstractions;

/// <summary>
/// 错误码信息
/// </summary>
public record ErrorCodeInfo
{
    /// <summary>错误码</summary>
    public string Code { get; init; } = string.Empty;
    
    /// <summary>错误类别</summary>
    public string Category { get; init; } = string.Empty;
    
    /// <summary>严重级别</summary>
    public ErrorSeverity Severity { get; init; }
    
    /// <summary>默认消息（用于未找到本地化消息时）</summary>
    public string DefaultMessage { get; init; } = string.Empty;
    
    /// <summary>本地化消息字典 (语言代码 -> 消息)</summary>
    public IDictionary<string, string> LocalizedMessages { get; init; } = new Dictionary<string, string>();
    
    /// <summary>建议的解决方案</summary>
    public string? SuggestedSolution { get; init; }
    
    /// <summary>相关文档链接</summary>
    public string? DocumentationUrl { get; init; }
    
    /// <summary>是否可重试</summary>
    public bool IsRetryable { get; init; }
}

/// <summary>
/// 错误严重级别
/// </summary>
public enum ErrorSeverity
{
    /// <summary>信息级别</summary>
    Info = 0,
    /// <summary>警告级别</summary>
    Warning = 1,
    /// <summary>错误级别</summary>
    Error = 2,
    /// <summary>严重错误</summary>
    Critical = 3,
    /// <summary>致命错误</summary>
    Fatal = 4
}

/// <summary>
/// 错误结果
/// </summary>
public record ErrorResult
{
    /// <summary>错误码</summary>
    public string Code { get; init; } = string.Empty;
    
    /// <summary>本地化消息</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>严重级别</summary>
    public ErrorSeverity Severity { get; init; }
    
    /// <summary>发生时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>建议解决方案</summary>
    public string? SuggestedSolution { get; init; }
    
    /// <summary>格式化参数</summary>
    public object[]? FormatArgs { get; init; }
    
    /// <summary>附加上下文</summary>
    public IDictionary<string, object>? Context { get; init; }
}

/// <summary>
/// 错误码管理接口
/// 提供多语言错误描述、错误码注册和查询功能
/// </summary>
public interface IErrorCodeManager
{
    /// <summary>
    /// 当前语言
    /// </summary>
    CultureInfo CurrentCulture { get; set; }

    /// <summary>
    /// 注册错误码
    /// </summary>
    /// <param name="errorCode">错误码信息</param>
    void Register(ErrorCodeInfo errorCode);

    /// <summary>
    /// 批量注册错误码
    /// </summary>
    /// <param name="errorCodes">错误码集合</param>
    void RegisterRange(IEnumerable<ErrorCodeInfo> errorCodes);

    /// <summary>
    /// 从配置文件加载错误码
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取错误码信息
    /// </summary>
    /// <param name="code">错误码</param>
    /// <returns>错误码信息，如果不存在返回null</returns>
    ErrorCodeInfo? GetErrorCode(string code);

    /// <summary>
    /// 获取本地化错误消息
    /// </summary>
    /// <param name="code">错误码</param>
    /// <param name="culture">语言文化，null表示使用当前语言</param>
    /// <param name="args">格式化参数</param>
    /// <returns>本地化消息</returns>
    string GetMessage(string code, CultureInfo? culture = null, params object[] args);

    /// <summary>
    /// 创建错误结果
    /// </summary>
    /// <param name="code">错误码</param>
    /// <param name="args">格式化参数</param>
    /// <param name="context">附加上下文</param>
    /// <returns>错误结果</returns>
    ErrorResult CreateError(string code, object[]? args = null, IDictionary<string, object>? context = null);

    /// <summary>
    /// 检查错误码是否存在
    /// </summary>
    /// <param name="code">错误码</param>
    /// <returns>是否存在</returns>
    bool Exists(string code);

    /// <summary>
    /// 获取指定类别的所有错误码
    /// </summary>
    /// <param name="category">错误类别</param>
    /// <returns>错误码集合</returns>
    IReadOnlyList<ErrorCodeInfo> GetByCategory(string category);

    /// <summary>
    /// 获取所有已注册的错误码
    /// </summary>
    /// <returns>所有错误码</returns>
    IReadOnlyList<ErrorCodeInfo> GetAll();

    /// <summary>
    /// 移除错误码
    /// </summary>
    /// <param name="code">错误码</param>
    /// <returns>是否成功移除</returns>
    bool Remove(string code);

    /// <summary>
    /// 清除所有错误码
    /// </summary>
    void Clear();

    /// <summary>
    /// 导出错误码到文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExportToFileAsync(string filePath, CancellationToken cancellationToken = default);
}
