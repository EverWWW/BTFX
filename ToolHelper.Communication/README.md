# ToolHelper.Communication - 工业通信协议工具库

## 概述

ToolHelper.Communication 是一个面向上位机软件开发的工业通信协议工具库，提供了完整的通信协议实现，支持依赖注入、异步操作、对象池优化等现代化开发特性。

## 特性

- ? **模块化设计** - 每个通信协议独立封装，按需引用
- ? **接口抽象** - 统一的 `IConnection`、`IClientConnection`、`IServerConnection` 接口
- ? **依赖注入** - 完整支持 Microsoft.Extensions.DependencyInjection
- ? **异步优先** - 所有 IO 操作使用 async/await
- ? **配置驱动** - 通过 Options 模式配置各项参数
- ? **性能优化** - 使用 ArrayPool 减少 GC 压力
- ? **完整测试** - 配套完整的单元测试和集成测试
- ? **文档完善** - 完整的 XML 注释和示例代码

## 支持的通信协议

### 基础通信协议

#### 1. TCP 客户端 (TcpClientHelper)
- ? 心跳保活机制
- ? 自动断线重连
- ? 粘包处理
- ? 缓冲区优化

#### 2. TCP 服务端 (TcpServerHelper)
- ? 多客户端管理
- ? 广播消息
- ? 客户端连接/断开事件

#### 3. UDP 通信 (UdpHelper)
- ? 单播
- ? 组播
- ? 广播

#### 4. 串口通信 (SerialPortHelper)
- ? 自动识别可用串口
- ? 波特率自适应
- ? 多种校验位和停止位支持
- ? 异步数据收发

#### 5. WebSocket 通信 (WebSocketHelper)
- ? 实时双向通信
- ? 文本和二进制消息支持
- ? 自动重连
- ? 心跳保活
- ? 子协议支持

#### 6. HTTP 通信 (HttpHelper)
- ? GET/POST 请求
- ? 文件上传下载
- ? 自定义请求头
- ? 超时控制

### 工业协议

#### 7. Modbus TCP (ModbusTcpHelper)
- ? 主站/从站模式
- ? 读取线圈 (功能码 0x01)
- ? 读取离散输入 (功能码 0x02)
- ? 读取保持寄存器 (功能码 0x03)
- ? 读取输入寄存器 (功能码 0x04)
- ? 写单个线圈 (功能码 0x05)
- ? 写单个寄存器 (功能码 0x06)
- ? 写多个线圈 (功能码 0x0F)
- ? 写多个寄存器 (功能码 0x10)
- ? 事务管理
- ? 异常处理

#### 8. Modbus RTU (ModbusRtuHelper)
- ? 串口通信
- ? CRC-16 校验
- ? 完整的 Modbus 功能码支持
- ? 重试机制
- ? 帧间隔处理

## 安装

### NuGet 包依赖

```bash
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Options
dotnet add package System.IO.Ports
```

### 项目引用

```bash
dotnet add reference path/to/ToolHelper.Communication.csproj
```

## 快速开始

### 1. 注册服务

在 `Program.cs` 或 `Startup.cs` 中注册所有通信服务：

```csharp
using ToolHelper.Communication.Extensions;

// 注册所有通信模块
services.AddCommunication();

// 或按需注册单个模块
services.AddTcpClient(options => 
{
    options.Host = "127.0.0.1";
    options.Port = 8080;
});

services.AddSerialPort(options => 
{
    options.PortName = "COM1";
    options.BaudRate = 9600;
});
```

### 2. TCP 客户端使用示例

```csharp
using ToolHelper.Communication.Tcp;

public class MyService
{
    private readonly TcpClientHelper _tcpClient;
    private readonly ILogger<MyService> _logger;

    public MyService(TcpClientHelper tcpClient, ILogger<MyService> logger)
    {
        _tcpClient = tcpClient;
        _logger = logger;
    }

    public async Task ConnectAndSendAsync()
    {
        // 订阅事件
        _tcpClient.ConnectionStateChanged += OnConnectionStateChanged;
        _tcpClient.DataReceived += OnDataReceived;

        // 连接服务器
        if (await _tcpClient.ConnectAsync())
        {
            _logger.LogInformation("连接成功");

            // 启动接收
            await _tcpClient.StartReceivingAsync();

            // 发送数据
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await _tcpClient.SendAsync(data);
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _logger.LogInformation("连接状态: {OldState} -> {NewState}", e.OldState, e.NewState);
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        _logger.LogInformation("接收到 {Length} 字节数据", e.Length);
        // 处理接收到的数据
    }
}
```

### 3. 串口通信使用示例

```csharp
using ToolHelper.Communication.SerialPort;

public class SerialCommunication
{
    private readonly SerialPortHelper _serialPort;

    public SerialCommunication(SerialPortHelper serialPort)
    {
        _serialPort = serialPort;
    }

    public async Task CommunicateAsync()
    {
        // 获取可用串口
        var ports = SerialPortHelper.GetAvailablePorts();
        Console.WriteLine($"可用串口: {string.Join(", ", ports)}");

        // 订阅数据接收事件
        _serialPort.DataReceived += (sender, e) =>
        {
            Console.WriteLine($"接收: {BitConverter.ToString(e.Data)}");
        };

        // 打开串口
        if (await _serialPort.ConnectAsync())
        {
            await _serialPort.StartReceivingAsync();

            // 发送数据
            var command = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            await _serialPort.SendAsync(command);
        }
    }
}
```

### 4. WebSocket 使用示例

```csharp
using ToolHelper.Communication.WebSocket;

public class WebSocketClient
{
    private readonly WebSocketHelper _webSocket;

    public WebSocketClient(WebSocketHelper webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task StartAsync()
    {
        // 订阅文本消息接收
        _webSocket.TextMessageReceived += (sender, e) =>
        {
            Console.WriteLine($"收到文本: {e.Text}");
        };

        // 订阅二进制消息接收
        _webSocket.DataReceived += (sender, e) =>
        {
            Console.WriteLine($"收到二进制: {e.Length} 字节");
        };

        // 连接服务器
        if (await _webSocket.ConnectAsync())
        {
            await _webSocket.StartReceivingAsync();

            // 发送文本消息
            await _webSocket.SendTextAsync("Hello, WebSocket!");

            // 发送二进制消息
            var binaryData = new byte[] { 0x01, 0x02, 0x03 };
            await _webSocket.SendAsync(binaryData);
        }
    }
}
```

### 5. Modbus TCP 使用示例

```csharp
using ToolHelper.Communication.Modbus;

public class ModbusClient
{
    private readonly ModbusTcpHelper _modbus;

    public ModbusClient(ModbusTcpHelper modbus)
    {
        _modbus = modbus;
    }

    public async Task ReadAndWriteAsync()
    {
        // 连接 Modbus TCP 服务器
        if (await _modbus.ConnectAsync())
        {
            // 读取 10 个线圈
            var coils = await _modbus.ReadCoilsAsync(0, 10);
            Console.WriteLine($"线圈状态: {string.Join(", ", coils)}");

            // 读取 5 个保持寄存器
            var registers = await _modbus.ReadHoldingRegistersAsync(0, 5);
            Console.WriteLine($"寄存器值: {string.Join(", ", registers)}");

            // 写单个线圈
            await _modbus.WriteSingleCoilAsync(0, true);

            // 写单个寄存器
            await _modbus.WriteSingleRegisterAsync(0, 1234);

            // 写多个寄存器
            var values = new ushort[] { 100, 200, 300 };
            await _modbus.WriteMultipleRegistersAsync(0, values);

            // 断开连接
            await _modbus.DisconnectAsync();
        }
    }
}
```

### 6. Modbus RTU 使用示例

```csharp
using ToolHelper.Communication.Modbus;

public class ModbusRtuClient
{
    private readonly ModbusRtuHelper _modbus;

    public ModbusRtuClient(ModbusRtuHelper modbus)
    {
        _modbus = modbus;
    }

    public async Task CommunicateAsync()
    {
        // 打开串口
        if (await _modbus.ConnectAsync())
        {
            try
            {
                // 读取保持寄存器 (从站地址 1, 起始地址 0, 数量 10)
                var registers = await _modbus.ReadHoldingRegistersAsync(0, 10);
                
                foreach (var (value, index) in registers.Select((v, i) => (v, i)))
                {
                    Console.WriteLine($"寄存器[{index}] = {value}");
                }

                // 写入寄存器
                await _modbus.WriteSingleRegisterAsync(0, 5678);

                // 验证写入
                var readBack = await _modbus.ReadHoldingRegistersAsync(0, 1);
                Console.WriteLine($"写入验证: {readBack[0]}");
            }
            finally
            {
                await _modbus.DisconnectAsync();
            }
        }
    }
}
```

## 配置选项

### TCP 客户端配置

```csharp
services.AddTcpClient(options =>
{
    options.Host = "192.168.1.100";
    options.Port = 8080;
    options.ConnectTimeout = 5000;          // 连接超时 (ms)
    options.ReceiveTimeout = 3000;          // 接收超时 (ms)
    options.SendTimeout = 3000;             // 发送超时 (ms)
    options.ReceiveBufferSize = 8192;       // 接收缓冲区
    options.SendBufferSize = 8192;          // 发送缓冲区
    options.EnableAutoReconnect = true;     // 自动重连
    options.MaxReconnectAttempts = 3;       // 最大重连次数
    options.ReconnectInterval = 2000;       // 重连间隔 (ms)
    options.HeartbeatInterval = 30000;      // 心跳间隔 (ms)
    options.HeartbeatTimeout = 10000;       // 心跳超时 (ms)
    options.NoDelay = true;                 // 禁用 Nagle 算法
    options.KeepAlive = true;               // 启用 Keep-Alive
});
```

### 串口配置

```csharp
services.AddSerialPort(options =>
{
    options.PortName = "COM3";
    options.BaudRate = 115200;
    options.DataBits = 8;
    options.StopBits = StopBits.One;
    options.Parity = Parity.None;
    options.ReadTimeout = 1000;
    options.WriteTimeout = 1000;
    options.AutoDetectPort = true;          // 自动识别串口
    options.AutoBaudRate = true;            // 自动波特率
    options.BaudRatesToTry = new[] { 9600, 19200, 38400, 57600, 115200 };
});
```

### WebSocket 配置

```csharp
services.AddWebSocket(options =>
{
    options.Uri = "wss://example.com/ws";
    options.ConnectTimeout = 30000;
    options.HeartbeatInterval = 30000;
    options.EnableAutoReconnect = true;
    options.MaxReconnectAttempts = -1;      // 无限重连
    options.ReconnectInterval = 5000;
    options.EnableCompression = false;
    
    // 添加子协议
    options.SubProtocols.Add("my-protocol");
    
    // 添加请求头
    options.Headers["Authorization"] = "Bearer token";
});
```

### Modbus TCP 配置

```csharp
services.AddModbusTcp(options =>
{
    options.Host = "192.168.1.10";
    options.Port = 502;
    options.UnitId = 1;                     // 从站地址
    options.ConnectTimeout = 5000;
    options.ReadTimeout = 3000;
    options.WriteTimeout = 3000;
    options.TransactionTimeout = 5000;
    options.MaxConcurrentTransactions = 10;
    options.EnableAutoReconnect = true;
});
```

### Modbus RTU 配置

```csharp
services.AddModbusRtu(options =>
{
    options.PortName = "COM1";
    options.BaudRate = 9600;
    options.DataBits = 8;
    options.StopBits = StopBits.One;
    options.Parity = Parity.None;
    options.SlaveId = 1;                    // 从站地址
    options.ReadTimeout = 1000;
    options.WriteTimeout = 1000;
    options.FrameDelay = 10;                // 帧间隔 (ms)
    options.EnableCrcCheck = true;          // CRC 校验
    options.MaxRetries = 3;                 // 最大重试次数
    options.RetryInterval = 100;            // 重试间隔 (ms)
});
```

## 接口说明

### IConnection 接口

基础连接接口，所有通信类都实现此接口：

```csharp
public interface IConnection : IDisposable
{
    bool IsConnected { get; }
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<int> SendAsync(byte[] data, CancellationToken cancellationToken = default);
    Task<int> SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}
```

### IClientConnection 接口

客户端连接接口，扩展了数据接收功能：

```csharp
public interface IClientConnection : IConnection
{
    event EventHandler<DataReceivedEventArgs>? DataReceived;
    
    Task StartReceivingAsync(CancellationToken cancellationToken = default);
    void StopReceiving();
}
```

### IServerConnection 接口

服务器连接接口，支持多客户端管理：

```csharp
public interface IServerConnection : IDisposable
{
    bool IsRunning { get; }
    event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
    event EventHandler<ServerDataReceivedEventArgs>? DataReceived;
    
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<int> SendToClientAsync(string clientId, byte[] data, CancellationToken cancellationToken = default);
    Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default);
}
```

## 测试

### 测试覆盖情况

| 工具类 | 测试文件 | 测试状态 | 覆盖率 |
|--------|----------|---------|--------|
| TcpClientHelper | TcpClientHelperTests.cs | ? 已完成 | 90% |
| TcpServerHelper | TcpServerHelperTests.cs | ? 已完成 | 95% |
| UdpHelper | UdpHelperTests.cs | ? 已完成 | 90% |
| HttpHelper | HttpHelperTests.cs | ? 已完成 | 85% |
| SerialPortHelper | SerialPortHelperTests.cs | ? 已完成 | 80% |
| WebSocketHelper | WebSocketHelperTests.cs | ? 已完成 | 85% |
| ModbusTcpHelper | ModbusTcpHelperTests.cs | ? 已完成 | 90% |
| ModbusRtuHelper | ModbusRtuHelperTests.cs | ? 已完成 | 90% |

查看详细的测试覆盖报告: [TEST_COVERAGE_REPORT.md](TEST_COVERAGE_REPORT.md)

### 运行单元测试

```bash
# 运行所有测试
dotnet test

# 运行特定测试类
dotnet test --filter FullyQualifiedName~TcpServerHelperTests

# 运行特定测试方法
dotnet test --filter "FullyQualifiedName=ToolHelperTest.Communication.TcpServerHelperTests.IntegrationTest_ClientConnectAndDisconnect"

# 显示详细输出
dotnet test -v detailed
```

### 集成测试说明

部分集成测试需要真实设备或模拟器支持，这些测试默认被标记为 Skip：

- **串口测试** - 需要真实串口设备或虚拟串口
- **WebSocket 测试** - 需要 WebSocket 服务器 (可使用 echo.websocket.org)
- **Modbus TCP 测试** - 需要 Modbus TCP 模拟器 (如 ModRSsim2)
- **Modbus RTU 测试** - 需要 Modbus RTU 设备或 USB-to-RS485 转换器

要运行这些测试，请移除 `[Fact(Skip = "...")]` 中的 Skip 参数。

## 详细文档

### 使用指南

- ?? [TCP 服务器使用指南](TCPSERVER_GUIDE.md) - TcpServerHelper 详细教程
- ?? [UDP 通信使用指南](UDP_GUIDE.md) - UDP 单播、广播、组播完整示例
- ?? [测试指南](TESTING_GUIDE.md) - 如何测试各个工具类

### 技术文档

- ?? [测试覆盖报告](TEST_COVERAGE_REPORT.md) - 测试覆盖情况分析
- ?? API 文档 - XML 注释已包含在源代码中

### 快速参考

#### TCP 服务器快速开始
```csharp
// 参见 TCPSERVER_GUIDE.md 获取完整示例
var server = new TcpServerHelper(8080, logger);
server.ClientConnected += (s, e) => Console.WriteLine($"客户端连接: {e.ClientId}");
server.DataReceived += (s, e) => Console.WriteLine($"收到数据: {e.Length} 字节");
await server.StartAsync();
```

#### UDP 通信快速开始
```csharp
// 参见 UDP_GUIDE.md 获取完整示例
var udp = new UdpHelper(8000, logger);
udp.DataReceived += (s, e) => Console.WriteLine($"收到: {Encoding.UTF8.GetString(e.Data)}");
udp.Initialize();
await udp.StartListeningAsync();
await udp.SendAsync(Encoding.UTF8.GetBytes("Hello"), "127.0.0.1", 8001);
```

## 测试

### 运行单元测试

```bash
dotnet test
```

### 集成测试说明

部分集成测试需要真实设备或模拟器支持，这些测试默认被标记为 Skip：

- **串口测试** - 需要真实串口设备或虚拟串口
- **WebSocket 测试** - 需要 WebSocket 服务器 (可使用 echo.websocket.org)
- **Modbus TCP 测试** - 需要 Modbus TCP 模拟器 (如 ModRSsim2)
- **Modbus RTU 测试** - 需要 Modbus RTU 设备或 USB-to-RS485 转换器

要运行这些测试，请移除 `[Fact(Skip = "...")]` 中的 Skip 参数。

## 性能优化

1. **对象池使用** - 使用 `ArrayPool<byte>` 减少数组分配和 GC 压力
2. **异步优先** - 所有 I/O 操作使用异步模式
3. **取消令牌** - 支持取消操作，避免资源浪费
4. **信号量控制** - 使用 `SemaphoreSlim` 控制并发访问
5. **缓冲区优化** - 可配置的缓冲区大小

## 最佳实践

1. **使用依赖注入** - 通过 DI 容器管理生命周期
2. **正确释放资源** - 使用 `using` 或调用 `Dispose()`
3. **异常处理** - 捕获并处理可能的异常
4. **日志记录** - 利用 `ILogger` 记录关键信息
5. **超时控制** - 合理配置各种超时参数
6. **重连策略** - 根据业务需求配置重连参数

## 常见问题

### 1. 串口打不开？
- 检查串口是否被其他程序占用
- 确认串口名称正确 (Windows: COMx, Linux: /dev/ttyUSBx)
- 检查串口权限 (Linux 需要将用户添加到 dialout 组)

### 2. Modbus 通信失败？
- 检查从站地址是否正确
- 验证波特率、数据位、停止位、校验位配置
- 确认功能码是否被设备支持
- 检查 CRC 校验是否启用

### 3. WebSocket 自动重连不工作？
- 确认 `EnableAutoReconnect` 已设置为 true
- 检查 `MaxReconnectAttempts` 是否已达到上限
- 查看日志了解重连失败原因

### 4. TCP 粘包问题？
- 在应用层定义消息协议 (固定长度、分隔符、长度前缀)
- 在 `DataReceived` 事件中处理粘包逻辑
- 考虑使用消息队列缓存不完整的数据包

## 版本历史

### v1.0.0 (2024-01-XX)
- ? 初始版本发布
- ? 实现 TCP 客户端/服务端
- ? 实现 UDP 通信
- ? 实现 HTTP 客户端
- ? 实现串口通信
- ? 实现 WebSocket 通信
- ? 实现 Modbus TCP 协议
- ? 实现 Modbus RTU 协议
- ? 完整的单元测试覆盖

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！

## 联系方式

- 项目主页: [GitHub Repository]
- 问题反馈: [Issues]
- 邮箱: [your-email@example.com]

## 致谢

感谢所有贡献者和使用者的支持！
