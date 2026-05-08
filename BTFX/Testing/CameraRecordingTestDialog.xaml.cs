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
        SidePlaybackMediaElement.MediaEnded += (_, _) => StopPlayback(SidePlaybackMediaElement);
        FrontPlaybackMediaElement.MediaEnded += (_, _) => StopPlayback(FrontPlaybackMediaElement);
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
        StopPlayback(SidePlaybackMediaElement);
        StopPlayback(FrontPlaybackMediaElement);
        _playbackTimer.Stop();
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
            }
            else if (ReferenceEquals(element, FrontPlaybackMediaElement))
            {
                FrontPlaybackSlider.Value = 0;
            }
        }

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
                PreparePlaybackSource(SidePlaybackMediaElement, SidePlaybackSlider, null);
                PreparePlaybackSource(FrontPlaybackMediaElement, FrontPlaybackSlider, null);
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
        element.Stop();
        element.Source = null;

        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return;
        }

        element.Volume = 0;
        element.Position = TimeSpan.Zero;
        element.Source = new Uri(path, UriKind.Absolute);
        element.Play();
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
            }
        }
        finally
        {
            _warmingElements.Remove(element);
        }
    }
}
