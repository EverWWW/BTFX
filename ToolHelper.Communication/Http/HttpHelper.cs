using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ToolHelper.Communication.Configuration;

namespace ToolHelper.Communication.Http;

/// <summary>
/// HTTP 请求帮助类
/// 支持 GET/POST 请求、文件上传下载、自动重试等功能
/// </summary>
public class HttpHelper : IDisposable
{
    private readonly HttpOptions _options;
    private readonly ILogger<HttpHelper> _logger;
    private readonly HttpClient _httpClient;
    private bool _isDisposed = false;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">HTTP 配置</param>
    /// <param name="logger">日志记录器</param>
    public HttpHelper(IOptions<HttpOptions> options, ILogger<HttpHelper> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = CreateHttpClient();
    }

    /// <summary>
    /// 构造函数（用于手动配置）
    /// </summary>
    /// <param name="baseAddress">基础地址</param>
    /// <param name="logger">日志记录器</param>
    public HttpHelper(string? baseAddress, ILogger<HttpHelper> logger)
    {
        _options = new HttpOptions { BaseAddress = baseAddress };
        _logger = logger;
        _httpClient = CreateHttpClient();
    }

    /// <summary>
    /// 创建 HTTP 客户端
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = _options.AllowAutoRedirect,
            MaxAutomaticRedirections = _options.MaxAutomaticRedirections,
            MaxConnectionsPerServer = _options.MaxConnectionsPerServer
        };

        // 自动解压
        if (_options.AutomaticDecompression)
        {
            handler.AutomaticDecompression = DecompressionMethods.All;
        }

        // 配置代理
        if (_options.UseProxy && !string.IsNullOrEmpty(_options.ProxyAddress))
        {
            handler.Proxy = new WebProxy(_options.ProxyAddress);
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(_options.Timeout)
        };

        // 设置基础地址
        if (!string.IsNullOrEmpty(_options.BaseAddress))
        {
            client.BaseAddress = new Uri(_options.BaseAddress);
        }

        // 设置默认请求头
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        foreach (var header in _options.DefaultHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client;
    }

    #region GET 请求

    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("发送 GET 请求: {Url}", url);
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("GET 请求成功: {Url}, 状态码: {StatusCode}", url, response.StatusCode);
            return content;
        }, $"GET {url}");
    }

    /// <summary>
    /// 发送 GET 请求并反序列化 JSON
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="url">请求 URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        var content = await GetAsync(url, cancellationToken);
        return JsonSerializer.Deserialize<T>(content);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url">文件 URL</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载的字节数</returns>
    public async Task<long> DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogInformation("开始下载文件: {Url} -> {SavePath}", url, savePath);

            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await responseStream.CopyToAsync(fileStream, cancellationToken);

            var fileSize = fileStream.Length;
            _logger.LogInformation("文件下载成功: {SavePath}, 大小: {FileSize} 字节", savePath, fileSize);
            return fileSize;
        }, $"DownloadFile {url}");
    }

    #endregion

    #region POST 请求

    /// <summary>
    /// 发送 POST 请求（JSON 内容）
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="data">请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> PostJsonAsync(string url, object data, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("发送 POST 请求 (JSON): {Url}", url);

            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("POST 请求成功: {Url}, 状态码: {StatusCode}", url, response.StatusCode);
            return responseContent;
        }, $"POST {url}");
    }

    /// <summary>
    /// 发送 POST 请求（JSON 内容）并反序列化响应
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="url">请求 URL</param>
    /// <param name="data">请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的响应对象</returns>
    public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken cancellationToken = default)
    {
        var content = await PostJsonAsync(url, data!, cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(content);
    }

    /// <summary>
    /// 发送 POST 请求（表单内容）
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="formData">表单数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> PostFormAsync(string url, Dictionary<string, string> formData, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("发送 POST 请求 (Form): {Url}", url);

            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("POST 请求成功: {Url}, 状态码: {StatusCode}", url, response.StatusCode);
            return responseContent;
        }, $"POST {url}");
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="url">上传 URL</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="fileFieldName">文件字段名</param>
    /// <param name="additionalFields">额外的表单字段</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> UploadFileAsync(
        string url, 
        string filePath, 
        string fileFieldName = "file",
        Dictionary<string, string>? additionalFields = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            _logger.LogInformation("开始上传文件: {FilePath} -> {Url}", filePath, url);

            using var content = new MultipartFormDataContent();

            // 添加文件
            var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, fileFieldName, Path.GetFileName(filePath));

            // 添加额外字段
            if (additionalFields != null)
            {
                foreach (var field in additionalFields)
                {
                    content.Add(new StringContent(field.Value), field.Key);
                }
            }

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("文件上传成功: {FilePath}", filePath);
            return responseContent;
        }, $"UploadFile {url}");
    }

    /// <summary>
    /// 上传多个文件
    /// </summary>
    /// <param name="url">上传 URL</param>
    /// <param name="files">文件列表（字段名 -> 文件路径）</param>
    /// <param name="additionalFields">额外的表单字段</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> UploadMultipleFilesAsync(
        string url,
        Dictionary<string, string> files,
        Dictionary<string, string>? additionalFields = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogInformation("开始上传 {Count} 个文件 -> {Url}", files.Count, url);

            using var content = new MultipartFormDataContent();

            // 添加所有文件
            foreach (var file in files)
            {
                if (!File.Exists(file.Value))
                {
                    throw new FileNotFoundException($"文件不存在: {file.Value}");
                }

                var fileStream = File.OpenRead(file.Value);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, file.Key, Path.GetFileName(file.Value));
            }

            // 添加额外字段
            if (additionalFields != null)
            {
                foreach (var field in additionalFields)
                {
                    content.Add(new StringContent(field.Value), field.Key);
                }
            }

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("多文件上传成功");
            return responseContent;
        }, $"UploadMultipleFiles {url}");
    }

    #endregion

    #region 其他 HTTP 方法

    /// <summary>
    /// 发送 PUT 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="data">请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> PutJsonAsync(string url, object data, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("发送 PUT 请求: {Url}", url);

            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("PUT 请求成功: {Url}, 状态码: {StatusCode}", url, response.StatusCode);
            return responseContent;
        }, $"PUT {url}");
    }

    /// <summary>
    /// 发送 DELETE 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public async Task<string> DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("发送 DELETE 请求: {Url}", url);

            var response = await _httpClient.DeleteAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("DELETE 请求成功: {Url}, 状态码: {StatusCode}", url, response.StatusCode);
            return responseContent;
        }, $"DELETE {url}");
    }

    #endregion

    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.MaxRetryAttempts)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _options.MaxRetryAttempts - 1)
            {
                attempt++;
                lastException = ex;
                
                _logger.LogWarning(ex, "操作 {OperationName} 失败 (第 {Attempt}/{MaxAttempts} 次尝试), {Delay}ms 后重试", 
                    operationName, attempt, _options.MaxRetryAttempts, _options.RetryInterval);

                await Task.Delay(_options.RetryInterval);
            }
        }

        _logger.LogError(lastException, "操作 {OperationName} 在 {MaxAttempts} 次尝试后仍然失败", 
            operationName, _options.MaxRetryAttempts);

        throw lastException ?? new Exception($"操作 {operationName} 失败");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _httpClient?.Dispose();

        GC.SuppressFinalize(this);
    }
}
