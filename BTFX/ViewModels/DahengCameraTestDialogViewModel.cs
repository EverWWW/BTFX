using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BTFX.Models.Camera;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GxIAPINET;

namespace BTFX.ViewModels;

public partial class DahengCameraTestDialogViewModel : ObservableObject, IDisposable
{
    private readonly object _syncRoot = new();
    private IGXFactory? _factory;
    private IGXDevice? _device;
    private IGXStream? _stream;
    private IGXFeatureControl? _featureControl;
    private IGXFeatureControl? _streamFeatureControl;
    private IGXImageFormatConvert? _formatConvert;
    private IntPtr _convertBuffer = IntPtr.Zero;
    private ulong _convertBufferSize;
    private readonly object _recordingLock = new();
    private Process? _recordingProcess;
    private Channel<byte[]>? _recordingChannel;
    private CancellationTokenSource? _recordingCancellation;
    private Task? _recordingWriterTask;
    private DateTimeOffset _recordingStartedAt;
    private string? _recordingOutputPath;
    private int _recordingWidth = 2048;
    private int _recordingHeight = 1536;
    private int _recordingFrameRate = 30;
    private int _recordingTargetFrameCount;
    private long _recordingQueuedFrameCount;
    private long _recordingWrittenFrameCount;
    private bool _isFactoryInitialized;
    private DateTimeOffset _lastPreviewFrameAt = DateTimeOffset.MinValue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDeviceCommand))]
    private DahengCameraDeviceInfo? _selectedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartPreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopPreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    private bool _isDeviceOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartPreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopPreviewCommand))]
    private bool _isPreviewing;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private string _statusText = "未连接";

    [ObservableProperty]
    private string _cameraParameterSummary = "--";

    [ObservableProperty]
    private bool _autoLoadUserSet0 = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingCommand))]
    private bool _isRecording;

    [ObservableProperty]
    private int _selectedDurationSeconds = 10;

    [ObservableProperty]
    private double _recordingProgress;

    [ObservableProperty]
    private string _recordingStatusText = "未录制";

    [ObservableProperty]
    private string? _lastRecordingPath;

    [ObservableProperty]
    private string _selectedResolution = "2048x1536";

    [ObservableProperty]
    private int _selectedFrameRate = 90;

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

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                EnsureFactory();
                var devices = new List<IGXDeviceInfo>();
                _factory!.UpdateAllDeviceList(300, devices);

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

                    SelectedDevice = Devices.FirstOrDefault();
                    StatusText = Devices.Count > 0 ? $"发现 {Devices.Count} 台设备" : "未发现大恒相机";
                    AppendLog(StatusText);
                });
            }
            catch (Exception ex)
            {
                DispatchError("枚举设备失败", ex);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanOpenDevice))]
    private async Task OpenDeviceAsync()
    {
        var device = SelectedDevice;
        if (device is null)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                CloseDeviceCore();
                EnsureFactory();

                _device = _factory!.OpenDeviceBySN(device.SerialNumber, GX_ACCESS_MODE.GX_ACCESS_EXCLUSIVE);
                _featureControl = _device.GetRemoteFeatureControl();
                _stream = _device.OpenStream(0);
                _streamFeatureControl = _stream.GetFeatureControl();
                _formatConvert = _factory.CreateImageFormatConvert();

                ConfigureDevice();
                var parameterSummary = ReadCameraParameterSummary();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsDeviceOpen = true;
                    StatusText = $"已打开: {device.ModelName} / SN {device.SerialNumber}";
                    CameraParameterSummary = parameterSummary;
                    AppendLog(StatusText);
                });
            }
            catch (Exception ex)
            {
                CloseDeviceCore();
                DispatchError("打开设备失败", ex);
            }
        });
    }

    private bool CanOpenDevice() => SelectedDevice is not null && !IsDeviceOpen && !IsPreviewing;

    [RelayCommand(CanExecute = nameof(CanCloseDevice))]
    private void CloseDevice()
    {
        CloseDeviceCore();
        IsDeviceOpen = false;
        IsPreviewing = false;
        PreviewImage = null;
        StatusText = "已关闭";
        AppendLog(StatusText);
    }

    private bool CanCloseDevice() => IsDeviceOpen && !IsPreviewing;

    [RelayCommand(CanExecute = nameof(CanStartPreview))]
    private void StartPreview()
    {
        try
        {
            if (_streamFeatureControl is not null)
            {
                TrySetEnum(_streamFeatureControl, "StreamBufferHandlingMode", "OldestFirst");
            }

            _stream!.RegisterCaptureCallback(this, OnFrameCallback);
            _stream.StartGrab();
            _featureControl!.GetCommandFeature("AcquisitionStart").Execute();
            IsPreviewing = true;
            StatusText = "预览中";
            AppendLog(StatusText);
        }
        catch (Exception ex)
        {
            DispatchError("启动预览失败", ex);
        }
    }

    private bool CanStartPreview() => IsDeviceOpen && !IsPreviewing;

    [RelayCommand(CanExecute = nameof(CanStopPreview))]
    private void StopPreview()
    {
        StopPreviewCore();
        IsPreviewing = false;
        StatusText = "预览已停止";
        AppendLog(StatusText);
    }

    private bool CanStopPreview() => IsDeviceOpen && IsPreviewing;

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        if (!IsPreviewing)
        {
            StartPreview();
        }

        if (!IsPreviewing)
        {
            AppendLog("录制失败: 预览未启动，无法获取相机帧。");
            return;
        }

        try
        {
            Directory.CreateDirectory(SaveDirectory);
            _recordingWidth = (int)TryReadInt(_featureControl, "Width", ParseResolution(SelectedResolution).Width);
            _recordingHeight = (int)TryReadInt(_featureControl, "Height", ParseResolution(SelectedResolution).Height);
            _recordingFrameRate = Math.Max(1, (int)Math.Round(TryReadFloatValue(_featureControl, "AcquisitionFrameRate", SelectedFrameRate)));
            _recordingOutputPath = Path.Combine(
                SaveDirectory,
                $"{DateTime.Now:yyyyMMdd_HHmmss}_Daheng_{SafeFileName(SelectedDevice?.SerialNumber ?? "Camera")}.mp4");

            var ffmpegPath = FfmpegPath;
            if (!File.Exists(ffmpegPath))
            {
                AppendLog($"录制失败: 未找到 ffmpeg.exe: {ffmpegPath}");
                return;
            }

            _recordingCancellation?.Dispose();
            _recordingCancellation = new CancellationTokenSource();
            Interlocked.Exchange(ref _recordingQueuedFrameCount, 0);
            Interlocked.Exchange(ref _recordingWrittenFrameCount, 0);
            _recordingTargetFrameCount = Math.Max(1, _recordingFrameRate * Math.Max(1, SelectedDurationSeconds));
            _recordingChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            _recordingProcess = StartFfmpegRecordingProcess(ffmpegPath, _recordingOutputPath, _recordingWidth, _recordingHeight, _recordingFrameRate);
            _recordingWriterTask = Task.Run(() => WriteRecordingFramesAsync(_recordingProcess, _recordingChannel, _recordingCancellation.Token));
            RecordingProgress = 0;
            RecordingStatusText = $"录制中 0 / {_recordingTargetFrameCount} 帧";
            IsRecording = true;
            _recordingStartedAt = DateTimeOffset.UtcNow;
            AppendLog($"开始录制 MP4: {_recordingOutputPath}, {_recordingWidth}x{_recordingHeight}@{_recordingFrameRate}fps, 目标 {_recordingTargetFrameCount} 帧");

            await TrackRecordingProgressAsync(_recordingCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("录制已停止。");
        }
        catch (Exception ex)
        {
            AppendLog($"录制失败: {ex.Message}");
        }
        finally
        {
            await StopRecordingCoreAsync();
        }
    }

    private bool CanStartRecording() => IsDeviceOpen && !IsRecording;

    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private async Task StopRecordingAsync()
    {
        _recordingCancellation?.Cancel();
        await StopRecordingCoreAsync();
    }

    private bool CanStopRecording() => IsRecording;

    private void EnsureFactory()
    {
        if (_factory is null)
        {
            _factory = IGXFactory.GetInstance();
        }

        if (!_isFactoryInitialized)
        {
            _factory.Init();
            _isFactoryInitialized = true;
        }
    }

    private void ConfigureDevice()
    {
        if (_featureControl is null)
        {
            return;
        }

        if (AutoLoadUserSet0)
        {
            TryLoadUserSet0(_featureControl);
        }

        TrySetEnum(_featureControl, "AcquisitionMode", "Continuous");
        TrySetEnum(_featureControl, "TriggerMode", "Off");

        var (width, height) = ParseResolution(SelectedResolution);
        TrySetInt(_featureControl, "Width", width);
        TrySetInt(_featureControl, "Height", height);

        TrySetBool(_featureControl, "AcquisitionFrameRateEnable", true);
        TrySetEnum(_featureControl, "AcquisitionFrameRateMode", "On");
        TrySetFloat(_featureControl, "AcquisitionFrameRate", SelectedFrameRate);
    }

    private void TryLoadUserSet0(IGXFeatureControl featureControl)
    {
        try
        {
            if (!featureControl.IsImplemented("UserSetSelector")
                || !featureControl.IsWritable("UserSetSelector")
                || !featureControl.IsImplemented("UserSetLoad"))
            {
                AppendLog("当前相机不支持 UserSet0 自动加载。");
                return;
            }

            featureControl.GetEnumFeature("UserSetSelector").SetValue("UserSet0");
            featureControl.GetCommandFeature("UserSetLoad").Execute();
            AppendLog("已加载相机本体参数组 UserSet0。");
        }
        catch (Exception ex)
        {
            AppendLog($"加载 UserSet0 失败: {ex.Message}");
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
        var gamma = TryReadFloat(_featureControl, "Gamma", "0.##");
        var gammaEnable = TryReadBool(_featureControl, "GammaEnable");
        var pixelFormat = TryReadEnum(_featureControl, "PixelFormat");
        var frameRate = TryReadFloat(_featureControl, "AcquisitionFrameRate", "0.##");
        return $"Exposure={exposure}, Gain={gain}, GammaEnable={gammaEnable}, Gamma={gamma}, PixelFormat={pixelFormat}, FPS={frameRate}";
    }

    private void OnFrameCallback(object userParam, IFrameData frameData)
    {
        if (frameData.GetStatus() != GX_FRAME_STATUS_LIST.GX_FRAME_STATUS_SUCCESS)
        {
            return;
        }

        try
        {
            var frame = ConvertFrameToBgrFrame(frameData);
            TryQueueRecordingFrame(frame.Bytes);

            var now = DateTimeOffset.UtcNow;
            if (now - _lastPreviewFrameAt < TimeSpan.FromMilliseconds(33))
            {
                return;
            }

            _lastPreviewFrameAt = now;
            var image = CreateBitmapSource(frame);
            Application.Current.Dispatcher.BeginInvoke(() => PreviewImage = image);
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.BeginInvoke(() => AppendLog($"帧转换失败: {ex.Message}"));
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

    private void TryQueueRecordingFrame(byte[] bytes)
    {
        if (!IsRecording || _recordingChannel is null)
        {
            return;
        }

        if (_recordingChannel.Writer.TryWrite(bytes))
        {
            Interlocked.Increment(ref _recordingQueuedFrameCount);
        }
    }

    private static Process StartFfmpegRecordingProcess(string ffmpegPath, string outputPath, int width, int height, int frameRate)
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
        process.StartInfo.ArgumentList.Add("veryfast");
        process.StartInfo.ArgumentList.Add("-crf");
        process.StartInfo.ArgumentList.Add("18");
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

    private async Task WriteRecordingFramesAsync(Process process, Channel<byte[]> channel, CancellationToken cancellationToken)
    {
        await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
        {
            if (process.HasExited)
            {
                break;
            }

            await process.StandardInput.BaseStream.WriteAsync(frame, cancellationToken);
            Interlocked.Increment(ref _recordingWrittenFrameCount);
        }
    }

    private async Task TrackRecordingProgressAsync(CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Max(SelectedDurationSeconds + 5, SelectedDurationSeconds * 1.5);
        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = (DateTimeOffset.UtcNow - _recordingStartedAt).TotalSeconds;
            var queuedFrames = Interlocked.Read(ref _recordingQueuedFrameCount);
            RecordingProgress = Math.Clamp(queuedFrames / Math.Max(1.0, _recordingTargetFrameCount) * 100.0, 0, 100);
            RecordingStatusText = $"录制中 {queuedFrames} / {_recordingTargetFrameCount} 帧，已用时 {elapsed:0.0}s";
            if (queuedFrames >= _recordingTargetFrameCount)
            {
                break;
            }

            if (elapsed >= timeoutSeconds)
            {
                AppendLog($"录制超时: 已收到 {queuedFrames} / {_recordingTargetFrameCount} 帧。");
                break;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private async Task StopRecordingCoreAsync()
    {
        Process? process;
        Task? writerTask;
        Channel<byte[]>? channel;
        CancellationTokenSource? cancellation;
        string? outputPath;

        lock (_recordingLock)
        {
            if (!IsRecording && _recordingProcess is null)
            {
                return;
            }

            process = _recordingProcess;
            writerTask = _recordingWriterTask;
            channel = _recordingChannel;
            cancellation = _recordingCancellation;
            outputPath = _recordingOutputPath;
            IsRecording = false;
            _recordingProcess = null;
            _recordingWriterTask = null;
            _recordingChannel = null;
            _recordingCancellation = null;
            _recordingOutputPath = null;
        }

        try
        {
            channel?.Writer.TryComplete();
            if (writerTask is not null)
            {
                await writerTask.WaitAsync(TimeSpan.FromSeconds(10));
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
            cancellation?.Dispose();
            process?.Dispose();
        }

        RecordingProgress = 100;
        var queuedFrames = Interlocked.Read(ref _recordingQueuedFrameCount);
        var writtenFrames = Interlocked.Read(ref _recordingWrittenFrameCount);
        var estimatedDuration = _recordingFrameRate > 0 ? writtenFrames / (double)_recordingFrameRate : 0;
        if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
        {
            LastRecordingPath = outputPath;
            RecordingStatusText = $"录制完成: {Path.GetFileName(outputPath)}";
            AppendLog($"录制完成: {outputPath}");
            AppendLog($"录制帧统计: 入队 {queuedFrames} 帧，写入 {writtenFrames} 帧，目标 {_recordingFrameRate}fps，估算时长 {estimatedDuration:0.00}s。");
        }
        else
        {
            RecordingStatusText = "录制已停止";
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

    private void CloseDeviceCore()
    {
        if (IsRecording)
        {
            _recordingCancellation?.Cancel();
            StopRecordingCoreAsync().GetAwaiter().GetResult();
        }

        StopPreviewCore();

        try
        {
            _stream?.Close();
        }
        catch
        {
        }

        _stream = null;
        _streamFeatureControl = null;

        try
        {
            _device?.Close();
        }
        catch
        {
        }

        _device = null;
        _featureControl = null;
        _formatConvert = null;
        ReleaseConvertBuffer();
    }

    private void StopPreviewCore()
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
    }

    private void TrySetEnum(IGXFeatureControl featureControl, string featureName, string value)
    {
        try
        {
            if (featureControl.IsImplemented(featureName) && featureControl.IsWritable(featureName))
            {
                featureControl.GetEnumFeature(featureName).SetValue(value);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"{featureName} 设置跳过: {ex.Message}");
        }
    }

    private void TrySetInt(IGXFeatureControl featureControl, string featureName, long value)
    {
        try
        {
            if (!featureControl.IsImplemented(featureName) || !featureControl.IsWritable(featureName))
            {
                return;
            }

            var feature = featureControl.GetIntFeature(featureName);
            var safeValue = Math.Clamp(value, feature.GetMin(), feature.GetMax());
            feature.SetValue(safeValue);
        }
        catch (Exception ex)
        {
            AppendLog($"{featureName} 设置跳过: {ex.Message}");
        }
    }

    private void TrySetFloat(IGXFeatureControl featureControl, string featureName, double value)
    {
        try
        {
            if (!featureControl.IsImplemented(featureName) || !featureControl.IsWritable(featureName))
            {
                return;
            }

            var feature = featureControl.GetFloatFeature(featureName);
            var safeValue = Math.Clamp(value, feature.GetMin(), feature.GetMax());
            feature.SetValue(safeValue);
        }
        catch (Exception ex)
        {
            AppendLog($"{featureName} 设置跳过: {ex.Message}");
        }
    }

    private void TrySetBool(IGXFeatureControl featureControl, string featureName, bool value)
    {
        try
        {
            if (featureControl.IsImplemented(featureName) && featureControl.IsWritable(featureName))
            {
                featureControl.GetBoolFeature(featureName).SetValue(value);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"{featureName} 设置跳过: {ex.Message}");
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

    private static double TryReadFloatValue(IGXFeatureControl? featureControl, string featureName, double fallback)
    {
        try
        {
            return featureControl is not null
                   && featureControl.IsImplemented(featureName)
                   && featureControl.IsReadable(featureName)
                ? featureControl.GetFloatFeature(featureName).GetValue()
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string TryReadBool(IGXFeatureControl featureControl, string featureName)
    {
        try
        {
            return featureControl.IsImplemented(featureName) && featureControl.IsReadable(featureName)
                ? featureControl.GetBoolFeature(featureName).GetValue().ToString()
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

    private void DispatchError(string prefix, Exception ex)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = prefix;
            AppendLog($"{prefix}: {ex.Message}");
        });
    }

    private void AppendLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (LogLines.Count > 200)
            {
                LogLines.RemoveAt(0);
            }
        });
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

        return (2048, 1536);
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value;

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

    private static string FfmpegPath => Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    private static string SaveDirectory => Path.Combine(AppContext.BaseDirectory, "video", "daheng-test");

    public void Dispose()
    {
        CloseDeviceCore();
        try
        {
            if (_isFactoryInitialized)
            {
                _factory?.Uninit();
            }
        }
        catch
        {
        }

        _isFactoryInitialized = false;
        _factory = null;
    }

    private sealed record BgrFrame(byte[] Bytes, int Width, int Height, int Stride);
}
