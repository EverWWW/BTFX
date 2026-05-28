using System.Windows.Threading;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.ViewModels;

/// <summary>
/// 通用耗时操作进度弹窗 ViewModel。
/// </summary>
public partial class OperationProgressDialogViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly DispatcherTimer _animationTimer;
    private double _targetProgress;

    public OperationProgressDialogViewModel(
        string title,
        string stage,
        string message,
        CancellationTokenSource cancellationTokenSource,
        bool canCancel = true)
    {
        Title = title;
        Stage = stage;
        Message = message;
        CanCancel = canCancel;
        _cancellationTokenSource = cancellationTokenSource;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) => AnimateProgress();
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _stage;

    [ObservableProperty]
    private string _message;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _canCancel;

    [ObservableProperty]
    private bool _isCancelling;

    public string ProgressText => IsIndeterminate ? "处理中" : $"{Math.Round(Progress)}%";

    partial void OnProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }

    partial void OnIsIndeterminateChanged(bool value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }

    public void Update(OperationProgressInfo info)
    {
        SetTargetProgress(info.Percent);
        Stage = info.Stage;
        Message = info.Message;
        IsIndeterminate = info.IsIndeterminate;
    }

    public void MarkCompleted(string message)
    {
        CanCancel = false;
        IsCancelling = false;
        IsIndeterminate = false;
        SetTargetProgress(100);
        Stage = "完成";
        Message = message;
    }

    public void MarkFailed(string message)
    {
        CanCancel = false;
        IsCancelling = false;
        IsIndeterminate = false;
        Stage = "失败";
        Message = message;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCancel))]
    private void Cancel()
    {
        if (IsCancelling)
        {
            return;
        }

        IsCancelling = true;
        CanCancel = false;
        Stage = "正在取消";
        Message = "正在停止当前操作，请稍候...";
        _cancellationTokenSource.Cancel();
    }

    private bool CanExecuteCancel() => CanCancel && !IsCancelling;

    private void SetTargetProgress(double value)
    {
        _targetProgress = Math.Clamp(value, 0, 100);
        if (IsIndeterminate || _targetProgress <= Progress)
        {
            Progress = _targetProgress;
            return;
        }

        if (!_animationTimer.IsEnabled)
        {
            _animationTimer.Start();
        }
    }

    private void AnimateProgress()
    {
        var delta = _targetProgress - Progress;
        if (delta <= 0.1)
        {
            Progress = _targetProgress;
            _animationTimer.Stop();
            return;
        }

        var step = Math.Max(0.6, delta * 0.12);
        Progress = Math.Min(_targetProgress, Progress + step);
    }
}
