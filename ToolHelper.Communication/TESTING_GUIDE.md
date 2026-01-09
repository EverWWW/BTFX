# TcpServerHelper 测试指南

## 测试方法总览

1. **单元测试** - 使用 XUnit 测试框架
2. **集成测试** - 模拟真实客户端连接
3. **手动测试** - 使用命令行工具
4. **性能测试** - 压力测试和负载测试

---

## 1. 运行单元测试

### 使用 Visual Studio

1. 打开测试资源管理器 (Test Explorer)
   - 快捷键: `Ctrl + E, T`
   - 或菜单: 测试 → 测试资源管理器

2. 点击"运行所有测试"
   - 快捷键: `Ctrl + R, A`

3. 查看测试结果

### 使用命令行

```bash
# 进入测试项目目录
cd ToolHelperTest

# 运行所有测试
dotnet test

# 运行特定测试类
dotnet test --filter FullyQualifiedName~TcpServerHelperTests

# 运行特定测试方法
dotnet test --filter "FullyQualifiedName=ToolHelperTest.Communication.TcpServerHelperTests.IntegrationTest_ClientConnectAndDisconnect"

# 显示详细输出
dotnet test -v detailed
```

### 测试用例说明

#### 基础测试
- `Constructor_WithPort_ShouldCreateInstance` - 验证构造函数
- `IsRunning_WhenNotStarted_ShouldReturnFalse` - 验证初始状态
- `StartAsync_ShouldStartServer` - 验证启动功能
- `StopAsync_WhenRunning_ShouldStopServer` - 验证停止功能

#### 集成测试
- `IntegrationTest_ClientConnectAndDisconnect` - 客户端连接和断开
- `IntegrationTest_SendDataToClient` - 向客户端发送数据
- `IntegrationTest_ReceiveDataFromClient` - 接收客户端数据
- `IntegrationTest_BroadcastToAllClients` - 广播消息
- `IntegrationTest_ServerDisconnectClient` - 服务器主动断开
- `IntegrationTest_MultipleClientsConnectSimultaneously` - 多客户端并发
- `IntegrationTest_LargeDataTransfer` - 大数据传输

#### 压力测试 (默认跳过)
- `StressTest_RapidConnectDisconnect` - 快速连接断开测试

---

## 2. 使用测试客户端

### 方法 1: Telnet (简单快速)

```bash
# Windows
telnet 127.0.0.1 8080

# 如果 Telnet 未启用，需要先启用
# 控制面板 → 程序和功能 → 启用或关闭 Windows 功能 → Telnet 客户端

# Linux / Mac
telnet 127.0.0.1 8080

# 或使用 netcat
nc 127.0.0.1 8080
```

**操作步骤:**
1. 启动服务器
2. 打开新的命令行窗口
3. 运行 telnet 命令
4. 输入文本并按回车发送
5. 按 `Ctrl + ]` 然后输入 `quit` 退出

### 方法 2: PowerShell 脚本

创建 `test-client.ps1`:

```powershell
# TCP 测试客户端脚本

param(
    [string]$Server = "127.0.0.1",
    [int]$Port = 8080
)

try {
    $client = New-Object System.Net.Sockets.TcpClient
    Write-Host "正在连接到 $Server:$Port ..." -ForegroundColor Yellow
    
    $client.Connect($Server, $Port)
    Write-Host "已连接!" -ForegroundColor Green
    
    $stream = $client.GetStream()
    $writer = New-Object System.IO.StreamWriter($stream)
    $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($stream)
    
    # 启动接收线程
    $receiveJob = Start-Job -ScriptBlock {
        param($reader)
        while ($true) {
            try {
                $line = $reader.ReadLine()
                if ($line) {
                    Write-Host "收到: $line" -ForegroundColor Cyan
                }
            }
            catch {
                break
            }
        }
    } -ArgumentList $reader
    
    # 发送循环
    while ($true) {
        $input = Read-Host "输入消息 (quit 退出)"
        
        if ($input -eq "quit") {
            break
        }
        
        $writer.WriteLine($input)
    }
}
catch {
    Write-Host "错误: $_" -ForegroundColor Red
}
finally {
    if ($receiveJob) {
        Stop-Job $receiveJob
        Remove-Job $receiveJob
    }
    if ($reader) { $reader.Close() }
    if ($writer) { $writer.Close() }
    if ($stream) { $stream.Close() }
    if ($client) { $client.Close() }
    
    Write-Host "已断开连接" -ForegroundColor Yellow
}
```

**使用方法:**
```powershell
.\test-client.ps1
.\test-client.ps1 -Server "192.168.1.100" -Port 8080
```

### 方法 3: C# 控制台客户端

使用提供的 `TcpServerDemo.cs` 中的 `TestClient` 类：

```csharp
// 创建测试客户端
await TestClient.RunAsync();
```

---

## 3. 手动测试场景

### 场景 1: 基本连接测试

1. **启动服务器**
```bash
dotnet run --project ToolHelper.Communication
```

2. **连接客户端**
```bash
telnet 127.0.0.1 8080
```

3. **预期结果**
   - 服务器显示: `[连接] 客户端 xxx 已连接`
   - 客户端成功连接

4. **断开测试**
   - 在客户端按 `Ctrl + ]` 然后输入 `quit`
   - 服务器显示: `[断开] 客户端 xxx 已断开`

### 场景 2: 数据传输测试

1. **连接客户端**
2. **发送数据**
   ```
   Hello Server!
   ```
3. **预期结果**
   - 服务器显示: `[收到] 来自 xxx: Hello Server!`
   - 客户端收到回显: `回显: Hello Server!`

### 场景 3: 多客户端测试

1. **启动服务器**
2. **打开 3 个客户端窗口**
   ```bash
   # 窗口 1
   telnet 127.0.0.1 8080
   
   # 窗口 2
   telnet 127.0.0.1 8080
   
   # 窗口 3
   telnet 127.0.0.1 8080
   ```

3. **在服务器执行命令**
   ```
   > list
   当前连接的客户端 (共 3 个):
     [1] xxx-xxx-xxx
     [2] yyy-yyy-yyy
     [3] zzz-zzz-zzz
   
   > broadcast 大家好！
   ? 已向 3 个客户端广播消息
   ```

4. **预期结果**
   - 所有客户端都收到: `[广播] 大家好！`

### 场景 4: 单点发送测试

1. **获取客户端 ID**
   ```
   > list
   ```

2. **向指定客户端发送**
   ```
   > send xxx-xxx-xxx Hello Client 1!
   ? 已发送消息给客户端 xxx-xxx-xxx
   ```

3. **预期结果**
   - 只有指定客户端收到消息
   - 其他客户端不受影响

### 场景 5: 踢出客户端测试

1. **查看连接列表**
   ```
   > list
   ```

2. **踢出指定客户端**
   ```
   > kick xxx-xxx-xxx
   ? 已踢出客户端 xxx-xxx-xxx
   ```

3. **预期结果**
   - 客户端连接被关闭
   - 服务器显示断开事件

---

## 4. 性能测试

### 测试 1: 并发连接数

创建 `load-test.ps1`:

```powershell
param(
    [int]$ClientCount = 100,
    [string]$Server = "127.0.0.1",
    [int]$Port = 8080
)

$jobs = @()
$sw = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "启动 $ClientCount 个客户端..." -ForegroundColor Yellow

for ($i = 1; $i -le $ClientCount; $i++) {
    $job = Start-Job -ScriptBlock {
        param($server, $port)
        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $client.Connect($server, $port)
            Start-Sleep -Seconds 10
            $client.Close()
            return $true
        }
        catch {
            return $false
        }
    } -ArgumentList $Server, $Port
    
    $jobs += $job
    
    if ($i % 10 -eq 0) {
        Write-Host "已启动 $i 个客户端..." -ForegroundColor Cyan
    }
}

Write-Host "等待所有客户端完成..." -ForegroundColor Yellow
$results = $jobs | Wait-Job | Receive-Job
$sw.Stop()

$successCount = ($results | Where-Object { $_ -eq $true }).Count
$failCount = $ClientCount - $successCount

Write-Host "`n测试结果:" -ForegroundColor Green
Write-Host "  总客户端数: $ClientCount"
Write-Host "  成功连接: $successCount"
Write-Host "  连接失败: $failCount"
Write-Host "  总耗时: $($sw.Elapsed.TotalSeconds) 秒"

$jobs | Remove-Job
```

**运行测试:**
```powershell
.\load-test.ps1 -ClientCount 100
```

### 测试 2: 消息吞吐量

使用 `PerformanceTest` 类 (在使用指南中):

```csharp
await PerformanceTest.RunLoadTestAsync();
```

**预期结果:**
- 100 个客户端 × 1000 条消息 = 100,000 条消息
- 吞吐量 > 10,000 消息/秒 (取决于硬件)

### 测试 3: 长时间稳定性

创建 `stability-test.ps1`:

```powershell
param(
    [int]$DurationMinutes = 60,
    [int]$ClientCount = 50,
    [int]$MessageInterval = 1000 # 毫秒
)

Write-Host "稳定性测试启动" -ForegroundColor Green
Write-Host "  持续时间: $DurationMinutes 分钟"
Write-Host "  客户端数: $ClientCount"
Write-Host "  消息间隔: $MessageInterval 毫秒"

$endTime = (Get-Date).AddMinutes($DurationMinutes)
$clients = @()

# 创建客户端
for ($i = 1; $i -le $ClientCount; $i++) {
    $client = New-Object System.Net.Sockets.TcpClient
    $client.Connect("127.0.0.1", 8080)
    $clients += $client
}

Write-Host "所有客户端已连接" -ForegroundColor Cyan

$messageCount = 0
$errorCount = 0

# 发送消息循环
while ((Get-Date) -lt $endTime) {
    foreach ($client in $clients) {
        try {
            $stream = $client.GetStream()
            $writer = New-Object System.IO.StreamWriter($stream)
            $writer.WriteLine("Test message $messageCount")
            $writer.Flush()
            $messageCount++
        }
        catch {
            $errorCount++
        }
    }
    
    Start-Sleep -Milliseconds $MessageInterval
    
    if ($messageCount % 1000 -eq 0) {
        Write-Host "已发送 $messageCount 条消息, 错误: $errorCount" -ForegroundColor Yellow
    }
}

# 清理
foreach ($client in $clients) {
    $client.Close()
}

Write-Host "`n测试完成!" -ForegroundColor Green
Write-Host "  总消息数: $messageCount"
Write-Host "  错误次数: $errorCount"
Write-Host "  成功率: $([Math]::Round(($messageCount - $errorCount) / $messageCount * 100, 2))%"
```

---

## 5. 自动化测试流程

### CI/CD 集成

在 Azure DevOps 或 GitHub Actions 中添加：

```yaml
# .github/workflows/test.yml
name: Run Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run tests
      run: dotnet test --no-build --verbosity normal
```

---

## 6. 测试检查清单

### 功能测试
- [ ] 服务器启动和停止
- [ ] 客户端连接和断开
- [ ] 发送数据到指定客户端
- [ ] 广播数据到所有客户端
- [ ] 接收客户端数据
- [ ] 主动断开客户端
- [ ] 获取连接客户端列表

### 异常测试
- [ ] 端口已被占用
- [ ] 客户端突然断开
- [ ] 发送到不存在的客户端
- [ ] 超过最大连接数
- [ ] 发送超大数据包

### 性能测试
- [ ] 100+ 并发连接
- [ ] 1000+ 消息/秒吞吐量
- [ ] 长时间稳定运行 (>1小时)
- [ ] 内存泄漏检查

### 安全测试
- [ ] 拒绝服务攻击防护
- [ ] 恶意数据处理
- [ ] 连接数限制

---

## 7. 常见问题排查

### 问题 1: 测试失败 - 端口被占用

**症状:**
```
System.Net.Sockets.SocketException: Only one usage of each socket address
```

**解决方法:**
```bash
# Windows 查找占用进程
netstat -ano | findstr :8080
taskkill /PID <进程ID> /F

# Linux 查找占用进程
lsof -i :8080
kill -9 <进程ID>
```

### 问题 2: 客户端无法连接

**检查清单:**
1. 服务器是否已启动
2. 防火墙是否阻止
3. 端口是否正确
4. IP 地址是否正确 (127.0.0.1 vs 0.0.0.0)

### 问题 3: 测试超时

**原因:**
- 网络延迟
- 服务器负载过高
- 测试超时设置过短

**解决:**
```csharp
// 增加超时时间
await Task.Delay(2000); // 改为更长的等待时间
```

---

## 8. 测试报告

### 生成测试覆盖率报告

```bash
# 安装 coverlet
dotnet tool install --global coverlet.console

# 运行测试并生成覆盖率
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# 生成 HTML 报告
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage
```

### 查看报告

打开 `coverage/index.html` 查看详细的代码覆盖率报告。

---

## 总结

通过以上测试方法，您可以：
- ? 快速验证基本功能
- ? 进行完整的集成测试
- ? 执行性能和压力测试
- ? 在 CI/CD 中自动化测试
- ? 生成测试覆盖率报告

**建议的测试流程:**
1. 开发时 → 运行单元测试
2. 提交前 → 运行集成测试
3. 发布前 → 运行性能测试
4. 生产前 → 运行稳定性测试
