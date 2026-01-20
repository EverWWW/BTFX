using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
//using ToolHelper.Communication.Bluetooth;
using ToolHelper.Communication.Configuration;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Configuration;

namespace ToolHelperTest.Examples;

/// <summary>
/// 蓝牙通讯使用示例
/// 演示如何使用 BluetoothHelper 进行蓝牙设备扫描、连接和数据收发
/// </summary>
//public class BluetoothExample
//{
//    /// <summary>
//    /// 示例 1: 基本蓝牙设备扫描
//    /// 演示如何扫描附近的蓝牙设备
//    /// </summary>
//    public static async Task ScanDevicesAsync()
//    {
//        Console.WriteLine("=== 蓝牙设备扫描示例 ===\n");

//        var services = new ServiceCollection();
//        services.AddLogging(builder =>
//        {
//            builder.AddConsole();
//            builder.SetMinimumLevel(LogLevel.Information);
//        });

//        services.AddBluetooth(options =>
//        {
//            options.ScanTimeout = 10000;  // 扫描10秒
//        });

//        var serviceProvider = services.BuildServiceProvider();
//        var bluetooth = serviceProvider.GetRequiredService<BluetoothHelper>();

//        try
//        {
//            // 订阅设备发现事件
//            bluetooth.DeviceDiscovered += (sender, e) =>
//            {
//                Console.WriteLine($"发现设备: {e.Device.Name} ({e.Device.Address})");
//                Console.WriteLine($"  信号强度: {e.Device.SignalStrength} dBm");
//                Console.WriteLine($"  是否已配对: {e.Device.IsPaired}");
//                Console.WriteLine($"  是否BLE: {e.Device.IsBleDevice}");
//                Console.WriteLine();
//            };

//            // 订阅扫描完成事件
//            bluetooth.ScanCompleted += (sender, e) =>
//            {
//                Console.WriteLine($"\n扫描完成，共发现 {e.Devices.Count} 个设备");
//            };

//            Console.WriteLine("开始扫描蓝牙设备...\n");
//            var devices = await bluetooth.ScanDevicesAsync();

//            Console.WriteLine("\n发现的设备列表:");
//            foreach (var device in devices)
//            {
//                Console.WriteLine($"  • {device}");
//            }
//        }
//        finally
//        {
//            bluetooth.Dispose();
//        }
//    }

//    /// <summary>
//    /// 示例 2: 连接蓝牙设备
//    /// 演示如何连接到指定的蓝牙设备
//    /// </summary>
//    public static async Task ConnectDeviceAsync()
//    {
//        Console.WriteLine("=== 蓝牙设备连接示例 ===\n");

//        var services = new ServiceCollection();
//        services.AddLogging(builder =>
//        {
//            builder.AddConsole();
//            builder.SetMinimumLevel(LogLevel.Information);
//        });

//        // 配置目标设备地址
//        services.AddBluetooth(options =>
//        {
//            options.DeviceAddress = "00:11:22:33:44:55";  // 替换为实际设备地址
//            options.DeviceName = "My Bluetooth Device";
//            options.ConnectionTimeout = 10000;
//            options.AutoReconnect = true;
//            options.MaxReconnectAttempts = 3;
//        });

//        var serviceProvider = services.BuildServiceProvider();
//        var bluetooth = serviceProvider.GetRequiredService<BluetoothHelper>();

//        try
//        {
//            // 订阅连接状态变化事件
//            bluetooth.ConnectionStateChanged += (sender, e) =>
//            {
//                Console.WriteLine($"连接状态变化: {e.OldState} -> {e.NewState}");
//                if (e.Exception != null)
//                {
//                    Console.WriteLine($"  异常: {e.Exception.Message}");
//                }
//            };

//            Console.WriteLine("正在连接蓝牙设备...\n");

//            if (await bluetooth.ConnectAsync())
//            {
//                Console.WriteLine("✓ 连接成功！");
//                Console.WriteLine($"  设备名称: {bluetooth.ConnectedDevice?.Name}");
//                Console.WriteLine($"  设备地址: {bluetooth.ConnectedDevice?.Address}");

//                // 保持连接一段时间
//                await Task.Delay(3000);
//            }
//            else
//            {
//                Console.WriteLine("✗ 连接失败");
//            }
//        }
//        finally
//        {
//            await bluetooth.DisconnectAsync();
//            bluetooth.Dispose();
//            Console.WriteLine("\n蓝牙连接已断开");
//        }
//    }

//    /// <summary>
//    /// 示例 3: 蓝牙数据收发
//    /// 演示如何通过蓝牙发送和接收数据
//    /// </summary>
//    public static async Task SendReceiveDataAsync()
//    {
//        Console.WriteLine("=== 蓝牙数据收发示例 ===\n");

//        var services = new ServiceCollection();
//        services.AddLogging(builder =>
//        {
//            builder.AddConsole();
//            builder.SetMinimumLevel(LogLevel.Debug);
//        });

//        services.AddBluetooth(options =>
//        {
//            options.DeviceAddress = "00:11:22:33:44:55";  // 替换为实际设备地址
//            options.ReceiveBufferSize = 4096;
//            options.SendBufferSize = 4096;
//        });

//        var serviceProvider = services.BuildServiceProvider();
//        var bluetooth = serviceProvider.GetRequiredService<BluetoothHelper>();

//        try
//        {
//            // 订阅数据接收事件
//            bluetooth.DataReceived += (sender, e) =>
//            {
//                var receivedText = Encoding.UTF8.GetString(e.Data, 0, e.Length);
//                Console.WriteLine($"收到数据 ({e.Length} 字节): {receivedText}");
//                Console.WriteLine($"  接收时间: {e.ReceivedTime:HH:mm:ss.fff}");
//            };

//            Console.WriteLine("正在连接蓝牙设备...");

//            if (await bluetooth.ConnectAsync())
//            {
//                Console.WriteLine("✓ 连接成功\n");

//                // 启动接收
//                await bluetooth.StartReceivingAsync();
//                Console.WriteLine("已启动数据接收\n");

//                // 发送测试数据
//                var messages = new[]
//                {
//                    "Hello, Bluetooth!",
//                    "Test message 1",
//                    "Test message 2",
//                    "你好，蓝牙！"
//                };

//                foreach (var message in messages)
//                {
//                    var bytesSent = await bluetooth.SendTextAsync(message);
//                    Console.WriteLine($"已发送: {message} ({bytesSent} 字节)");
//                    await Task.Delay(500);
//                }

//                // 等待接收响应
//                Console.WriteLine("\n等待接收数据...");
//                await Task.Delay(5000);

//                // 停止接收
//                bluetooth.StopReceiving();
//                Console.WriteLine("已停止数据接收");
//            }
//            else
//            {
//                Console.WriteLine("✗ 连接失败");
//            }
//        }
//        finally
//        {
//            await bluetooth.DisconnectAsync();
//            bluetooth.Dispose();
//            Console.WriteLine("\n蓝牙连接已断开");
//        }
//    }

//    /// <summary>
//    /// 示例 4: 获取已配对设备
//    /// 演示如何获取系统中已配对的蓝牙设备列表
//    /// </summary>
//    public static async Task GetPairedDevicesAsync()
//    {
//        Console.WriteLine("=== 获取已配对蓝牙设备示例 ===\n");

//        var services = new ServiceCollection();
//        services.AddLogging(builder =>
//        {
//            builder.AddConsole();
//            builder.SetMinimumLevel(LogLevel.Information);
//        });

//        services.AddBluetooth();

//        var serviceProvider = services.BuildServiceProvider();
//        var bluetooth = serviceProvider.GetRequiredService<BluetoothHelper>();

//        try
//        {
//            Console.WriteLine("获取已配对设备列表...\n");
//            var pairedDevices = await bluetooth.GetPairedDevicesAsync();

//            if (pairedDevices.Count == 0)
//            {
//                Console.WriteLine("没有已配对的设备");
//                Console.WriteLine("\n注意：获取已配对设备需要平台特定的API支持");
//                Console.WriteLine("Windows: 使用 Windows.Devices.Bluetooth API");
//                Console.WriteLine("或使用第三方库: InTheHand.Net.Bluetooth (32feet.NET)");
//            }
//            else
//            {
//                Console.WriteLine($"找到 {pairedDevices.Count} 个已配对设备:");
//                foreach (var device in pairedDevices)
//                {
//                    Console.WriteLine($"  • {device.Name}");
//                    Console.WriteLine($"    地址: {device.Address}");
//                    Console.WriteLine($"    类型: {device.DeviceType}");
//                    Console.WriteLine($"    已连接: {device.IsConnected}");
//                    Console.WriteLine();
//                }
//            }
//        }
//        finally
//        {
//            bluetooth.Dispose();
//        }
//    }

//    /// <summary>
//    /// 示例 5: 手动创建 BluetoothHelper（不使用依赖注入）
//    /// 演示如何在不使用DI容器的情况下创建和使用BluetoothHelper
//    /// </summary>
//    public static async Task ManualCreationAsync()
//    {
//        Console.WriteLine("=== 手动创建 BluetoothHelper 示例 ===\n");

//        // 创建日志工厂
//        using var loggerFactory = LoggerFactory.Create(builder =>
//        {
//            builder.AddConsole();
//            builder.SetMinimumLevel(LogLevel.Debug);
//        });

//        var logger = loggerFactory.CreateLogger<BluetoothHelper>();

//        // 直接创建 BluetoothHelper 实例
//        var options = new BluetoothOptions
//        {
//            DeviceAddress = "00:11:22:33:44:55",
//            DeviceName = "Test Device",
//            ConnectionTimeout = 5000,
//            ScanTimeout = 10000,
//            AutoReconnect = false
//        };

//        using var bluetooth = new BluetoothHelper(options, logger);

//        // 订阅事件
//        bluetooth.ConnectionStateChanged += (s, e) =>
//            Console.WriteLine($"状态: {e.OldState} -> {e.NewState}");

//        bluetooth.DataReceived += (s, e) =>
//            Console.WriteLine($"收到 {e.Length} 字节数据");

//        // 扫描设备
//        Console.WriteLine("扫描设备...");
//        var devices = await bluetooth.ScanDevicesAsync();
//        Console.WriteLine($"发现 {devices.Count} 个设备\n");

//        // 连接
//        Console.WriteLine("尝试连接...");
//        var connected = await bluetooth.ConnectAsync();
//        Console.WriteLine($"连接结果: {(connected ? "成功" : "失败")}");

//        if (connected)
//        {
//            await Task.Delay(1000);
//            await bluetooth.DisconnectAsync();
//        }

//        Console.WriteLine("\n示例完成");
//    }

//    /// <summary>
//    /// 示例 6: BLE（低功耗蓝牙）模式
//    /// 演示如何使用BLE模式连接设备
//    /// </summary>
//    public static async Task BleDeviceAsync()
//    {
//        Console.WriteLine("=== BLE低功耗蓝牙示例 ===\n");

//        var services = new ServiceCollection();
//        services.AddLogging(builder =>
//        {
//            builder.AddConsole();
//            builder.SetMinimumLevel(LogLevel.Information);
//        });

//        services.AddBluetooth(options =>
//        {
//            options.UseBleMode = true;
//            options.DeviceAddress = "00:11:22:33:44:55";
//            // 通用属性服务UUID（示例）
//            options.ServiceUuid = new Guid("0000180f-0000-1000-8000-00805f9b34fb");  // Battery Service
//            // 电池电量特征UUID（示例）
//            options.CharacteristicUuid = new Guid("00002a19-0000-1000-8000-00805f9b34fb");  // Battery Level
//        });

//        var serviceProvider = services.BuildServiceProvider();
//        var bluetooth = serviceProvider.GetRequiredService<BluetoothHelper>();

//        try
//        {
//            Console.WriteLine("BLE设备连接功能说明：\n");
//            Console.WriteLine("BLE（低功耗蓝牙）是蓝牙4.0引入的技术，特点：");
//            Console.WriteLine("  • 低功耗，适合电池供电设备");
//            Console.WriteLine("  • 基于GATT协议");
//            Console.WriteLine("  • 使用服务(Service)和特征(Characteristic)进行数据交换");
//            Console.WriteLine();
//            Console.WriteLine("常见BLE服务UUID：");
//            Console.WriteLine("  • 心率服务: 0x180D");
//            Console.WriteLine("  • 电池服务: 0x180F");
//            Console.WriteLine("  • 设备信息: 0x180A");
//            Console.WriteLine();
//            Console.WriteLine("注意：完整的BLE支持需要使用：");
//            Console.WriteLine("  • Windows.Devices.Bluetooth.GenericAttributeProfile (UWP/WinRT)");
//            Console.WriteLine("  • Plugin.BLE (MAUI/Xamarin)");
//            Console.WriteLine("  • InTheHand.Net.Bluetooth (32feet.NET)");

//            // 扫描BLE设备
//            Console.WriteLine("\n扫描BLE设备...");
//            var devices = await bluetooth.ScanDevicesAsync();

//            foreach (var device in devices.Where(d => d.IsBleDevice))
//            {
//                Console.WriteLine($"  BLE设备: {device.Name} ({device.Address})");
//            }
//        }
//        finally
//        {
//            bluetooth.Dispose();
//        }
//    }

//    /// <summary>
//    /// 运行所有示例
//    /// </summary>
//    public static async Task RunAllExamplesAsync()
//    {
//        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
//        Console.WriteLine("║           蓝牙通讯帮助类 (BluetoothHelper) 示例            ║");
//        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
//        Console.WriteLine();

//        Console.WriteLine("可用示例：");
//        Console.WriteLine("  1. ScanDevicesAsync     - 扫描蓝牙设备");
//        Console.WriteLine("  2. ConnectDeviceAsync   - 连接蓝牙设备");
//        Console.WriteLine("  3. SendReceiveDataAsync - 蓝牙数据收发");
//        Console.WriteLine("  4. GetPairedDevicesAsync - 获取已配对设备");
//        Console.WriteLine("  5. ManualCreationAsync  - 手动创建实例");
//        Console.WriteLine("  6. BleDeviceAsync       - BLE低功耗蓝牙");
//        Console.WriteLine("  0. 退出");
//        Console.WriteLine();

//        while (true)
//        {
//            Console.Write("\n请选择示例 (0-6): ");
//            var input = Console.ReadLine();

//            Console.WriteLine();

//            try
//            {
//                switch (input)
//                {
//                    case "1":
//                        await ScanDevicesAsync();
//                        break;
//                    case "2":
//                        await ConnectDeviceAsync();
//                        break;
//                    case "3":
//                        await SendReceiveDataAsync();
//                        break;
//                    case "4":
//                        await GetPairedDevicesAsync();
//                        break;
//                    case "5":
//                        await ManualCreationAsync();
//                        break;
//                    case "6":
//                        await BleDeviceAsync();
//                        break;
//                    case "0":
//                        Console.WriteLine("退出示例程序");
//                        return;
//                    default:
//                        Console.WriteLine("无效的选择，请输入 0-6");
//                        break;
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"示例执行出错: {ex.Message}");
//            }
//        }
//    }
//}
