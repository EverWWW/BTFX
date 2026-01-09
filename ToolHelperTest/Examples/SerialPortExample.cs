using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.SerialPort;

namespace ToolHelperTest.Examples;

/// <summary>
/// 串口通信使用示例
/// 演示如何使用 SerialPortHelper 进行串口通信
/// </summary>
public class SerialPortExample
{
    /// <summary>
    /// 示例 1: 基础串口通信
    /// 演示最简单的串口打开、发送和接收
    /// </summary>
    public static async Task BasicSerialPortAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSerialPort(options =>
        {
            options.PortName = "COM8";           // 串口名称
            options.BaudRate = 9600;             // 波特率
            options.DataBits = 8;                // 数据位
            options.StopBits = StopBits.One;     // 停止位
            options.Parity = Parity.None;        // 校验位
        });

        var serviceProvider = services.BuildServiceProvider();
        var serialPort = serviceProvider.GetRequiredService<SerialPortHelper>();

        try
        {
            Console.WriteLine("=== 串口通信基础示例 ===\n");

            // 订阅数据接收事件
            serialPort.DataReceived += (sender, e) =>
            {
                var receivedText = Encoding.UTF8.GetString(e.Data);
                Console.WriteLine($"收到数据: {receivedText}");
            };

            // 打开串口
            Console.WriteLine($"打开串口: COM3, 波特率: 9600");
            if (await serialPort.ConnectAsync())
            {
                Console.WriteLine("? 串口已打开\n");

                // 启动接收
                await serialPort.StartReceivingAsync();

                // 发送数据
                var message = "Hello, Serial Port!";
                await serialPort.SendAsync(Encoding.UTF8.GetBytes(message));
                Console.WriteLine($"已发送: {message}");

                await Task.Delay(2000);
            }
            else
            {
                Console.WriteLine("? 串口打开失败");
            }
        }
        finally
        {
            await serialPort.DisconnectAsync();
            serialPort.Dispose();
            Console.WriteLine("\n串口已关闭");
        }
    }

    /// <summary>
    /// 示例 2: 串口自动识别
    /// 演示如何枚举和识别可用串口
    /// </summary>
    public static async Task AutoDetectSerialPortAsync()
    {
        Console.WriteLine("=== 串口自动识别示例 ===\n");

        // 获取可用串口列表
        var availablePorts = SerialPort.GetPortNames();
        Console.WriteLine($"找到 {availablePorts.Length} 个串口:");
        foreach (var port in availablePorts)
        {
            Console.WriteLine($"  ? {port}");
        }

        if (availablePorts.Length == 0)
        {
            Console.WriteLine("\n没有找到可用的串口");
            return;
        }

        // 测试第一个串口
        var testPort = availablePorts[0];
        Console.WriteLine($"\n测试串口: {testPort}");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSerialPort(options =>
        {
            options.PortName = testPort;
            options.BaudRate = 9600;
        });

        var serviceProvider = services.BuildServiceProvider();
        var serialPort = serviceProvider.GetRequiredService<SerialPortHelper>();

        try
        {
            if (await serialPort.ConnectAsync())
            {
                Console.WriteLine($"? {testPort} 可用");
                await serialPort.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? {testPort} 不可用: {ex.Message}");
        }
        finally
        {
            serialPort.Dispose();
        }
    }

    /// <summary>
    /// 示例 3: AT 命令通信
    /// 演示与 GSM/GPS 模块等设备的 AT 命令交互
    /// </summary>
    public static async Task AtCommandExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSerialPort(options =>
        {
            options.PortName = "COM8";
            options.BaudRate = 9600;  // GSM 模块常用波特率
        });

        var serviceProvider = services.BuildServiceProvider();
        var serialPort = serviceProvider.GetRequiredService<SerialPortHelper>();

        try
        {
            Console.WriteLine("=== AT 命令通信示例 ===\n");

            var responses = new System.Collections.Concurrent.ConcurrentQueue<string>();

            serialPort.DataReceived += (sender, e) =>
            {
                var response = Encoding.UTF8.GetString(e.Data).Trim();
                if (!string.IsNullOrEmpty(response))
                {
                    responses.Enqueue(response);
                    Console.WriteLine($"<< {response}");
                }
            };

            if (await serialPort.ConnectAsync())
            {
                await serialPort.StartReceivingAsync();
                Console.WriteLine("串口已打开，开始 AT 命令交互\n");

                // 发送 AT 命令示例
                var commands = new[]
                {
                    "AT",           // 测试命令
                    "AT+CSQ",       // 查询信号质量
                    "AT+CIMI",      // 查询 SIM 卡 IMSI
                    "AT+COPS?"      // 查询运营商
                };

                foreach (var cmd in commands)
                {
                    Console.WriteLine($">> {cmd}");
                    await serialPort.SendAsync(Encoding.UTF8.GetBytes(cmd + "\r\n"));
                    await Task.Delay(1000);  // 等待响应
                }

                await Task.Delay(2000);
            }
        }
        finally
        {
            await serialPort.DisconnectAsync();
            serialPort.Dispose();
        }
    }

    /// <summary>
    /// 示例 4: 协议通信
    /// 演示基于自定义协议的串口通信
    /// </summary>
    public static async Task ProtocolCommunicationAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSerialPort(options =>
        {
            options.PortName = "COM3";
            options.BaudRate = 9600;
        });

        var serviceProvider = services.BuildServiceProvider();
        var serialPort = serviceProvider.GetRequiredService<SerialPortHelper>();

        try
        {
            Console.WriteLine("=== 串口协议通信示例 ===\n");
            Console.WriteLine("协议格式: [0xAA 0x55][长度][数据][校验和]\n");

            serialPort.DataReceived += (sender, e) =>
            {
                ParseProtocolPacket(e.Data);
            };

            if (await serialPort.ConnectAsync())
            {
                await serialPort.StartReceivingAsync();

                // 发送协议数据包
                var packets = new[]
                {
                    BuildProtocolPacket(new byte[] { 0x01, 0x02, 0x03 }),
                    BuildProtocolPacket(Encoding.UTF8.GetBytes("Hello")),
                    BuildProtocolPacket(new byte[] { 0xFF, 0xFE, 0xFD })
                };

                foreach (var packet in packets)
                {
                    Console.WriteLine($"发送数据包: {BitConverter.ToString(packet)}");
                    await serialPort.SendAsync(packet);
                    await Task.Delay(1000);
                }

                await Task.Delay(2000);
            }
        }
        finally
        {
            await serialPort.DisconnectAsync();
            serialPort.Dispose();
        }
    }

    /// <summary>
    /// 构建协议数据包
    /// </summary>
    private static byte[] BuildProtocolPacket(byte[] data)
    {
        var packet = new byte[4 + data.Length + 1];
        packet[0] = 0xAA;  // 包头1
        packet[1] = 0x55;  // 包头2
        packet[2] = (byte)data.Length;  // 数据长度
        data.CopyTo(packet, 3);

        // 计算校验和
        byte checksum = 0;
        for (int i = 2; i < packet.Length - 1; i++)
        {
            checksum += packet[i];
        }
        packet[^1] = checksum;

        return packet;
    }

    /// <summary>
    /// 解析协议数据包
    /// </summary>
    private static void ParseProtocolPacket(byte[] data)
    {
        if (data.Length < 5) return;

        if (data[0] == 0xAA && data[1] == 0x55)
        {
            var length = data[2];
            Console.WriteLine($"收到协议包: 长度={length}");
            Console.WriteLine($"  数据: {BitConverter.ToString(data, 3, Math.Min(length, data.Length - 4))}");
        }
    }

    /// <summary>
    /// 示例 5: 传感器数据采集
    /// 演示持续采集传感器数据
    /// </summary>
    public static async Task SensorDataCollectionAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSerialPort(options =>
        {
            options.PortName = "COM3";
            options.BaudRate = 9600;
        });

        var serviceProvider = services.BuildServiceProvider();
        var serialPort = serviceProvider.GetRequiredService<SerialPortHelper>();

        try
        {
            Console.WriteLine("=== 传感器数据采集示例 ===\n");

            var dataCount = 0;

            serialPort.DataReceived += (sender, e) =>
            {
                dataCount++;
                var dataStr = Encoding.UTF8.GetString(e.Data).Trim();
                
                // 模拟解析传感器数据
                if (dataStr.StartsWith("TEMP:"))
                {
                    Console.WriteLine($"[{dataCount}] 温度数据: {dataStr}");
                }
                else if (dataStr.StartsWith("HUM:"))
                {
                    Console.WriteLine($"[{dataCount}] 湿度数据: {dataStr}");
                }
                else
                {
                    Console.WriteLine($"[{dataCount}] 原始数据: {dataStr}");
                }
            };

            if (await serialPort.ConnectAsync())
            {
                await serialPort.StartReceivingAsync();
                Console.WriteLine("开始采集数据...");
                Console.WriteLine("按任意键停止\n");

                // 定期请求数据
                var cts = new CancellationTokenSource();
                var requestTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        await serialPort.SendAsync(Encoding.UTF8.GetBytes("READ\r\n"));
                        await Task.Delay(2000, cts.Token);
                    }
                }, cts.Token);

                Console.ReadKey();
                cts.Cancel();

                try { await requestTask; } catch (OperationCanceledException) { }

                Console.WriteLine($"\n采集完成，共接收 {dataCount} 条数据");
            }
        }
        finally
        {
            await serialPort.DisconnectAsync();
            serialPort.Dispose();
        }
    }
}
