using System.Windows;
using BTFX.Common;
using BTFX.Models.Activation;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.ViewModels;

/// <summary>
/// 软件激活界面 ViewModel。
/// </summary>
public partial class ActivationViewModel : ObservableObject
{
    private readonly IActivationService _activationService;
    private readonly INavigationService _navigationService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly SoftKey _machineInfo;

    [ObservableProperty]
    private string _equipmentName = Constants.APP_DISPLAY_NAME;

    [ObservableProperty]
    private string _equipmentModel = Constants.ACTIVATION_PRODUCT_MODEL;

    [ObservableProperty]
    private string _equipmentVersion = Constants.VERSION_FULL;

    [ObservableProperty]
    private string _machineCode = string.Empty;

    [ObservableProperty]
    private string _cpuId = string.Empty;

    [ObservableProperty]
    private string _diskId = string.Empty;

    [ObservableProperty]
    private string _biosId = string.Empty;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private string _productCode = string.Empty;

    [ObservableProperty]
    private string _licenseKey = string.Empty;

    [ObservableProperty]
    private string _message = "在线激活需要填写产品编号；离线激活只需填写激活码。";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateOnlineCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActivateOfflineCommand))]
    private bool _isActivating;

    public ActivationViewModel(
        IActivationService activationService,
        INavigationService navigationService,
        IAppUpdateService appUpdateService)
    {
        _activationService = activationService;
        _navigationService = navigationService;
        _appUpdateService = appUpdateService;
        _machineInfo = _activationService.GetCurrentMachineInfo();

        EquipmentName = _machineInfo.EquipmentName ?? Constants.APP_DISPLAY_NAME;
        EquipmentModel = _machineInfo.EquipmentModel ?? Constants.ACTIVATION_PRODUCT_MODEL;
        EquipmentVersion = _machineInfo.EquipmentVersion ?? Constants.VERSION_FULL;
        MachineCode = _machineInfo.UniqCode ?? string.Empty;
        CpuId = _machineInfo.CpuId ?? string.Empty;
        DiskId = _machineInfo.HdId ?? string.Empty;
        BiosId = _machineInfo.BiosId ?? string.Empty;
        MacAddress = _machineInfo.MacAddress ?? string.Empty;
    }

    [RelayCommand]
    private void CopyMachineCode()
    {
        if (string.IsNullOrWhiteSpace(MachineCode))
        {
            return;
        }

        Clipboard.SetText(MachineCode);
        Message = "机器码已复制。";
    }

    [RelayCommand]
    private void CopyDeviceInfo()
    {
        var lines = new[]
        {
            $"软件名称：{EquipmentName}",
            $"产品型号：{EquipmentModel}",
            $"CPU编号：{CpuId}",
            $"硬盘信息：{DiskId}",
            $"BIOS编号：{BiosId}",
            $"机器码：{MachineCode}"
        };

        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        Message = "设备信息已复制，可用于离线生成激活码。";
    }

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private async Task ActivateOnlineAsync()
    {
        try
        {
            IsActivating = true;
            Message = "正在进行在线激活，请稍候...";
            var result = await _activationService.ActivateOnlineAsync(ProductCode);
            await HandleActivationResultAsync(result);
        }
        finally
        {
            IsActivating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private async Task ActivateOfflineAsync()
    {
        try
        {
            IsActivating = true;
            var result = _activationService.ActivateOffline(ProductCode, LicenseKey);
            await HandleActivationResultAsync(result);
        }
        finally
        {
            IsActivating = false;
        }
    }

    private bool CanActivate()
    {
        return !IsActivating;
    }

    private async Task HandleActivationResultAsync(ActivationResult result)
    {
        Message = result.Message;
        if (!result.IsSuccess)
        {
            MessageBox.Show(result.Message, "激活失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(result.Message, "激活成功", MessageBoxButton.OK, MessageBoxImage.Information);
        _navigationService.NavigateTo<LoginViewModel>();
        _ = _appUpdateService.CheckForUpdatesAsync();
        await Task.CompletedTask;
    }
}
