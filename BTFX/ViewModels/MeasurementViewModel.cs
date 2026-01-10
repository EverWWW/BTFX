using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.ViewModels;

/// <summary>
/// 测量界面ViewModel
/// </summary>
public partial class MeasurementViewModel : ObservableObject
{
    private readonly IMeasurementService _measurementService;
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();

    /// <summary>
    /// 是否正在录制
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingCommand))]
    private bool _isRecording;

    /// <summary>
    /// 连接状态
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    private string _connectionStatus = "未连接";

    /// <summary>
    /// 是否已连接
    /// </summary>
    private bool IsConnected => ConnectionStatus == "已连接";

    /// <summary>
    /// 设备列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _devices;

    /// <summary>
    /// 选中的设备
    /// </summary>
    [ObservableProperty]
    private string? _selectedDevice;

    /// <summary>
    /// 实时步频
    /// </summary>
    [ObservableProperty]
    private double _cadence;

    /// <summary>
    /// 实时速度
    /// </summary>
    [ObservableProperty]
    private double _speed;

    public MeasurementViewModel(IMeasurementService measurementService)
    {
        _measurementService = measurementService;
        _devices = new ObservableCollection<string>
        {
            "模拟相机设备 01",
            "模拟相机设备 02"
        };
        _selectedDevice = _devices.FirstOrDefault();

        // 用于模拟实时数据的定时器
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        // 模拟数据更新
        Cadence = 100 + _random.Next(-5, 5);
        Speed = 1.2 + (_random.NextDouble() * 0.2 - 0.1);
    }

    /// <summary>
    /// 连接设备
    /// </summary>
    [RelayCommand]
    private void Connect()
    {
        ConnectionStatus = "已连接";
        StartRecordingCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 断开设备
    /// </summary>
    [RelayCommand]
    private void Disconnect()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        ConnectionStatus = "未连接";
        StartRecordingCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 开始录制
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private void StartRecording()
    {
        IsRecording = true;
        _timer.Start();
    }

    private bool CanStartRecording() => IsConnected && !IsRecording;

    /// <summary>
    /// 停止录制
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private void StopRecording()
    {
        IsRecording = false;
        _timer.Stop();

        // 重置数据
        Cadence = 0;
        Speed = 0;
    }

    private bool CanStopRecording() => IsRecording;
}
