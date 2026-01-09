using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Http;

namespace ToolHelperTest.Examples;

/// <summary>
/// HTTP 客户端使用示例
/// 演示如何使用 HttpHelper 进行 HTTP 通信
/// </summary>
public class HttpExample
{
    /// <summary>
    /// 示例 1: 基础 GET 请求
    /// 演示最简单的 HTTP GET 请求
    /// </summary>
    public static async Task BasicGetRequestAsync()
    {
        // 1. 配置依赖注入
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 2. 添加 HTTP 服务
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://api.github.com";
            options.Timeout = 30000; // 30 秒超时
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== HTTP GET 请求示例 ===\n");

            // 3. 发送 GET 请求
            Console.WriteLine("发送 GET 请求到 GitHub API...");
            var response = await httpHelper.GetAsync("/");

            Console.WriteLine($"? 请求成功");
            Console.WriteLine($"响应长度: {response.Length} 字节");
            Console.WriteLine($"内容预览: {response.Substring(0, Math.Min(200, response.Length))}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 请求失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 2: POST 请求发送 JSON 数据
    /// 演示如何发送 POST 请求和 JSON 数据
    /// </summary>
    public static async Task PostJsonRequestAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://jsonplaceholder.typicode.com";
        });
        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();
        try
        {
            Console.WriteLine("=== HTTP POST JSON 请求示例 ===\n");
            // 准备要发送的数据
            var postData = new
            {
                title = "测试文章",
                body = "这是一篇测试文章的内容",
                userId = 1
            };
            Console.WriteLine($"发送的数据:");
            Console.WriteLine($"  标题: {postData.title}");
            Console.WriteLine($"  内容: {postData.body}\n");

            // 发送 POST 请求
            Console.WriteLine("发送 POST 请求...");
            var response = await httpHelper.PostJsonAsync("/posts", postData);

            Console.WriteLine($"? 请求成功");
            Console.WriteLine($"响应内容:\n{response}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 请求失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 3: GET 请求并反序列化 JSON
    /// 演示如何获取和解析 JSON 响应
    /// </summary>
    public static async Task GetAndDeserializeJsonAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://jsonplaceholder.typicode.com";
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== HTTP GET 并反序列化 JSON 示例 ===\n");

            // 获取用户列表
            Console.WriteLine("获取用户列表...");
            var users = await httpHelper.GetAsync<List<User>>("/users");

            Console.WriteLine($"? 共获取 {users?.Count ?? 0} 个用户:\n");

            if (users != null)
            {
                foreach (var user in users.Take(3)) // 只显示前 3 个
                {
                    Console.WriteLine($"用户 #{user.Id}");
                    Console.WriteLine($"  姓名: {user.Name}");
                    Console.WriteLine($"  邮箱: {user.Email}");
                    Console.WriteLine($"  网站: {user.Website}");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 请求失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 4: 文件下载
    /// 演示如何下载文件
    /// </summary>
    public static async Task FileDownloadAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHttp(options =>
        {
            options.Timeout = 60000; // 60 秒超时
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== HTTP 文件下载示例 ===\n");

            var fileUrl = "https://jsonplaceholder.typicode.com/posts/1";
            var savePath = "downloaded_file.json";

            Console.WriteLine($"下载文件: {fileUrl}");
            Console.WriteLine($"保存到: {savePath}\n");

            // 下载文件
            var fileSize = await httpHelper.DownloadFileAsync(fileUrl, savePath);

            var fileInfo = new FileInfo(savePath);
            Console.WriteLine("? 下载完成！");
            Console.WriteLine($"  文件大小: {fileSize} 字节");
            Console.WriteLine($"  保存路径: {fileInfo.FullName}");

            // 显示内容预览
            var content = await File.ReadAllTextAsync(savePath);
            Console.WriteLine($"\n文件内容:\n{content}");

            // 清理
            File.Delete(savePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 下载失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 5: 文件上传
    /// 演示如何上传文件
    /// </summary>
    public static async Task FileUploadAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://httpbin.org";
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== HTTP 文件上传示例 ===\n");

            // 创建测试文件
            var testFilePath = "test_upload.txt";
            await File.WriteAllTextAsync(testFilePath, "这是一个测试文件内容\nTest file content");

            Console.WriteLine($"准备上传文件: {testFilePath}");
            Console.WriteLine($"文件大小: {new FileInfo(testFilePath).Length} 字节\n");

            // 上传文件
            Console.WriteLine("开始上传...");
            var response = await httpHelper.UploadFileAsync("/post", testFilePath, "file");

            Console.WriteLine($"? 上传完成！");
            Console.WriteLine($"响应长度: {response.Length} 字节");
            Console.WriteLine($"\n响应内容预览:\n{response.Substring(0, Math.Min(500, response.Length))}...");

            // 清理测试文件
            File.Delete(testFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 上传失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 6: RESTful API CRUD 操作
    /// 演示完整的 CRUD 操作
    /// </summary>
    public static async Task RestfulApiCrudAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://jsonplaceholder.typicode.com";
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== RESTful API CRUD 操作示例 ===\n");

            // 1. CREATE - 创建新资源
            Console.WriteLine("1. CREATE - 创建新文章");
            var newPost = new
            {
                title = "新文章标题",
                body = "新文章内容",
                userId = 1
            };

            var createResponse = await httpHelper.PostJsonAsync("/posts", newPost);
            var createdPost = JsonSerializer.Deserialize<Post>(createResponse);
            Console.WriteLine($"   ? 已创建，ID: {createdPost?.Id}\n");

            // 2. READ - 读取资源
            Console.WriteLine("2. READ - 读取文章");
            var post = await httpHelper.GetAsync<Post>("/posts/1");
            Console.WriteLine($"   文章 #{post?.Id}: {post?.Title}");
            var bodyPreview = post?.Body?.Length > 50 ? post.Body.Substring(0, 50) : post?.Body ?? "";
            Console.WriteLine($"   内容: {bodyPreview}...\n");

            // 3. UPDATE - 更新资源（注意：API 示例）
            Console.WriteLine("3. UPDATE - 更新文章（模拟）");
            Console.WriteLine($"   说明: 可使用 PostJsonAsync 配合特定端点实现更新\n");

            // 4. DELETE - 删除资源（注意：API 示例）
            Console.WriteLine("4. DELETE - 删除文章（模拟）");
            Console.WriteLine($"   说明: 可使用自定义 HTTP 方法实现删除");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 操作失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 7: 并发请求
    /// 演示如何同时发送多个 HTTP 请求
    /// </summary>
    public static async Task ConcurrentRequestsAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://jsonplaceholder.typicode.com";
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== 并发请求示例 ===\n");

            var postIds = new[] { 1, 2, 3, 4, 5 };
            Console.WriteLine($"同时请求 {postIds.Length} 篇文章...\n");

            var startTime = DateTime.Now;

            // 创建并发任务
            var tasks = postIds.Select(id => httpHelper.GetAsync<Post>($"/posts/{id}"));

            // 等待所有任务完成
            var posts = await Task.WhenAll(tasks);

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            Console.WriteLine("? 请求完成！");
            Console.WriteLine($"总耗时: {elapsed:F0} ms\n");

            Console.WriteLine("获取的文章:");
            foreach (var post in posts)
            {
                Console.WriteLine($"  #{post?.Id}: {post?.Title}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 请求失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 8: 实际 API 调用 - GitHub API
    /// 演示实际应用场景的 API 调用
    /// </summary>
    public static async Task RealWorldApiExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHttp(options =>
        {
            options.BaseAddress = "https://api.github.com";
            options.DefaultHeaders = new Dictionary<string, string>
            {
                ["User-Agent"] = "ToolHelper-Example",
                ["Accept"] = "application/vnd.github.v3+json"
            };
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpHelper = serviceProvider.GetRequiredService<HttpHelper>();

        try
        {
            Console.WriteLine("=== GitHub API 实际应用示例 ===\n");

            // 1. 获取仓库信息
            Console.WriteLine("1. 获取 Microsoft/TypeScript 仓库信息");
            var repo = await httpHelper.GetAsync<GitHubRepo>("/repos/microsoft/typescript");

            Console.WriteLine($"   仓库: {repo?.FullName}");
            Console.WriteLine($"   描述: {repo?.Description}");
            Console.WriteLine($"   Stars: {repo?.StargazersCount:N0}");
            Console.WriteLine($"   Forks: {repo?.ForksCount:N0}");
            Console.WriteLine($"   语言: {repo?.Language}\n");

            // 2. 搜索仓库
            Console.WriteLine("2. 搜索 C# HTTP 相关仓库");
            var searchResult = await httpHelper.GetAsync<GitHubSearchResult>(
                "/search/repositories?q=http+language:csharp&sort=stars&per_page=3");

            Console.WriteLine($"   共找到 {searchResult?.TotalCount:N0} 个仓库，显示前 3 个:\n");

            if (searchResult?.Items != null)
            {
                foreach (var item in searchResult.Items)
                {
                    Console.WriteLine($"   ? {item.FullName}");
                    Console.WriteLine($"     ? {item.StargazersCount:N0}  ?? {item.ForksCount:N0}");
                    Console.WriteLine($"     {item.Description}");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? API 调用失败: {ex.Message}");
        }
        finally
        {
            httpHelper.Dispose();
        }
    }
}

// ===== 数据模型 =====

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
}

public class Post
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public int UserId { get; set; }
}

public class GitHubRepo
{
    public string? FullName { get; set; }
    public string? Description { get; set; }
    public int StargazersCount { get; set; }
    public int ForksCount { get; set; }
    public string? Language { get; set; }
}

public class GitHubSearchResult
{
    public int TotalCount { get; set; }
    public List<GitHubRepo>? Items { get; set; }
}
