using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.WebSocket;

namespace ToolHelperTest.Examples;

/// <summary>
/// WebSocket 使用示例
/// 演示如何使用 WebSocketHelper 进行实时双向通信
/// </summary>
public class WebSocketExample
{
    /// <summary>
    /// 示例 1: 基础 WebSocket 连接
    /// 演示最简单的 WebSocket 连接和消息收发
    /// </summary>
    public static async Task BasicWebSocketAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddWebSocket(options =>
        {
            options.Uri = "wss://echo.websocket.org"; // WebSocket 回显服务器
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsHelper = serviceProvider.GetRequiredService<WebSocketHelper>();
        try
        {
            Console.WriteLine("=== WebSocket 基础连接示例 ===\n");

            // 订阅连接状态变化
            wsHelper.ConnectionStateChanged += (sender, e) =>
            {
                Console.WriteLine($"连接状态: {e.OldState} -> {e.NewState}");
            };

            // 订阅文本消息接收
            wsHelper.TextMessageReceived += (sender, e) =>
            {
                Console.WriteLine($"收到消息: {e.Text}");
            };

            // 连接到服务器
            Console.WriteLine("正在连接...");
            if (await wsHelper.ConnectAsync())
            {
                Console.WriteLine("? 连接成功\n");
                // 启动接收（不要 await，让接收任务在后台运行）
                _ = wsHelper.StartReceivingAsync();
                // 发送文本消息
                var messages = new[] { "Hello", "WebSocket", "World!" };
                foreach (var msg in messages)
                {
                    await wsHelper.SendTextAsync(msg);
                    Console.WriteLine($"已发送: {msg}");
                    await Task.Delay(1000);
                }
                await Task.Delay(2000);
            }
        }
        finally
        {
            await wsHelper.DisconnectAsync();
            wsHelper.Dispose();
            Console.WriteLine("\nWebSocket 已关闭");
        }
    }

    /// <summary>
    /// 示例 2: 自动重连
    /// 演示 WebSocket 自动重连机制
    /// </summary>
    public static async Task AutoReconnectExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddWebSocket(options =>
        {
            //options.Uri = "wss://echo.websocket.org";
            options.Uri = "ws://localhost:8080/ws/";
            options.EnableAutoReconnect = true;
            options.ReconnectInterval = 5000;
            options.MaxReconnectAttempts = 3;
        });
        var serviceProvider = services.BuildServiceProvider();
        var wsHelper = serviceProvider.GetRequiredService<WebSocketHelper>();
        Console.WriteLine("=== WebSocket 自动重连示例 ===\n");
        wsHelper.ConnectionStateChanged += (sender, e) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 状态: {e.NewState}");
        };
        wsHelper.TextMessageReceived += (sender, e) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到: {e.Text}");
        };
        try
        {
            if (await wsHelper.ConnectAsync())
            {
                // 启动接收（不要 await，让接收任务在后台运行）
                _ = wsHelper.StartReceivingAsync();
                Console.WriteLine("连接已建立，按任意键停止\n");
                var cts = new CancellationTokenSource();
                var sendTask = Task.Run(async () =>
                {
                    int count = 0;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            if (wsHelper.IsConnected)
                            {
                                await wsHelper.SendTextAsync($"Message #{++count}");
                            }
                            await Task.Delay(3000, cts.Token);
                        }
                        catch (OperationCanceledException) { break; }
                    }
                }, cts.Token);
                Console.ReadKey();
                cts.Cancel();
                await sendTask;
            }
        }
        finally
        {
            await wsHelper.DisconnectAsync();
            wsHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 3: JSON 消息通信
    /// 演示 JSON 格式的消息收发
    /// </summary>
    public static async Task JsonMessageExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddWebSocket(options =>
        {
            options.Uri = "wss://echo.websocket.org";
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsHelper = serviceProvider.GetRequiredService<WebSocketHelper>();

        try
        {
            Console.WriteLine("=== WebSocket JSON 消息示例 ===\n");
            wsHelper.TextMessageReceived += (sender, e) =>
            {
                Console.WriteLine($"收到 JSON 响应:\n{e.Text}\n");
            };
            if (await wsHelper.ConnectAsync())
            {
                // 启动接收（不要 await，让接收任务在后台运行）
                _ = wsHelper.StartReceivingAsync();
                var messages = new[]
                {
                    new { type = "greeting", content = "Hello", timestamp = DateTime.Now },
                    new { type = "data", content = "Some data", timestamp = DateTime.Now },
                    new { type = "goodbye", content = "Bye", timestamp = DateTime.Now }
                };
                foreach (var msg in messages)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(msg);
                    await wsHelper.SendTextAsync(json);
                    Console.WriteLine($"已发送: {json}");
                    await Task.Delay(1000);
                }
                await Task.Delay(2000);
            }
        }
        finally
        {
            await wsHelper.DisconnectAsync();
            wsHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 4: 二进制数据传输
    /// 演示如何发送和接收二进制数据
    /// </summary>
    public static async Task BinaryDataExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddWebSocket(options =>
        {
            options.Uri = "wss://echo.websocket.org";
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsHelper = serviceProvider.GetRequiredService<WebSocketHelper>();

        try
        {
            Console.WriteLine("=== WebSocket 二进制数据示例 ===\n");

            wsHelper.DataReceived += (sender, e) =>
            {
                Console.WriteLine($"收到二进制数据: {e.Length} 字节");
                Console.WriteLine($"  内容: {BitConverter.ToString(e.Data.Take(Math.Min(20, e.Data.Length)).ToArray())}");
            };

            if (await wsHelper.ConnectAsync())
            {
                // 启动接收（不要 await，让接收任务在后台运行）
                _ = wsHelper.StartReceivingAsync();

                // 发送二进制数据
                var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
                await wsHelper.SendAsync(binaryData);
                Console.WriteLine($"已发送二进制数据: {BitConverter.ToString(binaryData)}");

                await Task.Delay(2000);
            }
        }
        finally
        {
            await wsHelper.DisconnectAsync();
            wsHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 5: 实时聊天客户端
    /// 演示实际的聊天应用场景
    /// </summary>
    public static async Task ChatClientExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddWebSocket(options =>
        {
            //options.Uri = "wss://echo.websocket.org"; // 实际应用中替换为聊天服务器ws://127.0.0.1:9501
            options.Uri = "ws://localhost:8080/ws/"; // 实际应用中替换为聊天服务器
        });

        var serviceProvider = services.BuildServiceProvider();
        var wsHelper = serviceProvider.GetRequiredService<WebSocketHelper>();

        try
        {
            Console.WriteLine("=== WebSocket 实时聊天客户端示例 ===\n");

            wsHelper.TextMessageReceived += (sender, e) =>
            {
                Console.WriteLine($"[服务器] {e.Text}");
            };

            if (await wsHelper.ConnectAsync())
            {
                // 启动接收（不要 await，让接收任务在后台运行）
                _ = wsHelper.StartReceivingAsync();
                Console.WriteLine("已连接到聊天服务器");
                Console.WriteLine("输入消息并按回车发送（输入 'quit' 退出）\n");

                while (true)
                {
                    Console.Write("You: ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
                    {
                        break;
                    }

                    var message = $"[{DateTime.Now:HH:mm:ss}] {input}";
                    await wsHelper.SendTextAsync(message);
                }
            }
        }
        finally
        {
            await wsHelper.DisconnectAsync();
            wsHelper.Dispose();
            Console.WriteLine("\n已退出聊天");
        }
    }
}

