using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;

namespace ToolHelper.LoggingDiagnostics.Alarm;

/// <summary>
/// 报警管理帮助类
/// 提供报警记录、声音提示、报警历史管理功能
/// </summary>
/// <example>
/// <code>
/// // 触发报警
/// var alarm = alarmHelper.Trigger("ALM001", "温度过高", AlarmLevel.Critical, "传感器1");
/// 
/// // 确认报警
/// alarmHelper.Acknowledge(alarm.Id, "操作员张三", "已处理");
/// 
/// // 恢复报警
/// alarmHelper.Recover(alarm.Id);
/// </code>
/// </example>
public class AlarmHelper : IAlarmHelper
{
    private readonly AlarmOptions _options;
    private readonly ConcurrentDictionary<string, AlarmRecord> _activeAlarms;
    private readonly ConcurrentQueue<AlarmRecord> _alarmHistory;
    private readonly SemaphoreSlim _soundLock = new(1, 1);
    private readonly Timer? _soundLoopTimer;
    private readonly Timer? _autoAcknowledgeTimer;
    private readonly Timer? _persistTimer;
    
    private bool _disposed;
    private bool _soundPlaying;
    private AlarmLevel _currentSoundLevel;

    /// <inheritdoc/>
    public event EventHandler<AlarmEventArgs>? AlarmTriggered;

    /// <inheritdoc/>
    public event EventHandler<AlarmEventArgs>? AlarmStatusChanged;

    /// <inheritdoc/>
    public int ActiveAlarmCount => _activeAlarms.Count;

    /// <inheritdoc/>
    public bool SoundEnabled { get; set; }

    /// <summary>
    /// 创建AlarmHelper实例
    /// </summary>
    /// <param name="options">报警配置选项</param>
    public AlarmHelper(IOptions<AlarmOptions> options)
    {
        _options = options.Value;
        _activeAlarms = new ConcurrentDictionary<string, AlarmRecord>();
        _alarmHistory = new ConcurrentQueue<AlarmRecord>();
        SoundEnabled = _options.EnableSound;

        // 初始化目录
        EnsureDirectoryExists();

        // 声音循环定时器
        if (_options.LoopSound)
        {
            _soundLoopTimer = new Timer(
                SoundLoopCallback,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        // 自动确认定时器
        if (_options.AutoAcknowledgeMinutes > 0)
        {
            _autoAcknowledgeTimer = new Timer(
                AutoAcknowledgeCallback,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
        }

        // 持久化定时器
        if (_options.PersistToFile)
        {
            _persistTimer = new Timer(
                PersistCallback,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));

            // 加载历史数据
            _ = LoadHistoryAsync();
        }
    }

    /// <inheritdoc/>
    public AlarmRecord Trigger(
        string code,
        string message,
        AlarmLevel level = AlarmLevel.Alarm,
        string? source = null,
        IDictionary<string, object>? data = null)
    {
        // 检查是否允许重复报警
        if (!_options.AllowDuplicateAlarms)
        {
            var existing = _activeAlarms.Values
                .FirstOrDefault(a => a.Code == code && a.Status == AlarmStatus.Active);
            
            if (existing != null)
            {
                return existing;
            }
        }

        var alarm = new AlarmRecord
        {
            Code = code,
            Message = message,
            Level = level,
            Status = AlarmStatus.Active,
            Source = source ?? "System",
            Data = data
        };

        _activeAlarms[alarm.Id] = alarm;
        _alarmHistory.Enqueue(alarm);

        // 限制历史记录数量
        TrimHistory();

        // 触发事件
        AlarmTriggered?.Invoke(this, new AlarmEventArgs(alarm, AlarmEventType.Triggered));

        // 播放声音
        if (SoundEnabled)
        {
            PlaySound(level);
        }

        return alarm;
    }

    /// <inheritdoc/>
    public async Task<AlarmRecord> TriggerAsync(
        string code,
        string message,
        AlarmLevel level = AlarmLevel.Alarm,
        string? source = null,
        IDictionary<string, object>? data = null,
        CancellationToken cancellationToken = default)
    {
        var alarm = Trigger(code, message, level, source, data);
        
        // 异步持久化
        if (_options.PersistToFile)
        {
            await PersistAlarmAsync(alarm, cancellationToken);
        }

        return alarm;
    }

    /// <inheritdoc/>
    public bool Acknowledge(string alarmId, string? acknowledgedBy = null, string? remarks = null)
    {
        if (!_activeAlarms.TryGetValue(alarmId, out var alarm))
        {
            return false;
        }

        if (alarm.Status != AlarmStatus.Active)
        {
            return false;
        }

        alarm.Status = AlarmStatus.Acknowledged;
        alarm.AcknowledgeTime = DateTime.Now;
        alarm.AcknowledgedBy = acknowledgedBy;
        alarm.Remarks = remarks;

        AlarmStatusChanged?.Invoke(this, new AlarmEventArgs(alarm, AlarmEventType.Acknowledged));

        // 如果没有活动报警，停止声音
        if (!_activeAlarms.Values.Any(a => a.Status == AlarmStatus.Active))
        {
            StopSound();
        }

        return true;
    }

    /// <inheritdoc/>
    public int AcknowledgeRange(IEnumerable<string> alarmIds, string? acknowledgedBy = null)
    {
        return alarmIds.Count(id => Acknowledge(id, acknowledgedBy));
    }

    /// <inheritdoc/>
    public int AcknowledgeAll(string? acknowledgedBy = null)
    {
        var activeIds = _activeAlarms.Values
            .Where(a => a.Status == AlarmStatus.Active)
            .Select(a => a.Id)
            .ToList();

        return AcknowledgeRange(activeIds, acknowledgedBy);
    }

    /// <inheritdoc/>
    public bool Recover(string alarmId)
    {
        if (!_activeAlarms.TryGetValue(alarmId, out var alarm))
        {
            return false;
        }

        if (alarm.Status == AlarmStatus.Recovered || alarm.Status == AlarmStatus.Closed)
        {
            return false;
        }

        alarm.Status = AlarmStatus.Recovered;
        alarm.RecoveryTime = DateTime.Now;

        _activeAlarms.TryRemove(alarmId, out _);

        AlarmStatusChanged?.Invoke(this, new AlarmEventArgs(alarm, AlarmEventType.Recovered));

        // 如果没有活动报警，停止声音
        if (!_activeAlarms.Values.Any(a => a.Status == AlarmStatus.Active))
        {
            StopSound();
        }

        return true;
    }

    /// <inheritdoc/>
    public int RecoverByCode(string code)
    {
        var matchingIds = _activeAlarms.Values
            .Where(a => a.Code == code && a.Status != AlarmStatus.Recovered && a.Status != AlarmStatus.Closed)
            .Select(a => a.Id)
            .ToList();

        return matchingIds.Count(Recover);
    }

    /// <inheritdoc/>
    public bool Close(string alarmId, string? remarks = null)
    {
        if (!_activeAlarms.TryRemove(alarmId, out var alarm))
        {
            // 尝试从历史中找
            alarm = _alarmHistory.FirstOrDefault(a => a.Id == alarmId);
            if (alarm == null) return false;
        }

        alarm.Status = AlarmStatus.Closed;
        alarm.CloseTime = DateTime.Now;
        if (!string.IsNullOrEmpty(remarks))
        {
            alarm.Remarks = remarks;
        }

        AlarmStatusChanged?.Invoke(this, new AlarmEventArgs(alarm, AlarmEventType.Closed));

        return true;
    }

    /// <inheritdoc/>
    public AlarmRecord? GetAlarm(string alarmId)
    {
        if (_activeAlarms.TryGetValue(alarmId, out var alarm))
        {
            return alarm;
        }

        return _alarmHistory.FirstOrDefault(a => a.Id == alarmId);
    }

    /// <inheritdoc/>
    public IReadOnlyList<AlarmRecord> GetActiveAlarms()
    {
        return _activeAlarms.Values
            .OrderByDescending(a => a.TriggerTime)
            .ToList();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AlarmRecord>> QueryAsync(AlarmQuery query, CancellationToken cancellationToken = default)
    {
        var results = _alarmHistory.AsEnumerable();

        if (query.StartTime.HasValue)
        {
            results = results.Where(a => a.TriggerTime >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            results = results.Where(a => a.TriggerTime <= query.EndTime.Value);
        }

        if (query.Level.HasValue)
        {
            results = results.Where(a => a.Level == query.Level.Value);
        }

        if (query.Status.HasValue)
        {
            results = results.Where(a => a.Status == query.Status.Value);
        }

        if (!string.IsNullOrEmpty(query.Source))
        {
            results = results.Where(a => a.Source.Contains(query.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query.Code))
        {
            results = results.Where(a => a.Code.Contains(query.Code, StringComparison.OrdinalIgnoreCase));
        }

        var paged = results
            .OrderByDescending(a => a.TriggerTime)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<AlarmRecord>>(paged);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AlarmRecord> QueryStreamAsync(
        AlarmQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = await QueryAsync(query, cancellationToken);
        
        foreach (var alarm in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return alarm;
        }
    }

    /// <inheritdoc/>
    public void PlaySound(AlarmLevel level)
    {
        if (!SoundEnabled) return;

        _soundLock.Wait();
        try
        {
            _soundPlaying = true;
            _currentSoundLevel = level;

            // 使用系统蜂鸣器
            if (_options.UseSystemBeep)
            {
                PlaySystemBeep(level);
            }

            // 启动循环播放
            if (_options.LoopSound)
            {
                _soundLoopTimer?.Change(
                    TimeSpan.FromSeconds(_options.SoundLoopIntervalSeconds),
                    TimeSpan.FromSeconds(_options.SoundLoopIntervalSeconds));
            }
        }
        finally
        {
            _soundLock.Release();
        }
    }

    /// <inheritdoc/>
    public void StopSound()
    {
        _soundLock.Wait();
        try
        {
            _soundPlaying = false;
            _soundLoopTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        finally
        {
            _soundLock.Release();
        }
    }

    /// <inheritdoc/>
    public AlarmStatistics GetStatistics(DateTime? startTime = null, DateTime? endTime = null)
    {
        var alarms = _alarmHistory.AsEnumerable();

        if (startTime.HasValue)
        {
            alarms = alarms.Where(a => a.TriggerTime >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            alarms = alarms.Where(a => a.TriggerTime <= endTime.Value);
        }

        var alarmList = alarms.ToList();

        return new AlarmStatistics
        {
            TotalCount = alarmList.Count,
            ActiveCount = alarmList.Count(a => a.Status == AlarmStatus.Active),
            AcknowledgedCount = alarmList.Count(a => a.Status == AlarmStatus.Acknowledged),
            RecoveredCount = alarmList.Count(a => a.Status == AlarmStatus.Recovered),
            ClosedCount = alarmList.Count(a => a.Status == AlarmStatus.Closed),
            CountByLevel = alarmList
                .GroupBy(a => a.Level)
                .ToDictionary(g => g.Key, g => g.Count()),
            CountBySource = alarmList
                .GroupBy(a => a.Source)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <inheritdoc/>
    public int ClearHistory(DateTime beforeTime)
    {
        var count = 0;
        var newHistory = new ConcurrentQueue<AlarmRecord>();

        while (_alarmHistory.TryDequeue(out var alarm))
        {
            if (alarm.TriggerTime >= beforeTime)
            {
                newHistory.Enqueue(alarm);
            }
            else
            {
                count++;
            }
        }

        // 重新添加保留的记录
        foreach (var alarm in newHistory)
        {
            _alarmHistory.Enqueue(alarm);
        }

        return count;
    }

    private void PlaySystemBeep(AlarmLevel level)
    {
        try
        {
            var (frequency, duration) = level switch
            {
                AlarmLevel.Info => (800, 200),
                AlarmLevel.Warning => (1000, 300),
                AlarmLevel.Alarm => (1200, 500),
                AlarmLevel.Critical => (1500, 700),
                AlarmLevel.Emergency => (2000, 1000),
                _ => (1000, 300)
            };

            // 使用Console.Beep (仅Windows)
            if (OperatingSystem.IsWindows())
            {
                Console.Beep(frequency, duration);
            }
        }
        catch
        {
            // 忽略蜂鸣器错误
        }
    }

    private void SoundLoopCallback(object? state)
    {
        if (_soundPlaying && SoundEnabled)
        {
            PlaySound(_currentSoundLevel);
        }
    }

    private void AutoAcknowledgeCallback(object? state)
    {
        var cutoffTime = DateTime.Now.AddMinutes(-_options.AutoAcknowledgeMinutes);
        var toAcknowledge = _activeAlarms.Values
            .Where(a => a.Status == AlarmStatus.Active && a.TriggerTime < cutoffTime)
            .Select(a => a.Id)
            .ToList();

        foreach (var id in toAcknowledge)
        {
            Acknowledge(id, "System", "自动确认");
        }
    }

    private void PersistCallback(object? state)
    {
        _ = PersistAllAsync();
    }

    private void TrimHistory()
    {
        var maxRecords = _options.MaxActiveAlarms * 10; // 保留活动报警10倍的历史
        while (_alarmHistory.Count > maxRecords)
        {
            _alarmHistory.TryDequeue(out _);
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!_options.PersistToFile) return;

        var dir = GetAlarmDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private string GetAlarmDirectory()
    {
        return Path.IsPathRooted(_options.AlarmDirectory)
            ? _options.AlarmDirectory
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.AlarmDirectory);
    }

    private async Task PersistAlarmAsync(AlarmRecord alarm, CancellationToken cancellationToken)
    {
        var fileName = $"alarm_{DateTime.Today:yyyyMMdd}.json";
        var filePath = Path.Combine(GetAlarmDirectory(), fileName);

        var json = JsonSerializer.Serialize(alarm, new JsonSerializerOptions { WriteIndented = false });
        await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
    }

    private async Task PersistAllAsync()
    {
        try
        {
            var fileName = $"alarms_snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(GetAlarmDirectory(), fileName);

            var data = new
            {
                Timestamp = DateTime.Now,
                ActiveAlarms = _activeAlarms.Values.ToList(),
                RecentHistory = _alarmHistory.TakeLast(1000).ToList()
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // 忽略持久化错误
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var dir = GetAlarmDirectory();
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "alarm_*.json")
                .OrderByDescending(f => f)
                .Take(7); // 最近7天

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var alarm = JsonSerializer.Deserialize<AlarmRecord>(line);
                        if (alarm != null)
                        {
                            _alarmHistory.Enqueue(alarm);
                            
                            // 恢复活动报警
                            if (alarm.Status == AlarmStatus.Active)
                            {
                                _activeAlarms[alarm.Id] = alarm;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略解析错误
                    }
                }
            }
        }
        catch
        {
            // 忽略加载错误
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _soundLoopTimer?.Dispose();
            _autoAcknowledgeTimer?.Dispose();
            _persistTimer?.Dispose();
            _soundLock.Dispose();

            // 最终持久化
            if (_options.PersistToFile)
            {
                PersistAllAsync().GetAwaiter().GetResult();
            }
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        _soundLoopTimer?.Dispose();
        _autoAcknowledgeTimer?.Dispose();
        _persistTimer?.Dispose();

        if (_options.PersistToFile)
        {
            await PersistAllAsync();
        }
    }
}
