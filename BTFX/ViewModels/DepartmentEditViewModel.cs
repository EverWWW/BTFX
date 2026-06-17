using System;
using System.Threading.Tasks;
using System.Windows;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Constants = BTFX.Common.Constants;

namespace BTFX.ViewModels;

/// <summary>
/// 科室编辑ViewModel
/// </summary>
public class DepartmentEditViewModel : ObservableObject
{
    private readonly IDepartmentService _departmentService;
    private readonly ILocalizationService _localizationService;
    private readonly Department? _originalDepartment;

    /// <summary>
    /// 是否为新增模式
    /// </summary>
    public bool IsNewDepartment => _originalDepartment == null;

    /// <summary>
    /// 对话框标题
    /// </summary>
    public string Title => _localizationService.GetString(IsNewDepartment ? "AddDepartment" : "EditDepartment");

    private string _name = string.Empty;
    /// <summary>
    /// 科室名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _code = string.Empty;
    /// <summary>
    /// 科室代码
    /// </summary>
    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    private string _description = string.Empty;
    /// <summary>
    /// 科室描述
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _phone = string.Empty;
    /// <summary>
    /// 科室电话
    /// </summary>
    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    private string _validationError = string.Empty;
    /// <summary>
    /// 验证错误信息
    /// </summary>
    public string ValidationError
    {
        get => _validationError;
        set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    /// <summary>
    /// 是否有验证错误
    /// </summary>
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    private bool _isSaving;
    /// <summary>
    /// 是否正在保存
    /// </summary>
    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    /// <summary>
    /// 取消命令
    /// </summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// 保存命令
    /// </summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="departmentService">科室服务</param>
    /// <param name="department">要编辑的科室（null表示新增）</param>
    public DepartmentEditViewModel(IDepartmentService departmentService, Department? department = null)
    {
        _departmentService = departmentService;
        _localizationService = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService
            ?? throw new InvalidOperationException("Localization service is not available.");
        _originalDepartment = department;

        CancelCommand = new RelayCommand(Cancel);
        SaveCommand = new AsyncRelayCommand(SaveAsync);

        LoadData();
    }

    /// <summary>
    /// 加载数据
    /// </summary>
    private void LoadData()
    {
        if (_originalDepartment != null)
        {
            Name = _originalDepartment.Name;
            Code = _originalDepartment.Code ?? string.Empty;
            Description = _originalDepartment.Description ?? string.Empty;
            Phone = _originalDepartment.Phone ?? string.Empty;
        }
    }

    /// <summary>
    /// 取消操作
    /// </summary>
    private void Cancel()
    {
        DialogHost.CloseDialogCommand.Execute(false, null);
    }

    /// <summary>
    /// 保存操作
    /// </summary>
    private async Task SaveAsync()
    {
        if (IsSaving) return;

        if (!await ValidateAsync()) return;

        IsSaving = true;
        try
        {
            var department = _originalDepartment ?? new Department();
            department.Name = Name.Trim();
            department.Code = string.IsNullOrWhiteSpace(Code) ? null : Code.Trim();
            department.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            department.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
            department.UpdatedAt = DateTime.Now;

            bool success;

            if (IsNewDepartment)
            {
                department.CreatedAt = DateTime.Now;
                var newId = await _departmentService.AddDepartmentAsync(department);
                success = newId > 0;
            }
            else
            {
                success = await _departmentService.UpdateDepartmentAsync(department);
            }

            if (success)
            {
                DialogHost.CloseDialogCommand.Execute(true, null);
            }
            else
            {
                ValidationError = _localizationService.GetString("SaveRetryError");
            }
        }
        catch (Exception ex)
        {
            ValidationError = string.Format(_localizationService.GetString("SaveExceptionFormat"), ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// 验证输入
    /// </summary>
    /// <returns>验证是否通过</returns>
    private async Task<bool> ValidateAsync()
    {
        ValidationError = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = _localizationService.GetString("DepartmentNameRequired");
            return false;
        }

        if (Name.Length > Constants.DEPARTMENT_NAME_MAX_LENGTH)
        {
            ValidationError = string.Format(_localizationService.GetString("DepartmentNameMaxLengthError"), Constants.DEPARTMENT_NAME_MAX_LENGTH);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Code) && Code.Trim().Length > 20)
        {
            ValidationError = _localizationService.GetString("DepartmentCodeMaxLengthError");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Description) && Description.Trim().Length > 200)
        {
            ValidationError = _localizationService.GetString("DepartmentDescriptionMaxLengthError");
            return false;
        }

        // 检查名称是否已存在
        var nameExists = await _departmentService.CheckNameExistsAsync(
            Name.Trim(), 
            _originalDepartment?.Id);
        
        if (nameExists)
        {
            ValidationError = _localizationService.GetString("DepartmentNameExists");
            return false;
        }

        // 验证电话
        if (!string.IsNullOrWhiteSpace(Phone))
        {
            if (Phone.Length < Constants.PHONE_MIN_LENGTH || Phone.Length > Constants.PHONE_MAX_LENGTH)
            {
                ValidationError = string.Format(_localizationService.GetString("PhoneLengthRangeError"), Constants.PHONE_MIN_LENGTH, Constants.PHONE_MAX_LENGTH);
                return false;
            }
        }

        return true;
    }
}
