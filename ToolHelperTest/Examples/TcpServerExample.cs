using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Tcp;

namespace ToolHelperTest.Examples;

/// <summary>
/// TCP 服务器使用示例
/// 演示如何使用 TcpServerHelper 创建 TCP 服务器
/// </summary>
public class TcpServerExample
{
    /// <summary>
    /// 示例 1: 基础回显服务器
    /// 演示最简单的 TCP 服务器，接收客户端消息并回显
    /// </summary>
    public static async Task BasicEchoServerAsync()
    {
        // 1. 配置依赖注入
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddFilter("Log", LogLevel.Debug);
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 2. 添加 TCP 服务器服务
        services.AddTcpServer(options =>
        {
            options.Port = 8080;                  // 监听端口
            options.MaxConnections = 100;         // 最大连接数
            options.ReceiveBufferSize = 4096;     // 接收缓冲区大小
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpServer = serviceProvider.GetRequiredService<TcpServerHelper>();

        try
        {
            // 3. 订阅客户端连接事件
            tcpServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端已连接: {e.ClientId}");
            };

            // 4. 订阅客户端断开事件
            tcpServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端已断开: {e.ClientId}");
            };

            // 5. 订阅数据接收事件（回显逻辑）
            tcpServer.DataReceived += async (sender, e) =>
            {
                var receivedText = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"[{e.ClientId}] 收到: {receivedText}");

                // 回显消息给客户端
                var response = $"Echo: {receivedText}";
                var responseData = Encoding.UTF8.GetBytes(response);
                await tcpServer.SendToClientAsync(e.ClientId, responseData);
                Console.WriteLine($"[{e.ClientId}] 回显: {response}");
            };

            // 6. 启动服务器
            Console.WriteLine($"正在启动 TCP 回显服务器，端口: 8080");
            await tcpServer.StartAsync();
            Console.WriteLine("服务器已启动！等待客户端连接...\n");
            Console.WriteLine("按任意键停止服务器");

            Console.ReadKey();
        }
        finally
        {
            // 7. 停止服务器
            await tcpServer.StopAsync();
            tcpServer.Dispose();
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
        services.AddTcpServer(options =>
        {
            options.Port = 8080;
            options.MaxConnections = 50;
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpServer = serviceProvider.GetRequiredService<TcpServerHelper>();

        // 存储客户端昵称
        var clientNicknames = new ConcurrentDictionary<string, string>();

        try
        {
            Console.WriteLine("=== TCP 聊天室服务器 ===\n");

            // 客户端连接
            tcpServer.ClientConnected += async (sender, e) =>
            {
                var nickname = $"用户{clientNicknames.Count + 1}";
                clientNicknames[e.ClientId] = nickname;

                Console.WriteLine($"? {nickname} 加入聊天室 ({e.ClientId})");

                // 发送欢迎消息
                var welcome = $"欢迎 {nickname}! 当前在线: {clientNicknames.Count} 人\n";
                await tcpServer.SendToClientAsync(e.ClientId, Encoding.UTF8.GetBytes(welcome));

                // 通知其他用户
                var joinMsg = $"[系统] {nickname} 加入了聊天室\n";
                await tcpServer.BroadcastAsync(Encoding.UTF8.GetBytes(joinMsg));
            };

            // 客户端断开
            tcpServer.ClientDisconnected += async (sender, e) =>
            {
                if (clientNicknames.TryRemove(e.ClientId, out var nickname))
                {
                    Console.WriteLine($"? {nickname} 离开聊天室");

                    // 通知其他用户
                    var leaveMsg = $"[系统] {nickname} 离开了聊天室\n";
                    await tcpServer.BroadcastAsync(Encoding.UTF8.GetBytes(leaveMsg));
                }
            };

            // 接收并广播消息
            tcpServer.DataReceived += async (sender, e) =>
            {
                if (clientNicknames.TryGetValue(e.ClientId, out var nickname))
                {
                    var message = Encoding.UTF8.GetString(e.Data).Trim();
                    Console.WriteLine($"[{nickname}] {message}");

                    // 广播给所有客户端
                    var broadcastMsg = $"[{nickname}] {message}\n";
                    await tcpServer.BroadcastAsync(Encoding.UTF8.GetBytes(broadcastMsg));
                }
            };

            await tcpServer.StartAsync();
            Console.WriteLine("聊天室服务器已启动，端口: 8080");
            Console.WriteLine("按任意键停止\n");

            Console.ReadKey();
        }
        finally
        {
            await tcpServer.StopAsync();
            tcpServer.Dispose();
            Console.WriteLine("聊天室服务器已停止");
        }
    }

    /// <summary>
    /// 示例 3: 数据采集服务器
    /// 演示接收多个客户端的数据并进行统计分析
    /// </summary>
    public static async Task DataCollectionServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddTcpServer(options =>
        {
            options.Port = 8080;
            options.MaxConnections = 100;
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpServer = serviceProvider.GetRequiredService<TcpServerHelper>();

        // 数据统计
        var dataStats = new ConcurrentDictionary<string, ClientStats>();

        try
        {
            Console.WriteLine("=== 数据采集服务器 ===\n");

            tcpServer.ClientConnected += (sender, e) =>
            {
                dataStats[e.ClientId] = new ClientStats
                {
                    ClientId = e.ClientId,
                    ConnectedTime = DateTime.Now,
                    PacketCount = 0,
                    TotalBytes = 0
                };
                Console.WriteLine($"? 客户端连接: {e.ClientId}");
            };

            tcpServer.ClientDisconnected += (sender, e) =>
            {
                if (dataStats.TryRemove(e.ClientId, out var stats))
                {
                    Console.WriteLine($"? 客户端断开: {e.ClientId}");
                    Console.WriteLine($"  连接时长: {(DateTime.Now - stats.ConnectedTime).TotalSeconds:F1} 秒");
                    Console.WriteLine($"  接收包数: {stats.PacketCount}");
                    Console.WriteLine($"  总字节数: {stats.TotalBytes}\n");
                }
            };

            tcpServer.DataReceived += (sender, e) =>
            {
                if (dataStats.TryGetValue(e.ClientId, out var stats))
                {
                    stats.PacketCount++;
                    stats.TotalBytes += e.Length;
                    stats.LastReceiveTime = DateTime.Now;
                }
            };

            // 启动统计显示任务
            var cts = new CancellationTokenSource();
            var statsTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(5000, cts.Token);

                    Console.WriteLine($"\n=== 实时统计 [{DateTime.Now:HH:mm:ss}] ===");
                    Console.WriteLine($"在线客户端: {dataStats.Count}");

                    foreach (var stat in dataStats.Values)
                    {
                        var duration = (DateTime.Now - stat.ConnectedTime).TotalSeconds;
                        var rate = stat.TotalBytes / duration;
                        Console.WriteLine($"  {stat.ClientId}:");
                        Console.WriteLine($"    包数: {stat.PacketCount}, 字节: {stat.TotalBytes}, 速率: {rate:F1} B/s");
                    }
                    Console.WriteLine();
                }
            }, cts.Token);

            await tcpServer.StartAsync();
            Console.WriteLine("数据采集服务器已启动，端口: 8080");
            Console.WriteLine("按任意键停止\n");

            Console.ReadKey();
            cts.Cancel();
            await statsTask;
        }
        finally
        {
            await tcpServer.StopAsync();
            tcpServer.Dispose();
        }
    }

    /// <summary>
    /// 示例 4: 协议服务器
    /// 演示基于自定义协议的服务器实现
    /// </summary>
    public static async Task ProtocolServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddTcpServer(options =>
        {
            options.Port = 8080;
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpServer = serviceProvider.GetRequiredService<TcpServerHelper>();

        try
        {
            Console.WriteLine("=== 协议服务器 ===");
            Console.WriteLine("支持的命令:");
            Console.WriteLine("  0x01 - 查询 (QUERY)");
            Console.WriteLine("  0x02 - 控制 (CONTROL)");
            Console.WriteLine("  0x03 - 配置 (CONFIG)\n");

            tcpServer.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端连接: {e.ClientId}");
            };

            tcpServer.ClientDisconnected += (sender, e) =>
            {
                Console.WriteLine($"? 客户端断开: {e.ClientId}");
            };

            // 处理协议命令
            tcpServer.DataReceived += async (sender, e) =>
            {
                if (e.Data.Length < 3)
                {
                    Console.WriteLine($"[{e.ClientId}] 无效的数据包");
                    return;
                }

                var commandCode = e.Data[0];
                var dataLength = BitConverter.ToUInt16(e.Data, 1);

                Console.WriteLine($"[{e.ClientId}] 命令: 0x{commandCode:X2}, 数据长度: {dataLength}");

                byte[] response;

                switch (commandCode)
                {
                    case 0x01: // 查询
                        var queryResult = "STATUS:OK,TEMP:25.5,PRESSURE:101.3";
                        response = BuildProtocolPacket(0x81, Encoding.UTF8.GetBytes(queryResult));
                        Console.WriteLine($"[{e.ClientId}] 响应查询: {queryResult}");
                        break;

                    case 0x02: // 控制
                        var controlResult = "CONTROL:SUCCESS";
                        response = BuildProtocolPacket(0x82, Encoding.UTF8.GetBytes(controlResult));
                        Console.WriteLine($"[{e.ClientId}] 响应控制: {controlResult}");
                        break;

                    case 0x03: // 配置
                        var configResult = "CONFIG:SAVED";
                        response = BuildProtocolPacket(0x83, Encoding.UTF8.GetBytes(configResult));
                        Console.WriteLine($"[{e.ClientId}] 响应配置: {configResult}");
                        break;

                    default:
                        var errorResult = "ERROR:UNKNOWN_COMMAND";
                        response = BuildProtocolPacket(0xFF, Encoding.UTF8.GetBytes(errorResult));
                        Console.WriteLine($"[{e.ClientId}] 未知命令");
                        break;
                }

                await tcpServer.SendToClientAsync(e.ClientId, response);
            };

            await tcpServer.StartAsync();
            Console.WriteLine("协议服务器已启动，端口: 8080");
            Console.WriteLine("按任意键停止\n");

            Console.ReadKey();
        }
        finally
        {
            await tcpServer.StopAsync();
            tcpServer.Dispose();
        }
    }

    /// <summary>
    /// 构建协议数据包
    /// </summary>
    private static byte[] BuildProtocolPacket(byte commandCode, byte[] data)
    {
        var packet = new byte[3 + data.Length];
        packet[0] = commandCode;
        BitConverter.GetBytes((ushort)data.Length).CopyTo(packet, 1);
        data.CopyTo(packet, 3);
        return packet;
    }

    /// <summary>
    /// 示例 5: 广播服务器
    /// 演示定时向所有客户端广播数据
    /// </summary>
    public static async Task BroadcastServerAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddTcpServer(options =>
        {
            options.Port = 8080;
            options.MaxConnections = 100;
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpServer = serviceProvider.GetRequiredService<TcpServerHelper>();

        try
        {
            Console.WriteLine("=== 广播服务器 ===\n");

            var clientCount = 0;

            tcpServer.ClientConnected += (sender, e) =>
            {
                clientCount++;
                Console.WriteLine($"? 客户端连接: {e.ClientId} (在线: {clientCount})");
            };

            tcpServer.ClientDisconnected += (sender, e) =>
            {
                clientCount--;
                Console.WriteLine($"? 客户端断开: {e.ClientId} (在线: {clientCount})");
            };

            await tcpServer.StartAsync();
            Console.WriteLine("广播服务器已启动，端口: 8080");
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

                    if (clientCount > 0)
                    {
                        messageCount++;
                        var timestamp = DateTime.Now.ToString("HH:mm:ss");
                        var message = $"[广播 #{messageCount}] 时间: {timestamp}, 在线: {clientCount}\n";
                        var data = Encoding.UTF8.GetBytes(message);

                        await tcpServer.BroadcastAsync(data);
                        Console.WriteLine($"已广播: {message.Trim()}");
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
            await tcpServer.StopAsync();
            tcpServer.Dispose();
            Console.WriteLine("\n广播服务器已停止");
        }
    }
}

/// <summary>
/// 客户端统计信息
/// </summary>
public class ClientStats
{
    public string ClientId { get; set; } = string.Empty;
    public DateTime ConnectedTime { get; set; }
    public DateTime LastReceiveTime { get; set; }
    public int PacketCount { get; set; }
    public long TotalBytes { get; set; }
}
