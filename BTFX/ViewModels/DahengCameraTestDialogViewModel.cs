using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BTFX.Models.Camera;
using BTFX.Services.Implementations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GxIAPINET;

namespace BTFX.ViewModels;

public partial class DahengCameraTestDialogViewModel : ObservableObject, IDisposable
{
    private readonly DahengCameraRuntime _runtime;
    private readonly CameraSlot _sideCamera;
    private readonly CameraSlot _frontCamera;
    private CancellationTokenSource? _recordingCancellation;

    public DahengCameraTestDialogViewModel(DahengCameraRuntime runtime)
    {
        _runtime = runtime;
        _sideCamera = new CameraSlot("侧面相机", AppendLog);
        _frontCamera = new CameraSlot("正面相机", AppendLog);
    }

    public CameraSlot SideCamera => _sideCamera;

    public CameraSlot FrontCamera => _frontCamera;

    public ObservableCollection<DahengCameraDeviceInfo> Devices { get; } = new();

    public ObservableCollection<string> ResolutionOptions { get; } = new()
    {
        "2048x1536",
        "1920x1080",
        "1280x720",
        "640x480"
    };

    public ObservableCollection<int> FrameRateOptions { get; } = new()
    {
        30,
        60,
        90,
        120
    };

    public ObservableCollection<int> DurationOptions { get; } = new()
    {
        5,
        10,
        20,
        30
    };

    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty]
    private bool _isDualMode = true;

    [ObservableProperty]
    private bool _autoLoadUserSet0 = true;

    [ObservableProperty]
    private bool _previewDuringRecording;

    [ObservableProperty]
    private bool _restartStreamBeforeRecording;

    [ObservableProperty]
    private string _selectedResolution = "1920x1080";

    [ObservableProperty]
    private int _selectedFrameRate = 120;

    [ObservableProperty]
    private int _selectedDurationSeconds = 10;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private double _recordingProgress;

    [ObservableProperty]
    private string _recordingStatusText = "未录制";

    [ObservableProperty]
    private string _lastRecordingPath = "--";

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var devices = _runtime.Execute(factory =>
                {
                    var foundDevices = new List<IGXDeviceInfo>();
                    factory.UpdateAllDeviceList(300, foundDevices);
                    return foundDevices;
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Devices.Clear();
                    foreach (var device in devices)
                    {
                        Devices.Add(new DahengCameraDeviceInfo(
                            Safe(device.GetDisplayName()),
                            Safe(device.GetSN()),
                            Safe(device.GetUserID()),
                            Safe(device.GetModelName()),
                            Safe(device.GetVendorName())));
                    }

                    SideCamera.SelectedDevice = Devices.FirstOrDefault();
                    FrontCamera.SelectedDevice = Devices.Skip(1).FirstOrDefault();
                    AppendLog(Devices.Count > 0 ? $"发现 {Devices.Count} 台大恒相机。" : "未发现大恒相机。");
                });
            }
            catch (Exception ex)
            {
                DispatchError("枚举设备失败", ex);
            }
        });
    }

    [RelayCommand]
    private async Task OpenSelectedCamerasAsync()
    {
        if (IsRecording)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                _runtime.Execute(factory =>
                {
                    SideCamera.Open(
                        factory,
                        SideCamera.SelectedDevice,
                        SelectedResolution,
                        SelectedFrameRate,
                        AutoLoadUserSet0);

                    if (IsDualMode)
                    {
                        FrontCamera.Open(
                            factory,
                            FrontCamera.SelectedDevice,
                            SelectedResolution,
                            SelectedFrameRate,
                            AutoLoadUserSet0);
                    }
                });
            }
            catch (Exception ex)
            {
                DispatchError("打开相机失败", ex);
            }
        });
    }

    [RelayCommand]
    private void CloseCameras()
    {
        if (IsRecording)
        {
            return;
        }

        _runtime.Execute(_ =>
        {
            SideCamera.Close();
            FrontCamera.Close();
        });
        RecordingStatusText = "未录制";
        RecordingProgress = 0;
        AppendLog("已关闭相机。");
    }

    [RelayCommand]
    private void StartPreview()
    {
        try
        {
            foreach (var slot in ActiveSlots())
            {
                slot.StartPreview();
            }

            AppendLog("预览已启动。双相机测试时预览会降频显示，以优先保证采集与写盘。");
        }
        catch (Exception ex)
        {
            DispatchError("启动预览失败", ex);
        }
    }

    [RelayCommand]
    private void StopPreview()
    {
        if (IsRecording)
        {
            return;
        }

        foreach (var slot in ActiveSlots())
        {
            slot.StopPreview();
        }

        AppendLog("预览已停止。");
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (IsRecording)
        {
            return;
        }

        var activeSlots = ActiveSlots().ToList();
        if (activeSlots.Count == 0 || activeSlots.Any(slot => !slot.IsOpen))
        {
            AppendLog("录制失败：请先打开需要录制的相机。");
            return;
        }

        var ffmpegPath = FfmpegPath;
        if (!File.Exists(ffmpegPath))
        {
            AppendLog($"录制失败：未找到 ffmpeg.exe，路径 {ffmpegPath}");
            return;
        }

        try
        {
            Directory.CreateDirectory(SaveDirectory);
            if (RestartStreamBeforeRecording)
            {
                foreach (var slot in activeSlots)
                {
                    slot.StopPreview();
                }
            }
            else
            {
                foreach (var slot in activeSlots)
                {
                    if (!slot.IsPreviewing)
                    {
                        slot.StartPreview();
                    }
                }
            }

            _recordingCancellation?.Dispose();
            _recordingCancellation = new CancellationTokenSource();
            var targetFrameCount = Math.Max(1, SelectedFrameRate * Math.Max(1, SelectedDurationSeconds));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var recordingStartAt = DateTimeOffset.UtcNow.AddMilliseconds(RestartStreamBeforeRecording ? 800 : 180);
            var recordingEndAt = recordingStartAt.AddSeconds(SelectedDurationSeconds);
            if (RestartStreamBeforeRecording)
            {
                foreach (var slot in activeSlots)
                {
                    slot.ResetTimestamp();
                }
            }

            foreach (var slot in activeSlots)
            {
                await slot.PrepareRecordingAsync(
                    ffmpegPath,
                    SaveDirectory,
                    timestamp,
                    SelectedFrameRate,
                    targetFrameCount,
                    PreviewDuringRecording,
                    _recordingCancellation.Token);
            }

            foreach (var slot in activeSlots)
            {
                slot.BeginRecording(recordingStartAt, recordingEndAt);
            }

            if (RestartStreamBeforeRecording)
            {
                foreach (var slot in activeSlots)
                {
                    slot.StartPreview();
                }
            }

            IsRecording = true;
            RecordingProgress = 0;
            RecordingStatusText = $"录制中 0 / {targetFrameCount} 帧";
            AppendLog($"开始同步录制：{activeSlots.Count} 路，{SelectedResolution}@{SelectedFrameRate}fps，目标 {targetFrameCount} 帧。");
            await TrackRecordingProgressAsync(activeSlots, targetFrameCount, recordingEndAt, _recordingCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("录制已停止。");
        }
        catch (Exception ex)
        {
            AppendLog($"录制失败：{ex.Message}");
        }
        finally
        {
            await StopRecordingCoreAsync(activeSlots, cancelWriters: false);
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        await StopRecordingCoreAsync(ActiveSlots().ToList(), cancelWriters: true);
    }

    private async Task TrackRecordingProgressAsync(
        IReadOnlyList<CameraSlot> slots,
        int targetFrameCount,
        DateTimeOffset recordingEndAt,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Max(SelectedDurationSeconds + 8, SelectedDurationSeconds * 1.8);
        var startedAt = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
            var minQueuedFrames = slots.Min(slot => slot.QueuedFrameCount);
            var status = string.Join("，", slots.Select(slot => $"{slot.Name}:{slot.QueuedFrameCount}/{targetFrameCount}"));
            RecordingProgress = Math.Clamp(minQueuedFrames / Math.Max(1.0, targetFrameCount) * 100.0, 0, 100);
            RecordingStatusText = $"录制中 {status}，已用时 {elapsed:0.0}s";

            if (slots.All(slot => slot.QueuedFrameCount >= targetFrameCount))
            {
                break;
            }

            if (DateTimeOffset.UtcNow >= recordingEndAt.AddMilliseconds(300))
            {
                break;
            }

            if (elapsed >= timeoutSeconds)
            {
                AppendLog($"录制超时：{status}。请检查曝光时间、USB带宽、分辨率和写盘速度。");
                break;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private async Task StopRecordingCoreAsync(IReadOnlyList<CameraSlot> slots, bool cancelWriters)
    {
        if (!IsRecording && slots.All(slot => !slot.IsRecording))
        {
            return;
        }

        IsRecording = false;
        if (cancelWriters)
        {
            _recordingCancellation?.Cancel();
        }

        foreach (var slot in slots)
        {
            await slot.StopRecordingAsync(fillRemaining: !cancelWriters);
        }

        _recordingCancellation?.Dispose();
        _recordingCancellation = null;
        RecordingProgress = 100;
        LastRecordingPath = string.Join(" | ", slots.Where(slot => !string.IsNullOrWhiteSpace(slot.LastRecordingPath)).Select(slot => slot.LastRecordingPath));
        RecordingStatusText = "录制完成";
        AppendLog("同步录制结束。");
    }

    private IEnumerable<CameraSlot> ActiveSlots()
    {
        yield return SideCamera;
        if (IsDualMode)
        {
            yield return FrontCamera;
        }
    }

    private void DispatchError(string prefix, Exception ex)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            AppendLog($"{prefix}: {ex.Message}");
        });
    }

    private void AppendLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (LogLines.Count > 300)
            {
                LogLines.RemoveAt(0);
            }
        });
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value;

    private static string FfmpegPath => Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    private static string SaveDirectory => Path.Combine(AppContext.BaseDirectory, "video", "daheng-test");

    public void Dispose()
    {
        _recordingCancellation?.Cancel();
        _runtime.Execute(_ =>
        {
            SideCamera.Dispose();
            FrontCamera.Dispose();
        });

        _recordingCancellation?.Dispose();
        _recordingCancellation = null;
    }

    public sealed partial class CameraSlot : ObservableObject, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly Action<string> _appendLog;
        private IGXDevice? _device;
        private IGXStream? _stream;
        private IGXFeatureControl? _featureControl;
        private IGXFeatureControl? _streamFeatureControl;
        private IGXImageFormatConvert? _formatConvert;
        private IntPtr _convertBuffer = IntPtr.Zero;
        private ulong _convertBufferSize;
        private Process? _recordingProcess;
        private Channel<byte[]>? _recordingChannel;
        private Task? _recordingWriterTask;
        private long _queuedFrameCount;
        private long _writtenFrameCount;
        private long _captureFrameCount;
        private long _skippedFrameCount;
        private long _duplicatedFrameCount;
        private int _recordingFrameRate;
        private int _recordingTargetFrameCount;
        private int _nextRecordingFrameIndex;
        private DateTimeOffset _recordingStartAt;
        private DateTimeOffset _recordingEndAt;
        private bool _previewDuringRecording;
        private long _timestampTickFrequency;
        private ulong? _firstRecordingTimestamp;
        private ulong _firstRecordingFrameId;
        private ulong _lastRecordingFrameId;
        private ulong _lastRecordingTimestamp;
        private byte[]? _lastRecordingFrameBytes;
        private DateTimeOffset _lastPreviewFrameAt = DateTimeOffset.MinValue;

        public CameraSlot(string name, Action<string> appendLog)
        {
            Name = name;
            _appendLog = appendLog;
            StatusText = "未连接";
            ParameterSummary = "--";
        }

        public string Name { get; }

        [ObservableProperty]
        private DahengCameraDeviceInfo? _selectedDevice;

        [ObservableProperty]
        private bool _isOpen;

        [ObservableProperty]
        private bool _isPreviewing;

        [ObservableProperty]
        private bool _isRecording;

        [ObservableProperty]
        private ImageSource? _previewImage;

        [ObservableProperty]
        private string _statusText;

        [ObservableProperty]
        private string _parameterSummary;

        [ObservableProperty]
        private string? _lastRecordingPath;

        [ObservableProperty]
        private CameraOrientation _orientation = CameraOrientation.Landscape;

        [ObservableProperty]
        private bool _flipVertical;

        public long QueuedFrameCount => Interlocked.Read(ref _queuedFrameCount);

        public long WrittenFrameCount => Interlocked.Read(ref _writtenFrameCount);

        public long CaptureFrameCount => Interlocked.Read(ref _captureFrameCount);

        public long SkippedFrameCount => Interlocked.Read(ref _skippedFrameCount);

        public long DuplicatedFrameCount => Interlocked.Read(ref _duplicatedFrameCount);

        public void Open(IGXFactory factory, DahengCameraDeviceInfo? deviceInfo, string resolution, int frameRate, bool autoLoadUserSet0)
        {
            if (deviceInfo is null)
            {
                throw new InvalidOperationException($"{Name}未选择设备。");
            }

            Close();
            _device = factory.OpenDeviceBySN(deviceInfo.SerialNumber, GX_ACCESS_MODE.GX_ACCESS_EXCLUSIVE);
            _featureControl = _device.GetRemoteFeatureControl();
            _stream = _device.OpenStream(0);
            _streamFeatureControl = _stream.GetFeatureControl();
            _formatConvert = factory.CreateImageFormatConvert();

            ConfigureDevice(resolution, frameRate, autoLoadUserSet0);
            ParameterSummary = ReadCameraParameterSummary();
            IsOpen = true;
            StatusText = $"已打开：{deviceInfo.ModelName} / SN {deviceInfo.SerialNumber}";
            _appendLog($"{Name} {StatusText}");
        }

        public void Close()
        {
            if (IsRecording)
            {
                StopRecordingAsync(fillRemaining: false).GetAwaiter().GetResult();
            }

            StopPreview();

            try
            {
                _stream?.Close();
            }
            catch
            {
            }

            try
            {
                _device?.Close();
            }
            catch
            {
            }

            _stream = null;
            _streamFeatureControl = null;
            _device = null;
            _featureControl = null;
            _formatConvert = null;
            ReleaseConvertBuffer();
            IsOpen = false;
            IsPreviewing = false;
            PreviewImage = null;
            StatusText = "未连接";
            ParameterSummary = "--";
        }

        public void StartPreview()
        {
            if (!IsOpen || IsPreviewing || _stream is null || _featureControl is null)
            {
                return;
            }

            if (_streamFeatureControl is not null)
            {
                TrySetEnum(_streamFeatureControl, "StreamBufferHandlingMode", "OldestFirst");
            }

            _stream.RegisterCaptureCallback(this, OnFrameCallback);
            _stream.StartGrab();
            _featureControl.GetCommandFeature("AcquisitionStart").Execute();
            IsPreviewing = true;
            StatusText = "预览中";
            _appendLog($"{Name} 预览已启动。");
        }

        public void StopPreview()
        {
            try
            {
                _featureControl?.GetCommandFeature("AcquisitionStop").Execute();
            }
            catch
            {
            }

            try
            {
                _stream?.StopGrab();
                _stream?.UnregisterCaptureCallback();
            }
            catch
            {
            }

            IsPreviewing = false;
            if (IsOpen)
            {
                StatusText = "已打开";
            }
        }

        public Task PrepareRecordingAsync(
            string ffmpegPath,
            string saveDirectory,
            string timestamp,
            int frameRate,
            int targetFrameCount,
            bool previewDuringRecording,
            CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"{Name}未开始预览，无法录制。");
            }

            _recordingFrameRate = Math.Max(1, frameRate);
            _recordingTargetFrameCount = Math.Max(1, targetFrameCount);
            _previewDuringRecording = previewDuringRecording;
            _timestampTickFrequency = TryReadInt(_featureControl, "TimestampTickFrequency", 0);
            var sourceWidth = (int)TryReadInt(_featureControl, "Width", 1920);
            var sourceHeight = (int)TryReadInt(_featureControl, "Height", 1080);
            var width = Orientation == CameraOrientation.PortraitClockwise ? sourceHeight : sourceWidth;
            var height = Orientation == CameraOrientation.PortraitClockwise ? sourceWidth : sourceHeight;
            var safeTag = SafeFileName(SelectedDevice?.SerialNumber ?? Name);
            LastRecordingPath = Path.Combine(saveDirectory, $"{timestamp}_Daheng_{Name}_{safeTag}.mp4");

            Interlocked.Exchange(ref _queuedFrameCount, 0);
            Interlocked.Exchange(ref _writtenFrameCount, 0);
            Interlocked.Exchange(ref _captureFrameCount, 0);
            Interlocked.Exchange(ref _skippedFrameCount, 0);
            Interlocked.Exchange(ref _duplicatedFrameCount, 0);
            _nextRecordingFrameIndex = 0;
            _firstRecordingTimestamp = null;
            _firstRecordingFrameId = 0;
            _lastRecordingFrameId = 0;
            _lastRecordingTimestamp = 0;
            _lastRecordingFrameBytes = null;
            _recordingChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _recordingProcess = StartFfmpegRecordingProcess(
                ffmpegPath,
                LastRecordingPath,
                sourceWidth,
                sourceHeight,
                _recordingFrameRate,
                Orientation,
                FlipVertical);
            _recordingWriterTask = Task.Run(() => WriteRecordingFramesAsync(_recordingProcess, _recordingChannel, cancellationToken), cancellationToken);
            StatusText = "等待同步开始";
            _appendLog($"{Name} 录制管道已准备：{LastRecordingPath}，{width}x{height}@{_recordingFrameRate}fps。");
            return Task.CompletedTask;
        }

        public void ResetTimestamp()
        {
            try
            {
                if (_featureControl is not null
                    && _featureControl.IsImplemented("TimestampReset")
                    && _featureControl.IsWritable("TimestampReset"))
                {
                    _featureControl.GetCommandFeature("TimestampReset").Execute();
                    _appendLog($"{Name} 已重置相机时间戳。");
                }
            }
            catch (Exception ex)
            {
                _appendLog($"{Name} 重置时间戳失败：{ex.Message}");
            }
        }

        public void BeginRecording(DateTimeOffset recordingStartAt, DateTimeOffset recordingEndAt)
        {
            if (_recordingProcess is null || _recordingChannel is null)
            {
                return;
            }

            _recordingStartAt = recordingStartAt;
            _recordingEndAt = recordingEndAt;
            IsRecording = true;
            StatusText = "录制中";
        }

        public async Task StopRecordingAsync(bool fillRemaining = true)
        {
            Process? process = _recordingProcess;
            Channel<byte[]>? channel = _recordingChannel;
            Task? writerTask = _recordingWriterTask;
            if (fillRemaining)
            {
                FillRemainingTimelineFrames();
            }
            _recordingProcess = null;
            _recordingChannel = null;
            _recordingWriterTask = null;
            IsRecording = false;

            try
            {
                channel?.Writer.TryComplete();
                if (writerTask is not null)
                {
                    await writerTask.WaitAsync(TimeSpan.FromSeconds(20));
                }
            }
            catch
            {
            }

            try
            {
                if (process is not null && !process.HasExited)
                {
                    await process.StandardInput.BaseStream.FlushAsync();
                    process.StandardInput.Close();
                    await process.WaitForExitAsync();
                }
            }
            catch
            {
                TryKill(process);
            }
            finally
            {
                process?.Dispose();
            }

            var queued = QueuedFrameCount;
            var written = WrittenFrameCount;
            var duration = _recordingFrameRate > 0 ? written / (double)_recordingFrameRate : 0;
            StatusText = IsOpen ? "已打开" : "未连接";
            _appendLog($"{Name} 录制统计：目标 {_recordingTargetFrameCount} 帧，入队 {queued} 帧，写入 {written} 帧，估算时长 {duration:0.00}s。");
            _appendLog($"{Name} 时间轴统计：相机回调 {CaptureFrameCount} 帧，跳过 {SkippedFrameCount} 帧，补帧 {DuplicatedFrameCount} 帧。");
            if (_lastRecordingFrameId > 0 || _lastRecordingTimestamp > 0)
            {
                _appendLog($"{Name} 帧标识统计：FrameID {_firstRecordingFrameId} -> {_lastRecordingFrameId}，Timestamp {_firstRecordingTimestamp.GetValueOrDefault()} -> {_lastRecordingTimestamp}，TickFrequency {_timestampTickFrequency}。");
            }

            if (written < _recordingTargetFrameCount)
            {
                _appendLog($"{Name} 警告：写入帧数不足，可能存在带宽、曝光时间、编码或磁盘写入瓶颈。");
            }
        }

        private void ConfigureDevice(string resolution, int frameRate, bool autoLoadUserSet0)
        {
            if (_featureControl is null)
            {
                return;
            }

            if (autoLoadUserSet0)
            {
                TryLoadUserSet0(_featureControl);
            }

            TrySetEnum(_featureControl, "AcquisitionMode", "Continuous");
            TrySetEnum(_featureControl, "TriggerMode", "Off");

            var (width, height) = ParseResolution(resolution);
            TrySetInt(_featureControl, "Width", width);
            TrySetInt(_featureControl, "Height", height);

            TrySetBool(_featureControl, "AcquisitionFrameRateEnable", true);
            TrySetEnum(_featureControl, "AcquisitionFrameRateMode", "On");
            TrySetFloat(_featureControl, "AcquisitionFrameRate", frameRate);
        }

        private void TryLoadUserSet0(IGXFeatureControl featureControl)
        {
            try
            {
                if (!featureControl.IsImplemented("UserSetSelector")
                    || !featureControl.IsWritable("UserSetSelector")
                    || !featureControl.IsImplemented("UserSetLoad"))
                {
                    _appendLog($"{Name} 不支持 UserSet0 自动加载。");
                    return;
                }

                featureControl.GetEnumFeature("UserSetSelector").SetValue("UserSet0");
                featureControl.GetCommandFeature("UserSetLoad").Execute();
                _appendLog($"{Name} 已加载 UserSet0。");
            }
            catch (Exception ex)
            {
                _appendLog($"{Name} 加载 UserSet0 失败：{ex.Message}");
            }
        }

        private string ReadCameraParameterSummary()
        {
            if (_featureControl is null)
            {
                return "--";
            }

            var exposure = TryReadFloat(_featureControl, "ExposureTime", "0.##");
            var gain = TryReadFloat(_featureControl, "Gain", "0.##");
            var pixelFormat = TryReadEnum(_featureControl, "PixelFormat");
            var frameRate = TryReadFloat(_featureControl, "AcquisitionFrameRate", "0.##");
            return $"Exposure={exposure}, Gain={gain}, PixelFormat={pixelFormat}, FPS={frameRate}";
        }

        private void OnFrameCallback(object userParam, IFrameData frameData)
        {
            if (frameData.GetStatus() != GX_FRAME_STATUS_LIST.GX_FRAME_STATUS_SUCCESS)
            {
                return;
            }

            try
            {
                var now = DateTimeOffset.UtcNow;
                var needsPreview = (!IsRecording || _previewDuringRecording)
                                   && now - _lastPreviewFrameAt >= TimeSpan.FromMilliseconds(IsRecording ? 250 : 66);
                if (!IsRecording && !needsPreview)
                {
                    return;
                }

                var frameTiming = ReadFrameTiming(frameData, now);
                var frame = ConvertFrameToBgrFrame(frameData);
                if (IsRecording)
                {
                    Interlocked.Increment(ref _captureFrameCount);
                    TryQueueRecordingFrameByTimeline(frame.Bytes, frameTiming);
                }

                if (!needsPreview)
                {
                    return;
                }

                _lastPreviewFrameAt = now;
                var image = CreateBitmapSource(ApplyTransform(frame));
                Application.Current.Dispatcher.BeginInvoke(() => PreviewImage = image);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(() => _appendLog($"{Name} 帧处理失败：{ex.Message}"));
            }
        }

        private BgrFrame ConvertFrameToBgrFrame(IFrameData frameData)
        {
            lock (_syncRoot)
            {
                if (_formatConvert is null)
                {
                    throw new InvalidOperationException("图像格式转换器未初始化。");
                }

                _formatConvert.SetDstFormat(GX_PIXEL_FORMAT_ENTRY.GX_PIXEL_FORMAT_BGR8);
                _formatConvert.SetValidBits(GX_VALID_BIT_LIST.GX_BIT_0_7);
                var requiredSize = _formatConvert.GetBufferSizeForConversion(frameData);
                EnsureConvertBuffer(requiredSize);
                _formatConvert.Convert(frameData, _convertBuffer, requiredSize, false);

                var width = (int)frameData.GetWidth();
                var height = (int)frameData.GetHeight();
                var stride = checked(width * 3);
                var bytes = new byte[checked(stride * height)];
                Marshal.Copy(_convertBuffer, bytes, 0, bytes.Length);
                return new BgrFrame(bytes, width, height, stride);
            }
        }

        private BgrFrame ApplyTransform(BgrFrame frame)
        {
            var transformed = Orientation == CameraOrientation.PortraitClockwise
                ? RotateClockwise(frame)
                : frame;

            return FlipVertical ? FlipFrameVertical(transformed) : transformed;
        }

        private static BgrFrame RotateClockwise(BgrFrame frame)
        {
            var outputWidth = frame.Height;
            var outputHeight = frame.Width;
            var outputStride = outputWidth * 3;
            var output = new byte[checked(outputStride * outputHeight)];

            for (var y = 0; y < frame.Height; y++)
            {
                var sourceRow = y * frame.Stride;
                for (var x = 0; x < frame.Width; x++)
                {
                    var sourceIndex = sourceRow + x * 3;
                    var targetX = frame.Height - 1 - y;
                    var targetY = x;
                    var targetIndex = targetY * outputStride + targetX * 3;
                    output[targetIndex] = frame.Bytes[sourceIndex];
                    output[targetIndex + 1] = frame.Bytes[sourceIndex + 1];
                    output[targetIndex + 2] = frame.Bytes[sourceIndex + 2];
                }
            }

            return new BgrFrame(output, outputWidth, outputHeight, outputStride);
        }

        private static BgrFrame FlipFrameVertical(BgrFrame frame)
        {
            var output = new byte[frame.Bytes.Length];
            for (var y = 0; y < frame.Height; y++)
            {
                var sourceOffset = y * frame.Stride;
                var targetOffset = (frame.Height - 1 - y) * frame.Stride;
                Buffer.BlockCopy(frame.Bytes, sourceOffset, output, targetOffset, frame.Stride);
            }

            return new BgrFrame(output, frame.Width, frame.Height, frame.Stride);
        }

        private FrameTiming ReadFrameTiming(IFrameData frameData, DateTimeOffset callbackAt)
        {
            ulong frameId = 0;
            ulong timestamp = 0;

            try
            {
                frameId = frameData.GetFrameID();
            }
            catch
            {
            }

            try
            {
                timestamp = frameData.GetTimeStamp();
            }
            catch
            {
            }

            return new FrameTiming(frameId, timestamp, callbackAt);
        }

        private void TryQueueRecordingFrameByTimeline(byte[] bytes, FrameTiming timing)
        {
            if (!IsRecording || _recordingChannel is null || _recordingFrameRate <= 0)
            {
                return;
            }

            _lastRecordingFrameBytes = bytes;
            _lastRecordingFrameId = timing.FrameId;
            _lastRecordingTimestamp = timing.Timestamp;

            var elapsedSeconds = ResolveRecordingElapsedSeconds(timing);
            if (elapsedSeconds < 0)
            {
                return;
            }

            var desiredFrameIndex = (int)Math.Floor(elapsedSeconds * _recordingFrameRate);
            if (desiredFrameIndex >= _recordingTargetFrameCount)
            {
                desiredFrameIndex = _recordingTargetFrameCount - 1;
            }

            if (desiredFrameIndex < _nextRecordingFrameIndex)
            {
                Interlocked.Increment(ref _skippedFrameCount);
                return;
            }

            while (_nextRecordingFrameIndex <= desiredFrameIndex
                   && _nextRecordingFrameIndex < _recordingTargetFrameCount)
            {
                if (_nextRecordingFrameIndex < desiredFrameIndex)
                {
                    Interlocked.Increment(ref _duplicatedFrameCount);
                }

                TryQueueRecordingFrame(bytes);
                _nextRecordingFrameIndex++;
            }
        }

        private double ResolveRecordingElapsedSeconds(FrameTiming timing)
        {
            if (timing.CallbackAt < _recordingStartAt)
            {
                return -1;
            }

            if (_firstRecordingTimestamp is null)
            {
                _firstRecordingTimestamp = timing.Timestamp;
                _firstRecordingFrameId = timing.FrameId;
            }

            return Math.Max(0, (timing.CallbackAt - _recordingStartAt).TotalSeconds);
        }

        private void FillRemainingTimelineFrames()
        {
            if (_recordingChannel is null
                || _lastRecordingFrameBytes is null
                || _nextRecordingFrameIndex >= _recordingTargetFrameCount)
            {
                return;
            }

            while (_nextRecordingFrameIndex < _recordingTargetFrameCount)
            {
                TryQueueRecordingFrame(_lastRecordingFrameBytes);
                _nextRecordingFrameIndex++;
                Interlocked.Increment(ref _duplicatedFrameCount);
            }
        }

        private void TryQueueRecordingFrame(byte[] bytes)
        {
            if (!IsRecording || _recordingChannel is null)
            {
                return;
            }

            if (_recordingChannel.Writer.TryWrite(bytes))
            {
                Interlocked.Increment(ref _queuedFrameCount);
            }
        }

        private async Task WriteRecordingFramesAsync(Process process, Channel<byte[]> channel, CancellationToken cancellationToken)
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (process.HasExited)
                {
                    break;
                }

                await process.StandardInput.BaseStream.WriteAsync(frame, cancellationToken);
                Interlocked.Increment(ref _writtenFrameCount);
            }
        }

        private void EnsureConvertBuffer(ulong requiredSize)
        {
            if (_convertBuffer != IntPtr.Zero && _convertBufferSize >= requiredSize)
            {
                return;
            }

            ReleaseConvertBuffer();
            _convertBuffer = Marshal.AllocCoTaskMem(checked((int)requiredSize));
            _convertBufferSize = requiredSize;
        }

        private void ReleaseConvertBuffer()
        {
            if (_convertBuffer == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeCoTaskMem(_convertBuffer);
            _convertBuffer = IntPtr.Zero;
            _convertBufferSize = 0;
        }

        public void Dispose()
        {
            Close();
        }
    }

    private sealed record BgrFrame(byte[] Bytes, int Width, int Height, int Stride);

    private readonly record struct FrameTiming(ulong FrameId, ulong Timestamp, DateTimeOffset CallbackAt);

    private static BitmapSource CreateBitmapSource(BgrFrame frame)
    {
        var source = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Bgr24,
            null,
            frame.Bytes,
            frame.Stride);
        source.Freeze();
        return source;
    }

    private static Process StartFfmpegRecordingProcess(
        string ffmpegPath,
        string outputPath,
        int width,
        int height,
        int frameRate,
        CameraOrientation orientation,
        bool flipVertical)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add("-y");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("rawvideo");
        process.StartInfo.ArgumentList.Add("-pix_fmt");
        process.StartInfo.ArgumentList.Add("bgr24");
        process.StartInfo.ArgumentList.Add("-video_size");
        process.StartInfo.ArgumentList.Add($"{width}x{height}");
        process.StartInfo.ArgumentList.Add("-framerate");
        process.StartInfo.ArgumentList.Add(frameRate.ToString());
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add("-");
        process.StartInfo.ArgumentList.Add("-an");
        process.StartInfo.ArgumentList.Add("-c:v");
        process.StartInfo.ArgumentList.Add("libx264");
        process.StartInfo.ArgumentList.Add("-preset");
        process.StartInfo.ArgumentList.Add("ultrafast");
        process.StartInfo.ArgumentList.Add("-crf");
        process.StartInfo.ArgumentList.Add("20");
        var videoFilter = BuildRecordingVideoFilter(orientation, flipVertical);
        if (!string.IsNullOrWhiteSpace(videoFilter))
        {
            process.StartInfo.ArgumentList.Add("-vf");
            process.StartInfo.ArgumentList.Add(videoFilter);
        }
        process.StartInfo.ArgumentList.Add("-pix_fmt");
        process.StartInfo.ArgumentList.Add("yuv420p");
        process.StartInfo.ArgumentList.Add("-movflags");
        process.StartInfo.ArgumentList.Add("+faststart");
        process.StartInfo.ArgumentList.Add(outputPath);
        process.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                while (!process.HasExited)
                {
                    _ = await process.StandardError.ReadLineAsync();
                }
            }
            catch
            {
            }
        });

        return process;
    }

    private static string BuildRecordingVideoFilter(CameraOrientation orientation, bool flipVertical)
    {
        var filters = new List<string>();
        if (orientation == CameraOrientation.PortraitClockwise)
        {
            filters.Add("transpose=1");
        }

        if (flipVertical)
        {
            filters.Add("vflip");
        }

        return string.Join(",", filters);
    }

    private static (long Width, long Height) ParseResolution(string value)
    {
        var parts = value.Split('x', 'X');
        if (parts.Length == 2
            && long.TryParse(parts[0], out var width)
            && long.TryParse(parts[1], out var height)
            && width > 0
            && height > 0)
        {
            return (width, height);
        }

        return (1920, 1080);
    }

    private static void TrySetEnum(IGXFeatureControl featureControl, string featureName, string value)
    {
        try
        {
            if (featureControl.IsImplemented(featureName) && featureControl.IsWritable(featureName))
            {
                featureControl.GetEnumFeature(featureName).SetValue(value);
            }
        }
        catch
        {
        }
    }

    private static void TrySetInt(IGXFeatureControl featureControl, string featureName, long value)
    {
        try
        {
            if (!featureControl.IsImplemented(featureName) || !featureControl.IsWritable(featureName))
            {
                return;
            }

            var feature = featureControl.GetIntFeature(featureName);
            feature.SetValue(Math.Clamp(value, feature.GetMin(), feature.GetMax()));
        }
        catch
        {
        }
    }

    private static void TrySetFloat(IGXFeatureControl featureControl, string featureName, double value)
    {
        try
        {
            if (!featureControl.IsImplemented(featureName) || !featureControl.IsWritable(featureName))
            {
                return;
            }

            var feature = featureControl.GetFloatFeature(featureName);
            feature.SetValue(Math.Clamp(value, feature.GetMin(), feature.GetMax()));
        }
        catch
        {
        }
    }

    private static void TrySetBool(IGXFeatureControl featureControl, string featureName, bool value)
    {
        try
        {
            if (featureControl.IsImplemented(featureName) && featureControl.IsWritable(featureName))
            {
                featureControl.GetBoolFeature(featureName).SetValue(value);
            }
        }
        catch
        {
        }
    }

    private static string TryReadFloat(IGXFeatureControl featureControl, string featureName, string format)
    {
        try
        {
            return featureControl.IsImplemented(featureName) && featureControl.IsReadable(featureName)
                ? featureControl.GetFloatFeature(featureName).GetValue().ToString(format)
                : "--";
        }
        catch
        {
            return "--";
        }
    }

    private static string TryReadEnum(IGXFeatureControl featureControl, string featureName)
    {
        try
        {
            return featureControl.IsImplemented(featureName) && featureControl.IsReadable(featureName)
                ? featureControl.GetEnumFeature(featureName).GetValue()
                : "--";
        }
        catch
        {
            return "--";
        }
    }

    private static long TryReadInt(IGXFeatureControl? featureControl, string featureName, long fallback)
    {
        try
        {
            return featureControl is not null
                   && featureControl.IsImplemented(featureName)
                   && featureControl.IsReadable(featureName)
                ? featureControl.GetIntFeature(featureName).GetValue()
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
