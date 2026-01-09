# TcpServerHelper 使用指南

## 目录
1. [快速开始](#快速开始)
2. [基础使用](#基础使用)
3. [高级功能](#高级功能)
4. [实战示例](#实战示例)
5. [测试方法](#测试方法)

---

## 快速开始

### 1. 基础配置和启动

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Tcp;

// 配置服务
var services = new ServiceCollection();

// 添加日志
services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// 添加 TCP 服务器
services.AddTcpServer(options =>
{
    options.Host = "0.0.0.0";           // 监听所有网卡
    options.Port = 8080;                 // 监听端口
    options.MaxConnections = 100;        // 最大连接数
});

var serviceProvider = services.BuildServiceProvider();

// 获取服务器实例
var server = serviceProvider.GetRequiredService<TcpServerHelper>();

// 订阅事件
server.ClientConnected += OnClientConnected;
server.ClientDisconnected += OnClientDisconnected;
server.DataReceived += OnDataReceived;

// 启动服务器
await server.StartAsync();

Console.WriteLine("服务器已启动，按任意键停止...");
Console.ReadKey();

// 停止服务器
await server.StopAsync();
```

### 2. 事件处理

```csharp
void OnClientConnected(object? sender, ClientConnectedEventArgs e)
{
    Console.WriteLine($"客户端已连接: {e.ClientId} 来自 {e.RemoteAddress}");
}

void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
{
    Console.WriteLine($"客户端已断开: {e.ClientId} 原因: {e.Reason}");
}

void OnDataReceived(object? sender, ServerDataReceivedEventArgs e)
{
    Console.WriteLine($"收到来自 {e.ClientId} 的数据: {e.Length} 字节");
    Console.WriteLine($"数据内容: {BitConverter.ToString(e.Data)}");
    
    // 回显数据
    _ = Task.Run(async () =>
    {
        var server = sender as TcpServerHelper;
        if (server != null)
        {
            await server.SendToClientAsync(e.ClientId, e.Data);
        }
    });
}
```

---

## 基础使用

### 1. 创建简单的回显服务器

```csharp
public class EchoServer
{
    private readonly TcpServerHelper _server;
    private readonly ILogger<EchoServer> _logger;

    public EchoServer(TcpServerHelper server, ILogger<EchoServer> logger)
    {
        _server = server;
        _logger = logger;
        
        // 订阅数据接收事件
        _server.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        await _server.StartAsync();
        _logger.LogInformation("回显服务器已启动");
    }

    public async Task StopAsync()
    {
        await _server.StopAsync();
        _logger.LogInformation("回显服务器已停止");
    }

    private async void OnDataReceived(object? sender, ServerDataReceivedEventArgs e)
    {
        _logger.LogInformation($"收到来自 {e.ClientId} 的 {e.Length} 字节数据");
        
        // 回显数据给客户端
        try
        {
            await _server.SendToClientAsync(e.ClientId, e.Data);
            _logger.LogDebug($"已回显数据给 {e.ClientId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"回显数据给 {e.ClientId} 失败");
        }
    }
}
```

### 2. 向指定客户端发送数据

```csharp
public class MessageService
{
    private readonly TcpServerHelper _server;

    public MessageService(TcpServerHelper server)
    {
        _server = server;
    }

    // 向单个客户端发送消息
    public async Task SendMessageToClientAsync(string clientId, string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _server.SendToClientAsync(clientId, data);
    }

    // 向所有客户端广播消息
    public async Task BroadcastMessageAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        var successCount = await _server.BroadcastAsync(data);
        Console.WriteLine($"消息已发送给 {successCount} 个客户端");
    }

    // 获取所有连接的客户端
    public IReadOnlyList<string> GetAllClients()
    {
        return _server.GetConnectedClients();
    }
}
```

---

## 高级功能

### 1. 客户端管理系统

```csharp
public class ClientManager
{
    private readonly TcpServerHelper _server;
    private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos = new();

    public ClientManager(TcpServerHelper server)
    {
        _server = server;
        
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _server.DataReceived += OnDataReceived;
    }

    private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        var info = new ClientInfo
        {
            ClientId = e.ClientId,
            RemoteAddress = e.RemoteAddress,
            ConnectedTime = DateTime.Now,
            LastActiveTime = DateTime.Now
        };
        
        _clientInfos.TryAdd(e.ClientId, info);
        Console.WriteLine($"新客户端: {e.ClientId} ({e.RemoteAddress})");
        Console.WriteLine($"当前连接数: {_clientInfos.Count}");
    }

    private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        if (_clientInfos.TryRemove(e.ClientId, out var info))
        {
            var duration = DateTime.Now - info.ConnectedTime;
            Console.WriteLine($"客户端 {e.ClientId} 断开，在线时长: {duration:hh\\:mm\\:ss}");
        }
    }

    private void OnDataReceived(object? sender, ServerDataReceivedEventArgs e)
    {
        if (_clientInfos.TryGetValue(e.ClientId, out var info))
        {
            info.LastActiveTime = DateTime.Now;
            info.ReceivedBytes += e.Length;
            info.ReceivedPackets++;
        }
    }

    // 获取客户端信息
    public ClientInfo? GetClientInfo(string clientId)
    {
        _clientInfos.TryGetValue(clientId, out var info);
        return info;
    }

    // 获取所有客户端信息
    public IEnumerable<ClientInfo> GetAllClientInfos()
    {
        return _clientInfos.Values;
    }

    // 踢出不活跃的客户端
    public async Task KickInactiveClientsAsync(TimeSpan timeout)
    {
        var now = DateTime.Now;
        var inactiveClients = _clientInfos
            .Where(kvp => now - kvp.Value.LastActiveTime > timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var clientId in inactiveClients)
        {
            await _server.DisconnectClientAsync(clientId);
            Console.WriteLine($"踢出不活跃客户端: {clientId}");
        }
    }
}

public class ClientInfo
{
    public string ClientId { get; set; } = string.Empty;
    public string RemoteAddress { get; set; } = string.Empty;
    public DateTime ConnectedTime { get; set; }
    public DateTime LastActiveTime { get; set; }
    public long ReceivedBytes { get; set; }
    public long ReceivedPackets { get; set; }
}
```

### 2. 协议解析和消息分发

```csharp
public class ProtocolServer
{
    private readonly TcpServerHelper _server;
    private readonly ILogger<ProtocolServer> _logger;

    public ProtocolServer(TcpServerHelper server, ILogger<ProtocolServer> logger)
    {
        _server = server;
        _logger = logger;
        _server.DataReceived += OnDataReceived;
    }

    private async void OnDataReceived(object? sender, ServerDataReceivedEventArgs e)
    {
        try
        {
            // 解析协议
            var message = ParseMessage(e.Data);
            
            // 根据消息类型分发
            var response = await HandleMessageAsync(message);
            
            // 发送响应
            if (response != null)
            {
                await _server.SendToClientAsync(e.ClientId, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理客户端 {e.ClientId} 的消息时出错");
        }
    }

    private Message ParseMessage(byte[] data)
    {
        // 示例协议: [消息类型(1字节)][数据长度(2字节)][数据]
        if (data.Length < 3)
            throw new InvalidDataException("消息长度不足");

        var messageType = (MessageType)data[0];
        var dataLength = BitConverter.ToUInt16(data, 1);
        var payload = data.Skip(3).Take(dataLength).ToArray();

        return new Message
        {
            Type = messageType,
            Payload = payload
        };
    }

    private async Task<byte[]?> HandleMessageAsync(Message message)
    {
        return message.Type switch
        {
            MessageType.Heartbeat => await HandleHeartbeatAsync(message),
            MessageType.DataQuery => await HandleDataQueryAsync(message),
            MessageType.Control => await HandleControlAsync(message),
            _ => null
        };
    }

    private async Task<byte[]> HandleHeartbeatAsync(Message message)
    {
        _logger.LogDebug("收到心跳");
        return new byte[] { (byte)MessageType.Heartbeat, 0, 0 };
    }

    private async Task<byte[]> HandleDataQueryAsync(Message message)
    {
        _logger.LogInformation("处理数据查询");
        // 处理查询逻辑
        var responseData = Encoding.UTF8.GetBytes("Query response");
        var response = new byte[3 + responseData.Length];
        response[0] = (byte)MessageType.DataQuery;
        BitConverter.GetBytes((ushort)responseData.Length).CopyTo(response, 1);
        responseData.CopyTo(response, 3);
        return response;
    }

    private async Task<byte[]> HandleControlAsync(Message message)
    {
        _logger.LogInformation("处理控制命令");
        // 处理控制逻辑
        return new byte[] { (byte)MessageType.Control, 0, 0 };
    }
}

public enum MessageType : byte
{
    Heartbeat = 0x01,
    DataQuery = 0x02,
    Control = 0x03
}

public class Message
{
    public MessageType Type { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}
```

---

## 实战示例

### 1. 聊天室服务器

```csharp
public class ChatRoomServer
{
    private readonly TcpServerHelper _server;
    private readonly ConcurrentDictionary<string, string> _clientNames = new();

    public ChatRoomServer(TcpServerHelper server)
    {
        _server = server;
        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _server.DataReceived += OnDataReceived;
    }

    private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        // 分配默认名称
        _clientNames[e.ClientId] = $"用户{_clientNames.Count + 1}";
        
        // 通知所有人
        var message = $"系统: {_clientNames[e.ClientId]} 加入了聊天室";
        _ = BroadcastMessageAsync(message);
    }

    private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        if (_clientNames.TryRemove(e.ClientId, out var name))
        {
            var message = $"系统: {name} 离开了聊天室";
            _ = BroadcastMessageAsync(message);
        }
    }

    private async void OnDataReceived(object? sender, ServerDataReceivedEventArgs e)
    {
        var text = Encoding.UTF8.GetString(e.Data).Trim();
        
        // 处理命令
        if (text.StartsWith("/"))
        {
            await HandleCommandAsync(e.ClientId, text);
            return;
        }

        // 广播聊天消息
        if (_clientNames.TryGetValue(e.ClientId, out var name))
        {
            var message = $"{name}: {text}";
            await BroadcastMessageAsync(message);
        }
    }

    private async Task HandleCommandAsync(string clientId, string command)
    {
        var parts = command.Split(' ', 2);
        var cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "/name":
                if (parts.Length > 1 && _clientNames.TryGetValue(clientId, out var oldName))
                {
                    var newName = parts[1].Trim();
                    _clientNames[clientId] = newName;
                    await SendToClientAsync(clientId, $"系统: 您的名称已改为 {newName}");
                    await BroadcastMessageAsync($"系统: {oldName} 改名为 {newName}");
                }
                break;

            case "/list":
                var users = string.Join(", ", _clientNames.Values);
                await SendToClientAsync(clientId, $"在线用户: {users}");
                break;

            case "/help":
                await SendToClientAsync(clientId, 
                    "命令列表:\n" +
                    "/name [名称] - 修改昵称\n" +
                    "/list - 查看在线用户\n" +
                    "/help - 显示帮助");
                break;
        }
    }

    private async Task BroadcastMessageAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message + "\n");
        await _server.BroadcastAsync(data);
    }

    private async Task SendToClientAsync(string clientId, string message)
    {
        var data = Encoding.UTF8.GetBytes(message + "\n");
        await _server.SendToClientAsync(clientId, data);
    }
}
```

### 2. 数据采集服务器

```csharp
public class DataCollectionServer
{
    private readonly TcpServerHelper _server;
    private readonly ILogger<DataCollectionServer> _logger;
    private readonly ConcurrentQueue<DataPoint> _dataQueue = new();

    public DataCollectionServer(TcpServerHelper server, ILogger<DataCollectionServer> logger)
    {
        _server = server;
        _logger = logger;
        _server.DataReceived += OnDataReceived;
        
        // 启动数据处理线程
        _ = Task.Run(ProcessDataAsync);
    }

    private void OnDataReceived(object? sender, ServerDataReceivedEventArgs e)
    {
        try
        {
            // 解析数据点
            var dataPoint = new DataPoint
            {
                ClientId = e.ClientId,
                Timestamp = DateTime.Now,
                Data = e.Data
            };

            _dataQueue.Enqueue(dataPoint);
            _logger.LogDebug($"收到来自 {e.ClientId} 的数据，队列长度: {_dataQueue.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析数据失败");
        }
    }

    private async Task ProcessDataAsync()
    {
        while (true)
        {
            try
            {
                if (_dataQueue.TryDequeue(out var dataPoint))
                {
                    // 处理数据（存储到数据库、分析等）
                    await SaveToDataBaseAsync(dataPoint);
                    
                    // 发送确认
                    var ack = Encoding.UTF8.GetBytes("ACK");
                    await _server.SendToClientAsync(dataPoint.ClientId, ack);
                }
                else
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理数据时出错");
            }
        }
    }

    private async Task SaveToDataBaseAsync(DataPoint dataPoint)
    {
        // 模拟保存到数据库
        _logger.LogInformation($"保存数据: 客户端={dataPoint.ClientId}, " +
                              $"时间={dataPoint.Timestamp}, " +
                              $"大小={dataPoint.Data.Length}字节");
        
        await Task.Delay(50); // 模拟I/O操作
    }
}

public class DataPoint
{
    public string ClientId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
```

---

## 测试方法

### 1. 使用 XUnit 进行单元测试

参见 `TcpServerHelperTests.cs` 文件中的完整测试用例。

### 2. 使用控制台客户端测试

创建一个简单的测试客户端：

```csharp
using System.Net.Sockets;
using System.Text;

class TcpTestClient
{
    static async Task Main(string[] args)
    {
        using var client = new TcpClient();
        
        Console.WriteLine("连接到服务器...");
        await client.ConnectAsync("127.0.0.1", 8080);
        Console.WriteLine("已连接!");

        var stream = client.GetStream();

        // 启动接收线程
        _ = Task.Run(async () =>
        {
            var buffer = new byte[1024];
            while (true)
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead > 0)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"收到: {message}");
                    }
                }
                catch
                {
                    break;
                }
            }
        });

        // 发送消息
        while (true)
        {
            Console.Write("输入消息 (quit 退出): ");
            var input = Console.ReadLine();
            
            if (input?.ToLower() == "quit")
                break;

            var data = Encoding.UTF8.GetBytes(input ?? "");
            await stream.WriteAsync(data);
        }
    }
}
```

### 3. 使用 Telnet 测试

```bash
# Windows
telnet 127.0.0.1 8080

# Linux/Mac
telnet 127.0.0.1 8080
# 或
nc 127.0.0.1 8080
```

### 4. 性能测试工具

使用 `tcp_bench` 或编写压力测试：

```csharp
public class PerformanceTest
{
    public static async Task RunLoadTestAsync()
    {
        var clientCount = 100;
        var messagesPerClient = 1000;
        var clients = new List<TcpClient>();

        var sw = Stopwatch.StartNew();

        // 创建客户端
        var tasks = new List<Task>();
        for (int i = 0; i < clientCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 8080);
                
                lock (clients)
                {
                    clients.Add(client);
                }

                var stream = client.GetStream();
                var data = Encoding.UTF8.GetBytes("Test message");

                for (int j = 0; j < messagesPerClient; j++)
                {
                    await stream.WriteAsync(data);
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        var totalMessages = clientCount * messagesPerClient;
        var messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"总客户端: {clientCount}");
        Console.WriteLine($"总消息数: {totalMessages}");
        Console.WriteLine($"总耗时: {sw.Elapsed}");
        Console.WriteLine($"吞吐量: {messagesPerSecond:F2} 消息/秒");

        // 清理
        foreach (var client in clients)
        {
            client.Close();
        }
    }
}
```

---

## 配置建议

### 开发环境配置
```csharp
services.AddTcpServer(options =>
{
    options.Host = "127.0.0.1";
    options.Port = 8080;
    options.MaxConnections = 10;
    options.ReceiveBufferSize = 4096;
    options.SendBufferSize = 4096;
    options.ReceiveTimeout = 10000;
    options.SendTimeout = 10000;
});
```

### 生产环境配置
```csharp
services.AddTcpServer(options =>
{
    options.Host = "0.0.0.0";           // 监听所有网卡
    options.Port = 8080;
    options.MaxConnections = 1000;      // 更大的连接数
    options.ReceiveBufferSize = 8192;   // 更大的缓冲区
    options.SendBufferSize = 8192;
    options.NoDelay = true;             // 禁用 Nagle 算法
    options.KeepAlive = true;           // 启用 Keep-Alive
    options.EnablePacketProcessing = true; // 启用粘包处理
});
```

---

## 常见问题

### 1. 端口被占用
```csharp
// 解决方法：使用不同的端口或关闭占用端口的程序
// 检查端口占用: netstat -ano | findstr :8080
```

### 2. 防火墙阻止连接
```bash
# Windows 防火墙规则
netsh advfirewall firewall add rule name="TCP Server" dir=in action=allow protocol=TCP localport=8080

# Linux 防火墙规则
sudo ufw allow 8080/tcp
```

### 3. 客户端断开检测
```csharp
// 使用 KeepAlive 检测
options.KeepAlive = true;

// 或实现应用层心跳
```

---

## 总结

TcpServerHelper 提供了完整的 TCP 服务器功能：
- ? 多客户端管理
- ? 广播和单播消息
- ? 粘包处理
- ? 事件驱动架构
- ? 异步操作
- ? 完整的测试覆盖

通过本指南的示例，您应该能够快速上手并构建自己的 TCP 服务器应用！
