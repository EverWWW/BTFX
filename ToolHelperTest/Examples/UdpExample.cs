using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Udp;

namespace ToolHelperTest.Examples;

/// <summary>
/// UDP 通信使用示例
/// 演示如何使用 UdpHelper 进行 UDP 通信
/// </summary>
public class UdpExample
{
    /// <summary>
    /// 示例 1: 单播通信
    /// 演示点对点的 UDP 通信
    /// </summary>
    public static async Task UnicastExampleAsync()
    {
        // 1. 配置依赖注入
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 2. 添加 UDP 服务
        services.AddUdp(options =>
        {
            options.LocalPort = 2200;             // 本地监听端口
            options.ReceiveBufferSize = 4096;     // 接收缓冲区大小
        });

        var serviceProvider = services.BuildServiceProvider();
        var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();

        try
        {
            // 3. 订阅数据接收事件
            udpHelper.DataReceived += (sender, e) =>
            {
                var message = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"收到来自 {e.RemoteEndPoint} 的消息: {message}");
            };

            // 4. 启动接收（不要 await，让监听任务在后台运行）
            Console.WriteLine($"正在启动 UDP 通信，监听端口: 8080");
            _ = udpHelper.StartListeningAsync();
            Console.WriteLine("UDP 已启动！\n");

            // 5. 发送单播消息
            var targetEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081);
            var message = "Hello, UDP!";
            var data = Encoding.UTF8.GetBytes(message);

            await udpHelper.SendAsync(data, targetEndPoint);
            Console.WriteLine($"已发送单播消息到 {targetEndPoint}: {message}");

            // 6. 等待接收响应
            await Task.Delay(101000);
        }
        finally
        {
            // 7. 停止并释放资源
            udpHelper.StopListening();
            udpHelper.Dispose();
            Console.WriteLine("\nUDP 已停止");
        }
    }

    /// <summary>
    /// 示例 2: 广播通信
    /// 演示向局域网所有设备广播消息
    /// </summary>
    public static async Task BroadcastExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddUdp(options =>
        {
            //options.LocalHost = "192.168.1.255";
            options.LocalPort = 10000;
            options.EnableBroadcast = true;       // 启用广播
        });
        var serviceProvider = services.BuildServiceProvider();
        var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();
        try
        {
            Console.WriteLine("=== UDP 广播示例 ===\n");
            var localAddresses = new HashSet<IPAddress>(Dns.GetHostAddresses(Dns.GetHostName()));
            // 订阅数据接收
            udpHelper.DataReceived += (sender, e) =>
            {
                var remoteIp = ((System.Net.IPEndPoint)e.RemoteEndPoint).Address;
                if (localAddresses.Contains(remoteIp))
                {
                    // 忽略自己发送的广播/回环消息
                    return;
                }
                var message = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到广播响应: {message}");
                Console.WriteLine($"  来自: {e.RemoteEndPoint}");
            };
            // 启动监听（不要 await，让监听任务在后台运行）
            _ = udpHelper.StartListeningAsync();
            Console.WriteLine("开始广播消息...\n");
            // 广播消息
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, 60000);
            for (int i = 1; i <= 100; i++)
            {
                var message = $"广播消息 #{i} - {DateTime.Now:HH:mm:ss}";
                var data = Encoding.UTF8.GetBytes(message);
                await udpHelper.SendAsync(data, broadcastEndPoint);
                Console.WriteLine($"已广播: {message}");
                await Task.Delay(1000);
            }
            Console.WriteLine("\n等待响应...");
            await Task.Delay(3000);
        }
        finally
        {
            udpHelper.StopListening();
            udpHelper.Dispose();
            Console.WriteLine("\n广播已停止");
        }
    }

    /// <summary>
    /// 示例 3: 组播通信
    /// 演示加入组播组并进行通信
    /// </summary>
    public static async Task MulticastExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        //services.AddUdp(options =>
        //{
        //    options.LocalPort = 65000;
        //});
        services.AddUdp(options =>
        {
            options.LocalPort = 65000;
            options.MulticastAddress = "224.1.1.1"; // 要加入的组播组地址
            options.RemotePort = 65000;             // 组播目标端口（与 LocalPort 一致）
            options.MulticastTtl = 1;               // TTL，1 表示仅本地子网
            options.ReuseAddress = true;            // 多个进程在同一主机上监听同一端口时需要
        });

        var serviceProvider = services.BuildServiceProvider();
        var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();
        try
        {
            Console.WriteLine("=== UDP 组播示例 ===\n");
            // 订阅数据接收
            udpHelper.DataReceived += (sender, e) =>
            {
                var message = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"收到组播消息: {message}");
                Console.WriteLine($"  来自: {e.RemoteEndPoint}");
            };
            // 注意：组播需要在配置中设置
            var multicastAddress = IPAddress.Parse("224.1.1.1");
            Console.WriteLine($"组播地址: {multicastAddress}");
            // 启动监听（不要 await，让监听任务在后台运行）
            _ = udpHelper.StartListeningAsync();
            Console.WriteLine("组播已启动\n");
            // 发送组播消息
            var multicastEndPoint = new IPEndPoint(multicastAddress, 65000);
            for (int i = 1; i <= 5; i++)
            {
                var message = $"组播消息 #{i}";
                var data = Encoding.UTF8.GetBytes(message);
                await udpHelper.SendAsync(data, multicastEndPoint);
                Console.WriteLine($"已发送: {message}");
                await Task.Delay(1000);
            }
            Console.WriteLine("\n等待更多消息...");
            await Task.Delay(3000);
                Console.WriteLine("\n组播通信完成");
        }
        finally
        {
            udpHelper.StopListening();
            udpHelper.Dispose();
        }
    }

    /// <summary>
    /// 示例 4: 服务发现
    /// 演示使用 UDP 广播实现服务发现机制
    /// </summary>
    public static async Task ServiceDiscoveryExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddUdp(options =>
        {
            options.LocalPort = 10000;
            options.EnableBroadcast = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();

        var discoveredServices = new List<DiscoveredService>();

        try
        {
            Console.WriteLine("=== UDP 服务发现示例 ===\n");

            // 订阅数据接收（处理服务响应）
            udpHelper.DataReceived += (sender, e) =>
            {
                var response = Encoding.UTF8.GetString(e.Data);
                
                if (response.StartsWith("SERVICE:"))
                {
                    var parts = response.Split('|');
                    if (parts.Length >= 3)
                    {
                        var service = new DiscoveredService
                        {
                            Name = parts[0].Replace("SERVICE:", ""),
                            Version = parts[1].Replace("VERSION:", ""),
                            Port = int.Parse(parts[2].Replace("PORT:", "")),
                            Address = e.RemoteEndPoint.Address.ToString()
                        };

                        if (!discoveredServices.Any(s => s.Address == service.Address))
                        {
                            discoveredServices.Add(service);
                            Console.WriteLine($"? 发现服务: {service.Name}");
                            Console.WriteLine($"  版本: {service.Version}");
                            Console.WriteLine($"  地址: {service.Address}:{service.Port}\n");
                        }
                    }
                }
            };

            // 启动监听（不要 await，让监听任务在后台运行）
            _ = udpHelper.StartListeningAsync();

            // 发送服务发现请求
            Console.WriteLine("发送服务发现请求...\n");
            var discoveryRequest = "DISCOVER:SERVICE";
            var data = Encoding.UTF8.GetBytes(discoveryRequest);
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, 60000);

            await udpHelper.SendAsync(data, broadcastEndPoint);

            // 等待服务响应
            Console.WriteLine("等待服务响应 (5 秒)...\n");
            await Task.Delay(5000);

            // 显示发现的服务
            Console.WriteLine($"\n共发现 {discoveredServices.Count} 个服务:");
            foreach (var service in discoveredServices)
            {
                Console.WriteLine($"  - {service.Name} v{service.Version} @ {service.Address}:{service.Port}");
            }
        }
        finally
        {
                    udpHelper.StopListening();
                    udpHelper.Dispose();
                }
            }

    /// <summary>
    /// 示例 5: 实时数据传输
    /// 演示使用 UDP 传输实时数据流
    /// </summary>
    public static async Task RealTimeDataStreamAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddUdp(options =>
        {
            options.LocalPort = 10000;
        });

        var serviceProvider = services.BuildServiceProvider();
        var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();

        var receivedPackets = 0;
        var totalBytes = 0L;

        try
        {
            Console.WriteLine("=== UDP 实时数据流示例 ===\n");

            // 订阅数据接收
            udpHelper.DataReceived += (sender, e) =>
            {
                receivedPackets++;
                totalBytes += e.Length;
            };

            // 启动监听（不要 await，让监听任务在后台运行）
            _ = udpHelper.StartListeningAsync();
            Console.WriteLine("开始传输实时数据...\n");

            //var targetEndPoint = new IPEndPoint(IPAddress.Loopback, 60000);
            var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 60000); 
            var cts = new CancellationTokenSource();

            // 发送任务
            var sendTask = Task.Run(async () =>
            {
                var random = new Random();
                var packetCount = 0;

                while (!cts.Token.IsCancellationRequested)
                {
                    // 模拟传感器数据
                    var sensorData = new
                    {
                        PacketId = ++packetCount,
                        Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Temperature = 20 + random.NextDouble() * 10,
                        Humidity = 50 + random.NextDouble() * 30,
                        Pressure = 1000 + random.NextDouble() * 50
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(sensorData);
                    var data = Encoding.UTF8.GetBytes(json);

                    await udpHelper.SendAsync(data, targetEndPoint);

                    await Task.Delay(100, cts.Token); // 每 100ms 发送一次
                }
            }, cts.Token);

            // 统计任务
            var statsTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(2000, cts.Token);

                    var rate = totalBytes / 2.0 / 1024; // KB/s
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 接收包数: {receivedPackets}, 速率: {rate:F2} KB/s");

                    receivedPackets = 0;
                    totalBytes = 0;
                }
            }, cts.Token);

            Console.WriteLine("按任意键停止...\n");
            Console.ReadKey();

            cts.Cancel();
            try
            {
                await Task.WhenAll(sendTask, statsTask);
            }
            catch (OperationCanceledException) { }
        }
        finally
        {
            udpHelper.StopListening();
            udpHelper.Dispose();
            Console.WriteLine("\n数据流已停止");
        }
    }

    /// <summary>
    /// 示例 6: NAT 穿透（打洞）
    /// 演示基本的 UDP 打洞技术
    /// </summary>
    public static async Task NatPunchThroughExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddUdp(options =>
        {
            options.LocalPort = 0; // 使用随机端口
        });

        var serviceProvider = services.BuildServiceProvider();
        var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();

        try
        {
            Console.WriteLine("=== UDP NAT 穿透示例 ===\n");

            // 订阅数据接收
            udpHelper.DataReceived += (sender, e) =>
            {
                var message = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"收到穿透消息: {message}");
                Console.WriteLine($"  来自: {e.RemoteEndPoint}");
            };

            // 启动监听（不要 await，让监听任务在后台运行）
            _ = udpHelper.StartListeningAsync();

            // 获取本地端点
            var localEndPoint = "(本地随机端口)";
            Console.WriteLine($"本地端点: {localEndPoint}\n");

            // 模拟与服务器交换端点信息
            // 在实际应用中，这些信息会通过中继服务器交换
            Console.WriteLine("NAT 穿透步骤:");
            Console.WriteLine("1. 连接到中继服务器");
            Console.WriteLine("2. 向服务器发送保活包，获取公网地址");
            Console.WriteLine("3. 通过服务器交换对方的公网地址");
            Console.WriteLine("4. 同时向对方发送数据包（打洞）");
            Console.WriteLine("5. NAT 会话建立，可以直接通信\n");

            // 模拟发送打洞包
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8081);
            
            for (int i = 0; i < 5; i++)
            {
                var punchMessage = $"PUNCH:{localEndPoint}";
                await udpHelper.SendAsync(Encoding.UTF8.GetBytes(punchMessage), peerEndPoint);
                Console.WriteLine($"发送打洞包 #{i + 1} 到 {peerEndPoint}");
                await Task.Delay(500);
            }

            Console.WriteLine("\n等待建立连接...");
            await Task.Delay(3000);

            Console.WriteLine("\n说明:");
            Console.WriteLine("- UDP 打洞可以穿透大多数 NAT");
            Console.WriteLine("- 需要中继服务器协助交换地址");
            Console.WriteLine("- 对称型 NAT 难以穿透");
            Console.WriteLine("- 常用于 P2P 应用（视频通话、游戏等）");
        }
        finally
        {
              udpHelper.StopListening();
              udpHelper.Dispose();
        }
     }
 }

 /// <summary>
/// 发现的服务信息
/// </summary>
public class DiscoveredService
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
}
