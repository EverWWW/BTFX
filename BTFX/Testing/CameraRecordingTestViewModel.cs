using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.Testing;

public partial class CameraRecordingTestViewModel : ObservableObject
{
    private readonly ICameraRecordingService _cameraRecordingService;
    private readonly ICameraCaptureSettingsService _settingsService;
    private readonly List<PreviewProcess> _previewProcesses = new();
    private readonly SemaphoreSlim _cameraStatusProbeGate = new(1, 1);
    private readonly SemaphoreSlim _previewRestartGate = new(1, 1);
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _sidePreviewRestartDebounceCancellation;
    private CancellationTokenSource? _frontPreviewRestartDebounceCancellation;
    private CancellationTokenSource? _cameraStatusMonitoringCancellation;
    private CancellationTokenSource? _recordingCancellation;
    private CameraCaptureSettings _settings;
    private bool _isLoadingSettings;
    private int _sidePreviewGeneration;
    private int _frontPreviewGeneration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDualMode))]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private CameraCaptureMode _currentMode = CameraCaptureMode.Dual;

    [ObservableProperty]
    private string _sideCameraName = string.Empty;

    [ObservableProperty]
    private string _frontCameraName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideCameraStatusBrush))]
    private string _sideCameraStatus = "未检测";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrontCameraStatusBrush))]
    private string _frontCameraStatus = "未检测";

    [ObservableProperty]
    private string _selectedResolution = "3840x2160";

    [ObservableProperty]
    private FrameRateOption _selectedFrameRate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingElapsedDisplayText))]
    private DurationOption _selectedDuration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewState))]
    [NotifyPropertyChangedFor(nameof(IsRecordingState))]
    [NotifyPropertyChangedFor(nameof(IsCompletedState))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionText))]
    [NotifyPropertyChangedFor(nameof(IsRecordingCountdownVisible))]
    [NotifyPropertyChangedFor(nameof(IsTranscodingVisible))]
    [NotifyPropertyChangedFor(nameof(RecordingStageText))]
    private CameraCaptureUiState _captureState = CameraCaptureUiState.Preview;

    [ObservableProperty]
    private double _recordingProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingElapsedDisplayText))]
    private double _recordingRemainingSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordingCountdownVisible))]
    [NotifyPropertyChangedFor(nameof(IsTranscodingVisible))]
    [NotifyPropertyChangedFor(nameof(RecordingStageText))]
    private bool _isTranscoding;

    [ObservableProperty]
    private string _transcodeLogText = string.Empty;

    [ObservableProperty]
    private string _statusText = "预览中";

    [ObservableProperty]
    private ImageSource? _sidePreviewImage;

    [ObservableProperty]
    private ImageSource? _frontPreviewImage;

    [ObservableProperty]
    private CameraOrientation _sideOrientation = CameraOrientation.Landscape;

    [ObservableProperty]
    private CameraOrientation _frontOrientation = CameraOrientation.Landscape;

    [ObservableProperty]
    private bool _sideFlipHorizontal;

    [ObservableProperty]
    private bool _frontFlipHorizontal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string? _sideOutputPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string? _frontOutputPath;

    [ObservableProperty]
    private CameraCaptureDialogResult? _captureResult;

    [ObservableProperty]
    private bool _isCameraNameEditorOpen;

    [ObservableProperty]
    private CameraViewRole _editingCameraRole;

    [ObservableProperty]
    private string _editingCameraName = string.Empty;

    public ObservableCollection<string> Resolutions { get; } = new()
    {
        "3840x2160",
        "1920x1080",
        "1280x720"
    };

    public ObservableCollection<FrameRateOption> FrameRateOptions { get; } = new()
    {
        new("高帧率", 59),
        new("中帧率", 45),
        new("低帧率", 30)
    };

    public ObservableCollection<DurationOption> DurationOptions { get; } = new()
    {
        new("10s", 10),
        new("20s", 20),
        new("30s", 30)
    };

    public ObservableCollection<string> LogLines { get; } = new();

    public bool IsDualMode => CurrentMode == CameraCaptureMode.Dual;

    public bool IsPreviewState => CaptureState == CameraCaptureUiState.Preview;

    public bool IsRecordingState => CaptureState == CameraCaptureUiState.Recording;

    public bool IsCompletedState => CaptureState == CameraCaptureUiState.Completed;

    public bool IsRecordingCountdownVisible => IsRecordingState && !IsTranscoding;

    public bool IsTranscodingVisible => IsRecordingState && IsTranscoding;

    public bool CanConfirm => CaptureState == CameraCaptureUiState.Completed
                              && !string.IsNullOrWhiteSpace(SideOutputPath)
                              && (!IsDualMode || !string.IsNullOrWhiteSpace(FrontOutputPath));

    public string ModeText => IsDualMode ? "双视角视频采集" : "单视角视频采集";

    public string PrimaryActionText => CaptureState == CameraCaptureUiState.Completed ? "重新录制" : "开始录制";

    public string RecordingStageText => IsTranscodingVisible ? "视频录制完成，正在转码" : IsRecordingState ? "正在录制" : string.Empty;

    public string RecordingElapsedDisplayText =>
        $"{FormatDuration(SelectedDuration.Value - RecordingRemainingSeconds)} / {FormatDuration(SelectedDuration.Value)}";

    public Brush SideCameraStatusBrush => GetCameraStatusBrush(SideCameraStatus);

    public Brush FrontCameraStatusBrush => GetCameraStatusBrush(FrontCameraStatus);

    public string FfmpegPath => Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    public string SaveDirectory => Path.Combine(AppContext.BaseDirectory, "video");

    public CameraRecordingTestViewModel(
        ICameraRecordingService cameraRecordingService,
        ICameraCaptureSettingsService settingsService)
    {
        _cameraRecordingService = cameraRecordingService;
        _settingsService = settingsService;
        _settings = _settingsService.Load();

        _selectedFrameRate = FrameRateOptions.First();
        _selectedDuration = DurationOptions.First();
        LoadSettings(_settings.LastMode);
    }

    public void Initialize(CameraCaptureMode mode)
    {
        StopPreview();
        LoadSettings(mode);
        ResetRecordingState();
        StartCameraStatusMonitoring();
        _ = RestartPreviewAsync();
    }

    public void StopAllMediaWork()
    {
        StopCameraStatusMonitoring();
        _sidePreviewRestartDebounceCancellation?.Cancel();
        _sidePreviewRestartDebounceCancellation?.Dispose();
        _sidePreviewRestartDebounceCancellation = null;
        _frontPreviewRestartDebounceCancellation?.Cancel();
        _frontPreviewRestartDebounceCancellation?.Dispose();
        _frontPreviewRestartDebounceCancellation = null;
        StopPreview();
        _recordingCancellation?.Cancel();
    }

    partial void OnCurrentModeChanged(CameraCaptureMode value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.LastMode = value;
        SaveSettings();
        ResetRecordingState();
        _ = RefreshCameraStatusAsync();
        _ = RestartPreviewAsync();
    }

    partial void OnSelectedResolutionChanged(string value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.Resolution = value;
        SaveSettings();
    }

    partial void OnSelectedFrameRateChanged(FrameRateOption value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.FrameRate = value.Value;
        SaveSettings();
    }

    partial void OnSelectedDurationChanged(DurationOption value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.DurationSeconds = value.Value;
        SaveSettings();
    }

    partial void OnSideOrientationChanged(CameraOrientation value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.SideTransform.Orientation = value;
        SaveSettings();
        RestartPreviewIfActive(CameraViewRole.Side);
    }

    partial void OnFrontOrientationChanged(CameraOrientation value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.FrontTransform.Orientation = value;
        SaveSettings();
        RestartPreviewIfActive(CameraViewRole.Front);
    }

    partial void OnSideFlipHorizontalChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.SideTransform.FlipHorizontal = value;
        SaveSettings();
        RestartPreviewIfActive(CameraViewRole.Side);
    }

    partial void OnFrontFlipHorizontalChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.FrontTransform.FlipHorizontal = value;
        SaveSettings();
        RestartPreviewIfActive(CameraViewRole.Front);
    }

    [RelayCommand]
    private void SetSingleMode() => CurrentMode = CameraCaptureMode.Single;

    [RelayCommand]
    private void SetDualMode() => CurrentMode = CameraCaptureMode.Dual;

    [RelayCommand]
    private void EditSideCamera()
    {
        EditingCameraRole = CameraViewRole.Side;
        EditingCameraName = SideCameraName;
        IsCameraNameEditorOpen = true;
    }

    [RelayCommand]
    private void EditFrontCamera()
    {
        EditingCameraRole = CameraViewRole.Front;
        EditingCameraName = FrontCameraName;
        IsCameraNameEditorOpen = true;
    }

    [RelayCommand]
    private void CancelEditCameraName() => IsCameraNameEditorOpen = false;

    [RelayCommand]
    private async Task ConfirmEditCameraNameAsync()
    {
        var value = EditingCameraName.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (EditingCameraRole == CameraViewRole.Side)
        {
            SideCameraName = value;
            _settings.SideCameraName = value;
        }
        else
        {
            FrontCameraName = value;
            _settings.FrontCameraName = value;
        }

        SaveSettings();
        IsCameraNameEditorOpen = false;
        await RefreshCameraStatusAsync();
        await RestartPreviewAsync();
    }

    [RelayCommand]
    private Task RefreshCameraStatusAsync()
    {
        return RefreshCameraStatusCoreAsync(CancellationToken.None);
    }

    private async Task RefreshCameraStatusCoreAsync(CancellationToken cancellationToken)
    {
        if (!await _cameraStatusProbeGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var newSideStatus = await ProbeCameraAsync(SideCameraName, cancellationToken);
            var newFrontStatus = IsDualMode ? await ProbeCameraAsync(FrontCameraName, cancellationToken) : "未启用";

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var previousSideStatus = SideCameraStatus;
                var previousFrontStatus = FrontCameraStatus;
                SideCameraStatus = newSideStatus;
                FrontCameraStatus = newFrontStatus;

                if (CaptureState == CameraCaptureUiState.Preview
                    && !string.Equals(previousSideStatus, SideCameraStatus, StringComparison.Ordinal))
                {
                    RestartPreviewIfActive(CameraViewRole.Side);
                }

                if (CaptureState == CameraCaptureUiState.Preview
                    && !string.Equals(previousFrontStatus, FrontCameraStatus, StringComparison.Ordinal))
                {
                    RestartPreviewIfActive(CameraViewRole.Front);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cameraStatusProbeGate.Release();
        }
    }

    [RelayCommand]
    private async Task StartOrResetRecordingAsync()
    {
        if (CaptureState == CameraCaptureUiState.Completed)
        {
            ResetRecordingState();
            await RestartPreviewAsync();
            return;
        }

        await StartRecordingAsync();
    }

    [RelayCommand]
    private void OpenSaveDirectory()
    {
        Directory.CreateDirectory(SaveDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = SaveDirectory,
            UseShellExecute = true
        });
    }

    private async Task StartRecordingAsync()
    {
        StopPreview();
        _recordingCancellation?.Dispose();
        _recordingCancellation = new CancellationTokenSource();
        CaptureResult = null;
        SideOutputPath = null;
        FrontOutputPath = null;
        RecordingProgress = 0;
        RecordingRemainingSeconds = SelectedDuration.Value;
        IsTranscoding = false;
        TranscodeLogText = string.Empty;
        LogLines.Clear();
        CaptureState = CameraCaptureUiState.Recording;
        StatusText = "录制中";

        using var progressCancellation = new CancellationTokenSource();
        var progressTask = TrackRecordingProgressAsync(progressCancellation.Token);

        try
        {
            Directory.CreateDirectory(SaveDirectory);
            var cameraNames = IsDualMode
                ? new[] { SideCameraName, FrontCameraName }
                : new[] { SideCameraName };

            var transforms = new Dictionary<string, CameraTransformOptions>(StringComparer.OrdinalIgnoreCase)
            {
                [SideCameraName] = new()
                {
                    Orientation = SideOrientation,
                    FlipHorizontal = SideFlipHorizontal
                }
            };

            if (IsDualMode)
            {
                transforms[FrontCameraName] = new()
                {
                    Orientation = FrontOrientation,
                    FlipHorizontal = FrontFlipHorizontal
                };
            }

            var options = new CameraRecordingOptions
            {
                FfmpegPath = FfmpegPath,
                SaveDirectory = SaveDirectory,
                CameraNames = cameraNames,
                VideoSize = SelectedResolution,
                FrameRate = SelectedFrameRate.Value,
                DurationSeconds = SelectedDuration.Value,
                TranscodeToMp4 = true,
                DeleteAviAfterMp4 = true,
                TransformOptionsByCameraName = transforms
            };

            var results = await _cameraRecordingService.RecordAsync(
                options,
                new Progress<string>(HandleRecordingProgress),
                _recordingCancellation.Token);

            var sideResult = results.FirstOrDefault(item => string.Equals(item.CameraName, SideCameraName, StringComparison.OrdinalIgnoreCase));
            var frontResult = results.FirstOrDefault(item => string.Equals(item.CameraName, FrontCameraName, StringComparison.OrdinalIgnoreCase));

            SideOutputPath = sideResult?.Mp4File ?? sideResult?.AviFile;
            FrontOutputPath = IsDualMode ? frontResult?.Mp4File ?? frontResult?.AviFile : null;
            CaptureResult = new CameraCaptureDialogResult
            {
                Mode = CurrentMode,
                SideVideoPath = SideOutputPath,
                FrontVideoPath = FrontOutputPath,
                SideCameraName = SideCameraName,
                FrontCameraName = IsDualMode ? FrontCameraName : null
            };

            RecordingProgress = 100;
            RecordingRemainingSeconds = 0;
            CaptureState = CameraCaptureUiState.Completed;
            StatusText = "录制完成";
            AppendLog("录制和转码完成。");
        }
        catch (OperationCanceledException)
        {
            CaptureState = CameraCaptureUiState.Preview;
            StatusText = "已取消";
            AppendLog("录制已取消。");
            await RestartPreviewAsync();
        }
        catch (Exception ex)
        {
            CaptureState = CameraCaptureUiState.Preview;
            StatusText = "录制失败";
            AppendLog($"错误: {ex.Message}");
            await RestartPreviewAsync();
        }
        finally
        {
            progressCancellation.Cancel();
            try
            {
                await progressTask;
            }
            catch (OperationCanceledException)
            {
            }

            _recordingCancellation?.Dispose();
            _recordingCancellation = null;
        }
    }

    private async Task TrackRecordingProgressAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.Now;
        while (!cancellationToken.IsCancellationRequested && CaptureState == CameraCaptureUiState.Recording)
        {
            if (!IsTranscoding)
            {
                var elapsed = (DateTime.Now - startedAt).TotalSeconds;
                RecordingRemainingSeconds = Math.Max(0, SelectedDuration.Value - elapsed);
                RecordingProgress = Math.Clamp(elapsed / Math.Max(1, SelectedDuration.Value) * 70.0, 0, 70);
            }
            else
            {
                RecordingRemainingSeconds = 0;
                RecordingProgress = Math.Clamp(RecordingProgress + 0.6, 72, 96);
            }

            await Task.Delay(200, cancellationToken);
        }
    }

    private void HandleRecordingProgress(string message)
    {
        if (message.Contains("STAGE:RECORD_DONE", StringComparison.OrdinalIgnoreCase))
        {
            RecordingProgress = 70;
            RecordingRemainingSeconds = 0;
            StatusText = "转码准备中";
            AppendLog("录制阶段完成。");
            return;
        }

        if (message.Contains("STAGE:TRANSCODE_START", StringComparison.OrdinalIgnoreCase))
        {
            IsTranscoding = true;
            RecordingProgress = Math.Max(RecordingProgress, 72);
            RecordingRemainingSeconds = 0;
            StatusText = "转码中";
            AppendLog("开始转码。");
            return;
        }

        if (message.Contains("STAGE:DONE", StringComparison.OrdinalIgnoreCase))
        {
            RecordingProgress = 100;
            StatusText = "录制完成";
            AppendLog("全部任务完成。");
            return;
        }

        AppendLog(message);
        if (IsTranscoding)
        {
            TranscodeLogText = message;
        }
    }

    private void LoadSettings(CameraCaptureMode mode)
    {
        _isLoadingSettings = true;
        _settings = _settingsService.Load();
        CurrentMode = mode;
        SideCameraName = _settings.SideCameraName;
        FrontCameraName = _settings.FrontCameraName;
        SelectedResolution = Resolutions.Contains(_settings.Resolution) ? _settings.Resolution : Resolutions[0];
        SelectedFrameRate = FrameRateOptions.FirstOrDefault(item => item.Value == _settings.FrameRate) ?? FrameRateOptions[0];
        SelectedDuration = DurationOptions.FirstOrDefault(item => item.Value == _settings.DurationSeconds) ?? DurationOptions[0];
        SideOrientation = _settings.SideTransform.Orientation;
        SideFlipHorizontal = _settings.SideTransform.FlipHorizontal;
        FrontOrientation = _settings.FrontTransform.Orientation;
        FrontFlipHorizontal = _settings.FrontTransform.FlipHorizontal;
        _isLoadingSettings = false;
    }

    private void ResetRecordingState()
    {
        CaptureState = CameraCaptureUiState.Preview;
        StatusText = "预览中";
        RecordingProgress = 0;
        RecordingRemainingSeconds = 0;
        IsTranscoding = false;
        TranscodeLogText = string.Empty;
        SideOutputPath = null;
        FrontOutputPath = null;
        CaptureResult = null;
    }

    private async Task<string> ProbeCameraAsync(string cameraName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            return "未配置";
        }

        if (!File.Exists(FfmpegPath))
        {
            return "FFmpeg缺失";
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = "-hide_banner -list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };
            process.Start();
            var output = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return output.Contains(cameraName, StringComparison.OrdinalIgnoreCase) ? "已连接" : "未发现";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return "检测失败";
        }
    }

    private async Task RestartPreviewAsync()
    {
        await _previewRestartGate.WaitAsync();
        try
        {
            StopPreview();
            if (CaptureState != CameraCaptureUiState.Preview || !File.Exists(FfmpegPath))
            {
                return;
            }

            SidePreviewImage = null;
            FrontPreviewImage = null;
            _previewCancellation = new CancellationTokenSource();
            var sideGeneration = GetPreviewGeneration(CameraViewRole.Side);
            await StartPreviewProcessAsync(CameraViewRole.Side, SideCameraName, sideGeneration, _previewCancellation.Token);
            if (IsDualMode)
            {
                var frontGeneration = GetPreviewGeneration(CameraViewRole.Front);
                await StartPreviewProcessAsync(CameraViewRole.Front, FrontCameraName, frontGeneration, _previewCancellation.Token);
            }
        }
        finally
        {
            _previewRestartGate.Release();
        }
    }

    private void RestartPreviewIfActive(CameraViewRole role)
    {
        if (CaptureState == CameraCaptureUiState.Preview)
        {
            var debounceCancellation = GetPreviewRestartDebounceCancellation(role);
            debounceCancellation?.Cancel();
            debounceCancellation?.Dispose();
            debounceCancellation = new CancellationTokenSource();
            SetPreviewRestartDebounceCancellation(role, debounceCancellation);
            var token = debounceCancellation.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    await RestartPreviewRoleAsync(role);
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }
    }

    private async Task RestartPreviewRoleAsync(CameraViewRole role)
    {
        await _previewRestartGate.WaitAsync();
        try
        {
            if (CaptureState != CameraCaptureUiState.Preview || !File.Exists(FfmpegPath))
            {
                return;
            }

            StopPreview(role);
            _previewCancellation ??= new CancellationTokenSource();

            if (role == CameraViewRole.Side)
            {
                SidePreviewImage = null;
                await StartPreviewProcessAsync(CameraViewRole.Side, SideCameraName, GetPreviewGeneration(CameraViewRole.Side), _previewCancellation.Token);
            }
            else if (IsDualMode)
            {
                FrontPreviewImage = null;
                await StartPreviewProcessAsync(CameraViewRole.Front, FrontCameraName, GetPreviewGeneration(CameraViewRole.Front), _previewCancellation.Token);
            }
        }
        finally
        {
            _previewRestartGate.Release();
        }
    }

    private async Task StartPreviewProcessAsync(CameraViewRole role, string cameraName, int generation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            return;
        }

        try
        {
            var filter = BuildPreviewFilter(role == CameraViewRole.Side ? SideOrientation : FrontOrientation,
                role == CameraViewRole.Side ? SideFlipHorizontal : FrontFlipHorizontal);
            var arguments = string.Join(
                " ",
                "-hide_banner",
                "-loglevel warning",
                "-f dshow",
                "-rtbufsize 128M",
                $"-i {Quote($"video={cameraName}")}",
                $"-vf {Quote(filter)}",
                "-an",
                "-f image2pipe",
                "-vcodec mjpeg",
                "-q:v 5",
                "-");

            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.Start();
            _previewProcesses.Add(new PreviewProcess(role, process));
            _ = Task.Run(() => ReadPreviewFramesAsync(role, process, generation, cancellationToken), cancellationToken);
            _ = Task.Run(() => ReadPreviewErrorsAsync(cameraName, process, cancellationToken), cancellationToken);
            await Task.Delay(30, cancellationToken);
        }
        catch (Exception ex)
        {
            AppendLog($"预览启动失败({cameraName}): {ex.Message}");
        }
    }

    private async Task ReadPreviewErrorsAsync(string cameraName, Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                var line = await process.StandardError.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    AppendLog($"预览({cameraName}): {line}");
                }
                else if (line == null)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    private async Task ReadPreviewFramesAsync(CameraViewRole role, Process process, int generation, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[8192];
            var bytes = new List<byte>(256 * 1024);
            var stream = process.StandardOutput.BaseStream;

            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    bytes.Add(buffer[i]);
                }

                while (TryExtractJpeg(bytes, out var jpeg))
                {
                    var image = CreateBitmap(jpeg);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (generation != GetPreviewGeneration(role) || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (role == CameraViewRole.Side)
                        {
                            SidePreviewImage = image;
                        }
                        else
                        {
                            FrontPreviewImage = image;
                        }
                    });
                }
            }
        }
        catch
        {
        }
    }

    private void StopPreview()
    {
        Interlocked.Increment(ref _sidePreviewGeneration);
        Interlocked.Increment(ref _frontPreviewGeneration);
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;

        foreach (var previewProcess in _previewProcesses.ToList())
        {
            try
            {
                var process = previewProcess.Process;
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                process.Dispose();
            }
            catch
            {
            }
        }

        _previewProcesses.Clear();
        SidePreviewImage = null;
        FrontPreviewImage = null;
    }

    private void StopPreview(CameraViewRole role)
    {
        if (role == CameraViewRole.Side)
        {
            Interlocked.Increment(ref _sidePreviewGeneration);
        }
        else
        {
            Interlocked.Increment(ref _frontPreviewGeneration);
        }

        foreach (var previewProcess in _previewProcesses.Where(item => item.Role == role).ToList())
        {
            try
            {
                var process = previewProcess.Process;
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                process.Dispose();
            }
            catch
            {
            }

            _previewProcesses.Remove(previewProcess);
        }
    }

    private void StartCameraStatusMonitoring()
    {
        StopCameraStatusMonitoring();
        _cameraStatusMonitoringCancellation = new CancellationTokenSource();
        var token = _cameraStatusMonitoringCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await RefreshCameraStatusCoreAsync(token);
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void StopCameraStatusMonitoring()
    {
        _cameraStatusMonitoringCancellation?.Cancel();
        _cameraStatusMonitoringCancellation?.Dispose();
        _cameraStatusMonitoringCancellation = null;
    }

    private CancellationTokenSource? GetPreviewRestartDebounceCancellation(CameraViewRole role)
    {
        return role == CameraViewRole.Side
            ? _sidePreviewRestartDebounceCancellation
            : _frontPreviewRestartDebounceCancellation;
    }

    private void SetPreviewRestartDebounceCancellation(CameraViewRole role, CancellationTokenSource? cancellationTokenSource)
    {
        if (role == CameraViewRole.Side)
        {
            _sidePreviewRestartDebounceCancellation = cancellationTokenSource;
        }
        else
        {
            _frontPreviewRestartDebounceCancellation = cancellationTokenSource;
        }
    }

    private void SaveSettings()
    {
        _settings.LastMode = CurrentMode;
        _settings.SideCameraName = SideCameraName;
        _settings.FrontCameraName = FrontCameraName;
        _settings.Resolution = SelectedResolution;
        _settings.FrameRate = SelectedFrameRate.Value;
        _settings.DurationSeconds = SelectedDuration.Value;
        _settingsService.Save(_settings);
    }

    private void AppendLog(string message)
    {
        LogLines.Add(message);
        while (LogLines.Count > 120)
        {
            LogLines.RemoveAt(0);
        }
    }

    private static string BuildPreviewFilter(CameraOrientation orientation, bool flipHorizontal)
    {
        var filters = new List<string> { "fps=10", "scale=640:-2" };
        if (orientation == CameraOrientation.PortraitClockwise)
        {
            filters.Add("transpose=1");
        }

        if (flipHorizontal)
        {
            filters.Add("vflip");
        }

        return string.Join(",", filters);
    }

    private static bool TryExtractJpeg(List<byte> source, out byte[] jpeg)
    {
        jpeg = Array.Empty<byte>();
        var start = -1;
        for (var i = 0; i < source.Count - 1; i++)
        {
            if (source[i] == 0xFF && source[i + 1] == 0xD8)
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            source.Clear();
            return false;
        }

        for (var i = start + 2; i < source.Count - 1; i++)
        {
            if (source[i] == 0xFF && source[i + 1] == 0xD9)
            {
                var length = i + 2 - start;
                jpeg = source.GetRange(start, length).ToArray();
                source.RemoveRange(0, i + 2);
                return true;
            }
        }

        if (start > 0)
        {
            source.RemoveRange(0, start);
        }

        return false;
    }

    private static BitmapImage CreateBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static string FormatDuration(double seconds)
    {
        var value = Math.Max(0, seconds);
        return TimeSpan.FromSeconds(value).ToString(value >= 60 ? @"mm\:ss" : @"ss\s");
    }

    private int GetPreviewGeneration(CameraViewRole role)
    {
        return role == CameraViewRole.Side ? _sidePreviewGeneration : _frontPreviewGeneration;
    }

    private static Brush GetCameraStatusBrush(string status)
    {
        return string.Equals(status, "已连接", StringComparison.Ordinal)
            ? Brushes.ForestGreen
            : Brushes.Firebrick;
    }

    private sealed record PreviewProcess(CameraViewRole Role, Process Process);
}

public enum CameraCaptureUiState
{
    Preview,
    Recording,
    Completed
}

public sealed record FrameRateOption(string Name, int Value)
{
    public override string ToString() => $"{Name} ({Value}fps)";
}

public sealed record DurationOption(string Name, int Value)
{
    public override string ToString() => Name;
}
