# UdpHelper 使用指南

## 目录
1. [UDP 协议简介](#udp-协议简介)
2. [快速开始](#快速开始)
3. [单播通信](#单播通信)
4. [广播通信](#广播通信)
5. [组播通信](#组播通信)
6. [实战示例](#实战示例)
7. [常见问题](#常见问题)

---

## UDP 协议简介

UDP (User Datagram Protocol) 是一种无连接的传输层协议，具有以下特点：

### 优点
- ? **低延迟** - 无需建立连接，直接发送
- ? **高效率** - 协议开销小
- ? **支持广播** - 一对多通信
- ? **支持组播** - 多对多通信

### 缺点
- ?? **不可靠** - 可能丢包、乱序
- ?? **无连接** - 不保证数据到达
- ?? **无流控** - 可能导致网络拥塞

### 适用场景
- ?? 实时音视频传输
- ?? 在线游戏
- ?? 传感器数据采集
- ?? 即时消息推送
- ?? 服务发现协议

---

## 快速开始

### 1. 基础配置

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.Communication.Extensions;
using ToolHelper.Communication.Udp;

// 配置服务
var services = new ServiceCollection();

services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// 添加 UDP 服务
services.AddUdp(options =>
{
    options.LocalPort = 8000;           // 本地监听端口
    options.RemoteHost = "127.0.0.1";   // 远程主机地址
    options.RemotePort = 8001;          // 远程端口
    options.EnableBroadcast = true;     // 启用广播
});

var serviceProvider = services.BuildServiceProvider();
var udpHelper = serviceProvider.GetRequiredService<UdpHelper>();
```

### 2. 简单收发示例

```csharp
// 初始化
udpHelper.Initialize();

// 订阅数据接收事件
udpHelper.DataReceived += (sender, e) =>
{
    var message = Encoding.UTF8.GetString(e.Data);
    Console.WriteLine($"收到来自 {e.RemoteEndPoint}: {message}");
};

// 启动监听
await udpHelper.StartListeningAsync();

// 发送数据
var data = Encoding.UTF8.GetBytes("Hello UDP!");
await udpHelper.SendAsync(data, "127.0.0.1", 8001);
```

---

## 单播通信

单播是点对点的通信方式，一对一传输数据。

### 1. 发送方

```csharp
public class UdpSender
{
    private readonly UdpHelper _udp;
    private readonly ILogger<UdpSender> _logger;

    public UdpSender(UdpHelper udp, ILogger<UdpSender> logger)
    {
        _udp = udp;
        _logger = logger;
    }

    public async Task SendMessageAsync(string message, string targetHost, int targetPort)
    {
        try
        {
            // 初始化（如果还未初始化）
            _udp.Initialize();

            // 发送数据
            var data = Encoding.UTF8.GetBytes(message);
            var bytesSent = await _udp.SendAsync(data, targetHost, targetPort);

            _logger.LogInformation("已发送 {BytesSent} 字节到 {Host}:{Port}", 
                bytesSent, targetHost, targetPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送失败");
            throw;
        }
    }

    // 使用配置的默认地址发送
    public async Task SendMessageAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _udp.SendAsync(data);
    }
}
```

### 2. 接收方

```csharp
public class UdpReceiver
{
    private readonly UdpHelper _udp;
    private readonly ILogger<UdpReceiver> _logger;

    public UdpReceiver(UdpHelper udp, ILogger<UdpReceiver> logger)
    {
        _udp = udp;
        _logger = logger;
    }

    public async Task StartReceivingAsync()
    {
        // 订阅事件
        _udp.DataReceived += OnDataReceived;

        // 初始化
        _udp.Initialize();

        // 开始监听
        _logger.LogInformation("开始监听 UDP 数据...");
        await _udp.StartListeningAsync();
    }

    public void StopReceiving()
    {
        _udp.StopListening();
        _logger.LogInformation("已停止监听");
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        var message = Encoding.UTF8.GetString(e.Data);
        _logger.LogInformation("收到来自 {RemoteEndPoint} 的消息: {Message}", 
            e.RemoteEndPoint, message);

        // 处理接收到的数据
        ProcessMessage(message, e.RemoteEndPoint);
    }

    private void ProcessMessage(string message, IPEndPoint remoteEndPoint)
    {
        // 业务逻辑处理
        Console.WriteLine($"处理消息: {message}");
    }
}
```

### 3. 双向通信示例

```csharp
public class UdpPeer
{
    private readonly UdpHelper _udp;
    private readonly int _localPort;

    public UdpPeer(int localPort, ILogger<UdpHelper> logger)
    {
        _localPort = localPort;
        _udp = new UdpHelper(localPort, logger);
        _udp.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        _udp.Initialize();
        _ = Task.Run(async () => await _udp.StartListeningAsync());
        await Task.Delay(500); // 等待初始化完成
        Console.WriteLine($"节点已启动，监听端口: {_localPort}");
    }

    public async Task SendToAsync(string message, string host, int port)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _udp.SendAsync(data, host, port);
        Console.WriteLine($"发送到 {host}:{port}: {message}");
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        var message = Encoding.UTF8.GetString(e.Data);
        Console.WriteLine($"收到来自 {e.RemoteEndPoint}: {message}");
    }

    public void Stop()
    {
        _udp.StopListening();
        _udp.Dispose();
    }
}

// 使用示例
var peer1 = new UdpPeer(8000, logger1);
var peer2 = new UdpPeer(8001, logger2);

await peer1.StartAsync();
await peer2.StartAsync();

// Peer1 发送给 Peer2
await peer1.SendToAsync("Hello from Peer1", "127.0.0.1", 8001);

// Peer2 发送给 Peer1
await peer2.SendToAsync("Hello from Peer2", "127.0.0.1", 8000);
```

---

## 广播通信

广播是一对多的通信方式，可以同时向网络中的所有设备发送数据。

### 1. 广播发送

```csharp
public class BroadcastSender
{
    private readonly UdpHelper _udp;

    public BroadcastSender(ILogger<UdpHelper> logger)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = 0,              // 使用随机端口
                EnableBroadcast = true      // 必须启用广播
            }
        );
        
        _udp = new UdpHelper(options, logger);
    }

    public async Task BroadcastAsync(string message, int targetPort)
    {
        _udp.Initialize();

        var data = Encoding.UTF8.GetBytes(message);
        var bytesSent = await _udp.BroadcastAsync(data, targetPort);

        Console.WriteLine($"广播了 {bytesSent} 字节到端口 {targetPort}");
    }
}
```

### 2. 广播接收

```csharp
public class BroadcastReceiver
{
    private readonly UdpHelper _udp;
    private readonly int _listenPort;

    public BroadcastReceiver(int listenPort, ILogger<UdpHelper> logger)
    {
        _listenPort = listenPort;
        
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = listenPort,
                ReuseAddress = true         // 允许多个接收者
            }
        );
        
        _udp = new UdpHelper(options, logger);
        _udp.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        _udp.Initialize();
        Console.WriteLine($"开始监听广播，端口: {_listenPort}");
        await _udp.StartListeningAsync();
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        var message = Encoding.UTF8.GetString(e.Data);
        Console.WriteLine($"收到广播: {message}");
    }
}

// 使用示例
var broadcastPort = 9000;

// 创建多个接收者
var receiver1 = new BroadcastReceiver(broadcastPort, logger1);
var receiver2 = new BroadcastReceiver(broadcastPort, logger2);
var receiver3 = new BroadcastReceiver(broadcastPort, logger3);

await receiver1.StartAsync();
await receiver2.StartAsync();
await receiver3.StartAsync();

// 创建发送者并广播
var sender = new BroadcastSender(loggerSender);
await sender.BroadcastAsync("Hello Everyone!", broadcastPort);

// 所有接收者都会收到消息
```

---

## 组播通信

组播是多对多的通信方式，只有加入组播组的设备才能接收数据。

### 1. 组播配置

```csharp
public class MulticastConfiguration
{
    public const string MulticastAddress = "239.0.0.1"; // 组播地址范围: 224.0.0.0 - 239.255.255.255
    public const int MulticastPort = 9001;
    public const int MulticastTtl = 32; // 生存时间 (Time To Live)
}
```

### 2. 组播发送

```csharp
public class MulticastSender
{
    private readonly UdpHelper _udp;

    public MulticastSender(ILogger<UdpHelper> logger)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = 0,
                MulticastAddress = MulticastConfiguration.MulticastAddress,
                RemotePort = MulticastConfiguration.MulticastPort,
                MulticastTtl = MulticastConfiguration.MulticastTtl
            }
        );
        
        _udp = new UdpHelper(options, logger);
    }

    public async Task SendAsync(string message)
    {
        _udp.Initialize();

        var data = Encoding.UTF8.GetBytes(message);
        var bytesSent = await _udp.MulticastAsync(data);

        Console.WriteLine($"发送组播消息: {message} ({bytesSent} 字节)");
    }
}
```

### 3. 组播接收

```csharp
public class MulticastReceiver
{
    private readonly UdpHelper _udp;
    private readonly string _name;

    public MulticastReceiver(string name, ILogger<UdpHelper> logger)
    {
        _name = name;
        
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = MulticastConfiguration.MulticastPort,
                MulticastAddress = MulticastConfiguration.MulticastAddress,
                RemotePort = MulticastConfiguration.MulticastPort,
                ReuseAddress = true
            }
        );
        
        _udp = new UdpHelper(options, logger);
        _udp.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        _udp.Initialize();
        Console.WriteLine($"[{_name}] 加入组播组 {MulticastConfiguration.MulticastAddress}");
        await _udp.StartListeningAsync();
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        var message = Encoding.UTF8.GetString(e.Data);
        Console.WriteLine($"[{_name}] 收到组播消息: {message}");
    }

    public void Stop()
    {
        _udp.StopListening();
        _udp.Dispose();
    }
}

// 使用示例
var receiver1 = new MulticastReceiver("接收者1", logger1);
var receiver2 = new MulticastReceiver("接收者2", logger2);
var receiver3 = new MulticastReceiver("接收者3", logger3);

await receiver1.StartAsync();
await receiver2.StartAsync();
await receiver3.StartAsync();

var sender = new MulticastSender(loggerSender);
await sender.SendAsync("组播消息测试");

// 所有加入组播组的接收者都会收到
```

---

## 实战示例

### 1. 服务发现协议

实现类似 mDNS 的服务发现功能：

```csharp
public class ServiceDiscovery
{
    private readonly UdpHelper _udp;
    private readonly Dictionary<string, ServiceInfo> _services = new();

    public ServiceDiscovery(ILogger<UdpHelper> logger)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = 5353, // mDNS 标准端口
                MulticastAddress = "224.0.0.251", // mDNS 组播地址
                RemotePort = 5353,
                ReuseAddress = true
            }
        );
        
        _udp = new UdpHelper(options, logger);
        _udp.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        _udp.Initialize();
        await _udp.StartListeningAsync();
    }

    // 发布服务
    public async Task AnnounceServiceAsync(string serviceName, int port)
    {
        var message = $"SERVICE:{serviceName}:{port}";
        var data = Encoding.UTF8.GetBytes(message);
        await _udp.MulticastAsync(data);
        
        Console.WriteLine($"发布服务: {serviceName} on port {port}");
    }

    // 发现服务
    public async Task QueryServicesAsync()
    {
        var query = "QUERY:*";
        var data = Encoding.UTF8.GetBytes(query);
        await _udp.MulticastAsync(data);
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        var message = Encoding.UTF8.GetString(e.Data);
        var parts = message.Split(':');

        if (parts[0] == "SERVICE" && parts.Length == 3)
        {
            var serviceName = parts[1];
            var port = int.Parse(parts[2]);
            
            _services[serviceName] = new ServiceInfo
            {
                Name = serviceName,
                Host = e.RemoteEndPoint.Address.ToString(),
                Port = port,
                LastSeen = DateTime.Now
            };

            Console.WriteLine($"发现服务: {serviceName} at {e.RemoteEndPoint}");
        }
        else if (parts[0] == "QUERY")
        {
            // 响应查询
            _ = Task.Run(async () =>
            {
                foreach (var service in _services.Values)
                {
                    await AnnounceServiceAsync(service.Name, service.Port);
                }
            });
        }
    }

    public IEnumerable<ServiceInfo> GetServices()
    {
        // 移除超时的服务
        var timeout = TimeSpan.FromMinutes(5);
        var now = DateTime.Now;
        var expiredServices = _services
            .Where(kvp => now - kvp.Value.LastSeen > timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredServices)
        {
            _services.Remove(key);
        }

        return _services.Values;
    }
}

public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime LastSeen { get; set; }
}
```

### 2. 实时数据采集

传感器数据实时传输：

```csharp
public class SensorDataCollector
{
    private readonly UdpHelper _udp;
    private readonly ConcurrentQueue<SensorReading> _dataQueue = new();

    public SensorDataCollector(string multicastAddress, int port, ILogger<UdpHelper> logger)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = port,
                MulticastAddress = multicastAddress,
                RemotePort = port,
                ReuseAddress = true
            }
        );
        
        _udp = new UdpHelper(options, logger);
        _udp.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        _udp.Initialize();
        await _udp.StartListeningAsync();
        
        // 启动数据处理线程
        _ = Task.Run(ProcessDataAsync);
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        try
        {
            // 解析传感器数据 (假设是 JSON 格式)
            var json = Encoding.UTF8.GetString(e.Data);
            var reading = System.Text.Json.JsonSerializer.Deserialize<SensorReading>(json);
            
            if (reading != null)
            {
                _dataQueue.Enqueue(reading);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解析数据失败: {ex.Message}");
        }
    }

    private async Task ProcessDataAsync()
    {
        while (true)
        {
            if (_dataQueue.TryDequeue(out var reading))
            {
                // 处理数据（存储、分析等）
                Console.WriteLine($"传感器 {reading.SensorId}: " +
                                $"温度={reading.Temperature}°C, " +
                                $"湿度={reading.Humidity}%");
                
                // 存储到数据库
                await SaveToDatabaseAsync(reading);
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    private async Task SaveToDatabaseAsync(SensorReading reading)
    {
        // 数据库操作
        await Task.Delay(10);
    }
}

public class SensorReading
{
    public string SensorId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public DateTime Timestamp { get; set; }
}

// 传感器端（发送数据）
public class Sensor
{
    private readonly UdpHelper _udp;
    private readonly string _sensorId;

    public Sensor(string sensorId, string multicastAddress, int port, ILogger<UdpHelper> logger)
    {
        _sensorId = sensorId;
        
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = 0,
                MulticastAddress = multicastAddress,
                RemotePort = port
            }
        );
        
        _udp = new UdpHelper(options, logger);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _udp.Initialize();

        while (!cancellationToken.IsCancellationRequested)
        {
            // 模拟读取传感器数据
            var reading = new SensorReading
            {
                SensorId = _sensorId,
                Temperature = 20 + Random.Shared.NextDouble() * 10,
                Humidity = 50 + Random.Shared.NextDouble() * 20,
                Timestamp = DateTime.Now
            };

            // 发送数据
            var json = System.Text.Json.JsonSerializer.Serialize(reading);
            var data = Encoding.UTF8.GetBytes(json);
            await _udp.MulticastAsync(data);

            // 每秒发送一次
            await Task.Delay(1000, cancellationToken);
        }
    }
}
```

### 3. 游戏状态同步

在线多人游戏的状态同步：

```csharp
public class GameStateSynchronizer
{
    private readonly UdpHelper _udp;
    private readonly Dictionary<string, PlayerState> _players = new();

    public GameStateSynchronizer(string multicastAddress, int port, ILogger<UdpHelper> logger)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new UdpOptions
            {
                LocalPort = port,
                MulticastAddress = multicastAddress,
                RemotePort = port,
                ReuseAddress = true
            }
        );
        
        _udp = new UdpHelper(options, logger);
        _udp.DataReceived += OnDataReceived;
    }

    public async Task StartAsync()
    {
        _udp.Initialize();
        await _udp.StartListeningAsync();
    }

    public async Task BroadcastPlayerStateAsync(PlayerState state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        var data = Encoding.UTF8.GetBytes(json);
        await _udp.MulticastAsync(data);
    }

    private void OnDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        try
        {
            var json = Encoding.UTF8.GetString(e.Data);
            var state = System.Text.Json.JsonSerializer.Deserialize<PlayerState>(json);
            
            if (state != null)
            {
                _players[state.PlayerId] = state;
                OnPlayerStateUpdated(state);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解析玩家状态失败: {ex.Message}");
        }
    }

    protected virtual void OnPlayerStateUpdated(PlayerState state)
    {
        Console.WriteLine($"玩家 {state.PlayerId} 位置更新: ({state.X}, {state.Y})");
    }

    public IEnumerable<PlayerState> GetAllPlayers()
    {
        return _players.Values;
    }
}

public class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public string Action { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}
```

---

## 常见问题

### 1. UDP 丢包问题

**问题:** UDP 通信经常丢包

**解决方案:**

```csharp
// 1. 增加缓冲区大小
services.AddUdp(options =>
{
    options.ReceiveBufferSize = 65536;
    options.SendBufferSize = 65536;
});

// 2. 降低发送频率
await Task.Delay(10); // 每次发送后延迟

// 3. 实现应用层确认机制
public class ReliableUdpSender
{
    public async Task SendWithAckAsync(byte[] data, string host, int port)
    {
        var maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            await _udp.SendAsync(data, host, port);
            
            // 等待 ACK
            if (await WaitForAckAsync(timeout: 1000))
            {
                return; // 成功
            }
        }
        
        throw new TimeoutException("发送失败，未收到确认");
    }
}
```

### 2. 组播不工作

**问题:** 组播消息无法收到

**解决方案:**

```csharp
// 1. 检查防火墙设置
// Windows: netsh advfirewall firewall add rule name="UDP Multicast" dir=in action=allow protocol=UDP localport=9000

// 2. 检查组播地址范围
// 必须使用 224.0.0.0 - 239.255.255.255
var validMulticastAddress = "239.0.0.1"; // ?
var invalidAddress = "192.168.1.100";    // ?

// 3. 确保启用了地址重用
options.ReuseAddress = true;

// 4. 检查 TTL 设置
options.MulticastTtl = 32; // 增加 TTL 值
```

### 3. 广播不工作

**问题:** 广播消息无法发送

**解决方案:**

```csharp
// 1. 必须启用广播
options.EnableBroadcast = true;

// 2. 使用正确的广播地址
// 局域网广播: 255.255.255.255
// 子网广播: 192.168.1.255 (根据实际子网)

// 3. 检查网络权限
// 某些网络环境禁止广播
```

### 4. 端口被占用

**问题:** 初始化时提示端口已被占用

**解决方案:**

```csharp
// 1. 使用随机端口
options.LocalPort = 0; // 系统自动分配

// 2. 启用地址重用
options.ReuseAddress = true;

// 3. 检查并关闭占用端口的程序
// Windows: netstat -ano | findstr :8000
// Linux: lsof -i :8000
```

### 5. 接收不到数据

**问题:** StartListeningAsync 后没有收到数据

**排查步骤:**

```csharp
// 1. 确认已订阅事件
_udp.DataReceived += OnDataReceived;

// 2. 确认已初始化
_udp.Initialize();

// 3. 确认端口正确
Console.WriteLine($"监听端口: {_udp.LocalPort}");

// 4. 使用 Wireshark 抓包确认数据是否到达

// 5. 检查防火墙
// 临时关闭防火墙测试
```

---

## 配置参考

### 单播配置

```csharp
new UdpOptions
{
    LocalPort = 8000,              // 本地监听端口
    RemoteHost = "192.168.1.100",  // 远程主机
    RemotePort = 8001,             // 远程端口
    ReceiveBufferSize = 8192,      // 接收缓冲区
    SendBufferSize = 8192,         // 发送缓冲区
    ReceiveTimeout = 5000,         // 接收超时
    SendTimeout = 5000             // 发送超时
}
```

### 广播配置

```csharp
new UdpOptions
{
    LocalPort = 8000,
    EnableBroadcast = true,        // 必须启用
    ReuseAddress = true,           // 允许多个接收者
    ReceiveBufferSize = 8192,
    SendBufferSize = 8192
}
```

### 组播配置

```csharp
new UdpOptions
{
    LocalPort = 9000,
    MulticastAddress = "239.0.0.1", // 组播地址
    RemotePort = 9000,
    MulticastTtl = 32,              // 生存时间
    ReuseAddress = true,
    ReceiveBufferSize = 8192,
    SendBufferSize = 8192
}
```

---

## 性能优化建议

### 1. 批量发送

```csharp
// 不推荐：逐条发送
for (int i = 0; i < 1000; i++)
{
    await udp.SendAsync(data, host, port);
}

// 推荐：批量组装后发送
var batch = new List<byte[]>();
for (int i = 0; i < 100; i++)
{
    batch.Add(data);
}
var batchData = batch.SelectMany(x => x).ToArray();
await udp.SendAsync(batchData, host, port);
```

### 2. 异步并发

```csharp
// 推荐：并发发送
var tasks = new List<Task>();
foreach (var target in targets)
{
    tasks.Add(udp.SendAsync(data, target.Host, target.Port));
}
await Task.WhenAll(tasks);
```

### 3. 数据压缩

```csharp
// 对于大数据包，使用压缩
public async Task SendCompressedAsync(byte[] data)
{
    using var ms = new MemoryStream();
    using (var gzip = new GZipStream(ms, CompressionMode.Compress))
    {
        await gzip.WriteAsync(data);
    }
    
    var compressed = ms.ToArray();
    await _udp.SendAsync(compressed, host, port);
}
```

---

## 总结

UdpHelper 提供了完整的 UDP 通信功能：
- ? 单播 - 点对点通信
- ? 广播 - 一对多通信  
- ? 组播 - 多对多通信
- ? 异步操作 - 高性能
- ? 事件驱动 - 易于使用

根据不同的应用场景选择合适的通信方式，并注意 UDP 协议的特性，合理处理丢包和乱序问题。
