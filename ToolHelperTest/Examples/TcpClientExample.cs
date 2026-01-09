using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using ToolHelper.Communication.Abstractions;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Tcp;

namespace ToolHelperTest.Examples;

/// <summary>
/// TCP 客户端使用示例
/// 演示如何使用 TcpClientHelper 进行 TCP 通信
/// </summary>
public class TcpClientExample
{
    /// <summary>
    /// 示例 1: 基础连接和数据发送
    /// 演示最简单的 TCP 客户端连接、发送和接收数据
    /// </summary>
    public static async Task BasicConnectAndSendAsync()
    {
        // 1. 配置依赖注入容器
        var services = new ServiceCollection();
        
        // 添加日志服务
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 2. 添加 TCP 客户端服务
        services.AddTcpClient(options =>
        {
            options.Host = "127.0.0.1";              // 服务器地址
            options.Port = 8080;                      // 服务器端口
            options.EnableAutoReconnect = false;      // 禁用自动重连
            options.ReceiveBufferSize = 4096;        // 接收缓冲区大小
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpClient = serviceProvider.GetRequiredService<TcpClientHelper>();

        try
        {
            // 3. 订阅连接状态变化事件
            tcpClient.ConnectionStateChanged += (sender, e) =>
            {
                Console.WriteLine($"连接状态变化: {e.OldState} -> {e.NewState}");
            };

            // 4. 订阅数据接收事件
            tcpClient.DataReceived += (sender, e) =>
            {
                var receivedText = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"收到数据: {receivedText}");
            };

            // 5. 连接到服务器
            Console.WriteLine("正在连接到服务器...");
            if (await tcpClient.ConnectAsync())
            {
                Console.WriteLine("连接成功！");

                // 6. 启动数据接收（不要 await，让接收任务在后台运行）
                _ = tcpClient.StartReceivingAsync();

                // 7. 发送文本数据
                var message = "Hello, TCP Server!";
                var data = Encoding.UTF8.GetBytes(message);
                await tcpClient.SendAsync(data);
                Console.WriteLine($"已发送: {message}");

                // 8. 等待接收响应
                await Task.Delay(2000);
            }
            else
            {
                Console.WriteLine("连接失败！");
            }
        }
        finally
        {
            // 9. 断开连接并释放资源
            await tcpClient.DisconnectAsync();
            tcpClient.Dispose();
            Console.WriteLine("连接已关闭");
        }
    }

    /// <summary>
    /// 示例 2: 带自动重连的长连接
    /// 演示如何配置自动重连，实现稳定的长连接通信
    /// </summary>
    public static async Task AutoReconnectExampleAsync()
    {
        var services = new ServiceCollection();

        // 添加日志服务（设置为 Debug 级别以查看详细重连日志）
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 配置带自动重连的 TCP 客户端
        services.AddTcpClient(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 8080;

            // 启用自动重连
            options.EnableAutoReconnect = true;
            options.ReconnectInterval = 3000;         // 重连间隔 3 秒
            options.MaxReconnectAttempts = 10;        // 最多重连 10 次

            // 禁用心跳（测试时简化）
            options.EnableHeartbeat = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpClient = serviceProvider.GetRequiredService<TcpClientHelper>();

        // 订阅连接状态变化
        tcpClient.ConnectionStateChanged += (sender, e) =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"\n[{timestamp}] ========== 状态变化 ==========");
            Console.WriteLine($"[{timestamp}] {e.OldState} -> {e.NewState}");

            if (e.NewState == ConnectionState.Connected)
            {
                Console.WriteLine($"[{timestamp}] ? 已连接到服务器");
            }
            else if (e.NewState == ConnectionState.Disconnected)
            {
                Console.WriteLine($"[{timestamp}] ? 已断开连接");
            }
            else if (e.NewState == ConnectionState.Reconnecting)
            {
                Console.WriteLine($"[{timestamp}] ? 正在尝试重新连接...");
            }
            Console.WriteLine($"[{timestamp}] ================================\n");
        };

        // 订阅数据接收
        tcpClient.DataReceived += (sender, e) =>
        {
            var text = Encoding.UTF8.GetString(e.Data);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 收到数据 ({e.Length} 字节): {text}");
        };

        try
        {
            Console.WriteLine("=== TCP 自动重连示例 ===");
            Console.WriteLine("正在连接到服务器...\n");

            // 连接服务器
            if (await tcpClient.ConnectAsync())
            {
                Console.WriteLine("初始连接成功！");

                // 启动数据接收（不要 await，让接收任务在后台运行）
                _ = tcpClient.StartReceivingAsync();

                Console.WriteLine("\n客户端已启动，每 5 秒发送一次数据");
                Console.WriteLine("请在 TCP 调试助手中断开连接测试自动重连");
                Console.WriteLine("按任意键停止...\n");

                // 定期发送数据
                var cts = new CancellationTokenSource();
                var messageCount = 0;

                var sendTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            if (tcpClient.IsConnected)
                            {
                                messageCount++;
                                var message = $"消息 #{messageCount} - {DateTime.Now:HH:mm:ss}";
                                await tcpClient.SendAsync(Encoding.UTF8.GetBytes(message));
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ? 已发送: {message}");
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ? 未连接，等待重连...");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ? 发送失败: {ex.Message}");
                        }

                        await Task.Delay(5000, cts.Token);
                    }
                }, cts.Token);

                Console.ReadKey();
                Console.WriteLine("\n正在停止...");
                cts.Cancel();

                try
                {
                    await sendTask;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
            }
            else
            {
                Console.WriteLine("初始连接失败！请确保 TCP 服务器正在运行。");
            }
        }
        finally
        {
            await tcpClient.DisconnectAsync();
            tcpClient.Dispose();
            Console.WriteLine("\n客户端已停止");
        }
    }

    /// <summary>
    /// 示例 3: 协议通信 - 请求响应模式
    /// 演示如何实现基于协议的请求-响应通信
    /// </summary>
    public static async Task ProtocolCommunicationAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddTcpClient(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 8080;
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpClient = serviceProvider.GetRequiredService<TcpClientHelper>();

        // 用于存储响应数据的队列
        var responseQueue = new System.Collections.Concurrent.ConcurrentQueue<byte[]>();
        var responseReceived = new TaskCompletionSource<bool>();

        // 订阅数据接收
        tcpClient.DataReceived += (sender, e) =>
        {
            responseQueue.Enqueue(e.Data);
            responseReceived.TrySetResult(true);
        };

        try
        {
            if (await tcpClient.ConnectAsync())
            {
                // 启动数据接收（不要 await，让接收任务在后台运行）
                _ = tcpClient.StartReceivingAsync();

                // 定义协议: [命令码(1字节)][数据长度(2字节)][数据]
                
                // 发送查询命令
                Console.WriteLine("发送查询命令...");
                var command = BuildProtocolPacket(0x01, Encoding.UTF8.GetBytes("QUERY"));
                await tcpClient.SendAsync(command);

                // 等待响应 (最多等待 5 秒)
                var responseTask = responseReceived.Task;
                if (await Task.WhenAny(responseTask, Task.Delay(5000)) == responseTask)
                {
                    if (responseQueue.TryDequeue(out var response))
                    {
                        ParseProtocolPacket(response);
                    }
                }
                else
                {
                    Console.WriteLine("等待响应超时");
                }

                // 重置并发送控制命令
                responseReceived = new TaskCompletionSource<bool>();
                Console.WriteLine("\n发送控制命令...");
                command = BuildProtocolPacket(0x02, new byte[] { 0x01 });
                await tcpClient.SendAsync(command);

                // 等待响应
                responseTask = responseReceived.Task;
                if (await Task.WhenAny(responseTask, Task.Delay(5000)) == responseTask)
                {
                    if (responseQueue.TryDequeue(out var response))
                    {
                        ParseProtocolPacket(response);
                    }
                }
            }
        }
        finally
        {
            await tcpClient.DisconnectAsync();
            tcpClient.Dispose();
        }
    }

    /// <summary>
    /// 构建协议数据包
    /// 格式: [命令码(1字节)][数据长度(2字节)][数据]
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
    /// 解析协议数据包
    /// </summary>
    private static void ParseProtocolPacket(byte[] packet)
    {
        if (packet.Length < 3)
        {
            Console.WriteLine("无效的数据包");
            return;
        }

        var commandCode = packet[0];
        var dataLength = BitConverter.ToUInt16(packet, 1);
        var data = new byte[dataLength];
        Array.Copy(packet, 3, data, 0, Math.Min(dataLength, packet.Length - 3));

        Console.WriteLine($"收到响应:");
        Console.WriteLine($"  命令码: 0x{commandCode:X2}");
        Console.WriteLine($"  数据长度: {dataLength}");
        Console.WriteLine($"  数据: {BitConverter.ToString(data)}");
    }

    /// <summary>
    /// 示例 4: 文件传输
    /// 演示如何通过 TCP 传输文件
    /// </summary>
    public static async Task FileTransferExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddTcpClient(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 8080;
            options.SendBufferSize = 65536; // 增大发送缓冲区
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpClient = serviceProvider.GetRequiredService<TcpClientHelper>();

        try
        {
            if (await tcpClient.ConnectAsync())
            {
                // 启动数据接收（不要 await，让接收任务在后台运行）
                _ = tcpClient.StartReceivingAsync();
                await Task.Delay(4000); // 等待接收任务启动
                // 模拟文件数据
                var fileContent = new byte[1024 * 100]; // 100 KB 文件
                new Random().NextBytes(fileContent);

                Console.WriteLine($"开始发送文件，大小: {fileContent.Length} 字节");

                // 1. 发送文件头信息
                var fileName = "test.dat";
                var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
                var header = new byte[4 + fileNameBytes.Length + 8];
                
                // 文件名长度 (4 字节)
                BitConverter.GetBytes(fileNameBytes.Length).CopyTo(header, 0);
                // 文件名
                fileNameBytes.CopyTo(header, 4);
                // 文件大小 (8 字节)
                BitConverter.GetBytes((long)fileContent.Length).CopyTo(header, 4 + fileNameBytes.Length);

                await tcpClient.SendAsync(header);
                Console.WriteLine($"已发送文件头: {fileName}, {fileContent.Length} 字节");

                // 2. 分块发送文件内容
                var chunkSize = 8192; // 每次发送 8KB
                var totalChunks = (fileContent.Length + chunkSize - 1) / chunkSize;
                
                for (int i = 0; i < totalChunks; i++)
                {
                    var offset = i * chunkSize;
                    var length = Math.Min(chunkSize, fileContent.Length - offset);
                    var chunk = new byte[length];
                    Array.Copy(fileContent, offset, chunk, 0, length);

                    await tcpClient.SendAsync(chunk);

                    var progress = (i + 1) * 100.0 / totalChunks;
                    Console.Write($"\r进度: {progress:F1}%");
                    await Task.Delay(400); // 等待接收任务启动
                }

                Console.WriteLine("\n文件发送完成！");
            }
        }
        finally
        {
            await tcpClient.DisconnectAsync();
            tcpClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 5: 并发连接多个服务器
    /// 演示如何同时连接和管理多个 TCP 服务器
    /// </summary>
    public static async Task MultipleConnectionsExampleAsync()
    {
        var servers = new[]
        {
            ("Server1", "127.0.0.1", 8080),
            ("Server2", "127.0.0.1", 8070),
            ("Server3", "127.0.0.1", 8060)
        };

        var tasks = new List<Task>();

        foreach (var (name, host, port) in servers)
        {
            tasks.Add(Task.Run(async () =>
            {
                // 为每个服务器创建独立的服务容器
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole());
                services.AddTcpClient(options =>
                {
                    options.Host = host;
                    options.Port = port;
                });

                var serviceProvider = services.BuildServiceProvider();
                var tcpClient = serviceProvider.GetRequiredService<TcpClientHelper>();

                try
                {
                    Console.WriteLine($"[{name}] 正在连接 {host}:{port}...");
                    
                    if (await tcpClient.ConnectAsync())
                    {
                        Console.WriteLine($"[{name}] 连接成功");

                        // 启动数据接收（不要 await，让接收任务在后台运行）
                        _ = tcpClient.StartReceivingAsync();

                        // 订阅数据接收
                        tcpClient.DataReceived += (sender, e) =>
                        {
                            var message = Encoding.UTF8.GetString(e.Data);
                            Console.WriteLine($"[{name}] 收到: {message}");
                        };

                        await Task.Delay(1000);
                        // 发送数据
                        var message = $"Hello from {name}";
                        await tcpClient.SendAsync(Encoding.UTF8.GetBytes(message));
                        Console.WriteLine($"[{name}] 已发送: {message}");

                        // 保持连接 10 秒
                        await Task.Delay(30000);
                    }
                    else
                    {
                        Console.WriteLine($"[{name}] 连接失败");
                    }
                }
                finally
                {
                    await tcpClient.DisconnectAsync();
                    tcpClient.Dispose();
                    Console.WriteLine($"[{name}] 连接已关闭");
                }
            }));
        }

        // 等待所有连接完成
        await Task.WhenAll(tasks);
        Console.WriteLine("所有连接已处理完毕");
    }

    /// <summary>
    /// 示例 6: 粘包处理
    /// 演示如何处理 TCP 粘包问题
    /// </summary>
    public static async Task StickyPacketHandlingAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        services.AddTcpClient(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 8080;
            
            // 配置粘包处理
            options.EnablePacketProcessing = true;
            options.PacketHeader = new byte[] { 0xAA, 0xBB }; // 包头
            options.PacketTail = new byte[] { 0xCC, 0xDD };   // 包尾
            options.MaxPacketLength = 1024;                    // 最大包长度
        });

        var serviceProvider = services.BuildServiceProvider();
        var tcpClient = serviceProvider.GetRequiredService<TcpClientHelper>();

        // 手动处理粘包的缓冲区
        var packetBuffer = new List<byte>();

        tcpClient.DataReceived += (sender, e) =>
        {
            // 注意：如果启用了粘包处理，框架会自动处理
            // 这里演示手动处理粘包
            packetBuffer.AddRange(e.Data);
            ProcessPackets(packetBuffer);
        };

        try
        {
            if (await tcpClient.ConnectAsync())
            {
                // 启动数据接收（不要 await，让接收任务在后台运行）
                _ = tcpClient.StartReceivingAsync();

                // 发送带包头包尾的数据
                var payload = Encoding.UTF8.GetBytes("Hello");
                var packet = new byte[2 + payload.Length + 2];
                packet[0] = 0xAA;
                packet[1] = 0xBB;
                payload.CopyTo(packet, 2);
                packet[^2] = 0xCC;
                packet[^1] = 0xDD;

                await tcpClient.SendAsync(packet);
                Console.WriteLine("已发送数据包");

                await Task.Delay(2000);
            }
        }
        finally
        {
            await tcpClient.DisconnectAsync();
            tcpClient.Dispose();
        }
    }

    /// <summary>
    /// 处理粘包数据
    /// </summary>
    private static void ProcessPackets(List<byte> buffer)
    {
        var header = new byte[] { 0xAA, 0xBB };
        var tail = new byte[] { 0xCC, 0xDD };

        while (buffer.Count >= header.Length + tail.Length)
        {
            // 查找包头
            var headerIndex = FindPattern(buffer, header);
            if (headerIndex == -1)
            {
                buffer.Clear();
                break;
            }

            // 移除包头之前的数据
            if (headerIndex > 0)
            {
                buffer.RemoveRange(0, headerIndex);
            }

            // 查找包尾
            var tailIndex = FindPattern(buffer, tail, header.Length);
            if (tailIndex == -1)
            {
                // 未找到完整的包，等待更多数据
                break;
            }

            // 提取完整的包
            var packetLength = tailIndex + tail.Length;
            var packet = buffer.GetRange(0, packetLength).ToArray();
            buffer.RemoveRange(0, packetLength);

            // 提取有效数据（去除包头和包尾）
            var payload = new byte[packet.Length - header.Length - tail.Length];
            Array.Copy(packet, header.Length, payload, 0, payload.Length);

            Console.WriteLine($"提取到完整数据包: {Encoding.UTF8.GetString(payload)}");
        }
    }

    /// <summary>
    /// 在缓冲区中查找指定模式
    /// </summary>
    private static int FindPattern(List<byte> buffer, byte[] pattern, int startIndex = 0)
    {
        for (int i = startIndex; i <= buffer.Count - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return i;
            }
        }
        return -1;
    }
}
