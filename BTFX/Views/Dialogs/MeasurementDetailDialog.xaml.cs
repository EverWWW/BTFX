using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BTFX.Views.Dialogs;

/// <summary>
/// MeasurementDetailDialog.xaml 的交互逻辑
/// </summary>
public partial class MeasurementDetailDialog
{
    private GaitAnalysisDetailViewModel? _currentAnalysisViewModel;
    private MeasurementDetailViewModel? _currentMeasurementViewModel;
    private readonly DispatcherTimer _analysisPreviewTimer;
    private bool _isResettingParameterScroll;
    private bool _isResettingVideoPreviewScroll;
    private bool _isAnalysisPreviewPlaying;
    private bool _isDraggingAnalysisPreviewSlider;
    private bool _isAnalysisPreviewCompleted;

    public MeasurementDetailDialog()
    {
        InitializeComponent();
        _analysisPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _analysisPreviewTimer.Tick += AnalysisPreviewTimer_OnTick;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MeasurementDetailViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.OldValue is GaitAnalysisDetailViewModel oldAnalysisVm)
        {
            oldAnalysisVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is MeasurementDetailViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }

        if (e.NewValue is GaitAnalysisDetailViewModel newAnalysisVm)
        {
            newAnalysisVm.CloseRequested += OnCloseRequested;
        }

        _currentMeasurementViewModel = e.NewValue as MeasurementDetailViewModel;
        _currentAnalysisViewModel = e.NewValue as GaitAnalysisDetailViewModel;
    }

    private void OnCloseRequested()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(OnCloseRequested);
            return;
        }

        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;

        if (TryCloseDialogHost())
        {
            return;
        }

        if (Parent is FrameworkElement parent)
        {
            parent.Visibility = Visibility.Collapsed;
            parent.IsHitTestVisible = false;
        }
    }

    private void AnalysisTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender))
        {
            return;
        }

        if (sender is TabControl { SelectedItem: TabItem { Header: "参数显示页" } })
        {
            ResetParameterScrollToTop();
        }
        else if (sender is TabControl { SelectedItem: TabItem { Header: "视频预览页" } })
        {
            ResetVideoPreviewScrollToTop();
        }
    }

    private void ParameterDetailScrollViewer_OnLoaded(object sender, RoutedEventArgs e)
    {
        ResetParameterScrollToTop();
    }

    private void ParameterDetailScrollViewer_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (_isResettingParameterScroll)
        {
            e.Handled = true;
        }
    }

    private void VideoPreviewDetailScrollViewer_OnLoaded(object sender, RoutedEventArgs e)
    {
        ResetVideoPreviewScrollToTop();
    }

    private void VideoPreviewDetailScrollViewer_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (_isResettingVideoPreviewScroll)
        {
            e.Handled = true;
        }
    }

    private void ResetParameterScrollToTop()
    {
        if (ParameterDetailScrollViewer is null)
        {
            return;
        }

        _isResettingParameterScroll = true;
        ParameterDetailScrollViewer.ScrollToTop();

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            ParameterDetailScrollViewer.ScrollToTop();
        }), DispatcherPriority.Loaded);

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            ParameterDetailScrollViewer.ScrollToTop();
            _isResettingParameterScroll = false;
        }), DispatcherPriority.ContextIdle);
    }

    private void ResetVideoPreviewScrollToTop()
    {
        if (VideoPreviewDetailScrollViewer is null)
        {
            return;
        }

        _isResettingVideoPreviewScroll = true;
        VideoPreviewDetailScrollViewer.ScrollToTop();

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            VideoPreviewDetailScrollViewer.ScrollToTop();
        }), DispatcherPriority.Loaded);

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            VideoPreviewDetailScrollViewer.ScrollToTop();
            _isResettingVideoPreviewScroll = false;
        }), DispatcherPriority.ContextIdle);
    }

    private void AnalysisPreviewMediaElement_OnMediaOpened(object sender, RoutedEventArgs e)
    {
        AnalysisPreviewMediaElement.SpeedRatio = 1.0;
        var duration = AnalysisPreviewMediaElement.NaturalDuration.HasTimeSpan
            ? AnalysisPreviewMediaElement.NaturalDuration.TimeSpan
            : TimeSpan.Zero;
        AnalysisPreviewProgressSlider.Maximum = duration.TotalSeconds > 0 ? duration.TotalSeconds : 1;
        AnalysisPreviewProgressSlider.Value = 0;
        AnalysisPreviewCurrentTimeText.Text = FormatPlaybackTime(TimeSpan.Zero);
        AnalysisPreviewDurationText.Text = duration.TotalSeconds > 0
            ? FormatDurationTime(duration)
            : (_currentAnalysisViewModel?.VideoDurationDisplay ?? "--");
        _currentAnalysisViewModel?.SetVideoPreviewDuration(AnalysisPreviewProgressSlider.Maximum);
        _currentAnalysisViewModel?.SetVideoPlaybackTime(0);
        if (_isAnalysisPreviewPlaying)
        {
            _analysisPreviewTimer.Start();
            AnalysisPreviewPlayIcon.Kind = PackIconKind.Pause;
        }
        else
        {
            SetAnalysisPreviewPlaying(false);
        }
    }

    private void AnalysisPreviewMediaElement_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _isAnalysisPreviewCompleted = true;
        var endSeconds = AnalysisPreviewProgressSlider.Maximum;
        AnalysisPreviewMediaElement.Stop();
        AnalysisPreviewProgressSlider.Value = endSeconds;
        AnalysisPreviewCurrentTimeText.Text = FormatDurationTime(TimeSpan.FromSeconds(endSeconds));
        _currentAnalysisViewModel?.SetVideoPlaybackTime(endSeconds);
        SetAnalysisPreviewPlaying(false);
    }

    private void AnalysisPreviewPlayButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentAnalysisViewModel?.HasAnnotatedVideo != true)
        {
            return;
        }

        if (_isAnalysisPreviewPlaying)
        {
            AnalysisPreviewMediaElement.Pause();
            SetAnalysisPreviewPlaying(false);
        }
        else
        {
            if (_isAnalysisPreviewCompleted)
            {
                _isAnalysisPreviewCompleted = false;
                SeekAnalysisPreview(0);
            }

            AnalysisPreviewMediaElement.SpeedRatio = 1.0;
            AnalysisPreviewMediaElement.Play();
            SetAnalysisPreviewPlaying(true);
        }
    }

    private void AnalysisPreviewProgressSlider_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider slider && slider.ActualWidth > 0)
        {
            var x = Math.Clamp(e.GetPosition(slider).X, 0, slider.ActualWidth);
            var ratio = x / slider.ActualWidth;
            slider.Value = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
            AnalysisPreviewCurrentTimeText.Text = FormatPlaybackTime(TimeSpan.FromSeconds(slider.Value));
            _currentAnalysisViewModel?.SetVideoPlaybackTime(slider.Value);
        }

        _isDraggingAnalysisPreviewSlider = true;
    }

    private void AnalysisPreviewProgressSlider_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        SeekAnalysisPreview(AnalysisPreviewProgressSlider.Value);
        _isDraggingAnalysisPreviewSlider = false;
    }

    private void AnalysisPreviewProgressSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingAnalysisPreviewSlider)
        {
            AnalysisPreviewCurrentTimeText.Text = FormatPlaybackTime(TimeSpan.FromSeconds(e.NewValue));
            _currentAnalysisViewModel?.SetVideoPlaybackTime(e.NewValue);
        }
    }

    private void AnalysisPreviewTimer_OnTick(object? sender, EventArgs e)
    {
        if (!_isAnalysisPreviewPlaying || _isDraggingAnalysisPreviewSlider)
        {
            return;
        }

        var seconds = AnalysisPreviewMediaElement.Position.TotalSeconds;
        var maximum = AnalysisPreviewProgressSlider.Maximum;
        if (maximum > 0 && maximum - seconds <= 0.35d)
        {
            seconds = maximum;
        }

        if (seconds <= maximum)
        {
            AnalysisPreviewProgressSlider.Value = seconds;
        }

        AnalysisPreviewCurrentTimeText.Text = seconds >= maximum
            ? FormatDurationTime(TimeSpan.FromSeconds(maximum))
            : FormatPlaybackTime(AnalysisPreviewMediaElement.Position);
        _currentAnalysisViewModel?.SetVideoPlaybackTime(seconds);
    }

    private void SeekAnalysisPreview(double seconds)
    {
        if (_currentAnalysisViewModel?.HasAnnotatedVideo != true)
        {
            return;
        }

        seconds = Math.Clamp(seconds, 0, AnalysisPreviewProgressSlider.Maximum);
        _isAnalysisPreviewCompleted = false;
        AnalysisPreviewMediaElement.Position = TimeSpan.FromSeconds(seconds);
        AnalysisPreviewCurrentTimeText.Text = FormatPlaybackTime(AnalysisPreviewMediaElement.Position);
        AnalysisPreviewProgressSlider.Value = seconds;
        _currentAnalysisViewModel?.SetVideoPlaybackTime(seconds);
    }

    private void SetAnalysisPreviewPlaying(bool isPlaying)
    {
        _isAnalysisPreviewPlaying = isPlaying;
        AnalysisPreviewPlayIcon.Kind = isPlaying ? PackIconKind.Pause : PackIconKind.Play;
        if (isPlaying)
        {
            _analysisPreviewTimer.Start();
        }
        else
        {
            _analysisPreviewTimer.Stop();
        }
    }

    private static string FormatPlaybackTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    private static string FormatDurationTime(TimeSpan time)
    {
        var roundedSeconds = Math.Max(0, (int)Math.Round(time.TotalSeconds, MidpointRounding.AwayFromZero));
        var rounded = TimeSpan.FromSeconds(roundedSeconds);
        return FormatPlaybackTime(rounded);
    }

    private bool TryCloseDialogHost()
    {
        var dialogHost = FindAncestor<DialogHost>(this);
        if (dialogHost?.CurrentSession is { } session)
        {
            session.Close();
            return true;
        }

        if (!DialogHost.IsDialogOpen("RootDialog"))
        {
            return false;
        }

        DialogHost.Close("RootDialog");
        return true;
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject) where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T target)
            {
                return target;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _analysisPreviewTimer.Stop();
        _analysisPreviewTimer.Tick -= AnalysisPreviewTimer_OnTick;
        AnalysisPreviewMediaElement.Stop();
        DataContextChanged -= OnDataContextChanged;
        Unloaded -= OnUnloaded;

        if (_currentMeasurementViewModel is not null)
        {
            _currentMeasurementViewModel.CloseRequested -= OnCloseRequested;
        }

        if (_currentAnalysisViewModel is not null)
        {
            _currentAnalysisViewModel.CloseRequested -= OnCloseRequested;
        }

        _currentMeasurementViewModel = null;
        _currentAnalysisViewModel = null;
    }
}
