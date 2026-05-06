using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.Testing;

public partial class CameraRecordingTestViewModel : ObservableObject
{
    private readonly ICameraRecordingService _cameraRecordingService;
    private CancellationTokenSource? _recordingCancellation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    private string _ffmpegPath = @"D:\ffmpeg\bin\ffmpeg.exe";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSaveDirectoryCommand))]
    private string _saveDirectory = @"D:\ffmpeg\video";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    private string _cameraNamesText = "Y-CAM-25320046\r\nY-CAM-24500213";

    [ObservableProperty]
    private string _selectedResolution = "3840x2160";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    private int _frameRate = 59;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    private int _durationSeconds = 6;

    [ObservableProperty]
    private bool _transcodeToMp4 = true;

    [ObservableProperty]
    private bool _deleteAviAfterMp4 = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRecordingCommand))]
    private bool _isRecording;

    [ObservableProperty]
    private string _statusText = "待机";

    public ObservableCollection<string> Resolutions { get; } = new()
    {
        "3840x2160",
        "2560x1440",
        "1920x1080",
        "1280x720",
    };

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<string> OutputFiles { get; } = new();

    public CameraRecordingTestViewModel(ICameraRecordingService cameraRecordingService)
    {
        _cameraRecordingService = cameraRecordingService;
    }

    private bool CanStartRecording()
    {
        return !IsRecording
            && !string.IsNullOrWhiteSpace(FfmpegPath)
            && !string.IsNullOrWhiteSpace(SaveDirectory)
            && !string.IsNullOrWhiteSpace(CameraNamesText)
            && FrameRate > 0
            && DurationSeconds > 0;
    }

    private bool CanCancelRecording() => IsRecording;

    private bool CanOpenSaveDirectory() => !string.IsNullOrWhiteSpace(SaveDirectory);

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        _recordingCancellation?.Dispose();
        _recordingCancellation = new CancellationTokenSource();

        LogLines.Clear();
        OutputFiles.Clear();
        IsRecording = true;
        StatusText = "录制中";

        try
        {
            var options = new CameraRecordingOptions
            {
                FfmpegPath = FfmpegPath.Trim(),
                SaveDirectory = SaveDirectory.Trim(),
                CameraNames = ParseCameraNames(CameraNamesText),
                VideoSize = SelectedResolution,
                FrameRate = FrameRate,
                DurationSeconds = DurationSeconds,
                TranscodeToMp4 = TranscodeToMp4,
                DeleteAviAfterMp4 = DeleteAviAfterMp4,
            };

            var progress = new Progress<string>(AppendLog);
            var results = await _cameraRecordingService.RecordAsync(
                options,
                progress,
                _recordingCancellation.Token);

            foreach (var result in results)
            {
                if (!string.IsNullOrWhiteSpace(result.Mp4File))
                {
                    OutputFiles.Add(result.Mp4File);
                }
                else
                {
                    OutputFiles.Add(result.AviFile);
                }
            }

            StatusText = "完成";
        }
        catch (OperationCanceledException)
        {
            AppendLog("任务已取消");
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            AppendLog($"错误: {ex.Message}");
            StatusText = "失败";
        }
        finally
        {
            IsRecording = false;
            _recordingCancellation?.Dispose();
            _recordingCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelRecording))]
    private void CancelRecording()
    {
        _recordingCancellation?.Cancel();
        AppendLog("正在取消 FFmpeg 任务...");
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogLines.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSaveDirectory))]
    private void OpenSaveDirectory()
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = SaveDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppendLog($"打开目录失败: {ex.Message}");
        }
    }

    private void AppendLog(string message)
    {
        LogLines.Add(message);

        const int maxLines = 500;
        while (LogLines.Count > maxLines)
        {
            LogLines.RemoveAt(0);
        }
    }

    private static IReadOnlyList<string> ParseCameraNames(string cameraNamesText)
    {
        return cameraNamesText
            .Split(new[] { "\r\n", "\n", "\r", ",", ";", "，", "；" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
