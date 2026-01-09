using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Modbus;

namespace ToolHelperTest.Examples;

/// <summary>
/// Modbus RTU 使用示例
/// 演示如何使用 ModbusRtuHelper 通过串口进行工业设备通信
/// </summary>
public class ModbusRtuExample
{
    /// <summary>
    /// 示例 1: 基础连接和读取
    /// 演示通过串口连接 Modbus RTU 设备并读取数据
    /// </summary>
    public static async Task BasicConnectionAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        services.AddModbusRtu(options =>
        {
            options.PortName = "COM10";           // 串口名称
            options.BaudRate = 9600;             // 波特率
            options.DataBits = 8;                // 数据位
            options.StopBits = StopBits.One;     // 停止位
            options.Parity = Parity.None;        // 校验位
            options.SlaveId = 1;                 // 从站地址
        });


        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

        try
        {
            Console.WriteLine("=== Modbus RTU 基础连接示例 ===\n");

            // 连接到 Modbus RTU 设备
            Console.WriteLine($"正在连接到 Modbus RTU 设备 (COM3, 9600, 8N1)...");

            // 使用 try-catch 包裹连接过程，以捕获可能的异常
            bool isConnected = false;
            try 
            {
                isConnected = await modbusClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? 连接异常: {ex.Message}");
            }

            if (isConnected)
            {
                Console.WriteLine("? 串口已打开\n");

                try
                {
                    // 读取保持寄存器 (功能码 0x03)
                    Console.WriteLine("读取保持寄存器 (地址: 0, 数量: 10)");
                    // var registers = await modbusClient.ReadHoldingRegistersAsync(0, 10);
                    var registers = await modbusClient.ReadCoilsAsync(0, 10);
                    Console.WriteLine("读取结果:");
                    for (int i = 0; i < registers.Length; i++)
                    {
                        Console.WriteLine($"  寄存器 {i}: {registers[i]} (0x{registers[i]:X4})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? 读取数据失败: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("? 串口打开失败");
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
            Console.WriteLine("\n串口已关闭");
        }
    }

    /// <summary>
    /// 示例 2: 读取操作 - 所有功能码
    /// 演示 Modbus RTU 的所有读取功能
    /// </summary>
    public static async Task ReadOperationsExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusRtu(options =>
        {
            options.PortName = "COM8";
            options.BaudRate = 9600;
            options.SlaveId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

        try
        {
            Console.WriteLine("=== Modbus RTU 读取操作示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                // 1. 读取线圈 (功能码 0x01)
                Console.WriteLine("1. 读取线圈状态 (0x01)");
                var coils = await modbusClient.ReadCoilsAsync(0, 8);
                Console.WriteLine($"   线圈 0-7: {string.Join(", ", coils.Select(c => c ? "1" : "0"))}");

                await Task.Delay(500);

                // 2. 读取离散输入 (功能码 0x02)
                Console.WriteLine("\n2. 读取离散输入 (0x02)");
                var discreteInputs = await modbusClient.ReadDiscreteInputsAsync(0, 8);
                Console.WriteLine($"   输入 0-7: {string.Join(", ", discreteInputs.Select(d => d ? "1" : "0"))}");

                await Task.Delay(500);

                // 3. 读取保持寄存器 (功能码 0x03)
                Console.WriteLine("\n3. 读取保持寄存器 (0x03)");
                var holdingRegs = await modbusClient.ReadHoldingRegistersAsync(0, 8);
                Console.WriteLine($"   寄存器 0-4: {string.Join(", ", holdingRegs)}");

                await Task.Delay(500);

                // 4. 读取输入寄存器 (功能码 0x04)
                Console.WriteLine("\n4. 读取输入寄存器 (0x04)");
                var inputRegs = await modbusClient.ReadInputRegistersAsync(0, 8);
                Console.WriteLine($"   输入寄存器 0-4: {string.Join(", ", inputRegs)}");

                Console.WriteLine("\n? 所有读取操作完成");
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
    /// 示例 3: 写入操作
    /// 演示 Modbus RTU 的写入功能
    /// </summary>
    public static async Task WriteOperationsExampleAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusRtu(options =>
        {
            options.PortName = "COM8";
            options.BaudRate = 9600;
            options.SlaveId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

        try
        {
            Console.WriteLine("=== Modbus RTU 写入操作示例 ===\n");

            if (await modbusClient.ConnectAsync())
            {
                // 1. 写单个线圈 (功能码 0x05)
                Console.WriteLine("1. 写单个线圈 (0x05)");
                await modbusClient.WriteSingleCoilAsync(0, true);
                Console.WriteLine("   ? 线圈 0 设置为 ON");

                await Task.Delay(1000);

                await modbusClient.WriteSingleCoilAsync(0, false);
                Console.WriteLine("   ? 线圈 0 设置为 OFF");

                await Task.Delay(500);

                // 2. 写单个寄存器 (功能码 0x06)
                Console.WriteLine("\n2. 写单个寄存器 (0x06)");
                await modbusClient.WriteSingleRegisterAsync(100, 1234);
                Console.WriteLine("   ? 寄存器 100 = 1234");

                // 读回验证
                var verify = await modbusClient.ReadHoldingRegistersAsync(100, 1);
                Console.WriteLine($"   验证读回: {verify[0]}");

                await Task.Delay(500);

                // 3. 写多个线圈 (功能码 0x0F)
                Console.WriteLine("\n3. 写多个线圈 (0x0F)");
                var coilValues = new bool[] { true, false, true, true, false, false, true, false };
                await modbusClient.WriteMultipleCoilsAsync(0, coilValues);
                Console.WriteLine($"   ? 写入线圈 0-7: {string.Join(", ", coilValues.Select(c => c ? "1" : "0"))}");

                await Task.Delay(500);

                // 4. 写多个寄存器 (功能码 0x10)
                Console.WriteLine("\n4. 写多个寄存器 (0x10)");
                ushort[] registerValues = { 100, 200, 300, 400, 500 };
                await modbusClient.WriteMultipleRegistersAsync(100, registerValues);
                Console.WriteLine($"   ? 写入寄存器 100-104: {string.Join(", ", registerValues)}");

                // 读回验证
                var verifyMultiple = await modbusClient.ReadHoldingRegistersAsync(100, 5);
                Console.WriteLine($"   验证读回: {string.Join(", ", verifyMultiple)}");

                Console.WriteLine("\n? 所有写入操作完成");
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
    /// 示例 4: CRC 校验演示
    /// 演示 Modbus RTU 的 CRC-16 校验机制
    /// </summary>
    public static async Task CrcValidationExampleAsync()
    {
        Console.WriteLine("=== Modbus RTU CRC 校验演示 ===\n");

        // 演示 CRC 计算
        Console.WriteLine("Modbus RTU 使用 CRC-16 校验码确保数据完整性\n");

        // 示例：读保持寄存器命令
        var command = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
        Console.WriteLine($"原始命令: {BitConverter.ToString(command)}");
        Console.WriteLine("  从站地址: 0x01");
        Console.WriteLine("  功能码:   0x03 (读保持寄存器)");
        Console.WriteLine("  起始地址: 0x0000");
        Console.WriteLine("  寄存器数: 0x000A (10个)");

        // 计算 CRC (实际实现中由库自动完成)
        var crc = CalculateCrc16(command);
        Console.WriteLine($"\n计算的 CRC: 0x{crc:X4}");
        Console.WriteLine($"  低字节: 0x{(crc & 0xFF):X2}");
        Console.WriteLine($"  高字节: 0x{(crc >> 8):X2}");

        // 完整的帧
        var frame = command.Concat(new byte[] { (byte)(crc & 0xFF), (byte)(crc >> 8) }).ToArray();
        Console.WriteLine($"\n完整帧: {BitConverter.ToString(frame)}");

        Console.WriteLine("\n说明:");
        Console.WriteLine("  ? Modbus RTU 每个帧都包含 CRC 校验");
        Console.WriteLine("  ? 接收方会重新计算 CRC 并验证");
        Console.WriteLine("  ? 校验失败的帧会被丢弃");
        Console.WriteLine("  ? ToolHelper 库自动处理 CRC 计算和验证");

        await Task.CompletedTask;
    }

    /// <summary>
    /// 计算 CRC-16 (Modbus)
    /// </summary>
    private static ushort CalculateCrc16(byte[] data)
    {
        ushort crc = 0xFFFF;

        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        return crc;
    }

    /// <summary>
    /// 示例 5: 多从站轮询
    /// 演示在同一串口上轮询多个 Modbus RTU 从站
    /// 注意：使用单个客户端实例，通过切换从站地址来轮询不同从站
    /// </summary>
    public static async Task MultiSlavePollingAsync()
    {
        Console.WriteLine("=== 多从站轮询示例 ===\n");

        var slaves = new[]
        {
            (Id: (byte)1, Name: "温度控制器"),
            (Id: (byte)2, Name: "压力传感器"),
            (Id: (byte)3, Name: "流量计")
        };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddModbusRtu(options =>
        {
            options.PortName = "COM8";
            options.BaudRate = 9600;
            options.SlaveId = 1;  // 初始从站地址
            options.MaxRetries = 1;  // 减少重试次数，加快轮询速度
            options.ReadTimeout = 500;  // 缩短超时时间
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

        try
        {
            // 只打开一次串口
            if (!await modbusClient.ConnectAsync())
            {
                Console.WriteLine("串口打开失败");
                return;
            }

            Console.WriteLine("串口已打开，开始轮询从站...\n");

            for (int cycle = 1; cycle <= 3; cycle++)
            {
                Console.WriteLine($"第 {cycle} 轮:\n");

                foreach (var slave in slaves)
                {
                    try
                    {
                        // 使用带从站地址的重载方法，或者先设置 SlaveId 再读取
                        var registers = await modbusClient.ReadHoldingRegistersAsync(slave.Id, 0, 2);
                        Console.WriteLine($"  从站 {slave.Id} ({slave.Name}):");
                        Console.WriteLine($"    寄存器 0: {registers[0]}");
                        Console.WriteLine($"    寄存器 1: {registers[1]}");
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine($"  从站 {slave.Id} ({slave.Name}): 无响应 (可能离线)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  从站 {slave.Id} ({slave.Name}): 错误 - {ex.Message}");
                    }

                    // 从站之间的延时
                    await Task.Delay(100);
                }

                Console.WriteLine();
                await Task.Delay(1000);
            }

            Console.WriteLine("轮询完成");
        }
        finally
        {
            // 只关闭一次串口
            await modbusClient.DisconnectAsync();
            modbusClient.Dispose();
        }
    }

    /// <summary>
    /// 示例 6: 传感器数据采集与监控
    /// 演示实际应用中的连续数据采集
    /// </summary>
    public static async Task SensorMonitoringAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddModbusRtu(options =>
        {
            options.PortName = "COM3";
            options.BaudRate = 9600;
            options.SlaveId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

        try
        {
            Console.WriteLine("=== 传感器数据采集与监控示例 ===\n");

            bool isConnected = false;
            try
            {
                isConnected = await modbusClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? 连接异常: {ex.Message}");
            }

            if (isConnected)
            {
                Console.WriteLine("开始监控传感器数据...");
                Console.WriteLine("按任意键停止\n");

                var cts = new CancellationTokenSource();
                var monitorTask = Task.Run(async () =>
                {
                    int sampleCount = 0;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            sampleCount++;
                            // 读取传感器数据
                            var sensorData = await modbusClient.ReadInputRegistersAsync(0, 4);
                            // 解析数据 (根据实际设备调整)
                            var temperature = sensorData[0] / 10.0;
                            var humidity = sensorData[1] / 10.0;
                            var pressure = sensorData[2] / 100.0;
                            var voltage = sensorData[3] / 1000.0;
                            // 显示数据
                            Console.Clear();
                            Console.WriteLine($"=== 传感器监控 - 采样 #{sampleCount} [{DateTime.Now:HH:mm:ss}] ===\n");
                            Console.WriteLine("?? 传感器读数:");
                            Console.WriteLine($"  ???  温度:   {temperature:F1}°C");
                            Console.WriteLine($"  ?? 湿度:   {humidity:F1}%");
                            Console.WriteLine($"  ?? 压力:   {pressure:F2} kPa");
                            Console.WriteLine($"  ? 电压:   {voltage:F3} V");

                            // 简单的异常检测
                            Console.WriteLine("\n?? 状态检查:");
                            Console.WriteLine($"  温度: {(temperature > 80 ? "?? 过高" : temperature < 0 ? "?? 过低" : "? 正常")}");
                            Console.WriteLine($"  湿度: {(humidity > 90 ? "?? 过高" : humidity < 10 ? "?? 过低" : "? 正常")}");
                            Console.WriteLine($"  压力: {(pressure > 150 ? "?? 过高" : pressure < 50 ? "?? 过低" : "? 正常")}");

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
    /// 示例 7: 故障诊断
    /// 演示 Modbus RTU 常见故障的诊断方法
    /// </summary>
    public static async Task DiagnosticsExampleAsync()
    {
        Console.WriteLine("=== Modbus RTU 故障诊断示例 ===\n");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddModbusRtu(options =>
        {
            options.PortName = "COM3";
            options.BaudRate = 9600;
            options.SlaveId = 1;
        });

        var serviceProvider = services.BuildServiceProvider();
        var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

        Console.WriteLine("1. 检查串口连接");
        try
        {
            if (await modbusClient.ConnectAsync())
            {
                Console.WriteLine("   ? 串口打开成功");
                await modbusClient.DisconnectAsync();
            }
            else
            {
                Console.WriteLine("   ? 串口打开失败");
                Console.WriteLine("   原因可能:");
                Console.WriteLine("     - 串口不存在或被占用");
                Console.WriteLine("     - 权限不足");
                Console.WriteLine("     - 串口驱动问题");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ? 异常: {ex.Message}");
        }

        Console.WriteLine("\n2. 检查通信参数");
        Console.WriteLine("   常见波特率: 9600, 19200, 38400, 115200");
        Console.WriteLine("   常见格式: 8N1 (8数据位, 无校验, 1停止位)");
        Console.WriteLine("   或: 8E1 (8数据位, 偶校验, 1停止位)");

        Console.WriteLine("\n3. 检查从站地址");
        Console.WriteLine("   从站地址范围: 1-247");
        Console.WriteLine("   地址 0 用于广播 (不推荐)");
        Console.WriteLine("   地址 248-255 保留");

        Console.WriteLine("\n4. 常见错误码");
        Console.WriteLine("   0x01: 非法功能码");
        Console.WriteLine("   0x02: 非法数据地址");
        Console.WriteLine("   0x03: 非法数据值");
        Console.WriteLine("   0x04: 从站设备故障");
        Console.WriteLine("   0x06: 从站设备忙");

        Console.WriteLine("\n5. 排查建议");
        Console.WriteLine("   ? 确认接线正确 (A+, B-)");
        Console.WriteLine("   ? 检查终端电阻 (120Ω)");
        Console.WriteLine("   ? 验证通信参数匹配");
        Console.WriteLine("   ? 测试不同波特率");
        Console.WriteLine("   ? 使用串口监听工具查看原始数据");
        Console.WriteLine("   ? 检查设备地址设置");
        Console.WriteLine("   ? 确认寄存器地址正确");

        modbusClient.Dispose();
        await Task.CompletedTask;
    }
}
