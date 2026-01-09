using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;

namespace ToolHelper.LoggingDiagnostics.ErrorManagement;

/// <summary>
/// 错误码管理类
/// 提供多语言错误描述、错误码注册和查询功能
/// </summary>
/// <example>
/// <code>
/// // 注册错误码
/// errorCodeManager.Register(new ErrorCodeInfo
/// {
///     Code = "E1001",
///     Category = "Database",
///     Severity = ErrorSeverity.Error,
///     DefaultMessage = "数据库连接失败",
///     LocalizedMessages = new Dictionary&lt;string, string&gt;
///     {
///         ["zh-CN"] = "数据库连接失败: {0}",
///         ["en-US"] = "Database connection failed: {0}"
///     }
/// });
/// 
/// // 获取本地化消息
/// var message = errorCodeManager.GetMessage("E1001", args: "超时");
/// </code>
/// </example>
public class ErrorCodeManager : IErrorCodeManager
{
    private readonly ErrorCodeOptions _options;
    private readonly ConcurrentDictionary<string, ErrorCodeInfo> _errorCodes;
    private CultureInfo _currentCulture;

    /// <inheritdoc/>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set => _currentCulture = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 创建ErrorCodeManager实例
    /// </summary>
    /// <param name="options">错误码配置选项</param>
    public ErrorCodeManager(IOptions<ErrorCodeOptions> options)
    {
        _options = options.Value;
        _errorCodes = new ConcurrentDictionary<string, ErrorCodeInfo>(StringComparer.OrdinalIgnoreCase);
        _currentCulture = CultureInfo.GetCultureInfo(_options.DefaultCulture);

        // 注册通用错误码
        RegisterCommonErrorCodes();

        // 启动时加载配置文件
        if (_options.LoadOnStartup && !string.IsNullOrEmpty(_options.ConfigFilePath))
        {
            _ = LoadFromFileAsync(_options.ConfigFilePath);
        }
    }

    /// <inheritdoc/>
    public void Register(ErrorCodeInfo errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        
        if (string.IsNullOrWhiteSpace(errorCode.Code))
        {
            throw new ArgumentException("错误码不能为空", nameof(errorCode));
        }

        var code = NormalizeCode(errorCode.Code);

        if (!_options.AllowOverwrite && _errorCodes.ContainsKey(code))
        {
            throw new InvalidOperationException($"错误码 {code} 已存在，不允许重复注册");
        }

        _errorCodes[code] = errorCode with { Code = code };
    }

    /// <inheritdoc/>
    public void RegisterRange(IEnumerable<ErrorCodeInfo> errorCodes)
    {
        foreach (var errorCode in errorCodes)
        {
            Register(errorCode);
        }
    }

    /// <inheritdoc/>
    public async Task LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"错误码配置文件不存在: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var errorCodes = JsonSerializer.Deserialize<ErrorCodeFileModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (errorCodes?.ErrorCodes != null)
        {
            foreach (var item in errorCodes.ErrorCodes)
            {
                Register(new ErrorCodeInfo
                {
                    Code = item.Code,
                    Category = item.Category ?? "General",
                    Severity = Enum.TryParse<ErrorSeverity>(item.Severity, true, out var severity) 
                        ? severity 
                        : ErrorSeverity.Error,
                    DefaultMessage = item.DefaultMessage ?? item.Code,
                    LocalizedMessages = item.Messages ?? new Dictionary<string, string>(),
                    SuggestedSolution = item.Solution,
                    DocumentationUrl = item.DocUrl,
                    IsRetryable = item.IsRetryable
                });
            }
        }
    }

    /// <inheritdoc/>
    public ErrorCodeInfo? GetErrorCode(string code)
    {
        var normalizedCode = NormalizeCode(code);
        return _errorCodes.TryGetValue(normalizedCode, out var errorCode) ? errorCode : null;
    }

    /// <inheritdoc/>
    public string GetMessage(string code, CultureInfo? culture = null, params object[] args)
    {
        var normalizedCode = NormalizeCode(code);
        var targetCulture = culture ?? _currentCulture;

        if (!_errorCodes.TryGetValue(normalizedCode, out var errorCode))
        {
            return string.Format(_options.UnknownErrorMessage, code);
        }

        // 查找本地化消息
        string message;
        
        // 首先尝试完整的文化名称
        if (errorCode.LocalizedMessages.TryGetValue(targetCulture.Name, out var localizedMessage))
        {
            message = localizedMessage;
        }
        // 然后尝试父文化
        else if (targetCulture.Parent != null && 
                 !string.IsNullOrEmpty(targetCulture.Parent.Name) &&
                 errorCode.LocalizedMessages.TryGetValue(targetCulture.Parent.Name, out localizedMessage))
        {
            message = localizedMessage;
        }
        // 最后使用默认消息
        else
        {
            message = errorCode.DefaultMessage;
        }

        // 格式化参数
        if (args.Length > 0)
        {
            try
            {
                message = string.Format(targetCulture, message, args);
            }
            catch (FormatException)
            {
                // 格式化失败，返回原始消息
            }
        }

        return message;
    }

    /// <inheritdoc/>
    public ErrorResult CreateError(string code, object[]? args = null, IDictionary<string, object>? context = null)
    {
        var normalizedCode = NormalizeCode(code);
        var errorCode = GetErrorCode(normalizedCode);
        
        return new ErrorResult
        {
            Code = normalizedCode,
            Message = GetMessage(normalizedCode, args: args ?? []),
            Severity = errorCode?.Severity ?? ErrorSeverity.Error,
            SuggestedSolution = errorCode?.SuggestedSolution,
            FormatArgs = args,
            Context = context
        };
    }

    /// <inheritdoc/>
    public bool Exists(string code)
    {
        return _errorCodes.ContainsKey(NormalizeCode(code));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ErrorCodeInfo> GetByCategory(string category)
    {
        return _errorCodes.Values
            .Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Code)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<ErrorCodeInfo> GetAll()
    {
        return _errorCodes.Values.OrderBy(e => e.Code).ToList();
    }

    /// <inheritdoc/>
    public bool Remove(string code)
    {
        return _errorCodes.TryRemove(NormalizeCode(code), out _);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _errorCodes.Clear();
        RegisterCommonErrorCodes();
    }

    /// <inheritdoc/>
    public async Task ExportToFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var model = new ErrorCodeFileModel
        {
            ErrorCodes = _errorCodes.Values
                .OrderBy(e => e.Category)
                .ThenBy(e => e.Code)
                .Select(e => new ErrorCodeFileItem
                {
                    Code = e.Code,
                    Category = e.Category,
                    Severity = e.Severity.ToString(),
                    DefaultMessage = e.DefaultMessage,
                    Messages = e.LocalizedMessages.ToDictionary(kv => kv.Key, kv => kv.Value),
                    Solution = e.SuggestedSolution,
                    DocUrl = e.DocumentationUrl,
                    IsRetryable = e.IsRetryable
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(_options.ErrorCodePrefix))
        {
            return code.ToUpperInvariant();
        }

        var prefix = _options.ErrorCodePrefix.ToUpperInvariant();
        var upperCode = code.ToUpperInvariant();
        
        return upperCode.StartsWith(prefix) ? upperCode : $"{prefix}{upperCode}";
    }

    private void RegisterCommonErrorCodes()
    {
        // 系统通用错误码
        var commonCodes = new[]
        {
            new ErrorCodeInfo
            {
                Code = "E0000",
                Category = "System",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "未知错误",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "未知错误",
                    ["en-US"] = "Unknown error"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E0001",
                Category = "System",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "操作超时",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "操作超时: {0}",
                    ["en-US"] = "Operation timeout: {0}"
                },
                IsRetryable = true
            },
            new ErrorCodeInfo
            {
                Code = "E0002",
                Category = "System",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "参数无效",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "参数无效: {0}",
                    ["en-US"] = "Invalid parameter: {0}"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E0003",
                Category = "System",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "资源未找到",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "资源未找到: {0}",
                    ["en-US"] = "Resource not found: {0}"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E0004",
                Category = "System",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "访问被拒绝",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "访问被拒绝: {0}",
                    ["en-US"] = "Access denied: {0}"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E0005",
                Category = "System",
                Severity = ErrorSeverity.Critical,
                DefaultMessage = "系统内部错误",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "系统内部错误: {0}",
                    ["en-US"] = "Internal system error: {0}"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E1001",
                Category = "Database",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "数据库连接失败",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "数据库连接失败: {0}",
                    ["en-US"] = "Database connection failed: {0}"
                },
                IsRetryable = true,
                SuggestedSolution = "请检查数据库服务是否运行，以及连接字符串是否正确"
            },
            new ErrorCodeInfo
            {
                Code = "E1002",
                Category = "Database",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "数据库查询失败",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "数据库查询失败: {0}",
                    ["en-US"] = "Database query failed: {0}"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E2001",
                Category = "Network",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "网络连接失败",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "网络连接失败: {0}",
                    ["en-US"] = "Network connection failed: {0}"
                },
                IsRetryable = true
            },
            new ErrorCodeInfo
            {
                Code = "E3001",
                Category = "IO",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "文件读取失败",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "文件读取失败: {0}",
                    ["en-US"] = "File read failed: {0}"
                }
            },
            new ErrorCodeInfo
            {
                Code = "E3002",
                Category = "IO",
                Severity = ErrorSeverity.Error,
                DefaultMessage = "文件写入失败",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "文件写入失败: {0}",
                    ["en-US"] = "File write failed: {0}"
                }
            }
        };

        foreach (var code in commonCodes)
        {
            _errorCodes.TryAdd(code.Code, code);
        }
    }
}

// 错误码配置文件模型
internal class ErrorCodeFileModel
{
    public List<ErrorCodeFileItem> ErrorCodes { get; set; } = [];
}

internal class ErrorCodeFileItem
{
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? DefaultMessage { get; set; }
    public Dictionary<string, string>? Messages { get; set; }
    public string? Solution { get; set; }
    public string? DocUrl { get; set; }
    public bool IsRetryable { get; set; }
}
