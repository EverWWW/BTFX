using System.Windows.Controls;
using System.Windows.Threading;
using BTFX.Models.Camera;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Testing;

public partial class CameraRecordingTestDialog : UserControl
{
    private readonly DispatcherTimer _playbackTimer;
    private readonly HashSet<MediaElement> _warmingElements = new();
    private MediaElement? _activePlaybackElement;
    private Slider? _activePlaybackSlider;
    private PackIcon? _activePlaybackIcon;
    private CameraRecordingTestViewModel? _viewModel;
    private string? _preparedSidePath;
    private string? _preparedFrontPath;
    private int _mediaCleanupQueued;

    public CameraRecordingTestDialog()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<CameraRecordingTestViewModel>();
        _viewModel = (CameraRecordingTestViewModel)DataContext;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _playbackTimer.Tick += PlaybackTimer_OnTick;

        SidePlaybackMediaElement.MediaOpened += async (_, _) => await WarmPlaybackElementAsync(SidePlaybackMediaElement, SidePlaybackSlider);
        FrontPlaybackMediaElement.MediaOpened += async (_, _) => await WarmPlaybackElementAsync(FrontPlaybackMediaElement, FrontPlaybackSlider);
        SidePlaybackMediaElement.MediaEnded += (_, _) => StopPlayback(SidePlaybackMediaElement, resetToStart: true);
        FrontPlaybackMediaElement.MediaEnded += (_, _) => StopPlayback(FrontPlaybackMediaElement, resetToStart: true);
    }

    public void Initialize(CameraCaptureMode mode)
    {
        if (DataContext is CameraRecordingTestViewModel vm)
        {
            vm.Initialize(mode);
        }
    }

    private void CameraRecordingTestDialog_OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ReleaseAllPlaybackResources();
        if (DataContext is CameraRecordingTestViewModel vm)
        {
            vm.StopAllMediaWork();
        }

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void SidePlaybackButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        PlayExclusive(SidePlaybackMediaElement, SidePlaybackSlider, SidePlaybackIcon);
    }

    private void FrontPlaybackButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        PlayExclusive(FrontPlaybackMediaElement, FrontPlaybackSlider, FrontPlaybackIcon);
    }

    private void CloseButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "是否退出视频采集？",
            "退出确认",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.OK)
        {
            DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }

    private void SidePlaybackSlider_OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SeekPlayback(SidePlaybackMediaElement, SidePlaybackSlider, e);
    }

    private void FrontPlaybackSlider_OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SeekPlayback(FrontPlaybackMediaElement, FrontPlaybackSlider, e);
    }

    private void PlayExclusive(MediaElement element, Slider slider, PackIcon icon)
    {
        if (ReferenceEquals(_activePlaybackElement, element))
        {
            StopPlayback(element, resetToStart: true);
            return;
        }

        StopPlayback(SidePlaybackMediaElement, resetToStart: true);
        StopPlayback(FrontPlaybackMediaElement, resetToStart: true);
        _activePlaybackElement = element;
        _activePlaybackSlider = slider;
        _activePlaybackIcon = icon;
        if (slider.Maximum > 0 && element.Position.TotalSeconds >= slider.Maximum - 0.05)
        {
            element.Position = TimeSpan.Zero;
            slider.Value = 0;
        }

        icon.Kind = PackIconKind.Stop;
        element.Volume = 0;
        element.Visibility = System.Windows.Visibility.Visible;
        element.Play();
        _playbackTimer.Start();
    }

    private void StopPlayback(MediaElement element, bool resetToStart = false)
    {
        element.Pause();
        if (resetToStart)
        {
            element.Position = TimeSpan.Zero;
            if (ReferenceEquals(element, SidePlaybackMediaElement))
            {
                SidePlaybackSlider.Value = 0;
                SidePlaybackElapsedText.Text = FormatPlaybackTime(TimeSpan.Zero);
            }
            else if (ReferenceEquals(element, FrontPlaybackMediaElement))
            {
                FrontPlaybackSlider.Value = 0;
                FrontPlaybackElapsedText.Text = FormatPlaybackTime(TimeSpan.Zero);
            }
        }

        element.Visibility = System.Windows.Visibility.Hidden;

        if (ReferenceEquals(element, SidePlaybackMediaElement))
        {
            SidePlaybackIcon.Kind = PackIconKind.Play;
        }
        else if (ReferenceEquals(element, FrontPlaybackMediaElement))
        {
            FrontPlaybackIcon.Kind = PackIconKind.Play;
        }

        if (ReferenceEquals(_activePlaybackElement, element))
        {
            _activePlaybackElement = null;
            _activePlaybackSlider = null;
            _activePlaybackIcon = null;
            _playbackTimer.Stop();
        }
    }

    private void SeekPlayback(MediaElement element, Slider slider, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (slider.ActualWidth <= 0 || slider.Maximum <= 0)
        {
            return;
        }

        var position = e.GetPosition(slider);
        var ratio = Math.Clamp(position.X / slider.ActualWidth, 0, 1);
        var seconds = slider.Maximum * ratio;
        element.Position = TimeSpan.FromSeconds(seconds);
        slider.Value = seconds;
        e.Handled = true;
    }

    private void PlaybackTimer_OnTick(object? sender, EventArgs e)
    {
        if (_activePlaybackElement == null || _activePlaybackSlider == null)
        {
            return;
        }

        _activePlaybackSlider.Value = _activePlaybackElement.Position.TotalSeconds;
        if (ReferenceEquals(_activePlaybackElement, SidePlaybackMediaElement))
        {
            SidePlaybackElapsedText.Text = FormatPlaybackTime(_activePlaybackElement.Position);
        }
        else if (ReferenceEquals(_activePlaybackElement, FrontPlaybackMediaElement))
        {
            FrontPlaybackElapsedText.Text = FormatPlaybackTime(_activePlaybackElement.Position);
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(CameraRecordingTestViewModel.CaptureState))
        {
            if (_viewModel.IsCompletedState)
            {
                _ = PrepareCompletedPlaybackSourcesAsync();
            }
            else
            {
                _preparedSidePath = null;
                _preparedFrontPath = null;
                ReleaseAllPlaybackResources();
            }
        }
        else if (e.PropertyName == nameof(CameraRecordingTestViewModel.SideOutputPath)
                 || e.PropertyName == nameof(CameraRecordingTestViewModel.FrontOutputPath))
        {
            if (_viewModel.IsCompletedState)
            {
                _ = PrepareCompletedPlaybackSourcesAsync();
            }
        }
    }

    private async Task PrepareCompletedPlaybackSourcesAsync()
    {
        if (_viewModel == null || !_viewModel.IsCompletedState)
        {
            return;
        }

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        if (_viewModel == null || !_viewModel.IsCompletedState)
        {
            return;
        }

        if (!string.Equals(_preparedSidePath, _viewModel.SideOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            _preparedSidePath = _viewModel.SideOutputPath;
            PreparePlaybackSource(SidePlaybackMediaElement, SidePlaybackSlider, _viewModel.SideOutputPath);
        }

        var frontPath = _viewModel.IsDualMode ? _viewModel.FrontOutputPath : null;
        if (!string.Equals(_preparedFrontPath, frontPath, StringComparison.OrdinalIgnoreCase))
        {
            _preparedFrontPath = frontPath;
            PreparePlaybackSource(FrontPlaybackMediaElement, FrontPlaybackSlider, frontPath);
        }
    }

    private void PreparePlaybackSource(MediaElement element, Slider slider, string? path)
    {
        slider.Value = 0;
        slider.Maximum = 0;
        SetPlaybackTimeText(element, TimeSpan.Zero, TimeSpan.Zero);
        ReleasePlaybackElement(element, resetSlider: false);

        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return;
        }

        element.Volume = 0;
        element.Position = TimeSpan.Zero;
        element.Visibility = System.Windows.Visibility.Hidden;
        element.Source = new Uri(path, UriKind.Absolute);
        element.Play();
    }

    private void ReleaseAllPlaybackResources()
    {
        StopPlayback(SidePlaybackMediaElement, resetToStart: true);
        StopPlayback(FrontPlaybackMediaElement, resetToStart: true);
        ReleasePlaybackElement(SidePlaybackMediaElement, resetSlider: true);
        ReleasePlaybackElement(FrontPlaybackMediaElement, resetSlider: true);
        _warmingElements.Clear();
        _activePlaybackElement = null;
        _activePlaybackSlider = null;
        _activePlaybackIcon = null;
        _preparedSidePath = null;
        _preparedFrontPath = null;
        _playbackTimer.Stop();
        QueueMediaResourceCleanup();
    }

    private void ReleasePlaybackElement(MediaElement element, bool resetSlider)
    {
        try
        {
            element.Stop();
            element.Source = null;
            element.ClearValue(MediaElement.SourceProperty);
            element.Volume = 0;
            element.Visibility = System.Windows.Visibility.Hidden;
        }
        catch
        {
        }

        if (resetSlider)
        {
            if (ReferenceEquals(element, SidePlaybackMediaElement))
            {
                SidePlaybackSlider.Value = 0;
                SidePlaybackSlider.Maximum = 0;
                SidePlaybackElapsedText.Text = FormatPlaybackTime(TimeSpan.Zero);
                SidePlaybackDurationText.Text = FormatPlaybackTime(TimeSpan.Zero);
                SidePlaybackIcon.Kind = PackIconKind.Play;
            }
            else if (ReferenceEquals(element, FrontPlaybackMediaElement))
            {
                FrontPlaybackSlider.Value = 0;
                FrontPlaybackSlider.Maximum = 0;
                FrontPlaybackElapsedText.Text = FormatPlaybackTime(TimeSpan.Zero);
                FrontPlaybackDurationText.Text = FormatPlaybackTime(TimeSpan.Zero);
                FrontPlaybackIcon.Kind = PackIconKind.Play;
            }
        }
    }

    private void QueueMediaResourceCleanup()
    {
        if (Interlocked.Exchange(ref _mediaCleanupQueued, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            finally
            {
                Interlocked.Exchange(ref _mediaCleanupQueued, 0);
            }
        });
    }

    private async Task WarmPlaybackElementAsync(MediaElement element, Slider slider)
    {
        if (!_warmingElements.Add(element))
        {
            return;
        }

        if (element.NaturalDuration.HasTimeSpan)
        {
            slider.Maximum = element.NaturalDuration.TimeSpan.TotalSeconds;
            SetPlaybackTimeText(element, TimeSpan.Zero, element.NaturalDuration.TimeSpan);
        }

        try
        {
            element.Volume = 0;
            element.Position = TimeSpan.Zero;
            element.Play();
            await Task.Delay(360);

            if (!ReferenceEquals(_activePlaybackElement, element))
            {
                element.Pause();
                element.Position = TimeSpan.Zero;
                slider.Value = 0;
                element.Visibility = System.Windows.Visibility.Hidden;
                SetPlaybackElapsedText(element, TimeSpan.Zero);
            }
        }
        finally
        {
            _warmingElements.Remove(element);
        }
    }

    private void SetPlaybackTimeText(MediaElement element, TimeSpan elapsed, TimeSpan duration)
    {
        if (ReferenceEquals(element, SidePlaybackMediaElement))
        {
            SidePlaybackElapsedText.Text = FormatPlaybackTime(elapsed);
            SidePlaybackDurationText.Text = FormatPlaybackTime(duration);
        }
        else if (ReferenceEquals(element, FrontPlaybackMediaElement))
        {
            FrontPlaybackElapsedText.Text = FormatPlaybackTime(elapsed);
            FrontPlaybackDurationText.Text = FormatPlaybackTime(duration);
        }
    }

    private void SetPlaybackElapsedText(MediaElement element, TimeSpan elapsed)
    {
        if (ReferenceEquals(element, SidePlaybackMediaElement))
        {
            SidePlaybackElapsedText.Text = FormatPlaybackTime(elapsed);
        }
        else if (ReferenceEquals(element, FrontPlaybackMediaElement))
        {
            FrontPlaybackElapsedText.Text = FormatPlaybackTime(elapsed);
        }
    }

    private static string FormatPlaybackTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }
}
