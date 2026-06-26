using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Globalization;
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
    private bool _isAnalysisPreviewMediaReady;
    private double _analysisPreviewSpeedRatio = 1.0;

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
            oldAnalysisVm.PropertyChanged -= OnAnalysisViewModelPropertyChanged;
        }

        if (e.NewValue is MeasurementDetailViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }

        if (e.NewValue is GaitAnalysisDetailViewModel newAnalysisVm)
        {
            newAnalysisVm.CloseRequested += OnCloseRequested;
            newAnalysisVm.PropertyChanged += OnAnalysisViewModelPropertyChanged;
        }

        _currentMeasurementViewModel = e.NewValue as MeasurementDetailViewModel;
        _currentAnalysisViewModel = e.NewValue as GaitAnalysisDetailViewModel;
        UpdateAnalysisPreviewOverlayButton();
    }

    private void OnAnalysisViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GaitAnalysisDetailViewModel.HasAnnotatedVideo)
            or nameof(GaitAnalysisDetailViewModel.AnnotatedVideoUri))
        {
            _ = Dispatcher.BeginInvoke(ResetAnalysisPreviewMedia, DispatcherPriority.Background);
        }
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

        if (sender is TabControl { SelectedItem: TabItem selectedItem }
            && ReferenceEquals(selectedItem, ParametersTabItem))
        {
            ResetParameterScrollToTop();
        }
        else if (sender is TabControl { SelectedItem: TabItem selectedVideoItem }
                 && ReferenceEquals(selectedVideoItem, VideoPreviewTabItem))
        {
            ResetVideoPreviewScrollToTop();
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                _currentAnalysisViewModel?.EnsureAnalysisPreviewReady();
            }), DispatcherPriority.Background);
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
        _isAnalysisPreviewMediaReady = true;
        ApplyAnalysisPreviewSpeed();
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

    private void AnalysisPreviewMediaElement_OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _isAnalysisPreviewMediaReady = false;
        _isAnalysisPreviewCompleted = false;
        SetAnalysisPreviewPlaying(false);
        AnalysisPreviewProgressSlider.Value = 0;
        AnalysisPreviewCurrentTimeText.Text = FormatPlaybackTime(TimeSpan.Zero);
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
        if (_currentAnalysisViewModel?.HasAnnotatedVideo != true || !_isAnalysisPreviewMediaReady)
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

            ApplyAnalysisPreviewSpeed();
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
        if (_currentAnalysisViewModel?.HasAnnotatedVideo != true || !_isAnalysisPreviewMediaReady)
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

    private void AnalysisPreviewSpeedComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnalysisPreviewSpeedComboBox.SelectedItem is not ComboBoxItem item
            || item.Tag is null
            || !double.TryParse(item.Tag.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
        {
            return;
        }

        _analysisPreviewSpeedRatio = Math.Clamp(speed, 0.1, 10.0);
        ApplyAnalysisPreviewSpeed();
    }

    private void AnalysisPreviewSpeedComboBox_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void ApplyAnalysisPreviewSpeed()
    {
        if (AnalysisPreviewMediaElement is null)
        {
            return;
        }

        AnalysisPreviewMediaElement.SpeedRatio = _analysisPreviewSpeedRatio;
    }

    private void SetAnalysisPreviewPlaying(bool isPlaying)
    {
        _isAnalysisPreviewPlaying = isPlaying;
        AnalysisPreviewPlayIcon.Kind = isPlaying ? PackIconKind.Pause : PackIconKind.Play;
        UpdateAnalysisPreviewOverlayButton();
        if (isPlaying)
        {
            _analysisPreviewTimer.Start();
        }
        else
        {
            _analysisPreviewTimer.Stop();
        }
    }

    private void UpdateAnalysisPreviewOverlayButton()
    {
        if (AnalysisPreviewOverlayPlayButton is null)
        {
            return;
        }

        AnalysisPreviewOverlayPlayButton.Visibility =
            _currentAnalysisViewModel?.HasAnnotatedVideo == true && _isAnalysisPreviewMediaReady && !_isAnalysisPreviewPlaying
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ResetAnalysisPreviewMedia()
    {
        if (AnalysisPreviewMediaElement is null)
        {
            return;
        }

        _isAnalysisPreviewMediaReady = false;
        _isAnalysisPreviewCompleted = false;
        SetAnalysisPreviewPlaying(false);
        AnalysisPreviewProgressSlider.Value = 0;
        AnalysisPreviewCurrentTimeText.Text = FormatPlaybackTime(TimeSpan.Zero);
        _currentAnalysisViewModel?.SetVideoPlaybackTime(0);

        AnalysisPreviewMediaElement.Stop();
        AnalysisPreviewMediaElement.SetCurrentValue(MediaElement.SourceProperty, null);
        if (_currentAnalysisViewModel?.AnnotatedVideoUri is { } uri)
        {
            AnalysisPreviewMediaElement.SetCurrentValue(MediaElement.SourceProperty, uri);
        }

        UpdateAnalysisPreviewOverlayButton();
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
        return FormatPlaybackTime(TimeSpan.FromSeconds(roundedSeconds));
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
        if (_currentAnalysisViewModel is not null)
        {
            _currentAnalysisViewModel.PropertyChanged -= OnAnalysisViewModelPropertyChanged;
        }

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
