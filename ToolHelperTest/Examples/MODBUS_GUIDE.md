# Modbus 通信示例说明

## ?? 重要说明

Modbus TCP 和 Modbus RTU 示例需要根据实际的 API 签名进行调整。以下是基础使用模板和说明文档。

---

## Modbus TCP 基础使用

### 配置和连接

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Modbus;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());

// 添加 Modbus TCP 服务
services.AddModbusTcp(options =>
{
    options.Host = "192.168.1.100";          // PLC IP 地址
    options.Port = 502;                       // Modbus TCP 标准端口
    options.UnitId = 1;                       // 单元标识符
    options.ConnectTimeout = 5000;            // 连接超时（毫秒）
    options.ReadTimeout = 3000;               // 读取超时（毫秒）
});

var serviceProvider = services.BuildServiceProvider();
var modbusClient = serviceProvider.GetRequiredService<ModbusTcpHelper>();

try
{
    // 连接到设备
    if (await modbusClient.ConnectAsync())
    {
        Console.WriteLine("? 连接成功");
        
        // 使用 Modbus 功能...
        
    }
}
finally
{
    await modbusClient.DisconnectAsync();
    modbusClient.Dispose();
}
```

### Modbus TCP 功能码

| 功能码 | 名称 | 说明 | 方法示例 |
|-------|------|------|---------|
| 0x01 | Read Coils | 读线圈 | `ReadCoilsAsync()` |
| 0x02 | Read Discrete Inputs | 读离散输入 | `ReadDiscreteInputsAsync()` |
| 0x03 | Read Holding Registers | 读保持寄存器 | `ReadHoldingRegistersAsync()` |
| 0x04 | Read Input Registers | 读输入寄存器 | `ReadInputRegistersAsync()` |
| 0x05 | Write Single Coil | 写单个线圈 | `WriteSingleCoilAsync()` |
| 0x06 | Write Single Register | 写单个寄存器 | `WriteSingleRegisterAsync()` |
| 0x0F | Write Multiple Coils | 写多个线圈 | `WriteMultipleCoilsAsync()` |
| 0x10 | Write Multiple Registers | 写多个寄存器 | `WriteMultipleRegistersAsync()` |

### 使用场景

#### 1. 读取 PLC 数据

```csharp
// 读取保持寄存器（常用于读取 PLC 数据）
// 注意：请根据实际 API 调整参数
var registers = await modbusClient.ReadHoldingRegistersAsync(...);
Console.WriteLine($"寄存器值: {string.Join(", ", registers)}");
```

#### 2. 控制输出

```csharp
// 写入单个线圈（用于控制开关）
await modbusClient.WriteSingleCoilAsync(...);

// 写入单个寄存器（用于设置参数）
await modbusClient.WriteSingleRegisterAsync(...);
```

#### 3. 实时监控

```csharp
while (isMonitoring)
{
    var data = await modbusClient.ReadHoldingRegistersAsync(...);
    Console.WriteLine($"温度: {data[0] / 10.0}°C");
    Console.WriteLine($"压力: {data[1] / 100.0} MPa");
    await Task.Delay(1000);
}
```

---

## Modbus RTU 基础使用

### 配置和连接

```csharp
using System.IO.Ports;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Modbus;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());

// 添加 Modbus RTU 服务
services.AddModbusRtu(options =>
{
    options.PortName = "COM3";               // 串口名称
    options.BaudRate = 9600;                 // 波特率
    options.DataBits = 8;                    // 数据位
    options.StopBits = StopBits.One;         // 停止位
    options.Parity = Parity.None;            // 校验位（None 或 Even）
});

var serviceProvider = services.BuildServiceProvider();
var modbusClient = serviceProvider.GetRequiredService<ModbusRtuHelper>();

try
{
    // 打开串口
    if (await modbusClient.ConnectAsync())
    {
        Console.WriteLine("? 串口已打开");
        
        // 使用 Modbus 功能...
        
    }
}
finally
{
    await modbusClient.DisconnectAsync();
    modbusClient.Dispose();
}
```

### RS-485 接线

```
主站                从站1              从站2
━━━━━              ━━━━━             ━━━━━
A+ ───────────── A+ ─────────── A+
B- ───────────── B- ─────────── B-
GND ──────────── GND ─────────── GND

注意事项:
? 总线两端需要 120Ω 终端电阻
? A+ 和 B- 不要接反
? 最长距离: 1200米 (9600bps)
? 最多从站: 247 个
? 从站地址: 1-247 (0 为广播)
```

### 常见波特率配置

| 波特率 | 最大距离 | 应用场景 |
|--------|---------|---------|
| 9600 | 1200m | 常用，稳定 |
| 19200 | 1000m | 中速 |
| 38400 | 500m | 高速 |
| 115200 | 200m | 超高速，短距离 |

### 使用场景

#### 1. 多从站轮询

```csharp
var slaves = new byte[] { 1, 2, 3 };

foreach (var slaveId in slaves)
{
    try
    {
        // 读取每个从站的数据
        // 注意：根据实际 API 调整参数
        var data = await modbusClient.ReadHoldingRegistersAsync(...);
        Console.WriteLine($"从站 {slaveId}: {string.Join(", ", data)}");
        
        // 从站之间需要延时
        await Task.Delay(200);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"从站 {slaveId} 错误: {ex.Message}");
    }
}
```

#### 2. CRC 校验

Modbus RTU 使用 CRC-16 校验码确保数据完整性：

```
数据帧格式:
┌──────────┬───────────┬────────┬──────────┬─────────┐
│ 从站地址 │ 功能码   │ 数据   │ CRC 低位 │ CRC 高位 │
│  1 字节  │  1 字节  │ N 字节 │  1 字节  │  1 字节  │
└──────────┴───────────┴────────┴──────────┴─────────┘

? ToolHelper 库自动计算和验证 CRC
? 校验失败的帧会被丢弃
? 无需手动处理 CRC
```

---

## ?? 故障排查

### Modbus TCP 常见问题

| 问题 | 可能原因 | 解决方案 |
|------|----------|----------|
| 连接超时 | IP/端口错误 | 检查网络连接，ping 测试 |
| 读取失败 | 寄存器地址错误 | 查看设备文档，确认地址 |
| 响应慢 | 网络延迟 | 增加超时时间 |
| 功能码错误 | 不支持的操作 | 查看设备支持的功能码 |

### Modbus RTU 常见问题

| 问题 | 可能原因 | 解决方案 |
|------|----------|----------|
| 串口打开失败 | 端口被占用 | 检查端口，关闭其他程序 |
| 通信超时 | 波特率不匹配 | 尝试不同波特率 |
| CRC 错误 | 接线问题 | 检查 A+/B- 接线 |
| 无响应 | 从站地址错误 | 确认从站地址设置 |
| 数据错乱 | 缺少终端电阻 | 添加 120Ω 电阻 |

### 诊断步骤

1. **检查物理连接**
   - TCP: 网线/交换机
   - RTU: 串口线/RS-485 转换器

2. **验证通信参数**
   - TCP: IP 地址、端口
   - RTU: 波特率、数据位、停止位、校验位

3. **测试基本通信**
   - 尝试读取单个寄存器
   - 使用已知地址和数据

4. **使用调试工具**
   - Modbus Poll/Slave (Windows)
   - 串口调试助手
   - Wireshark (TCP)

---

## ?? 参考资源

### 官方文档

- Modbus 协议规范: [modbus.org](https://modbus.org)
- Modbus TCP: RFC
 Modbus RTU: Serial Protocol

### 推荐工具

- **Modbus Poll** - Windows Modbus 主站模拟器
- **Modbus Slave** - Windows Modbus 从站模拟器
- **QModMaster** - 跨平台 Modbus 主站工具
- **Serial Port Monitor** - 串口监视工具

### 学习资料

1. Modbus 协议基础
2. PLC 编程入门
3. 工业网络通信
4. RS-485 总线原理

---

## ?? 最佳实践

### 1. 错误处理

```csharp
try
{
    var data = await modbusClient.ReadHoldingRegistersAsync(...);
}
catch (TimeoutException)
{
    // 超时 - 可能需要重试
}
catch (InvalidOperationException)
{
    // 操作无效 - 检查连接状态
}
catch (Exception ex)
{
    // 其他错误 - 记录日志
    logger.LogError(ex, "Modbus 通信错误");
}
```

### 2. 资源管理

```csharp
// 使用 using 或 try-finally
try
{
    var client = serviceProvider.GetRequiredService<ModbusTcpHelper>();
    await client.ConnectAsync();
    // 使用客户端...
}
finally
{
    await client.DisconnectAsync();
    client.Dispose();
}
```

### 3. 超时设置

```csharp
services.AddModbusTcp(options =>
{
    options.ConnectTimeout = 5000;    // 连接超时
    options.ReadTimeout = 3000;       // 读取超时
    options.WriteTimeout = 3000;      // 写入超时
});
```

### 4. 重试机制

```csharp
int maxRetries = 3;
for (int i = 0; i < maxRetries; i++)
{
    try
    {
        var data = await modbusClient.ReadHoldingRegistersAsync(...);
        return data; // 成功
    }
    catch (TimeoutException)
    {
        if (i == maxRetries - 1) throw;
        await Task.Delay(1000);
    }
}
```

---

## ?? 学习路径

### 初级（1-2 天）
1. 了解 Modbus 协议基础
2. 配置 Modbus TCP 连接
3. 读取简单的寄存器

### 中级（3-5 天）
1. 掌握所有功能码
2. 实现数据监控
3. 处理通信错误

### 高级（6-10 天）
1. 多设备轮询
2. 复杂数据解析
3. 性能优化

---

## ?? 获取帮助

- **API 文档:** 查看 ModbusTcpHelper 和 ModbusRtuHelper 的 XML 注释
- **问题反馈:** GitHub Issues
- **技术讨论:** GitHub Discussions

---

**最后更新:** 2024-12-27  
**版本:** 1.0

?? **提示:** 实际使用前请参考 ToolHelper.Communication 库的具体 API 文档，确认方法签名和参数。
