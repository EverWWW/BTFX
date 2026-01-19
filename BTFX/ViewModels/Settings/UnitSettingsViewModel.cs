using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 单位设置视图模型
/// </summary>
public partial class UnitSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private string _unitName = string.Empty;

    [ObservableProperty]
    private string _logoPath = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    public bool HasLogo => !string.IsNullOrEmpty(LogoPath) && System.IO.File.Exists(LogoPath);

    public UnitSettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsService.CurrentSettings;
            UnitName = settings.Unit.Name;
            LogoPath = settings.Unit.LogoPath;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载单位设置失败", ex);
        }
    }

    partial void OnLogoPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasLogo));
    }

    [RelayCommand]
    private void SelectLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择Logo图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var fileInfo = new System.IO.FileInfo(dialog.FileName);
            if (fileInfo.Length > BtfxConstants.LOGO_MAX_SIZE_KB * 1024)
            {
                System.Windows.MessageBox.Show(
                    $"Logo文件大小不能超过{BtfxConstants.LOGO_MAX_SIZE_KB}KB",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            LogoPath = dialog.FileName;
            _logHelper?.Information($"选择Logo：{dialog.FileName}");
        }
    }

    [RelayCommand]
    private void ClearLogo()
    {
        LogoPath = string.Empty;
        _logHelper?.Information("清除Logo");
    }

    [RelayCommand]
    private void SaveUnitSettings()
    {
        try
        {
            IsSaving = true;

            _settingsService.CurrentSettings.Unit.Name = UnitName;
            _settingsService.CurrentSettings.Unit.LogoPath = LogoPath;
            _settingsService.SaveSettings();

            System.Windows.MessageBox.Show("单位设置已保存！", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information("保存单位设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存单位设置失败", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
