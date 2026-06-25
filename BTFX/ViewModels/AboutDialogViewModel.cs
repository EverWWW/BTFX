using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace BTFX.ViewModels;

/// <summary>
/// 关于弹窗 ViewModel。
/// </summary>
public partial class AboutDialogViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    private bool _isInternalVersionVisible;

    public AboutDialogViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _localizationService.LanguageChanged += (_, _) => RefreshLocalizedValues();
    }

    public string CompanyName => UseValue(IsEnglish
        ? ProductInfo.CompanyNameEn
        : ProductInfo.CompanyNameZh, IsEnglish ? "Sunnyou Medical Co., Ltd." : "河南翔宇医疗设备股份有限公司");

    public string SoftwareName => UseValue(IsEnglish
        ? ProductInfo.SoftwareNameEn
        : ProductInfo.SoftwareNameZh, IsEnglish ? Constants.APP_DISPLAY_NAME_EN : Constants.APP_DISPLAY_NAME);

    public string SoftwareNameSecondary => UseValue(IsEnglish
        ? ProductInfo.SoftwareNameZh
        : ProductInfo.SoftwareNameEn, IsEnglish ? Constants.APP_DISPLAY_NAME : Constants.APP_DISPLAY_NAME_EN);

    public string EquipmentModel => UseValue(ProductInfo.EquipmentModel, Constants.ACTIVATION_PRODUCT_MODEL);

    public string ReleaseVersion => UseValue(ProductInfo.ReleaseVersion, Constants.VERSION_DISPLAY);

    public string SoftwareVersion => UseValue(ProductInfo.SoftwareVersion, Constants.VERSION_FULL);

    public string InternalVersion => UseValue(ProductInfo.InternalVersion, "V1.0.0.20260625_alpha01");

    public string Website => UseValue(ProductInfo.Website, "https://www.xyyl.com/");

    private ProductInfoSettings ProductInfo => _settingsService.CurrentSettings.ProductInfo;

    private bool IsEnglish => _settingsService.CurrentSettings.Application.Language == AppLanguage.English;

    private static string UseValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    [RelayCommand]
    private void ToggleInternalVersion()
    {
        IsInternalVersionVisible = !IsInternalVersionVisible;
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        var website = Website;
        if (string.IsNullOrWhiteSpace(website))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = website,
            UseShellExecute = true
        });
    }

    partial void OnIsInternalVersionVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInternalVersionVisible));
    }

    private void RefreshLocalizedValues()
    {
        OnPropertyChanged(nameof(CompanyName));
        OnPropertyChanged(nameof(SoftwareName));
        OnPropertyChanged(nameof(SoftwareNameSecondary));
        OnPropertyChanged(nameof(EquipmentModel));
        OnPropertyChanged(nameof(ReleaseVersion));
        OnPropertyChanged(nameof(SoftwareVersion));
        OnPropertyChanged(nameof(InternalVersion));
        OnPropertyChanged(nameof(Website));
    }
}
