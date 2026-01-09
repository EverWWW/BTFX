# ToolHelper.Communication 使用示例

完整的通信库使用示例集合，涵盖 TCP、UDP、HTTP、WebSocket、串口和 Modbus 通信。

## ?? 示例目录

| 示例文件 | 示例数 | 说明 | 难度 |
|---------|--------|------|------|
| [TcpClientExample.cs](#tcp-客户端示例) | 6 | TCP 客户端通信 | ?? |
| [TcpServerExample.cs](#tcp-服务器示例) | 5 | TCP 服务器开发 | ??? |
| [UdpExample.cs](#udp-示例) | 6 | UDP 通信 | ?? |
| [HttpExample.cs](#http-示例) | 8 | HTTP 客户端 | ? |
| [WebSocketExample.cs](#websocket-示例) | 5 | WebSocket 实时通信 | ?? |
| [SerialPortExample.cs](#串口示例) | 5 | 串口设备通信 | ?? |
| [ModbusTcpExample.cs](#modbus-tcp-示例) | 8 | Modbus TCP 工业通信 | ??? |
| [ModbusRtuExample.cs](#modbus-rtu-示例) | 7 | Modbus RTU 串口通信 | ??? |

**总计:** 8 个文件 ? 50 个示例 ? 覆盖 8 种通信协议

---

## ?? 快速开始

### 运行示例

```csharp
using ToolHelperTest.Examples;

// TCP 客户端
await TcpClientExample.BasicConnectAndSendAsync();

// HTTP 请求
await HttpExample.BasicGetRequestAsync();

// Modbus 通信
await ModbusTcpExample.BasicConnectionAsync();
```

### 前置要求

- .NET 10.0 或更高版本
- Visual Studio 2022 或 JetBrains Rider
- 对于串口/Modbus RTU 示例：需要实际硬件或虚拟串口

---

## ?? 详细示例说明

### TCP 客户端示例

**文件:** `TcpClientExample.cs`  
**协议:** TCP/IP  
**难度:** ?? 中级

#### 示例列表

1. **BasicConnectAndSendAsync** - 基础连接和数据发送
   ```csharp
   await TcpClientExample.BasicConnectAndSendAsync();
   ```
   学习要点：
   - TCP 客户端初始化
   - 连接到服务器
   - 发送和接收数据
   - 事件订阅

2. **AutoReconnectExampleAsync** - 自动重连机制
   ```csharp
   await TcpClientExample.AutoReconnectExampleAsync();
   ```
   学习要点：
   - 配置自动重连
   - 心跳保活
   - 断线检测
   - 重连策略

3. **ProtocolCommunicationAsync** - 协议通信
   学习要点：
   - 自定义协议设计
   - 请求-响应模式
   - 数据包封装

4. **FileTransferExampleAsync** - 文件传输
   学习要点：
   - 大数据传输
   - 分块发送
   - 进度显示

5. **MultipleConnectionsExampleAsync** - 并发连接
   学习要点：
   - 多服务器管理
   - 并发编程
   - 资源管理

6. **StickyPacketHandlingAsync** - 粘包处理
   学习要点：
   - TCP 粘包问题
   - 包头包尾设计
   - 数据包解析

#### 配置说明

```csharp
services.AddTcpClient(options =>
{
    options.Host = "127.0.0.1";              // 服务器地址
    options.Port = 8080;                      // 服务器端口
    options.EnableAutoReconnect = true;       // 启用自动重连
    options.ReconnectInterval = 5000;         // 重连间隔（毫秒）
    options.MaxReconnectAttempts = 10;        // 最大重连次数
    options.EnableHeartbeat = true;           // 启用心跳
    options.HeartbeatInterval = 30000;        // 心跳间隔（毫秒）
});
```

#### 应用场景

- 客户端/服务器应用
- 远程控制系统
- 数据采集客户端
- 实时消息接收

---

### TCP 服务器示例

**文件:** `TcpServerExample.cs`  
**协议:** TCP/IP  
**难度:** ??? 高级

#### 示例列表

1. **BasicEchoServerAsync** - 基础回显服务器
2. **ChatRoomServerAsync** - 聊天室服务器
3. **DataCollectionServerAsync** - 数据采集服务器
4. **ProtocolServerAsync** - 协议服务器
5. **BroadcastServerAsync** - 广播服务器

#### 应用场景

- 网络服务开发
- 聊天服务器
- 数据采集网关
- 协议代理服务器

---

### UDP 示例

**文件:** `UdpExample.cs`  
**协议:** UDP  
**难度:** ?? 中级

#### 示例列表

1. **UnicastExampleAsync** - 单播通信
2. **BroadcastExampleAsync** - 广播通信
3. **MulticastExampleAsync** - 组播通信
4. **ServiceDiscoveryExampleAsync** - 服务发现
5. **RealTimeDataStreamAsync** - 实时数据流
6. **NatPunchThroughExampleAsync** - NAT 穿透

#### 应用场景

- 设备发现
- 实时视频/音频传输
- 局域网广播
- P2P 应用

---

### HTTP 示例

**文件:** `HttpExample.cs`  
**协议:** HTTP/HTTPS  
**难度:** ? 入门

#### 示例列表

1. **BasicGetRequestAsync** - GET 请求
2. **PostJsonRequestAsync** - POST JSON 数据
3. **GetAndDeserializeJsonAsync** - JSON 反序列化
4. **FileDownloadAsync** - 文件下载
5. **FileUploadAsync** - 文件上传
6. **RestfulApiCrudAsync** - RESTful CRUD
7. **ConcurrentRequestsAsync** - 并发请求
8. **RealWorldApiExampleAsync** - GitHub API 实战

#### 应用场景

- API 集成
- Web 服务调用
- 文件上传下载
- 数据采集

---

### WebSocket 示例

**文件:** `WebSocketExample.cs`  
**协议:** WebSocket  
**难度:** ?? 中级

#### 示例列表

1. **BasicWebSocketAsync** - 基础连接
2. **AutoReconnectExampleAsync** - 自动重连
3. **JsonMessageExampleAsync** - JSON 消息
4. **BinaryDataExampleAsync** - 二进制数据
5. **ChatClientExampleAsync** - 聊天客户端

#### 应用场景

- 实时消息推送
- 在线聊天
- 实时数据监控
- 游戏通信

---

### 串口示例

**文件:** `SerialPortExample.cs`  
**协议:** Serial Port (RS-232/RS-485)  
**难度:** ?? 中级

#### 示例列表

1. **BasicSerialPortAsync** - 基础串口通信
   ```csharp
   await SerialPortExample.BasicSerialPortAsync();
   ```

2. **AutoDetectSerialPortAsync** - 串口自动识别
3. **AtCommandExampleAsync** - AT 命令通信
4. **ProtocolCommunicationAsync** - 协议通信
5. **SensorDataCollectionAsync** - 传感器数据采集

#### 配置说明

```csharp
services.AddSerialPort(options =>
{
    options.PortName = "COM3";               // 串口名称
    options.BaudRate = 9600;                 // 波特率
    options.DataBits = 8;                    // 数据位
    options.StopBits = StopBits.One;         // 停止位
    options.Parity = Parity.None;            // 校验位
});
```

#### 常见波特率

- 9600 - 常用，稳定
- 19200 - 中速
- 38400 - 中高速
- 57600 - 高速
- 115200 - 最高速

#### 应用场景

- 单片机通信
- GPS 模块
- GSM 模块
- 传感器数据采集
- PLC 串口通信

---

### Modbus TCP 示例

**文件:** `ModbusTcpExample.cs`  
**协议:** Modbus TCP  
**难度:** ??? 高级

#### 示例列表

1. **BasicConnectionAsync** - 基础连接和读取
   ```csharp
   await ModbusTcpExample.BasicConnectionAsync();
   ```
   
2. **ReadCoilsAsync** - 读取线圈状态
   - 功能码: 0x01
   - 用途: 读取开关量输出

3. **ReadInputRegistersAsync** - 读取输入寄存器
   - 功能码: 0x04
   - 用途: 读取传感器数据

4. **WriteSingleRegisterAsync** - 写单个寄存器
   - 功能码: 0x06
   - 用途: 设置单个参数

5. **WriteMultipleRegistersAsync** - 写多个寄存器
   - 功能码: 0x10
   - 用途: 批量设置参数

6. **WriteSingleCoilAsync** - 写单个线圈
   - 功能码: 0x05
   - 用途: 控制开关量

7. **PlcDataMonitoringAsync** - PLC 数据监控
   - 实时数据采集
   - 状态监控

8. **MultiDevicePollingAsync** - 多设备轮询
   - 多从站管理
   - 轮询策略

#### Modbus 功能码速查表

| 功能码 | 名称 | 说明 |
|-------|------|------|
| 0x01 | Read Coils | 读线圈 |
| 0x02 | Read Discrete Inputs | 读离散输入 |
| 0x03 | Read Holding Registers | 读保持寄存器 |
| 0x04 | Read Input Registers | 读输入寄存器 |
| 0x05 | Write Single Coil | 写单个线圈 |
| 0x06 | Write Single Register | 写单个寄存器 |
| 0x0F | Write Multiple Coils | 写多个线圈 |
| 0x10 | Write Multiple Registers | 写多个寄存器 |

#### 配置说明

```csharp
services.AddModbusTcp(options =>
{
    options.Host = "192.168.1.100";          // PLC IP 地址
    options.Port = 502;                       // Modbus TCP 端口
    options.SlaveId = 1;                      // 从站地址
    options.ConnectTimeout = 5000;            // 连接超时
    options.ReadTimeout = 3000;               // 读取超时
});
```

#### 应用场景

- PLC 通信
- SCADA 系统
- 工业自动化
- 设备监控
- 数据采集

---

### Modbus RTU 示例

**文件:** `ModbusRtuExample.cs`  
**协议:** Modbus RTU (Serial)  
**难度:** ??? 高级

#### 示例列表

1. **BasicConnectionAsync** - 基础连接和读取
   ```csharp
   await ModbusRtuExample.BasicConnectionAsync();
   ```

2. **ReadOperationsExampleAsync** - 读取操作演示
   - 所有读取功能码
   - 完整示例

3. **WriteOperationsExampleAsync** - 写入操作演示
   - 所有写入功能码
   - 验证机制

4. **CrcValidationExampleAsync** - CRC 校验演示
   - CRC-16 计算
   - 校验原理

5. **MultiSlavePollingAsync** - 多从站轮询
   - RS-485 多从站
   - 轮询时序

6. **SensorMonitoringAsync** - 传感器监控
   - 实时数据采集
   - 异常检测

7. **DiagnosticsExampleAsync** - 故障诊断
   - 常见问题
   - 排查方法

#### 配置说明

```csharp
services.AddModbusRtu(options =>
{
    options.PortName = "COM3";               // 串口名称
    options.BaudRate = 9600;                 // 波特率
    options.DataBits = 8;                    // 数据位
    options.StopBits = StopBits.One;         // 停止位
    options.Parity = Parity.None;            // 校验位 (通常为 None 或 Even)
    options.SlaveId = 1;                     // 从站地址
});
```

#### RS-485 接线说明

```
主站           从站1          从站2
A+ ────────── A+ ────────── A+
B- ────────── B- ────────── B-
GND ─────────── GND ────────── GND

注意事项:
? 总线两端需要 120Ω 终端电阻
? A+ 和 B- 不要接反
? 最长距离: 1200米 (9600bps)
? 最多从站: 247 个
```

#### 常见波特率配置

| 波特率 | 最大距离 | 应用场景 |
|--------|---------|---------|
| 9600 | 1200m | 常用，稳定 |
| 19200 | 1000m | 中速 |
| 38400 | 500m | 高速 |
| 115200 | 200m | 超高速，短距离 |

#### 应用场景

- 工业现场总线
- 楼宇自动化
- 电力监控
- 环境监测
- 远程 I/O

---

## ?? 学习路径

### 初学者（第 1-3 天）

1. **HTTP 示例** - 最简单，从这里开始
   ```csharp
   await HttpExample.BasicGetRequestAsync();
   await HttpExample.PostJsonRequestAsync();
   ```

2. **TCP 客户端** - 学习网络基础
   ```csharp
   await TcpClientExample.BasicConnectAndSendAsync();
   ```

3. **UDP 单播** - 理解无连接协议
   ```csharp
   await UdpExample.UnicastExampleAsync();
   ```

### 进阶（第 4-7 天）

1. **TCP 服务器** - 服务器开发
   ```csharp
   await TcpServerExample.BasicEchoServerAsync();
   ```

2. **WebSocket** - 实时通信
   ```csharp
   await WebSocketExample.BasicWebSocketAsync();
   ```

3. **串口通信** - 硬件通信
   ```csharp
   await SerialPortExample.BasicSerialPortAsync();
   ```

### 高级（第 8-14 天）

1. **Modbus TCP** - 工业通信
   ```csharp
   await ModbusTcpExample.BasicConnectionAsync();
   await ModbusTcpExample.PlcDataMonitoringAsync();
   ```

2. **Modbus RTU** - 串口工业通信
   ```csharp
   await ModbusRtuExample.BasicConnectionAsync();
   await ModbusRtuExample.MultiSlavePollingAsync();
   ```

3. **综合项目** - 整合所有技能

---

## ?? 最佳实践

### 1. 资源管理

```csharp
// ? 正确：使用 using 或 try-finally
try
{
    var client = serviceProvider.GetRequiredService<TcpClientHelper>();
    await client.ConnectAsync();
    // ... 使用客户端
}
finally
{
    await client.DisconnectAsync();
    client.Dispose();
}

// ? 错误：不释放资源
var client = new TcpClientHelper(...);
await client.ConnectAsync();
// 程序结束，资源泄漏
```

### 2. 异常处理

```csharp
// ? 正确：捕获具体异常
try
{
    await modbusClient.ReadHoldingRegistersAsync(1, 0, 10);
}
catch (TimeoutException ex)
{
    logger.LogError("读取超时: {Message}", ex.Message);
}
catch (InvalidOperationException ex)
{
    logger.LogError("操作无效: {Message}", ex.Message);
}

// ? 错误：忽略异常
await modbusClient.ReadHoldingRegistersAsync(1, 0, 10); // 可能抛异常
```

### 3. 异步编程

```csharp
// ? 正确：使用 async/await
public async Task SendDataAsync()
{
    await tcpClient.SendAsync(data);
}

// ? 错误：阻塞调用
public void SendData()
{
    tcpClient.SendAsync(data).Wait(); // 可能死锁
}
```

### 4. 依赖注入

```csharp
// ? 正确：使用 DI
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddTcpClient(options => { ... });
var client = serviceProvider.GetRequiredService<TcpClientHelper>();

// ? 错误：直接 new
var client = new TcpClientHelper("127.0.0.1", 8080, logger); // 不推荐
```

---

## ?? 故障排查

### TCP 连接失败

**问题：** 无法连接到服务器

**排查步骤：**
1. 检查服务器是否运行
2. 验证 IP 地址和端口
3. 检查防火墙设置
4. 测试网络连通性 (`ping`)

### 串口打开失败

**问题：** 串口无法打开

**排查步骤：**
1. 确认串口存在 (`SerialPort.GetPortNames()`)
2. 检查是否被其他程序占用
3. 验证权限 (Linux 需要 dialout 组)
4. 检查驱动程序

### Modbus 通信超时

**问题：** 读写超时

**排查步骤：**
1. 检查从站地址是否正确
2. 验证波特率、数据位、停止位、校验位
3. 检查物理连接 (RS-485 接线)
4. 测试终端电阻
5. 降低波特率尝试

---

## ?? 依赖包

所有示例需要以下 NuGet 包：

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.0" />
<PackageReference Include="ToolHelper.Communication" Version="1.0.0" />
```

---

## ?? 贡献指南

欢迎贡献新的示例！

### 添加新示例的步骤

1. 创建新的示例方法
2. 添加详细的 XML 注释
3. 包含完整的错误处理
4. 更新此 README
5. 提交 Pull Request

### 示例代码规范

```csharp
/// <summary>
/// 示例 X: 简短标题
/// 详细说明此示例的功能和学习要点
/// </summary>
public static async Task ExampleNameAsync()
{
    // 1. 配置
    var services = new ServiceCollection();
    services.AddLogging(...);
    services.AddXxx(...);

    try
    {
        // 2. 主要逻辑
        Console.WriteLine("=== 示例标题 ===\n");
        
        // 3. 实现
    }
    catch (Exception ex)
    {
        // 4. 错误处理
        Console.WriteLine($"? 错误: {ex.Message}");
    }
    finally
    {
        // 5. 清理
    }
}
```

---

## ?? 获取帮助

- **文档：** [ToolHelper.Communication README](../../ToolHelper.Communication/README.md)
- **问题反馈：** GitHub Issues
- **讨论：** GitHub Discussions

---

## ?? 许可证

本示例代码遵循 MIT 许可证。

---

## ? 快速索引

### 按协议分类

- **TCP/IP:** TcpClientExample, TcpServerExample
- **UDP:** UdpExample
- **HTTP:** HttpExample
- **WebSocket:** WebSocketExample
- **串口:** SerialPortExample, ModbusRtuExample
- **Modbus:** ModbusTcpExample, ModbusRtuExample

### 按应用场景分类

- **Web 开发:** HttpExample, WebSocketExample
- **物联网:** SerialPortExample, ModbusTcpExample, ModbusRtuExample
- **工业自动化:** ModbusTcpExample, ModbusRtuExample
- **实时通信:** WebSocketExample, UdpExample
- **服务器开发:** TcpServerExample

### 按难度分类

- **? 入门:** HttpExample
- **?? 中级:** TcpClientExample, UdpExample, WebSocketExample, SerialPortExample
- **??? 高级:** TcpServerExample, ModbusTcpExample, ModbusRtuExample

---

**最后更新:** 2024-12-27  
**版本:** 1.0  
**示例总数:** 50

?? **祝学习愉快！**
