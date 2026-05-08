using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BTFX.ViewModels.Measurement;
using MaterialDesignThemes.Wpf;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BTFX.Views.Measurement;

/// <summary>
/// Step3ReviewView.xaml 的交互逻辑。
/// </summary>
public partial class Step3ReviewView : UserControl
{
    private static readonly object LogLock = new();
    private readonly DispatcherTimer _playbackTimer;
    private readonly Stopwatch _clock = new();
    private VideoCapture? _frontDualCapture;
    private VideoCapture? _sideDualCapture;
    private VideoCapture? _frontSingleCapture;
    private VideoCapture? _sideSingleCapture;
    private MediaElement? _activeMediaElement;
    private bool _isPlaying;
    private bool _isDragging;
    private bool _isUpdatingSlider;
    private bool _resumeAfterDrag;
    private bool _isPreparingPreview;
    private bool _isPlaybackReady;
    private bool _pendingPlayAfterPrepare;
    private double _clockBaseSeconds;
    private double _playbackStartSeconds;
    private double _durationSeconds;
    private double _speedRatio = 1.0;
    private double _previewFrameRate = 20.0;
    private string _logFilePath = string.Empty;

    public Step3ReviewView()
    {
        _logFilePath = CreateLogFilePath();
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / _previewFrameRate)
        };
        _playbackTimer.Tick += PlaybackTimer_OnTick;

        InitializeComponent();
        SetPlaybackControlsEnabled(false);
        SetPlaybackStatus("正在准备预览...");

        Loaded += Step3ReviewView_OnLoaded;
        Unloaded += Step3ReviewView_OnUnloaded;
        DataContextChanged += Step3ReviewView_OnDataContextChanged;
    }

    private async void Step3ReviewView_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadPlayersAsync();
    }

    private void Step3ReviewView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        DisposeCaptures();
        ClearImages();
        ClearPreviewMedia();
    }

    private async void Step3ReviewView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await ReloadPlayersAsync();
        }
    }

    private async Task ReloadPlayersAsync()
    {
        WriteLog("ReloadPlayersAsync started.");
        _isPreparingPreview = true;
        _isPlaybackReady = false;
        _pendingPlayAfterPrepare = false;
        SetPlaybackControlsEnabled(false);
        SetPlaybackStatus("正在准备预览...");
        StopPlayback();
        DisposeCaptures();
        ClearImages();
        ClearPreviewMedia();

        _durationSeconds = GetMetadataDurationSeconds();
        ProgressSlider.Maximum = _durationSeconds;
        _clockBaseSeconds = 0;
        SetSliderValue(0);
        UpdateTimeText(0);
        UpdatePlayIcon();

        if (DataContext is not MeasurementViewModel vm)
        {
            WriteLog("ReloadPlayersAsync aborted: DataContext is not MeasurementViewModel.");
            _isPreparingPreview = false;
            SetPlaybackStatus("未检测到视频");
            return;
        }

        try
        {
            WriteLog($"Context: HasDual={vm.HasDualVideo}, HasFront={vm.HasFrontVideo}, HasSide={vm.HasSideVideo}, Front='{vm.FrontVideoPath}', Side='{vm.SideVideoPath}', Duration={_durationSeconds:F3}");

            if (vm.HasDualVideo)
            {
                var combinedPath = await PrepareCombinedPreviewVideoAsync(vm.FrontVideoPath, vm.SideVideoPath);
                SetPreviewMediaSource(DualPreviewMediaElement, combinedPath);
            }
            else if (vm.HasFrontVideo)
            {
                var frontPath = await PreparePreviewVideoAsync(vm.FrontVideoPath, vm.FrontVideoInfo);
                SetPreviewMediaSource(SinglePreviewMediaElement, frontPath);
            }
            else
            {
                var sidePath = await PreparePreviewVideoAsync(vm.SideVideoPath, vm.SideVideoInfo);
                SetPreviewMediaSource(SinglePreviewMediaElement, sidePath);
            }

            _clockBaseSeconds = 0;
            SetSliderValue(0);
            UpdateTimeText(0);
            SetPlaybackStatus("正在加载预览...");
            SetPlaybackControlsEnabled(_isPlaybackReady);

        }
        finally
        {
            _isPreparingPreview = false;
            if (!_isPlaybackReady)
            {
                SetPlaybackControlsEnabled(false);
            }
            else if (_pendingPlayAfterPrepare)
            {
                _pendingPlayAfterPrepare = false;
                StartPlayback();
            }
        }
    }

    private void SetPreviewMediaSource(MediaElement element, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            WriteLog($"SetPreviewMediaSource failed: path missing. Path='{path}'");
            _isPreparingPreview = false;
            _isPlaybackReady = false;
            SetPlaybackStatus("预览加载失败");
            return;
        }

        _activeMediaElement = element;
        _isPlaybackReady = false;
        _clockBaseSeconds = 0;
        SetSliderValue(0);
        UpdateTimeText(0);
        element.Source = new Uri(path, UriKind.Absolute);
        element.SpeedRatio = _speedRatio;
        element.Position = TimeSpan.Zero;
        element.Volume = 0;
        element.Play();
        WriteLog($"SetPreviewMediaSource source='{path}'");
    }

    private async void PreviewMediaElement_OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _activeMediaElement) || _activeMediaElement == null)
        {
            return;
        }

        if (_activeMediaElement.NaturalDuration.HasTimeSpan)
        {
            _durationSeconds = _activeMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            ProgressSlider.Maximum = _durationSeconds;
        }

        var openedMediaElement = _activeMediaElement;
        openedMediaElement.SpeedRatio = 1.0;
        openedMediaElement.Position = TimeSpan.Zero;
        openedMediaElement.Play();
        await Task.Delay(180);
        if (!ReferenceEquals(openedMediaElement, _activeMediaElement))
        {
            return;
        }

        openedMediaElement.Pause();
        openedMediaElement.Position = TimeSpan.Zero;
        openedMediaElement.SpeedRatio = _speedRatio;

        _clockBaseSeconds = 0;
        SetSliderValue(0);
        UpdateTimeText(0);
        _isPreparingPreview = false;
        _isPlaybackReady = true;
        SetPlaybackStatus(string.Empty);
        SetPlaybackControlsEnabled(true);
        WriteLog($"Preview media opened. Duration={_durationSeconds:F3}");

        if (_pendingPlayAfterPrepare)
        {
            _pendingPlayAfterPrepare = false;
            StartPlayback();
        }
    }

    private void PreviewMediaElement_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _activeMediaElement))
        {
            return;
        }

        _isPlaying = false;
        _playbackTimer.Stop();
        _clock.Reset();
        _clockBaseSeconds = _durationSeconds;
        SetSliderValue(_durationSeconds);
        UpdateTimeText(_durationSeconds);
        UpdatePlayIcon();
    }

    private void PreviewMediaElement_OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _activeMediaElement))
        {
            return;
        }

        _isPreparingPreview = false;
        _isPlaybackReady = false;
        SetPlaybackControlsEnabled(false);
        SetPlaybackStatus("预览加载失败");
        WriteLog($"Preview media failed: {e.ErrorException}");
    }

    private void ClearPreviewMedia()
    {
        foreach (var mediaElement in new[] { DualPreviewMediaElement, SinglePreviewMediaElement })
        {
            mediaElement.Stop();
            mediaElement.Source = null;
        }

        _activeMediaElement = null;
    }

    private VideoCapture? OpenCapture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            WriteLog($"OpenCapture skipped: path missing. Path='{path}'");
            return null;
        }

        try
        {
            var capture = new VideoCapture(path);
            var opened = capture.IsOpened();
            WriteLog($"OpenCapture path='{path}', opened={opened}, width={capture.Get(VideoCaptureProperties.FrameWidth)}, height={capture.Get(VideoCaptureProperties.FrameHeight)}, fps={capture.Get(VideoCaptureProperties.Fps)}, frames={capture.Get(VideoCaptureProperties.FrameCount)}");
            if (opened)
            {
                return capture;
            }

            capture.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            WriteLog($"OpenCapture exception path='{path}': {ex}");
            return null;
        }
    }

    private async Task<string?> PreparePreviewVideoAsync(string? sourcePath, VideoFileInfoViewModel info)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            WriteLog($"PreparePreviewVideoAsync skipped: source missing. Source='{sourcePath}'");
            return sourcePath;
        }

        var ffmpegPath = ResolveFfmpegPath();
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            WriteLog($"PreparePreviewVideoAsync fallback: ffmpeg not found. Source='{sourcePath}'");
            return sourcePath;
        }

        var proxyPath = GetPreviewProxyPath(sourcePath);
        if (File.Exists(proxyPath))
        {
            var proxyLength = new FileInfo(proxyPath).Length;
            WriteLog($"PreparePreviewVideoAsync cache found. Source='{sourcePath}', Proxy='{proxyPath}', Length={proxyLength}");
            return proxyLength > 128 * 1024 ? proxyPath : sourcePath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(proxyPath)!);
        var tempPath = proxyPath + ".tmp.mp4";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(sourcePath);
            process.StartInfo.ArgumentList.Add("-vf");
            process.StartInfo.ArgumentList.Add("scale=w=min(960\\,iw):h=-2,fps=20");
            process.StartInfo.ArgumentList.Add("-an");
            process.StartInfo.ArgumentList.Add("-c:v");
            process.StartInfo.ArgumentList.Add("libx264");
            process.StartInfo.ArgumentList.Add("-preset");
            process.StartInfo.ArgumentList.Add("veryfast");
            process.StartInfo.ArgumentList.Add("-crf");
            process.StartInfo.ArgumentList.Add("28");
            process.StartInfo.ArgumentList.Add("-g");
            process.StartInfo.ArgumentList.Add("10");
            process.StartInfo.ArgumentList.Add("-keyint_min");
            process.StartInfo.ArgumentList.Add("10");
            process.StartInfo.ArgumentList.Add("-sc_threshold");
            process.StartInfo.ArgumentList.Add("0");
            process.StartInfo.ArgumentList.Add("-pix_fmt");
            process.StartInfo.ArgumentList.Add("yuv420p");
            process.StartInfo.ArgumentList.Add("-movflags");
            process.StartInfo.ArgumentList.Add("+faststart");
            process.StartInfo.ArgumentList.Add(tempPath);

            WriteLog($"PreparePreviewVideoAsync transcode start. Source='{sourcePath}', Proxy='{proxyPath}', Ffmpeg='{ffmpegPath}'");
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stderr = await stderrTask;

            if (process.ExitCode != 0 || !File.Exists(tempPath) || new FileInfo(tempPath).Length <= 128 * 1024)
            {
                WriteLog($"PreparePreviewVideoAsync transcode failed. ExitCode={process.ExitCode}, TempExists={File.Exists(tempPath)}, Error='{stderr}'");
                return sourcePath;
            }

            if (File.Exists(proxyPath))
            {
                File.Delete(proxyPath);
            }

            File.Move(tempPath, proxyPath);
            WriteLog($"PreparePreviewVideoAsync transcode success. Proxy='{proxyPath}', Length={new FileInfo(proxyPath).Length}");
            return proxyPath;
        }
        catch (Exception ex)
        {
            WriteLog($"PreparePreviewVideoAsync exception. Source='{sourcePath}', Error={ex}");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return sourcePath;
        }
    }

    private async Task<string?> PrepareCombinedPreviewVideoAsync(string? frontPath, string? sidePath)
    {
        if (string.IsNullOrWhiteSpace(frontPath) || string.IsNullOrWhiteSpace(sidePath) ||
            !File.Exists(frontPath) || !File.Exists(sidePath))
        {
            WriteLog($"PrepareCombinedPreviewVideoAsync skipped: source missing. Front='{frontPath}', Side='{sidePath}'");
            return null;
        }

        var ffmpegPath = ResolveFfmpegPath();
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            WriteLog("PrepareCombinedPreviewVideoAsync failed: ffmpeg not found.");
            return null;
        }

        var proxyPath = GetCombinedPreviewProxyPath(frontPath, sidePath);
        if (File.Exists(proxyPath))
        {
            var proxyLength = new FileInfo(proxyPath).Length;
            WriteLog($"PrepareCombinedPreviewVideoAsync cache found. Proxy='{proxyPath}', Length={proxyLength}");
            return proxyLength > 128 * 1024 ? proxyPath : null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(proxyPath)!);
        var tempPath = proxyPath + ".tmp.mp4";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            const string filter =
                "[0:v]scale=640:360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2,fps=24,setpts=PTS-STARTPTS[left];" +
                "[1:v]scale=640:360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2,fps=24,setpts=PTS-STARTPTS[right];" +
                "[left][right]hstack=inputs=2[v]";

            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(frontPath);
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(sidePath);
            process.StartInfo.ArgumentList.Add("-filter_complex");
            process.StartInfo.ArgumentList.Add(filter);
            process.StartInfo.ArgumentList.Add("-map");
            process.StartInfo.ArgumentList.Add("[v]");
            process.StartInfo.ArgumentList.Add("-an");
            process.StartInfo.ArgumentList.Add("-shortest");
            process.StartInfo.ArgumentList.Add("-c:v");
            process.StartInfo.ArgumentList.Add("libx264");
            process.StartInfo.ArgumentList.Add("-preset");
            process.StartInfo.ArgumentList.Add("veryfast");
            process.StartInfo.ArgumentList.Add("-crf");
            process.StartInfo.ArgumentList.Add("28");
            process.StartInfo.ArgumentList.Add("-g");
            process.StartInfo.ArgumentList.Add("24");
            process.StartInfo.ArgumentList.Add("-keyint_min");
            process.StartInfo.ArgumentList.Add("24");
            process.StartInfo.ArgumentList.Add("-sc_threshold");
            process.StartInfo.ArgumentList.Add("0");
            process.StartInfo.ArgumentList.Add("-pix_fmt");
            process.StartInfo.ArgumentList.Add("yuv420p");
            process.StartInfo.ArgumentList.Add("-movflags");
            process.StartInfo.ArgumentList.Add("+faststart");
            process.StartInfo.ArgumentList.Add(tempPath);

            WriteLog($"PrepareCombinedPreviewVideoAsync transcode start. Front='{frontPath}', Side='{sidePath}', Proxy='{proxyPath}', Ffmpeg='{ffmpegPath}'");
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stderr = await stderrTask;

            if (process.ExitCode != 0 || !File.Exists(tempPath) || new FileInfo(tempPath).Length <= 128 * 1024)
            {
                WriteLog($"PrepareCombinedPreviewVideoAsync transcode failed. ExitCode={process.ExitCode}, TempExists={File.Exists(tempPath)}, Error='{stderr}'");
                return null;
            }

            if (File.Exists(proxyPath))
            {
                File.Delete(proxyPath);
            }

            File.Move(tempPath, proxyPath);
            WriteLog($"PrepareCombinedPreviewVideoAsync transcode success. Proxy='{proxyPath}', Length={new FileInfo(proxyPath).Length}");
            return proxyPath;
        }
        catch (Exception ex)
        {
            WriteLog($"PrepareCombinedPreviewVideoAsync exception. Front='{frontPath}', Side='{sidePath}', Error={ex}");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return null;
        }
    }

    private static bool ShouldUsePreviewProxy(VideoFileInfoViewModel info)
    {
        var (width, height) = ParseResolution(info.Resolution);
        return info.DurationSeconds >= 60
            || info.FrameRate > 30.5
            || width > 1280
            || height > 720;
    }

    private static (int Width, int Height) ParseResolution(string resolution)
    {
        var parts = resolution.Split('x', 'X');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var width) &&
            int.TryParse(parts[1], out var height))
        {
            return (width, height);
        }

        return (0, 0);
    }

    private static string GetPreviewProxyPath(string sourcePath)
    {
        var file = new FileInfo(sourcePath);
        const string previewCacheVersion = "opencv-preview-v3-960w-20fps";
        var key = $"{previewCacheVersion}|{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(Path.GetTempPath(), "BTFX", "PreviewCache", $"{hash}.mp4");
    }

    private static string GetCombinedPreviewProxyPath(string frontPath, string sidePath)
    {
        var front = new FileInfo(frontPath);
        var side = new FileInfo(sidePath);
        const string previewCacheVersion = "combined-preview-v1-1280x360-24fps";
        var key = string.Join(
            "|",
            previewCacheVersion,
            front.FullName,
            front.Length,
            front.LastWriteTimeUtc.Ticks,
            side.FullName,
            side.Length,
            side.LastWriteTimeUtc.Ticks);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(Path.GetTempPath(), "BTFX", "PreviewCache", $"{hash}.mp4");
    }

    private static string? ResolveFfmpegPath()
    {
        var candidates = new[]
        {
            @"D:\ffmpeg\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            "ffmpeg.exe"
        };

        return candidates.FirstOrDefault(candidate =>
        {
            if (Path.IsPathRooted(candidate))
            {
                return File.Exists(candidate);
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                return process?.WaitForExit(1000) == true && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    private void PlayPauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            PausePlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    private void PrevFrameButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isPlaybackReady)
        {
            return;
        }

        PausePlayback();
        SeekTo(Math.Max(0, _clockBaseSeconds - GetFrameStepSeconds()));
    }

    private void NextFrameButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isPlaybackReady)
        {
            return;
        }

        PausePlayback();
        SeekTo(Math.Min(_durationSeconds, _clockBaseSeconds + GetFrameStepSeconds()));
    }

    private void ProgressSlider_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPlaybackReady)
        {
            return;
        }

        _resumeAfterDrag = _isPlaying;
        _isDragging = true;
        PausePlayback();
        SeekSliderToMousePosition(e);
        ProgressSlider.CaptureMouse();
        e.Handled = true;
    }

    private void ProgressSlider_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPlaybackReady || !_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        SeekSliderToMousePosition(e);
        e.Handled = true;
    }

    private void ProgressSlider_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPlaybackReady)
        {
            return;
        }

        SeekSliderToMousePosition(e);
        _isDragging = false;
        ProgressSlider.ReleaseMouseCapture();
        SeekTo(ProgressSlider.Value);

        if (_resumeAfterDrag)
        {
            StartPlayback();
        }

        _resumeAfterDrag = false;
        e.Handled = true;
    }

    private void ProgressSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSlider)
        {
            return;
        }

        UpdateTimeText(e.NewValue);
        if (_isPlaybackReady && (_isDragging || ProgressSlider.IsMouseCaptureWithin || ProgressSlider.IsKeyboardFocusWithin))
        {
            SeekTo(e.NewValue);
        }
    }

    private void SpeedComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeedComboBox.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
        {
            if (_isPlaying)
            {
                _clockBaseSeconds = _activeMediaElement?.Position.TotalSeconds ?? GetPlaybackClockSeconds();
                _playbackStartSeconds = _clockBaseSeconds;
                _clock.Restart();
            }

            _speedRatio = speed;
            if (_activeMediaElement != null)
            {
                _activeMediaElement.SpeedRatio = _speedRatio;
            }

            if (_playbackTimer != null)
            {
                _playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _previewFrameRate);
            }
        }
    }

    private void PlaybackTimer_OnTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || !_isPlaybackReady || _isPreparingPreview || _isDragging || _activeMediaElement == null)
        {
            return;
        }

        var seconds = Math.Clamp(_activeMediaElement.Position.TotalSeconds, 0, Math.Max(0, _durationSeconds));
        _clockBaseSeconds = seconds;
        SetSliderValue(seconds);
        UpdateTimeText(seconds);
    }

    private void StartPlayback()
    {
        if (_isPreparingPreview)
        {
            _pendingPlayAfterPrepare = true;
            WriteLog("StartPlayback deferred: preview is preparing.");
            return;
        }

        if (!_isPlaybackReady || _activeMediaElement == null)
        {
            WriteLog("StartPlayback ignored: playback is not ready.");
            return;
        }

        if (_durationSeconds > 0 && _clockBaseSeconds >= _durationSeconds)
        {
            SeekTo(0);
        }

        _isPlaying = true;
        _activeMediaElement.SpeedRatio = _speedRatio;
        _activeMediaElement.Position = TimeSpan.FromSeconds(Math.Clamp(_clockBaseSeconds, 0, Math.Max(0, _durationSeconds)));
        _activeMediaElement.Play();
        _clock.Restart();
        _playbackTimer.Start();
        UpdatePlayIcon();
    }

    private void PausePlayback()
    {
        if (_isPlaying)
        {
            _clockBaseSeconds = _activeMediaElement?.Position.TotalSeconds ?? _clockBaseSeconds;
            _activeMediaElement?.Pause();
        }

        _isPlaying = false;
        _clock.Reset();
        _playbackTimer.Stop();
        UpdatePlayIcon();
    }

    private void StopPlayback()
    {
        _isPlaying = false;
        _clock.Reset();
        _playbackTimer.Stop();
        _activeMediaElement?.Pause();
        UpdatePlayIcon();
    }

    private void SeekTo(double seconds)
    {
        seconds = Math.Clamp(seconds, 0, Math.Max(0, _durationSeconds));
        _clockBaseSeconds = seconds;
        if (_activeMediaElement != null)
        {
            _activeMediaElement.Position = TimeSpan.FromSeconds(seconds);
        }

        SetSliderValue(seconds);
        UpdateTimeText(seconds);
        if (_isPlaying)
        {
            RestartClock(seconds);
        }
    }

    private void SeekSliderToMousePosition(MouseEventArgs e)
    {
        if (ProgressSlider.ActualWidth <= 0 || ProgressSlider.Maximum <= ProgressSlider.Minimum)
        {
            return;
        }

        var position = e.GetPosition(ProgressSlider);
        var ratio = Math.Clamp(position.X / ProgressSlider.ActualWidth, 0, 1);
        var targetSeconds = ProgressSlider.Minimum + (ProgressSlider.Maximum - ProgressSlider.Minimum) * ratio;
        SeekTo(targetSeconds);
    }

    private double GetPlaybackClockSeconds()
    {
        if (!_clock.IsRunning)
        {
            return Math.Clamp(_clockBaseSeconds, 0, Math.Max(0, _durationSeconds));
        }

        return Math.Clamp(
            _playbackStartSeconds + _clock.Elapsed.TotalSeconds * _speedRatio,
            0,
            Math.Max(0, _durationSeconds));
    }

    private bool RenderFrameAt(double seconds, bool forceSeek)
    {
        if (DataContext is MeasurementViewModel { HasDualVideo: true })
        {
            var front = RenderFrame(_frontDualCapture, FrontDualImage, seconds, "front-dual", forceSeek);
            var side = RenderFrame(_sideDualCapture, SideDualImage, seconds, "side-dual", forceSeek);
            return front || side;
        }

        if (DataContext is MeasurementViewModel { HasFrontVideo: true })
        {
            return RenderFrame(_frontSingleCapture, FrontSingleImage, seconds, "front-single", forceSeek);
        }
        else
        {
            return RenderFrame(_sideSingleCapture, SideSingleImage, seconds, "side-single", forceSeek);
        }
    }

    private async Task<bool> RenderFrameAtAsync(double seconds, bool forceSeek)
    {
        var hasDual = DataContext is MeasurementViewModel { HasDualVideo: true };
        var hasFront = DataContext is MeasurementViewModel { HasFrontVideo: true };
        var result = await Task.Run(() => CaptureFrames(seconds, hasDual, hasFront, forceSeek));
        if (result.FrontDual != null)
        {
            FrontDualImage.Source = result.FrontDual;
        }

        if (result.SideDual != null)
        {
            SideDualImage.Source = result.SideDual;
        }

        if (result.FrontSingle != null)
        {
            FrontSingleImage.Source = result.FrontSingle;
        }

        if (result.SideSingle != null)
        {
            SideSingleImage.Source = result.SideSingle;
        }

        return result.HasAny;
    }

    private FrameCaptureResult CaptureFrames(double seconds, bool hasDual, bool hasFront, bool forceSeek)
    {
        if (hasDual)
        {
            return new FrameCaptureResult(
                CaptureFrame(_frontDualCapture, seconds, "front-dual", forceSeek),
                CaptureFrame(_sideDualCapture, seconds, "side-dual", forceSeek),
                null,
                null);
        }

        if (hasFront)
        {
            return new FrameCaptureResult(null, null, CaptureFrame(_frontSingleCapture, seconds, "front-single", forceSeek), null);
        }

        return new FrameCaptureResult(null, null, null, CaptureFrame(_sideSingleCapture, seconds, "side-single", forceSeek));
    }

    private bool RenderFrame(VideoCapture? capture, Image target, double seconds, string label, bool forceSeek)
    {
        if (capture == null || !capture.IsOpened())
        {
            WriteLog($"RenderFrame skipped: capture not open. Label={label}, Seconds={seconds:F3}");
            return false;
        }

        if (forceSeek)
        {
            AlignCaptureForTarget(capture, seconds, forceSeek, label);
        }

        using var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            WriteLog($"RenderFrame failed: read empty. Label={label}, Seconds={seconds:F3}, PosMsec={capture.Get(VideoCaptureProperties.PosMsec):F3}");
            return false;
        }

        var bitmap = frame.ToBitmapSource();
        bitmap.Freeze();
        target.Source = bitmap;
        return true;
    }

    private System.Windows.Media.Imaging.BitmapSource? CaptureFrame(VideoCapture? capture, double seconds, string label, bool forceSeek)
    {
        if (capture == null || !capture.IsOpened())
        {
            WriteLog($"CaptureFrame skipped: capture not open. Label={label}, Seconds={seconds:F3}");
            return null;
        }

        try
        {
            if (forceSeek)
            {
                AlignCaptureForTarget(capture, seconds, forceSeek, label);
            }
            else
            {
                AlignCaptureForTarget(capture, seconds, forceSeek, label);
            }

            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty())
            {
                WriteLog($"CaptureFrame failed: read empty. Label={label}, Seconds={seconds:F3}, PosMsec={capture.Get(VideoCaptureProperties.PosMsec):F3}");
                return null;
            }

            var bitmap = frame.ToBitmapSource();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            WriteLog($"CaptureFrame exception. Label={label}, Seconds={seconds:F3}, Error={ex}");
            return null;
        }
    }

    private IEnumerable<VideoCapture?> VisibleCaptures()
    {
        if (DataContext is MeasurementViewModel { HasDualVideo: true })
        {
            yield return _frontDualCapture;
            yield return _sideDualCapture;
            yield break;
        }

        if (DataContext is MeasurementViewModel { HasFrontVideo: true })
        {
            yield return _frontSingleCapture;
        }
        else
        {
            yield return _sideSingleCapture;
        }
    }

    private IEnumerable<VideoCapture?> AllCaptures()
    {
        yield return _frontDualCapture;
        yield return _sideDualCapture;
        yield return _frontSingleCapture;
        yield return _sideSingleCapture;
    }

    private void DisposeCaptures()
    {
        foreach (var capture in AllCaptures())
        {
            capture?.Dispose();
        }

        _frontDualCapture = null;
        _sideDualCapture = null;
        _frontSingleCapture = null;
        _sideSingleCapture = null;
    }

    private void ClearImages()
    {
        FrontDualImage.Source = null;
        SideDualImage.Source = null;
        FrontSingleImage.Source = null;
        SideSingleImage.Source = null;
    }

    private void RestartClock(double startSeconds)
    {
        _clockBaseSeconds = Math.Clamp(startSeconds, 0, Math.Max(0, _durationSeconds));
        _playbackStartSeconds = _clockBaseSeconds;
        _clock.Restart();
    }

    private void AlignCaptureForTarget(VideoCapture capture, double seconds, bool forceSeek, string label)
    {
        var safeSeconds = GetSafeCaptureSeekSeconds(capture, seconds);
        if (forceSeek)
        {
            capture.Set(VideoCaptureProperties.PosMsec, safeSeconds * 1000.0);
            return;
        }

        var currentSeconds = capture.Get(VideoCaptureProperties.PosMsec) / 1000.0;
        if (currentSeconds <= 0)
        {
            return;
        }

        var driftSeconds = safeSeconds - currentSeconds;
        var catchUpThresholdSeconds = Math.Max(0.10, 2.0 / _previewFrameRate);
        if (driftSeconds > catchUpThresholdSeconds || driftSeconds < -catchUpThresholdSeconds)
        {
            var fps = capture.Get(VideoCaptureProperties.Fps);
            var framesToSkip = fps > 0 ? (int)Math.Floor(driftSeconds * fps) : 0;
            if (framesToSkip is > 0 and <= 12)
            {
                for (var i = 0; i < framesToSkip; i++)
                {
                    if (!capture.Grab())
                    {
                        break;
                    }
                }

                return;
            }

            capture.Set(VideoCaptureProperties.PosMsec, safeSeconds * 1000.0);
        }
    }

    private double GetSafeSeekSeconds(double seconds)
    {
        if (_durationSeconds <= 0)
        {
            return 0;
        }

        var maxSeekSeconds = Math.Max(0, _durationSeconds - 1.0 / _previewFrameRate);
        return Math.Clamp(seconds, 0, maxSeekSeconds);
    }

    private double GetSafeCaptureSeekSeconds(VideoCapture capture, double seconds)
    {
        var fps = capture.Get(VideoCaptureProperties.Fps);
        var frameCount = capture.Get(VideoCaptureProperties.FrameCount);
        if (fps > 0 && frameCount > 1)
        {
            return Math.Clamp(seconds, 0, Math.Max(0, (frameCount - 1) / fps));
        }

        return GetSafeSeekSeconds(seconds);
    }

    private double GetMetadataDurationSeconds()
    {
        if (DataContext is not MeasurementViewModel vm)
        {
            return 0;
        }

        if (vm.HasDualVideo)
        {
            var side = vm.SideVideoInfo.DurationSeconds;
            var front = vm.FrontVideoInfo.DurationSeconds;
            return side > 0 && front > 0 ? Math.Min(side, front) : Math.Max(side, front);
        }

        return vm.HasFrontVideo ? vm.FrontVideoInfo.DurationSeconds : vm.SideVideoInfo.DurationSeconds;
    }

    private double GetFrameStepSeconds()
    {
        if (_activeMediaElement != null)
        {
            return 1.0 / 24.0;
        }

        if (DataContext is MeasurementViewModel vm)
        {
            var frameRate = vm.HasDualVideo
                ? Math.Max(vm.SideVideoInfo.FrameRate, vm.FrontVideoInfo.FrameRate)
                : vm.HasFrontVideo ? vm.FrontVideoInfo.FrameRate : vm.SideVideoInfo.FrameRate;

            if (frameRate > 0)
            {
                return 1.0 / frameRate;
            }
        }

        return 1.0 / 30.0;
    }

    private void SetSliderValue(double value)
    {
        _isUpdatingSlider = true;
        ProgressSlider.Value = Math.Clamp(value, ProgressSlider.Minimum, ProgressSlider.Maximum);
        _isUpdatingSlider = false;
    }

    private void UpdateTimeText(double positionSeconds)
    {
        TimeTextBlock.Text = $"{FormatTime(positionSeconds)} / {FormatTime(_durationSeconds)}";
    }

    private void SetPlaybackControlsEnabled(bool isEnabled)
    {
        PrevFrameButton.IsEnabled = isEnabled;
        PlayPauseButton.IsEnabled = isEnabled;
        NextFrameButton.IsEnabled = isEnabled;
        ProgressSlider.IsEnabled = isEnabled;
        SpeedComboBox.IsEnabled = isEnabled;
    }

    private void SetPlaybackStatus(string status)
    {
        PlaybackStatusTextBlock.Text = status;
        PlaybackStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(status)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void UpdatePlayIcon()
    {
        PlayPauseIcon.Kind = _isPlaying ? PackIconKind.Pause : PackIconKind.Play;
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss\.f")
            : time.ToString(@"mm\:ss\.f");
    }

    private static string CreateLogFilePath()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Data", "Logs");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"review-playback-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    private void WriteLog(string message)
    {
        try
        {
            lock (LogLock)
            {
                File.AppendAllText(
                    _logFilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must not break playback.
        }
    }

    private sealed record FrameCaptureResult(
        System.Windows.Media.Imaging.BitmapSource? FrontDual,
        System.Windows.Media.Imaging.BitmapSource? SideDual,
        System.Windows.Media.Imaging.BitmapSource? FrontSingle,
        System.Windows.Media.Imaging.BitmapSource? SideSingle)
    {
        public bool HasAny => FrontDual != null || SideDual != null || FrontSingle != null || SideSingle != null;
    }
}
