using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BTFX.Helpers;
using BTFX.Models.Camera;
using BTFX.Services.Implementations;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GxIAPINET;

namespace BTFX.ViewModels;

public partial class CameraCaptureDialogViewModel : ObservableObject, IDisposable
{
    private const int PreviewDisplayFrameRate = 30;
    private const int DahengPreviewDisplayFrameRate = 12;
    private const int PreviewInputFrameRate = 30;
    private const string PreviewInputVideoSize = "1280x720";
    private const int PreviewOutputWidth = 480;
    private const int RecordingStartDelaySeconds = 5;
    private readonly ICameraRecordingService _cameraRecordingService;
    private readonly ICameraCaptureSettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IRuntimeDependencyPreflightService _runtimeDependencyPreflightService;
    private readonly List<PreviewProcess> _previewProcesses = new();
    private readonly SemaphoreSlim _cameraStatusProbeGate = new(1, 1);
    private readonly SemaphoreSlim _previewRestartGate = new(1, 1);
    private readonly CameraDialogLifetime _dialogLifetime = new();
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _sidePreviewRestartDebounceCancellation;
    private CancellationTokenSource? _frontPreviewRestartDebounceCancellation;
    private CancellationTokenSource? _cameraStatusMonitoringCancellation;
    private CancellationTokenSource? _recordingCancellation;
    private CameraCaptureSettings _settings;
    private CameraCaptureBackend _captureBackend;
    private bool _isLoadingSettings;
    private bool _hasCameraStatusSnapshot;
    private int _sidePreviewGeneration;
    private int _frontPreviewGeneration;
    private readonly DahengCameraRuntime _dahengRuntime;
    private readonly LanguageChangeSubscription _languageChangeSubscription;
    private readonly List<DahengPreviewSlot> _dahengPreviewSlots = new();
    private DateTimeOffset? _recordingScheduledStartAtUtc;
    private bool _disposed;

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
    private string _sideCameraStatus = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrontCameraStatusBrush))]
    private string _frontCameraStatus = string.Empty;

    [ObservableProperty]
    private string _selectedResolution = "3840x2160";

    [ObservableProperty]
    private FrameRateOption _selectedFrameRate = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingElapsedDisplayText))]
    private DurationOption _selectedDuration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewState))]
    [NotifyPropertyChangedFor(nameof(IsRecordingState))]
    [NotifyPropertyChangedFor(nameof(IsCompletedState))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionText))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionTextDisplay))]
    [NotifyPropertyChangedFor(nameof(IsRecordingCountdownVisible))]
    [NotifyPropertyChangedFor(nameof(IsTranscodingVisible))]
    [NotifyPropertyChangedFor(nameof(IsLivePreviewVisible))]
    [NotifyPropertyChangedFor(nameof(RecordingStageText))]
    [NotifyPropertyChangedFor(nameof(RecordingStageTextDisplay))]
    private CameraCaptureUiState _captureState = CameraCaptureUiState.Preview;

    [ObservableProperty]
    private double _recordingProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingElapsedDisplayText))]
    private double _recordingRemainingSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordingCountdownVisible))]
    [NotifyPropertyChangedFor(nameof(IsTranscodingVisible))]
    [NotifyPropertyChangedFor(nameof(IsLivePreviewVisible))]
    [NotifyPropertyChangedFor(nameof(RecordingStageText))]
    [NotifyPropertyChangedFor(nameof(RecordingStageTextDisplay))]
    private bool _isTranscoding;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordingCountdownVisible))]
    [NotifyPropertyChangedFor(nameof(RecordingStageText))]
    [NotifyPropertyChangedFor(nameof(RecordingStageTextDisplay))]
    private bool _isPreparingRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingStageText))]
    [NotifyPropertyChangedFor(nameof(RecordingStageTextDisplay))]
    private int _recordingStartDelayRemainingSeconds;

    [ObservableProperty]
    private string _transcodeLogText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private ImageSource? _sidePreviewImage;

    [ObservableProperty]
    private ImageSource? _frontPreviewImage;

    [ObservableProperty]
    private ImageSource? _sidePlaybackPosterImage;

    [ObservableProperty]
    private ImageSource? _frontPlaybackPosterImage;

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

    public ObservableCollection<FrameRateOption> FrameRateOptions { get; } = new();

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

    private bool IsDahengBackend => _captureBackend == CameraCaptureBackend.Daheng;

    public bool IsLivePreviewVisible => IsPreviewState || (IsDahengBackend && IsRecordingState && !IsTranscoding);

    public bool IsRecordingCountdownVisible => IsRecordingState && !IsTranscoding && !IsPreparingRecording;

    public bool IsTranscodingVisible => IsRecordingState && IsTranscoding;

    public bool CanConfirm => CaptureState == CameraCaptureUiState.Completed
                              && !string.IsNullOrWhiteSpace(SideOutputPath)
                              && (!IsDualMode || !string.IsNullOrWhiteSpace(FrontOutputPath));

    public string ModeText => IsDualMode ? L("CameraCapture.Mode.DualFull") : L("CameraCapture.Mode.SingleFull");

    public string PrimaryActionText => CaptureState == CameraCaptureUiState.Completed ? L("CameraCapture.Action.Rerecord") : L("CameraCapture.Action.StartRecording");

    public string RecordingStageText => IsPreparingRecording
        ? RecordingStartDelayRemainingSeconds > 0
            ? L("CameraCapture.Status.StartDelayFormat", RecordingStartDelayRemainingSeconds)
            : L("CameraCapture.Status.DevicePreparing")
        : IsTranscodingVisible ? L("CameraCapture.Status.Transcoding") : IsRecordingState ? L("CameraCapture.Status.Recording") : string.Empty;

    public string RecordingElapsedDisplayText =>
        $"{FormatDuration(SelectedDuration.Value - RecordingRemainingSeconds)} / {FormatDuration(SelectedDuration.Value)}";

    public Brush SideCameraStatusBrush => GetCameraStatusBrush(SideCameraStatus);

    public Brush FrontCameraStatusBrush => GetCameraStatusBrush(FrontCameraStatus);

    public string SideCameraStatusDisplay => GetCameraStatusDisplay(SideCameraStatus, SideCameraStatusBrush);

    public string FrontCameraStatusDisplay => GetCameraStatusDisplay(FrontCameraStatus, FrontCameraStatusBrush);

    public string PrimaryActionTextDisplay => CaptureState == CameraCaptureUiState.Completed
        ? L("CameraCapture.Action.Rerecord")
        : L("CameraCapture.Action.StartRecording");

    public string RecordingStageTextDisplay => IsPreparingRecording
        ? RecordingStartDelayRemainingSeconds > 0
            ? L("CameraCapture.Status.StartDelayFormat", RecordingStartDelayRemainingSeconds)
            : L("CameraCapture.Status.DevicePreparing")
        : IsTranscodingVisible ? L("CameraCapture.Status.Transcoding") : IsRecordingState ? L("CameraCapture.Status.Recording") : string.Empty;

    public string FfmpegPath => Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    public string SaveDirectory => Path.Combine(AppContext.BaseDirectory, "video");

    public CameraCaptureDialogViewModel(
        ICameraRecordingService cameraRecordingService,
        ICameraCaptureSettingsService settingsService,
        ILocalizationService localizationService,
        DahengCameraRuntime dahengRuntime,
        IRuntimeDependencyPreflightService runtimeDependencyPreflightService)
    {
        _cameraRecordingService = cameraRecordingService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _dahengRuntime = dahengRuntime;
        _runtimeDependencyPreflightService = runtimeDependencyPreflightService;
        _settings = _settingsService.Load();
        _captureBackend = _settings.ResolveBackend();

        _isLoadingSettings = true;
        _selectedDuration = DurationOptions.First();
        RebuildResolutionOptions();
        RebuildFrameRateOptions(_settings.FrameRate);
        _isLoadingSettings = false;
        _languageChangeSubscription = new LanguageChangeSubscription(
            _localizationService,
            (_, _) => RefreshLocalizedText());
        LoadSettings(_settings.LastMode);
    }

    private string L(string key, params object[] args)
    {
        var value = args.Length == 0
            ? _localizationService.GetString(key)
            : _localizationService.GetString(key, args);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void RebuildFrameRateOptions(int selectedValue)
    {
        FrameRateOptions.Clear();
        if (IsDahengBackend)
        {
            FrameRateOptions.Add(new FrameRateOption(L("CameraCapture.FrameRate.High"), 90));
            FrameRateOptions.Add(new FrameRateOption(L("CameraCapture.FrameRate.Medium"), 60));
            FrameRateOptions.Add(new FrameRateOption(L("CameraCapture.FrameRate.Low"), 30));
        }
        else
        {
            FrameRateOptions.Add(new FrameRateOption(L("CameraCapture.FrameRate.High"), 60));
            FrameRateOptions.Add(new FrameRateOption(L("CameraCapture.FrameRate.Medium"), 45));
            FrameRateOptions.Add(new FrameRateOption(L("CameraCapture.FrameRate.Low"), 30));
        }

        SelectedFrameRate = FrameRateOptions.FirstOrDefault(item => item.Value == selectedValue) ?? FrameRateOptions[0];
    }

    private void RebuildResolutionOptions()
    {
        Resolutions.Clear();
        if (IsDahengBackend)
        {
            Resolutions.Add("2048x1536");
            Resolutions.Add("1920x1080");
            Resolutions.Add("1280x960");
        }
        else
        {
            Resolutions.Add("3840x2160");
            Resolutions.Add("1920x1080");
            Resolutions.Add("1280x720");
        }
    }

    private void RefreshLocalizedText()
    {
        var selectedFrameRate = SelectedFrameRate.Value;
        var previousLoading = _isLoadingSettings;
        _isLoadingSettings = true;
        RebuildFrameRateOptions(selectedFrameRate);
        _isLoadingSettings = previousLoading;
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(PrimaryActionTextDisplay));
        OnPropertyChanged(nameof(RecordingStageTextDisplay));
        OnPropertyChanged(nameof(SideCameraStatusDisplay));
        OnPropertyChanged(nameof(FrontCameraStatusDisplay));
    }

    private string GetCameraStatusDisplay(string status, Brush statusBrush)
    {
        if (ReferenceEquals(statusBrush, Brushes.ForestGreen) || Equals(statusBrush, Brushes.ForestGreen))
        {
            return L("CameraCapture.CameraStatus.Connected");
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return L("CameraCapture.CameraStatus.Undetected");
        }

        if (status.Contains("FFmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return L("CameraCapture.CameraStatus.FfmpegMissing");
        }

        if (string.Equals(status, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return L("CameraCapture.CameraStatus.Disabled");
        }

        if (string.Equals(status, "Unconfigured", StringComparison.OrdinalIgnoreCase))
        {
            return L("CameraCapture.CameraStatus.Unconfigured");
        }

        if (string.Equals(status, "ProbeFailed", StringComparison.OrdinalIgnoreCase))
        {
            return L("CameraCapture.CameraStatus.ProbeFailed");
        }

        return L("CameraCapture.CameraStatus.NotFound");
    }

    public void Initialize(CameraCaptureMode mode)
    {
        if (_dialogLifetime.IsClosed)
        {
            return;
        }

        StopPreview();
        LoadSettings(mode);
        ResetRecordingState();
        var runtimeCheck = _runtimeDependencyPreflightService.CheckCamera(_settings);
        if (!runtimeCheck.IsReady)
        {
            StatusText = RuntimeDependencyMessages.Format(runtimeCheck, _localizationService);
            SideCameraStatus = runtimeCheck.Issues.Any(issue => issue.Code == RuntimeDependencyIssueCode.FfmpegMissing)
                ? "FFmpegMissing"
                : "ProbeFailed";
            FrontCameraStatus = IsDualMode ? SideCameraStatus : "Disabled";
            AppendLog(StatusText);
            return;
        }

        StartCameraStatusMonitoring();
        _ = RestartPreviewAsync();
    }

    public void StopAllMediaWork()
    {
        if (!_dialogLifetime.Close())
        {
            return;
        }

        StopCameraStatusMonitoring();
        _sidePreviewRestartDebounceCancellation?.Cancel();
        _sidePreviewRestartDebounceCancellation?.Dispose();
        _sidePreviewRestartDebounceCancellation = null;
        _frontPreviewRestartDebounceCancellation?.Cancel();
        _frontPreviewRestartDebounceCancellation?.Dispose();
        _frontPreviewRestartDebounceCancellation = null;
        StopPreview();
        _recordingCancellation?.Cancel();
        LogLines.Clear();
        TranscodeLogText = string.Empty;
        CaptureResult = null;
        SidePlaybackPosterImage = null;
        FrontPlaybackPosterImage = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAllMediaWork();
        _languageChangeSubscription.Dispose();
        _dialogLifetime.Dispose();
        _disposed = true;
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

    partial void OnSideCameraStatusChanged(string value)
    {
        OnPropertyChanged(nameof(SideCameraStatusDisplay));
    }

    partial void OnFrontCameraStatusChanged(string value)
    {
        OnPropertyChanged(nameof(FrontCameraStatusDisplay));
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
    private void ToggleCaptureMode() => CurrentMode = IsDualMode ? CameraCaptureMode.Single : CameraCaptureMode.Dual;

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
        return _dialogLifetime.IsClosed
            ? Task.CompletedTask
            : RefreshCameraStatusCoreAsync(_dialogLifetime.Token);
    }

    private async Task RefreshCameraStatusCoreAsync(CancellationToken cancellationToken)
    {
        if (IsDahengBackend && CaptureState != CameraCaptureUiState.Preview)
        {
            return;
        }

        if (!await _cameraStatusProbeGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var newSideStatus = await ProbeCameraAsync(SideCameraName, cancellationToken);
            var newFrontStatus = IsDualMode ? await ProbeCameraAsync(FrontCameraName, cancellationToken) : "Disabled";

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var previousSideStatus = SideCameraStatus;
                var previousFrontStatus = FrontCameraStatus;
                var hasStatusSnapshot = _hasCameraStatusSnapshot;
                SideCameraStatus = newSideStatus;
                FrontCameraStatus = newFrontStatus;
                _hasCameraStatusSnapshot = true;

                if (!hasStatusSnapshot)
                {
                    return;
                }

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
        var runtimeCheck = _runtimeDependencyPreflightService.CheckCamera(_settings);
        if (!runtimeCheck.IsReady)
        {
            StatusText = RuntimeDependencyMessages.Format(runtimeCheck, _localizationService);
            AppendLog(StatusText);
            return;
        }

        if (IsDahengBackend
            && (!DahengPreviewStartGuard.CanOpenDevice(SideCameraStatus)
                || (IsDualMode && !DahengPreviewStartGuard.CanOpenDevice(FrontCameraStatus))))
        {
            StatusText = L("CameraCapture.Status.CameraUnavailable");
            AppendLog(StatusText);
            return;
        }

        if (!DiskSpaceGuard.EnsureProgramDriveHasSpace(L("CameraCapture.Title")))
        {
            return;
        }

        var requestedStartAtUtc = IsDahengBackend
            ? DateTimeOffset.UtcNow.AddSeconds(RecordingStartDelaySeconds)
            : (DateTimeOffset?)null;

        StopPreview();

        _recordingCancellation?.Dispose();
        var recordingCancellation = new CancellationTokenSource();
        _recordingCancellation = recordingCancellation;
        CaptureResult = null;
        SideOutputPath = null;
        FrontOutputPath = null;
        SidePlaybackPosterImage = null;
        FrontPlaybackPosterImage = null;
        RecordingProgress = 0;
        RecordingRemainingSeconds = SelectedDuration.Value;
        IsTranscoding = false;
        IsPreparingRecording = true;
        RecordingStartDelayRemainingSeconds = RecordingStartDelaySeconds;
        TranscodeLogText = string.Empty;
        LogLines.Clear();
        CaptureState = CameraCaptureUiState.Recording;
        StatusText = L("CameraCapture.Status.StartDelayFormat", RecordingStartDelaySeconds);
        _recordingScheduledStartAtUtc = requestedStartAtUtc;

        using var progressCancellation = new CancellationTokenSource();
        var progressTask = TrackRecordingProgressAsync(progressCancellation.Token);
        var startDelayTask = IsDahengBackend
            ? TrackDahengRecordingStartDelayAsync(recordingCancellation.Token)
            : Task.CompletedTask;

        try
        {
            if (!IsDahengBackend)
            {
                await DelayBeforeRecordingAsync(recordingCancellation.Token);
                StatusText = L("CameraCapture.Status.Recording");
            }

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
                ScheduledStartAtUtc = requestedStartAtUtc,
                TranscodeToMp4 = true,
                DeleteAviAfterMp4 = true,
                TransformOptionsByCameraName = transforms,
                PreviewFrameReceived = IsDahengBackend ? HandleDahengRecordingPreviewFrame : null
            };

            var results = await _cameraRecordingService.RecordAsync(
                options,
                new Progress<string>(HandleRecordingProgress),
                recordingCancellation.Token);

            SidePreviewImage = null;
            FrontPreviewImage = null;

            var sideResult = results.FirstOrDefault(item => string.Equals(item.CameraName, SideCameraName, StringComparison.OrdinalIgnoreCase));
            var frontResult = results.FirstOrDefault(item => string.Equals(item.CameraName, FrontCameraName, StringComparison.OrdinalIgnoreCase));

            SideOutputPath = sideResult?.Mp4File ?? sideResult?.AviFile;
            FrontOutputPath = IsDualMode ? frontResult?.Mp4File ?? frontResult?.AviFile : null;
            await LoadPlaybackPosterImagesAsync(recordingCancellation.Token);
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
            StatusText = L("CameraCapture.Status.Completed");
            AppendLog(L("CameraCapture.Log.RecordAndTranscodeCompleted"));
        }
        catch (OperationCanceledException)
        {
            if (_dialogLifetime.IsClosed)
            {
                return;
            }

            CaptureState = CameraCaptureUiState.Preview;
            StatusText = L("CameraCapture.Status.Canceled");
            AppendLog(L("CameraCapture.Log.RecordingCanceled"));
            await RestartPreviewAsync();
        }
        catch (Exception ex)
        {
            if (_dialogLifetime.IsClosed)
            {
                return;
            }

            CaptureState = CameraCaptureUiState.Preview;
            StatusText = L("CameraCapture.Status.Failed");
            AppendLog($"Error: {ex.Message}");
            await RestartPreviewAsync();
        }
        finally
        {
            IsPreparingRecording = false;
            RecordingStartDelayRemainingSeconds = 0;
            _recordingScheduledStartAtUtc = null;
            progressCancellation.Cancel();
            try
            {
                await progressTask;
            }
            catch (OperationCanceledException)
            {
            }

            recordingCancellation.Cancel();
            try
            {
                await startDelayTask;
            }
            catch (OperationCanceledException)
            {
            }

            recordingCancellation.Dispose();
            if (ReferenceEquals(_recordingCancellation, recordingCancellation))
            {
                _recordingCancellation = null;
            }
        }
    }

    private async Task DelayBeforeRecordingAsync(CancellationToken cancellationToken)
    {
        for (var remaining = RecordingStartDelaySeconds; remaining > 0; remaining--)
        {
            RecordingStartDelayRemainingSeconds = remaining;
            StatusText = L("CameraCapture.Status.StartDelayFormat", remaining);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        RecordingStartDelayRemainingSeconds = 0;
        IsPreparingRecording = false;
    }

    private async Task TrackDahengRecordingStartDelayAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsPreparingRecording)
        {
            if (_recordingScheduledStartAtUtc is { } startAt)
            {
                RecordingStartDelayRemainingSeconds = Math.Max(
                    0,
                    (int)Math.Ceiling((startAt - DateTimeOffset.UtcNow).TotalSeconds));
                StatusText = RecordingStartDelayRemainingSeconds > 0
                    ? L("CameraCapture.Status.StartDelayFormat", RecordingStartDelayRemainingSeconds)
                    : L("CameraCapture.Status.DevicePreparing");
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task TrackRecordingProgressAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.Now;
        var wasPreparing = IsPreparingRecording;
        while (!cancellationToken.IsCancellationRequested && CaptureState == CameraCaptureUiState.Recording)
        {
            if (IsPreparingRecording)
            {
                wasPreparing = true;
                RecordingRemainingSeconds = SelectedDuration.Value;
                RecordingProgress = 0;
                await Task.Delay(200, cancellationToken);
                continue;
            }

            if (wasPreparing)
            {
                startedAt = DateTime.Now;
                wasPreparing = false;
            }

            if (!IsTranscoding)
            {
                var elapsed = (DateTime.Now - startedAt).TotalSeconds;
                RecordingRemainingSeconds = Math.Max(0, SelectedDuration.Value - elapsed);
                RecordingProgress = Math.Clamp(elapsed / Math.Max(1, SelectedDuration.Value) * 70.0, 0, 70);
            }
            else
            {
                RecordingRemainingSeconds = 0;
                RecordingProgress = Math.Clamp(RecordingProgress + 0.25, 72, 98);
            }

            await Task.Delay(200, cancellationToken);
        }
    }

    private void HandleRecordingProgress(string message)
    {
        const string countdownPrefix = "STAGE:COUNTDOWN:";
        if (message.StartsWith(countdownPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (DateTimeOffset.TryParse(message[countdownPrefix.Length..], out var startAt))
            {
                _recordingScheduledStartAtUtc = startAt.ToUniversalTime();
            }

            return;
        }

        if (message.Contains("STAGE:PREPARING", StringComparison.OrdinalIgnoreCase))
        {
            IsPreparingRecording = true;
            return;
        }

        if (message.Contains("STAGE:RECORD_START", StringComparison.OrdinalIgnoreCase))
        {
            IsPreparingRecording = false;
            RecordingStartDelayRemainingSeconds = 0;
            StatusText = L("CameraCapture.Status.Recording");
            AppendLog(L("CameraCapture.Status.Recording"));
            return;
        }

        if (message.Contains("STAGE:RECORD_DONE", StringComparison.OrdinalIgnoreCase))
        {
            RecordingProgress = 70;
            RecordingRemainingSeconds = 0;
            StatusText = L("CameraCapture.Status.TranscodePreparing");
            AppendLog(L("CameraCapture.Log.RecordStageCompleted"));
            return;
        }

        if (message.Contains("STAGE:TRANSCODE_START", StringComparison.OrdinalIgnoreCase))
        {
            IsTranscoding = true;
            RecordingProgress = Math.Max(RecordingProgress, 72);
            RecordingRemainingSeconds = 0;
            StatusText = L("CameraCapture.Status.TranscodingShort");
            AppendLog(L("CameraCapture.Log.TranscodeStarted"));
            return;
        }

        if (message.Contains("STAGE:DONE", StringComparison.OrdinalIgnoreCase))
        {
            RecordingProgress = 100;
            StatusText = L("CameraCapture.Status.Completed");
            AppendLog(L("CameraCapture.Log.AllTasksCompleted"));
            return;
        }

        AppendLog(message);
        if (IsTranscoding)
        {
            TranscodeLogText = message;
        }
    }

    private void HandleDahengRecordingPreviewFrame(string cameraName, ImageSource image)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (string.Equals(cameraName, SideCameraName, StringComparison.OrdinalIgnoreCase))
            {
                SidePreviewImage = image;
            }
            else if (string.Equals(cameraName, FrontCameraName, StringComparison.OrdinalIgnoreCase))
            {
                FrontPreviewImage = image;
            }
        });
    }

    private void LoadSettings(CameraCaptureMode mode)
    {
        _isLoadingSettings = true;
        _settings = _settingsService.Load();
        _captureBackend = _settings.ResolveBackend();
        RebuildResolutionOptions();
        RebuildFrameRateOptions(_settings.FrameRate);
        CurrentMode = mode;
        SideCameraName = IsDahengBackend ? _settings.DahengSideCameraSerialNumber : _settings.SideCameraName;
        FrontCameraName = IsDahengBackend ? _settings.DahengFrontCameraSerialNumber : _settings.FrontCameraName;
        var defaultResolution = IsDahengBackend ? "2048x1536" : "3840x2160";
        SelectedResolution = Resolutions.Contains(_settings.Resolution) ? _settings.Resolution : defaultResolution;
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
        StatusText = L("CameraCapture.Status.Previewing");
        RecordingProgress = 0;
        RecordingRemainingSeconds = 0;
        IsTranscoding = false;
        TranscodeLogText = string.Empty;
        SideOutputPath = null;
        FrontOutputPath = null;
        CaptureResult = null;
        SidePlaybackPosterImage = null;
        FrontPlaybackPosterImage = null;
        LogLines.Clear();
    }

    private async Task<string> ProbeCameraAsync(string cameraName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            return "Unconfigured";
        }

        if (IsDahengBackend)
        {
            return await ProbeDahengCameraAsync(cameraName, cancellationToken);
        }

        if (!File.Exists(FfmpegPath))
        {
            return "FFmpegMissing";
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
            return output.Contains(cameraName, StringComparison.OrdinalIgnoreCase) ? "Connected" : "NotFound";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return "ProbeFailed";
        }
    }

    private Task<string> ProbeDahengCameraAsync(string serialNumber, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return _dahengRuntime.Execute(factory =>
                {
                    var devices = new List<IGXDeviceInfo>();
                    factory.UpdateAllDeviceList(300, devices);
                    return devices.Any(device => string.Equals(device.GetSN(), serialNumber, StringComparison.OrdinalIgnoreCase))
                        ? "Connected"
                        : "NotFound";
                });
            }
            catch
            {
                return "ProbeFailed";
            }
        }, cancellationToken);
    }

    private async Task RestartPreviewAsync()
    {
        if (!_dialogLifetime.CanStartPreview)
        {
            return;
        }

        try
        {
            await _previewRestartGate.WaitAsync(_dialogLifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            StopPreview();
            if (!_dialogLifetime.CanStartPreview
                || CaptureState != CameraCaptureUiState.Preview
                || !File.Exists(FfmpegPath))
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
        if (_dialogLifetime.CanStartPreview && CaptureState == CameraCaptureUiState.Preview)
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
                finally
                {
                    if (ReferenceEquals(GetPreviewRestartDebounceCancellation(role), debounceCancellation))
                    {
                        SetPreviewRestartDebounceCancellation(role, null);
                    }

                    debounceCancellation.Dispose();
                }
            }, token);
        }
    }

    private async Task RestartPreviewRoleAsync(CameraViewRole role)
    {
        if (!_dialogLifetime.CanStartPreview)
        {
            return;
        }

        try
        {
            await _previewRestartGate.WaitAsync(_dialogLifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (!_dialogLifetime.CanStartPreview
                || CaptureState != CameraCaptureUiState.Preview
                || !File.Exists(FfmpegPath))
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

        if (IsDahengBackend)
        {
            await StartDahengPreviewAsync(role, cameraName, generation, cancellationToken);
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
                "-fflags nobuffer",
                "-flags low_delay",
                "-f dshow",
                "-rtbufsize 128M",
                $"-video_size {PreviewInputVideoSize}",
                $"-framerate {PreviewInputFrameRate}",
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
            AppendLog($"棰勮鍚姩澶辫触({cameraName}): {ex.Message}");
        }
    }

    private async Task StartDahengPreviewAsync(CameraViewRole role, string serialNumber, int generation, CancellationToken cancellationToken)
    {
        try
        {
            var probeStatus = await ProbeDahengCameraAsync(serialNumber, cancellationToken);
            if (role == CameraViewRole.Side)
            {
                SideCameraStatus = probeStatus;
            }
            else
            {
                FrontCameraStatus = probeStatus;
            }

            if (!DahengPreviewStartGuard.CanOpenDevice(probeStatus))
            {
                AppendLog($"大恒相机未连接，已跳过实时预览({serialNumber})。");
                return;
            }

            var orientation = role == CameraViewRole.Side ? SideOrientation : FrontOrientation;
            var flipVertical = role == CameraViewRole.Side ? SideFlipHorizontal : FrontFlipHorizontal;
            var resolution = SelectedResolution;
            var slot = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _dahengRuntime.Execute(factory =>
                {
                    var previewSlot = new DahengPreviewSlot(
                        factory,
                        role,
                        serialNumber,
                        resolution,
                        orientation,
                        flipVertical,
                        generation,
                        GetPreviewGeneration,
                        image =>
                        {
                            if (role == CameraViewRole.Side)
                            {
                                SidePreviewImage = image;
                            }
                            else
                            {
                                FrontPreviewImage = image;
                            }
                        },
                        message => AppendLog(message));
                    previewSlot.Start();
                    return previewSlot;
                });
            }, cancellationToken);

            _dahengPreviewSlots.Add(slot);
            cancellationToken.Register(() => StopPreview(role));
        }
        catch (Exception ex)
        {
            AppendLog($"大恒预览启动失败({serialNumber}): {ex.Message}");
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
                    AppendLog($"棰勮({cameraName}): {line}");
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
            var lastFrameAt = DateTimeOffset.MinValue;
            var minimumFrameInterval = TimeSpan.FromSeconds(1.0 / PreviewDisplayFrameRate);

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
                    var now = DateTimeOffset.UtcNow;
                    if (now - lastFrameAt < minimumFrameInterval)
                    {
                        continue;
                    }

                    lastFrameAt = now;
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
            TerminatePreviewProcess(previewProcess.Process);
        }

        _previewProcesses.Clear();
        foreach (var slot in _dahengPreviewSlots.ToList())
        {
            _dahengRuntime.Execute(_ => slot.Dispose());
        }

        _dahengPreviewSlots.Clear();
            SidePreviewImage = null;
            FrontPreviewImage = null;
            SidePlaybackPosterImage = null;
            FrontPlaybackPosterImage = null;
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
            TerminatePreviewProcess(previewProcess.Process);

            _previewProcesses.Remove(previewProcess);
        }

        foreach (var slot in _dahengPreviewSlots.Where(item => item.Role == role).ToList())
        {
            _dahengRuntime.Execute(_ => slot.Dispose());
            _dahengPreviewSlots.Remove(slot);
        }

        if (role == CameraViewRole.Side)
        {
            SidePreviewImage = null;
        }
        else
        {
            FrontPreviewImage = null;
        }
    }

    private static void TerminatePreviewProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(800);
            }
        }
        catch
        {
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
        }
    }

    private void StartCameraStatusMonitoring()
    {
        if (_dialogLifetime.IsClosed)
        {
            return;
        }

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
        if (SelectedFrameRate is null || SelectedDuration is null)
        {
            return;
        }

        _settings.LastMode = CurrentMode;
        if (IsDahengBackend)
        {
            _settings.DahengSideCameraSerialNumber = SideCameraName;
            _settings.DahengFrontCameraSerialNumber = FrontCameraName;
        }
        else
        {
            _settings.SideCameraName = SideCameraName;
            _settings.FrontCameraName = FrontCameraName;
        }

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

    private async Task LoadPlaybackPosterImagesAsync(CancellationToken cancellationToken)
    {
        SidePlaybackPosterImage = await ExtractFirstFrameAsync(SideOutputPath, cancellationToken);
        FrontPlaybackPosterImage = IsDualMode
            ? await ExtractFirstFrameAsync(FrontOutputPath, cancellationToken)
            : null;
    }

    private async Task<ImageSource?> ExtractFirstFrameAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !File.Exists(FfmpegPath))
        {
            return null;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = FfmpegPath,
            Arguments = string.Join(
                " ",
                "-hide_banner",
                "-loglevel error",
                "-ss 0",
                $"-i {Quote(path)}",
                "-frames:v 1",
                "-vf scale=640:-2",
                "-f image2pipe",
                "-vcodec png",
                "-"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            process.Start();
            await using var frameStream = new MemoryStream();
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(frameStream, cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0 || frameStream.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    AppendLog($"棣栧抚鍔犺浇澶辫触: {error.Trim()}");
                }

                return null;
            }

            return CreateBitmap(frameStream.ToArray());
        }
        catch (OperationCanceledException)
        {
            TerminatePreviewProcess(process);
            throw;
        }
        catch (Exception ex)
        {
            AppendLog($"棣栧抚鍔犺浇澶辫触: {ex.Message}");
            return null;
        }
    }

    private static string BuildPreviewFilter(CameraOrientation orientation, bool flipHorizontal)
    {
        var filters = new List<string> { $"fps={PreviewDisplayFrameRate}", $"scale={PreviewOutputWidth}:-2" };
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

    private int GetPreviewGeneration(CameraViewRole role)
    {
        return role == CameraViewRole.Side ? _sidePreviewGeneration : _frontPreviewGeneration;
    }

    private static Brush GetCameraStatusBrush(string status)
    {
        return string.Equals(status, "Connected", StringComparison.Ordinal)
            ? Brushes.ForestGreen
            : Brushes.Firebrick;
    }

    private sealed record PreviewProcess(CameraViewRole Role, Process Process);

    private sealed class DahengPreviewSlot : IDisposable
    {
        private readonly object _convertLock = new();
        private readonly IGXFactory _factory;
        private readonly string _resolution;
        private readonly CameraOrientation _orientation;
        private readonly bool _flipVertical;
        private readonly int _generation;
        private readonly Func<CameraViewRole, int> _getGeneration;
        private readonly Action<ImageSource> _setImage;
        private readonly Action<string> _log;
        private IGXDevice? _device;
        private IGXStream? _stream;
        private IGXFeatureControl? _featureControl;
        private IGXImageFormatConvert? _formatConvert;
        private IntPtr _convertBuffer = IntPtr.Zero;
        private ulong _convertBufferSize;
        private byte[]? _previewSourceBuffer;
        private byte[]? _previewTransformBuffer;
        private byte[]? _previewUiBuffer;
        private readonly object _previewUiLock = new();
        private bool _previewUiFramePending;
        private WriteableBitmap? _previewBitmap;
        private int _previewBitmapWidth;
        private int _previewBitmapHeight;
        private bool _previewBitmapAssigned;
        private DateTimeOffset _lastFrameAt = DateTimeOffset.MinValue;
        private bool _isRunning;

        public DahengPreviewSlot(
            IGXFactory factory,
            CameraViewRole role,
            string serialNumber,
            string resolution,
            CameraOrientation orientation,
            bool flipVertical,
            int generation,
            Func<CameraViewRole, int> getGeneration,
            Action<ImageSource> setImage,
            Action<string> log)
        {
            _factory = factory;
            Role = role;
            SerialNumber = serialNumber;
            _resolution = resolution;
            _orientation = orientation;
            _flipVertical = flipVertical;
            _generation = generation;
            _getGeneration = getGeneration;
            _setImage = setImage;
            _log = log;
        }

        public CameraViewRole Role { get; }

        private string SerialNumber { get; }

        public void Start()
        {
            try
            {
                DahengRecordingRetry.Execute(
                    maxAttempts: 3,
                    action: OpenDeviceAndStream,
                    waitBeforeRetry: attempt =>
                    {
                        CloseDeviceAndStream();
                        Thread.Sleep(250 * attempt);
                    });

                ConfigurePreviewDevice();
                _stream!.RegisterCaptureCallback(this, OnFrameCallback);
                _stream.StartGrab();
                _featureControl!.GetCommandFeature("AcquisitionStart").Execute();
                _isRunning = true;
            }
            catch (Exception ex)
            {
                CloseDeviceAndStream();
                throw new InvalidOperationException(
                    $"大恒相机 {SerialNumber} 的实时预览启动失败，请稍候重试或重新连接相机。",
                    ex);
            }
        }

        private void OpenDeviceAndStream()
        {
            _device = _factory.OpenDeviceBySN(SerialNumber, GX_ACCESS_MODE.GX_ACCESS_EXCLUSIVE)
                      ?? throw new InvalidOperationException($"未能打开大恒相机 {SerialNumber}。");
            _featureControl = _device.GetRemoteFeatureControl()
                              ?? throw new InvalidOperationException($"未能读取大恒相机 {SerialNumber} 的功能控制器。");
            _stream = _device.OpenStream(0)
                      ?? throw new InvalidOperationException($"未能打开大恒相机 {SerialNumber} 的采集流。");
            _formatConvert = _factory.CreateImageFormatConvert()
                             ?? throw new InvalidOperationException("未能创建大恒图像格式转换器。");
        }

        private void CloseDeviceAndStream()
        {
            try
            {
                _stream?.StopGrab();
                _stream?.UnregisterCaptureCallback();
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
            _device = null;
            _featureControl = null;
            _formatConvert = null;
        }

        private void ConfigurePreviewDevice()
        {
            if (_featureControl is null)
            {
                return;
            }

            TryLoadUserSet0(_featureControl);
            TrySetEnum(_featureControl, "AcquisitionMode", "Continuous");
            TrySetEnum(_featureControl, "TriggerMode", "Off");
            var (width, height) = ParseResolution(_resolution);
            TrySetInt(_featureControl, "Width", width);
            TrySetInt(_featureControl, "Height", height);
            TrySetInt(_featureControl, "OffsetX", 0);
            TrySetInt(_featureControl, "OffsetY", 0);
            TrySetBool(_featureControl, "AcquisitionFrameRateEnable", true);
            TrySetEnum(_featureControl, "AcquisitionFrameRateMode", "On");
            TrySetFloat(_featureControl, "AcquisitionFrameRate", PreviewInputFrameRate);
        }

        private void OnFrameCallback(object userParam, IFrameData frameData)
        {
            if (!_isRunning || frameData.GetStatus() != GX_FRAME_STATUS_LIST.GX_FRAME_STATUS_SUCCESS)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _lastFrameAt < TimeSpan.FromSeconds(1.0 / DahengPreviewDisplayFrameRate))
            {
                return;
            }

            _lastFrameAt = now;
            try
            {
                var frame = ApplyTransform(ConvertFrameToReusableBgrFrame(frameData));
                if (!TryQueuePreviewFrame(frame))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(() => _log($"大恒预览帧处理失败({SerialNumber}): {ex.Message}"));
            }
        }

        private BgrPreviewFrame ConvertFrameToReusableBgrFrame(IFrameData frameData)
        {
            lock (_convertLock)
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

                var width = checked((int)frameData.GetWidth());
                var height = checked((int)frameData.GetHeight());
                var stride = checked(width * 3);
                var length = checked(stride * height);
                _previewSourceBuffer = EnsureManagedBuffer(_previewSourceBuffer, length);
                Marshal.Copy(_convertBuffer, _previewSourceBuffer, 0, length);
                return new BgrPreviewFrame(_previewSourceBuffer, width, height, stride);
            }
        }

        private BgrPreviewFrame ApplyTransform(BgrPreviewFrame frame)
        {
            if (_orientation != CameraOrientation.PortraitClockwise && !_flipVertical)
            {
                return frame;
            }

            var rotated = _orientation == CameraOrientation.PortraitClockwise;
            var outputWidth = rotated ? frame.Height : frame.Width;
            var outputHeight = rotated ? frame.Width : frame.Height;
            var outputStride = checked(outputWidth * 3);
            var outputLength = checked(outputStride * outputHeight);
            _previewTransformBuffer = EnsureManagedBuffer(_previewTransformBuffer, outputLength);

            for (var y = 0; y < outputHeight; y++)
            {
                var rotatedY = _flipVertical ? outputHeight - 1 - y : y;
                for (var x = 0; x < outputWidth; x++)
                {
                    var sourceX = rotated ? rotatedY : x;
                    var sourceY = rotated ? frame.Height - 1 - x : rotatedY;
                    var sourceIndex = sourceY * frame.Stride + sourceX * 3;
                    var targetIndex = y * outputStride + x * 3;
                    _previewTransformBuffer[targetIndex] = frame.Bytes[sourceIndex];
                    _previewTransformBuffer[targetIndex + 1] = frame.Bytes[sourceIndex + 1];
                    _previewTransformBuffer[targetIndex + 2] = frame.Bytes[sourceIndex + 2];
                }
            }

            return new BgrPreviewFrame(_previewTransformBuffer, outputWidth, outputHeight, outputStride);
        }

        private bool TryQueuePreviewFrame(BgrPreviewFrame frame)
        {
            lock (_previewUiLock)
            {
                if (_previewUiFramePending)
                {
                    return false;
                }

                _previewUiBuffer = EnsureManagedBuffer(_previewUiBuffer, frame.Bytes.Length);
                Buffer.BlockCopy(frame.Bytes, 0, _previewUiBuffer, 0, frame.Bytes.Length);
                _previewUiFramePending = true;
            }

            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (_generation == _getGeneration(Role))
                        {
                            UpdatePreviewBitmap(frame.Width, frame.Height, frame.Stride);
                        }
                    }
                    finally
                    {
                        lock (_previewUiLock)
                        {
                            _previewUiFramePending = false;
                        }
                    }
                }, DispatcherPriority.Render);
            }
            catch
            {
                lock (_previewUiLock)
                {
                    _previewUiFramePending = false;
                }

                return false;
            }

            return true;
        }

        private void UpdatePreviewBitmap(int width, int height, int stride)
        {
            if (_previewUiBuffer is null)
            {
                return;
            }

            if (_previewBitmap is null || _previewBitmapWidth != width || _previewBitmapHeight != height)
            {
                _previewBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                _previewBitmapWidth = width;
                _previewBitmapHeight = height;
                _previewBitmapAssigned = false;
            }

            _previewBitmap.WritePixels(new Int32Rect(0, 0, width, height), _previewUiBuffer, stride, 0);
            if (!_previewBitmapAssigned)
            {
                _setImage(_previewBitmap);
                _previewBitmapAssigned = true;
            }
        }

        private static byte[] EnsureManagedBuffer(byte[]? buffer, int requiredLength)
        {
            return buffer is not null && buffer.Length >= requiredLength
                ? buffer
                : new byte[requiredLength];
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
            _isRunning = false;
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

            ReleaseConvertBuffer();
            _previewSourceBuffer = null;
            _previewTransformBuffer = null;
            _previewUiBuffer = null;
            _previewBitmap = null;
        }
    }

    private sealed record BgrPreviewFrame(byte[] Bytes, int Width, int Height, int Stride);

    private static void TryLoadUserSet0(IGXFeatureControl featureControl)
    {
        try
        {
            if (featureControl.IsImplemented("UserSetSelector")
                && featureControl.IsWritable("UserSetSelector")
                && featureControl.IsImplemented("UserSetLoad"))
            {
                featureControl.GetEnumFeature("UserSetSelector").SetValue("UserSet0");
                featureControl.GetCommandFeature("UserSetLoad").Execute();
            }
        }
        catch
        {
        }
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
}

public enum CameraCaptureUiState
{
    Preview,
    Recording,
    Completed
}

public sealed record FrameRateOption(string Name, int Value)
{
    public override string ToString() => Name;
}

public sealed record DurationOption(string Name, int Value)
{
    public override string ToString() => Name;
}

