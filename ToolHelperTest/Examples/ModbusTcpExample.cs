using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Modbus;

namespace ToolHelperTest.Examples;

/// <summary>
/// Modbus TCP 使用示例
/// 演示如何使用 ModbusTcpHelper 进行工业设备通信
/// </summary>
public class ModbusTcpExample
{
    /// <summary>
    /// 示例 1: 基础连接和读取保持寄存器
    /// 演示连接 Modbus TCP 设备并读取数据
    /// </summary>
    public static async Task BasicConnectionAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";      // PLC/设备 IP 地址
            options.Port = 502;                   // Modbus TCP 标准端口
            options.UnitId = 1;                   // 单元标识符
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== Modbus TCP 基础连接示例 ===\n");
            // 连接到 Modbus 设备
            Console.WriteLine("正在连接到 Modbus TCP 设备...");
            if (await modbusClient.ConnectAsync())
            {
                Console.WriteLine("? 连接成功\n");
                // 读取保持寄存器 (功能码 0x03)
                Console.WriteLine("读取保持寄存器 (地址: 0, 数量: 10)");
                var registers = await modbusClient.ReadHoldingRegistersAsync(0, 10);
                Console.WriteLine("读取结果:");
                for (int i = 0; i < registers.Length; i++)
                {
                    Console.WriteLine($"  寄存器 {i}: {registers[i]} (0x{registers[i]:X4})");
                }
            }
            else
            {
                Console.WriteLine("? 连接失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
            Console.WriteLine("\n连接已关闭");
        }
    }

    /// <summary>
    /// 示例 2: 读取线圈状态
    /// 演示读取线圈 (Coils) 的开关状态
    /// </summary>
    public static async Task ReadCoilsAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 502;
            options.UnitId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== Modbus TCP 读取线圈示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                // 读取线圈状态 (功能码 0x01)
                Console.WriteLine("读取线圈状态 (地址: 0, 数量: 8)");
                var coils = await modbusClient.ReadCoilsAsync(0, 8);

                Console.WriteLine("线圈状态:");
                for (int i = 0; i < coils.Length; i++)
                {
                    var status = coils[i] ? "ON (1)" : "OFF(0)";
                    Console.WriteLine($"  线圈 {i}: {status}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 3: 读取输入寄存器
    /// 演示读取输入寄存器（传感器数据）
    /// </summary>
    public static async Task ReadInputRegistersAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 502;
            options.UnitId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== Modbus TCP 读取输入寄存器示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                // 读取输入寄存器 (功能码 0x04)
                Console.WriteLine("读取输入寄存器 (地址: 0, 数量: 4)");
                var inputs = await modbusClient.ReadInputRegistersAsync(0, 4);

                Console.WriteLine("输入寄存器值:");
                for (int i = 0; i < inputs.Length; i++)
                {
                    // 假设这些是温度传感器值 (需要根据实际设备转换)
                    var temperature = inputs[i] / 10.0;  // 示例：除以10得到实际温度
                    Console.WriteLine($"  传感器 {i}: {inputs[i]} (原始) = {temperature:F1}°C");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 4: 写单个寄存器
    /// 演示向保持寄存器写入单个值
    /// </summary>
    public static async Task WriteSingleRegisterAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 502;
            options.UnitId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== Modbus TCP 写单个寄存器示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                ushort address = 100;
                ushort value = 1234;

                // 写单个保持寄存器 (功能码 0x06)
                Console.WriteLine($"写入寄存器 {address}: {value}");
                await modbusClient.WriteSingleRegisterAsync(address, value);
                Console.WriteLine("? 写入成功");

                // 读回验证
                var readBack = await modbusClient.ReadHoldingRegistersAsync(address, 1);
                Console.WriteLine($"读回验证: {readBack[0]}");

                if (readBack[0] == value)
                {
                    Console.WriteLine("? 验证成功");
                }
                else
                {
                    Console.WriteLine($"? 验证失败: 期望 {value}, 实际 {readBack[0]}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 5: 写多个寄存器
    /// 演示批量写入保持寄存器
    /// </summary>
    public static async Task WriteMultipleRegistersAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 502;
            options.UnitId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== Modbus TCP 写多个寄存器示例 ===\n");
            if (await modbusClient.ConnectAsync())
            {
                ushort startAddress = 100;
                ushort[] values = { 100, 200, 300, 400, 500 };
                // 写多个保持寄存器 (功能码 0x10)
                Console.WriteLine($"写入 {values.Length} 个寄存器 (起始地址: {startAddress})");
                Console.WriteLine($"值: [{string.Join(", ", values)}]");
                await modbusClient.WriteMultipleRegistersAsync(startAddress, values);
                Console.WriteLine("? 写入成功");
                // 读回验证
                var readBack = await modbusClient.ReadHoldingRegistersAsync(startAddress, (ushort)values.Length);
                Console.WriteLine($"\n读回验证:");
                bool allMatch = true;
                for (int i = 0; i < values.Length; i++)
                {
                    var match = readBack[i] == values[i] ? "?" : "?";
                    Console.WriteLine($"  寄存器 {startAddress + i}: {readBack[i]} {match}");
                    if (readBack[i] != values[i]) allMatch = false;
                }

                Console.WriteLine(allMatch ? "\n? 全部验证成功" : "\n? 部分验证失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 6: 写单个线圈
    /// 演示控制单个线圈的开关
    /// </summary>
    public static async Task WriteSingleCoilAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 502;
            options.UnitId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== Modbus TCP 写单个线圈示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                ushort coilAddress = 0;

                // 打开线圈 (功能码 0x05)
                Console.WriteLine($"打开线圈 {coilAddress}");
                await modbusClient.WriteSingleCoilAsync(coilAddress, true);
                await Task.Delay(1000);

                // 读回验证
                var coils = await modbusClient.ReadCoilsAsync(coilAddress, 1);
                Console.WriteLine($"当前状态: {(coils[0] ? "ON" : "OFF")}");

                await Task.Delay(2000);

                // 关闭线圈
                Console.WriteLine($"\n关闭线圈 {coilAddress}");
                await modbusClient.WriteSingleCoilAsync(coilAddress, false);
                await Task.Delay(1000);

                // 再次读回验证
                coils = await modbusClient.ReadCoilsAsync(coilAddress, 1);
                Console.WriteLine($"当前状态: {(coils[0] ? "ON" : "OFF")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 7: PLC 数据采集与监控
    /// 演示实际生产环境的数据采集场景
    /// </summary>
    public static async Task PlcDataMonitoringAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddModbusTcp(options =>
        {
            options.Host = "127.0.0.1";
            options.Port = 502;
            options.UnitId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

        try
        {
            Console.WriteLine("=== PLC 数据采集与监控示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                Console.WriteLine("开始监控 PLC 数据...");
                Console.WriteLine("按任意键停止\n");

                var cts = new CancellationTokenSource();
                var monitorTask = Task.Run(async () =>
                {
                    int cycleCount = 0;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            cycleCount++;

                            // 读取生产数据 (假设地址布局)
                            var productionData = await modbusClient.ReadHoldingRegistersAsync(0, 10);
                            var statusCoils = await modbusClient.ReadCoilsAsync(0, 8);

                            // 解析数据 (根据实际设备调整)
                            var temperature = productionData[0] / 10.0;
                            var pressure = productionData[1] / 100.0;
                            var speed = productionData[2];
                            var counter = productionData[3];

                            // 显示数据
                            Console.Clear();
                            Console.WriteLine($"=== PLC 监控 - 周期 #{cycleCount} [{DateTime.Now:HH:mm:ss}] ===\n");
                            Console.WriteLine("?? 生产参数:");
                            Console.WriteLine($"  温度: {temperature:F1}°C");
                            Console.WriteLine($"  压力: {pressure:F2} MPa");
                            Console.WriteLine($"  速度: {speed} RPM");
                            Console.WriteLine($"  计数: {counter}");
                            
                            Console.WriteLine("\n?? 设备状态:");
                            Console.WriteLine($"  电机运行: {(statusCoils[0] ? "运行" : "停止")}");
                            Console.WriteLine($"  加热器:   {(statusCoils[1] ? "开启" : "关闭")}");
                            Console.WriteLine($"  报警:     {(statusCoils[2] ? "有报警" : "正常")}");
                            Console.WriteLine($"  自动模式: {(statusCoils[3] ? "自动" : "手动")}");

                            Console.WriteLine("\n按任意键停止监控...");

                            await Task.Delay(2000, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"? 采集错误: {ex.Message}");
                            await Task.Delay(5000, cts.Token);
                        }
                    }
                }, cts.Token);

                Console.ReadKey();
                cts.Cancel();

                try { await monitorTask; } catch (OperationCanceledException) { }

                Console.WriteLine("\n\n监控已停止");
            }
        }
        finally
        {
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 8: 多设备轮询
    /// 演示同时管理多个 Modbus 设备
    /// </summary>
    public static async Task MultiDevicePollingAsync()
    {
        Console.WriteLine("=== 多设备轮询示例 ===\n");

        var devices = new[]
        {
            (UnitId: (byte)1, Name: "设备1 (PLC)"),
            (UnitId: (byte)2, Name: "设备2 (变频器)"),
            (UnitId: (byte)3, Name: "设备3 (温控器)")
        };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var clients = new List<ModbusTcpHelper>();

        try
        {
            // 为每个设备创建客户端
            foreach (var device in devices)
            {
                var deviceServices = new ServiceCollection();
                deviceServices.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
                deviceServices.AddModbusTcp(options =>
                {
                    options.Host = "127.0.0.1";
                    options.Port = 502;
                    options.UnitId = device.UnitId;
                });

                var serviceProvider = deviceServices.BuildServiceProvider();
                var client = serviceProvider.GetRequiredService<ModbusTcpHelper>();
                clients.Add(client);
            }
            // 轮询所有设备
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                Console.WriteLine($"第 {cycle} 轮轮询:\n");
                for (int i = 0; i < devices.Length; i++)
                {
                    try
                    {
                        var client = clients[i];
                        var device = devices[i];
                        if (await client.ConnectAsync())
                        {
                            var registers = await client.ReadHoldingRegistersAsync(0, 2);
                            Console.WriteLine($"  ? {device.Name}: 寄存器[0]={registers[0]}, 寄存器[1]={registers[1]}");
                            await client.DisconnectAsync();
                        }
                        else
                        {
                            Console.WriteLine($"  ? {device.Name}: 连接失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ? {devices[i].Name}: {ex.Message}");
                    }
                    await Task.Delay(500);
                }
                Console.WriteLine();
                await Task.Delay(2000);
            }
            Console.WriteLine("轮询完成");
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }
}
