using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using BTFX.ViewModels.Measurement;
using OxyPlot;
using OxyPlot.Wpf;

namespace BTFX.Views.Measurement;

/// <summary>
/// Step4AnalyzeView.xaml 的交互逻辑
/// 负责 MediaElement 播放控制、DispatcherTimer 时间同步、VM 事件响应
/// </summary>
public partial class Step4AnalyzeView : UserControl
{
    private Step4AnalyzeViewModel? _viewModel;
    private DispatcherTimer? _positionTimer;
    private bool _isDraggingSlider;

    /// <summary>
    /// 构造函数
    /// </summary>
    public Step4AnalyzeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// DataContext 变更时订阅/取消订阅 VM 事件
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 取消旧 VM 订阅
        if (_viewModel is not null)
        {
            _viewModel.VideoPositionChanged -= OnVideoPositionChanged;
            _viewModel.PlayStateChanged -= OnPlayStateChanged;
            _viewModel.SpeedChanged -= OnSpeedChanged;
        }

        _viewModel = e.NewValue as Step4AnalyzeViewModel;

        // 订阅新 VM 事件
        if (_viewModel is not null)
        {
            _viewModel.VideoPositionChanged += OnVideoPositionChanged;
            _viewModel.PlayStateChanged += OnPlayStateChanged;
            _viewModel.SpeedChanged += OnSpeedChanged;
        }
    }

    #region MediaElement 事件

    /// <summary>
    /// 视频加载完成，获取时长并启动定时器
    /// </summary>
    private void AnnotatedVideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (AnnotatedVideoPlayer.NaturalDuration.HasTimeSpan)
        {
            _viewModel?.SetVideoDuration(AnnotatedVideoPlayer.NaturalDuration.TimeSpan);
        }

        StartPositionTimer();
    }

    /// <summary>
    /// 视频播放结束
    /// </summary>
    private void AnnotatedVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _viewModel?.OnVideoEnded();
        StopPositionTimer();
    }

    /// <summary>
    /// 视频加载失败，降级到参数概览
    /// </summary>
    private void AnnotatedVideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        StopPositionTimer();
        var errorMessage = e.ErrorException?.Message ?? "未知视频错误";
        _viewModel?.OnVideoFailed(errorMessage);
    }

    #endregion

    #region Slider 拖拽事件

    /// <summary>
    /// 用户开始拖动进度条，暂停定时器避免冲突
    /// </summary>
    private void VideoSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    /// <summary>
    /// 用户结束拖动，执行 Seek 并恢复定时器
    /// </summary>
    private void VideoSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingSlider = false;

        if (_viewModel is not null)
        {
            var targetTime = TimeSpan.FromSeconds(_viewModel.VideoCurrentTimeSeconds);
            AnnotatedVideoPlayer.Position = targetTime;
        }
    }

    #endregion

    #region OxyPlot 点击事件

    /// <summary>
    /// 曲线图被点击，跳转到对应时间点
    /// </summary>
    private void PlotView_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not PlotView plotView || plotView.Model is null || _viewModel is null)
            return;

        var position = e.GetPosition(plotView);
        var plotModel = plotView.Model;

        // 将鼠标坐标转换为数据坐标
        var xAxis = plotModel.DefaultXAxis;
        if (xAxis is null)
            return;

        var screenPoint = new ScreenPoint(position.X, position.Y);
        var result = xAxis.InverseTransform(screenPoint.X);

        _viewModel.OnChartClicked(result);

        // 同步 MediaElement 位置
        AnnotatedVideoPlayer.Position = TimeSpan.FromSeconds(
            Math.Clamp(result, 0, _viewModel.VideoDurationSeconds));
    }

    #endregion

    #region VM 事件处理

    /// <summary>
    /// VM 请求跳转视频位置（Slider 绑定变更触发）
    /// </summary>
    private void OnVideoPositionChanged(object? sender, double seconds)
    {
        if (!_isDraggingSlider)
        {
            AnnotatedVideoPlayer.Position = TimeSpan.FromSeconds(seconds);
        }
    }

    /// <summary>
    /// VM 请求播放/暂停
    /// </summary>
    private void OnPlayStateChanged(object? sender, bool isPlaying)
    {
        if (isPlaying)
        {
            AnnotatedVideoPlayer.Play();
            StartPositionTimer();
        }
        else
        {
            AnnotatedVideoPlayer.Pause();
            StopPositionTimer();
        }
    }

    /// <summary>
    /// VM 请求变更播放速度
    /// </summary>
    private void OnSpeedChanged(object? sender, double speed)
    {
        AnnotatedVideoPlayer.SpeedRatio = speed;
    }

    #endregion

    #region DispatcherTimer 时间同步

    /// <summary>
    /// 启动 33ms 定时器，持续同步 MediaElement 位置到 VM
    /// </summary>
    private void StartPositionTimer()
    {
        if (_positionTimer is not null)
            return;

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _positionTimer.Tick += PositionTimer_Tick;
        _positionTimer.Start();
    }

    /// <summary>
    /// 停止定时器
    /// </summary>
    private void StopPositionTimer()
    {
        if (_positionTimer is null)
            return;

        _positionTimer.Stop();
        _positionTimer.Tick -= PositionTimer_Tick;
        _positionTimer = null;
    }

    /// <summary>
    /// 定时器回调：将 MediaElement 当前位置同步到 VM
    /// </summary>
    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDraggingSlider || _viewModel is null)
            return;

        _viewModel.UpdateVideoTimeFromPlayer(AnnotatedVideoPlayer.Position);
    }

    #endregion
}
