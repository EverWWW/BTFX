using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.WebSocket;

namespace ToolHelperTest.Examples;

/// <summary>
/// WebSocket 服务端使用示例
/// 演示如何使用 WebSocketServerHelper 创建 WebSocket 服务器
/// </summary>
public class WebSocketServerExample
{
    /// <summary>
    /// 示例 1: 基础回显服务器
    /// 演示最简单的 WebSocket 服务器，接收客户端消息并回显
    /// </summary>
    public static async Task BasicEchoServerAsync()
    {
        // 1. 配置依赖注入
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 2. 添加 WebSocket 服务端服务
        services.AddWebSocketServer(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.Path = "/ws";
            options.MaxConnections = 100;
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsServer = serviceProvider.GetRequiredService<WebSocketServerHelper>();

        try
        {
            // 3. 订阅客户端连接事件
            wsServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端已连接: {e.ClientId}");
                Console.WriteLine($"  地址: {e.RemoteAddress}");
                Console.WriteLine($"  当前连接数: {wsServer.ClientCount}");
            };

            // 4. 订阅客户端断开事件
            wsServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端已断开: {e.ClientId}");
                Console.WriteLine($"  原因: {e.Reason}");
                Console.WriteLine($"  当前连接数: {wsServer.ClientCount}");
            };

            // 5. 订阅文本消息接收事件（回显逻辑）
            wsServer.TextMessageReceived += async (sender, e) =>
            {
                Console.WriteLine($"[{e.ClientId}] 收到: {e.Text}");

                // 回显消息给客户端
                var response = $"Echo: {e.Text}";
                await wsServer.SendTextToClientAsync(e.ClientId, response);
                Console.WriteLine($"[{e.ClientId}] 回显: {response}");
            };

            // 6. 启动服务器
            Console.WriteLine("=== WebSocket 回显服务器示例 ===\n");
            Console.WriteLine($"正在启动 WebSocket 服务器...");
            await wsServer.StartAsync();
            Console.WriteLine($"服务器已启动！");
            Console.WriteLine($"客户端连接地址: ws://localhost:8080/ws");
            Console.WriteLine("\n按任意键停止服务器...\n");

            Console.ReadKey();
        }
        finally
        {
            // 7. 停止服务器
            await wsServer.StopAsync();
            wsServer.Dispose();
            Console.WriteLine("\n服务器已停止");
        }
    }

    /// <summary>
    /// 示例 2: 聊天室服务器
    /// 演示多客户端聊天室功能，广播消息给所有客户端
    /// </summary>
    public static async Task ChatRoomServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddWebSocketServer(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.Path = "/chat";
            options.MaxConnections = 50;
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsServer = serviceProvider.GetRequiredService<WebSocketServerHelper>();

        // 存储客户端昵称
        var clientNicknames = new Dictionary<string, string>();
        var lockObj = new object();

        try
        {
            Console.WriteLine("=== WebSocket 聊天室服务器 ===\n");

            // 客户端连接
            wsServer.ClientConnected += async (sender, e) =>
            {
                string nickname;
                lock (lockObj)
                {
                    nickname = $"用户{clientNicknames.Count + 1}";
                    clientNicknames[e.ClientId] = nickname;
                }

                Console.WriteLine($"? {nickname} 加入聊天室 ({e.ClientId})");

                // 发送欢迎消息
                var welcome = $"欢迎 {nickname}! 当前在线: {wsServer.ClientCount} 人";
                await wsServer.SendTextToClientAsync(e.ClientId, welcome);

                // 通知其他用户
                var joinMsg = $"[系统] {nickname} 加入了聊天室";
                await wsServer.BroadcastTextAsync(joinMsg);
            };

            // 客户端断开
            wsServer.ClientDisconnected += async (sender, e) =>
            {
                string? nickname;
                lock (lockObj)
                {
                    clientNicknames.TryGetValue(e.ClientId, out nickname);
                    clientNicknames.Remove(e.ClientId);
                }

                if (nickname != null)
                {
                    Console.WriteLine($"? {nickname} 离开聊天室");

                    // 通知其他用户
                    var leaveMsg = $"[系统] {nickname} 离开了聊天室";
                    await wsServer.BroadcastTextAsync(leaveMsg);
                }
            };

            // 接收并广播消息
            wsServer.TextMessageReceived += async (sender, e) =>
            {
                string? nickname;
                lock (lockObj)
                {
                    clientNicknames.TryGetValue(e.ClientId, out nickname);
                }

                if (nickname != null)
                {
                    Console.WriteLine($"[{nickname}] {e.Text}");

                    // 广播给所有客户端
                    var broadcastMsg = $"[{nickname}] {e.Text}";
                    await wsServer.BroadcastTextAsync(broadcastMsg);
                }
            };

            await wsServer.StartAsync();
            Console.WriteLine("聊天室服务器已启动");
            Console.WriteLine("客户端连接地址: ws://localhost:8080/chat");
            Console.WriteLine("按任意键停止\n");

            Console.ReadKey();
        }
        finally
        {
            await wsServer.StopAsync();
            wsServer.Dispose();
            Console.WriteLine("聊天室服务器已停止");
        }
    }

    /// <summary>
    /// 示例 3: 广播服务器
    /// 演示定时向所有客户端广播数据
    /// </summary>
    public static async Task BroadcastServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddWebSocketServer(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.Path = "/broadcast";
            options.MaxConnections = 100;
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsServer = serviceProvider.GetRequiredService<WebSocketServerHelper>();

        try
        {
            Console.WriteLine("=== WebSocket 广播服务器 ===\n");

            wsServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端连接: {e.ClientId} (在线: {wsServer.ClientCount})");
            };

            wsServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端断开: {e.ClientId} (在线: {wsServer.ClientCount})");
            };

            await wsServer.StartAsync();
            Console.WriteLine("广播服务器已启动");
            Console.WriteLine("客户端连接地址: ws://localhost:8080/broadcast");
            Console.WriteLine("每 3 秒广播一次数据\n");
            Console.WriteLine("按任意键停止\n");

            // 定时广播任务
            var cts = new CancellationTokenSource();
            var broadcastTask = Task.Run(async () =>
            {
                int messageCount = 0;

                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(3000, cts.Token);

                    if (wsServer.ClientCount > 0)
                    {
                        messageCount++;
                        var timestamp = DateTime.Now.ToString("HH:mm:ss");
                        var message = $"[广播 #{messageCount}] 时间: {timestamp}, 在线: {wsServer.ClientCount}";

                        var count = await wsServer.BroadcastTextAsync(message);
                        Console.WriteLine($"已广播到 {count} 个客户端: {message}");
                    }
                }
            }, cts.Token);

            Console.ReadKey();
            cts.Cancel();

            try
            {
                await broadcastTask;
            }
            catch (OperationCanceledException) { }
        }
        finally
        {
            await wsServer.StopAsync();
            wsServer.Dispose();
            Console.WriteLine("\n广播服务器已停止");
        }
    }

    /// <summary>
    /// 示例 4: JSON 消息服务器
    /// 演示处理 JSON 格式的消息
    /// </summary>
    public static async Task JsonMessageServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddWebSocketServer(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.Path = "/api";
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsServer = serviceProvider.GetRequiredService<WebSocketServerHelper>();

        try
        {
            Console.WriteLine("=== WebSocket JSON 消息服务器 ===\n");

            wsServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端连接: {e.ClientId}");
            };

            wsServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端断开: {e.ClientId}");
            };

            // 处理 JSON 消息
            wsServer.TextMessageReceived += async (sender, e) =>
            {
                Console.WriteLine($"[{e.ClientId}] 收到: {e.Text}");

                try
                {
                    // 尝试解析 JSON
                    var request = System.Text.Json.JsonSerializer.Deserialize<JsonRequest>(e.Text);

                    if (request != null)
                    {
                        // 根据类型处理请求
                        object response = request.Type?.ToLower() switch
                        {
                            "ping" => new { type = "pong", timestamp = DateTime.Now.ToString("o") },
                            "echo" => new { type = "echo", content = request.Content ?? "", timestamp = DateTime.Now.ToString("o") },
                            "time" => new { type = "time", content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), timestamp = DateTime.Now.ToString("o") },
                            _ => new { type = "error", content = "Unknown request type", timestamp = DateTime.Now.ToString("o") }
                        };

                        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
                        await wsServer.SendTextToClientAsync(e.ClientId, responseJson);
                        Console.WriteLine($"[{e.ClientId}] 响应: {responseJson}");
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    var errorResponse = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "error",
                        content = "Invalid JSON format",
                        timestamp = DateTime.Now
                    });
                    await wsServer.SendTextToClientAsync(e.ClientId, errorResponse);
                }
            };

            await wsServer.StartAsync();
            Console.WriteLine("JSON 消息服务器已启动");
            Console.WriteLine("客户端连接地址: ws://localhost:8080/api");
            Console.WriteLine("\n支持的请求类型:");
            Console.WriteLine("  { \"type\": \"ping\" }");
            Console.WriteLine("  { \"type\": \"echo\", \"content\": \"your message\" }");
            Console.WriteLine("  { \"type\": \"time\" }");
            Console.WriteLine("\n按任意键停止\n");

            Console.ReadKey();
        }
        finally
        {
            await wsServer.StopAsync();
            wsServer.Dispose();
            Console.WriteLine("\nJSON 消息服务器已停止");
        }
    }

    /// <summary>
    /// 示例 5: 带心跳检测的服务器
    /// 演示启用心跳检测功能
    /// </summary>
    public static async Task HeartbeatServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddWebSocketServer(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.Path = "/heartbeat";
            options.EnableHeartbeat = true;
            options.HeartbeatInterval = 10000;  // 10 秒检测一次
            options.HeartbeatTimeout = 30000;   // 30 秒无活动则断开
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsServer = serviceProvider.GetRequiredService<WebSocketServerHelper>();

        try
        {
            Console.WriteLine("=== WebSocket 心跳检测服务器 ===\n");

            wsServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端连接: {e.ClientId}");
            };

            wsServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端断开: {e.ClientId} (原因: {e.Reason})");
            };

            wsServer.TextMessageReceived += async (sender, e) =>
            {
                Console.WriteLine($"[{e.ClientId}] 收到: {e.Text}");

                // 回复消息（会更新客户端的最后活动时间）
                await wsServer.SendTextToClientAsync(e.ClientId, $"收到: {e.Text}");
            };

            await wsServer.StartAsync();
            Console.WriteLine("心跳检测服务器已启动");
            Console.WriteLine("客户端连接地址: ws://localhost:8080/heartbeat");
            Console.WriteLine("\n配置:");
            Console.WriteLine("  心跳检测间隔: 10 秒");
            Console.WriteLine("  超时时间: 30 秒");
            Console.WriteLine("\n客户端需要定期发送消息以保持连接");
            Console.WriteLine("按任意键停止\n");

            Console.ReadKey();
        }
        finally
        {
            await wsServer.StopAsync();
            wsServer.Dispose();
            Console.WriteLine("\n心跳检测服务器已停止");
        }
    }

    /// <summary>
    /// 示例 6: 服务器与客户端联合测试
    /// 同时启动服务器和客户端进行测试
    /// </summary>
    public static async Task ServerClientTestAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 添加服务端
        services.AddWebSocketServer(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.Path = "/test";
        });

        // 添加客户端
        services.AddWebSocket(options =>
        {
            options.Uri = "ws://localhost:8080/test";
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsServer = serviceProvider.GetRequiredService<WebSocketServerHelper>();
        var wsClient = serviceProvider.GetRequiredService<WebSocketHelper>();

        try
        {
            Console.WriteLine("=== WebSocket 服务器与客户端联合测试 ===\n");

            // 配置服务端事件
            wsServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"[服务端] 客户端已连接: {e.ClientId}");
            };

            wsServer.TextMessageReceived += async (sender, e) =>
            {
                Console.WriteLine($"[服务端] 收到: {e.Text}");
                await wsServer.SendTextToClientAsync(e.ClientId, $"服务端回复: {e.Text}");
            };

            // 配置客户端事件
            wsClient.ConnectionStateChanged += (sender, e) =>
            {
                Console.WriteLine($"[客户端] 连接状态: {e.OldState} -> {e.NewState}");
            };

            wsClient.TextMessageReceived += (sender, e) =>
            {
                Console.WriteLine($"[客户端] 收到: {e.Text}");
            };

            // 启动服务端
            Console.WriteLine("启动服务端...");
            await wsServer.StartAsync();
            Console.WriteLine("服务端已启动\n");

            // 启动客户端
            Console.WriteLine("启动客户端...");
            if (await wsClient.ConnectAsync())
            {
                Console.WriteLine("客户端已连接\n");

                // 启动接收
                _ = wsClient.StartReceivingAsync();

                // 发送测试消息
                var messages = new[] { "Hello", "WebSocket", "Test!" };
                foreach (var msg in messages)
                {
                    await wsClient.SendTextAsync(msg);
                    Console.WriteLine($"[客户端] 发送: {msg}");
                    await Task.Delay(1000);
                }

                await Task.Delay(2000);
            }

            Console.WriteLine("\n按任意键停止...");
            Console.ReadKey();
        }
        finally
        {
            await wsClient.DisconnectAsync();
            wsClient.Dispose();

            await wsServer.StopAsync();
            wsServer.Dispose();

            Console.WriteLine("\n测试完成");
        }
    }
}

/// <summary>
/// JSON 请求模型
/// </summary>
internal class JsonRequest
{
    public string? Type { get; set; }
    public string? Content { get; set; }
}
