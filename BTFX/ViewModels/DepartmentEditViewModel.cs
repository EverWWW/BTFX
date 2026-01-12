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
    private readonly Department? _originalDepartment;

    /// <summary>
    /// 是否为新增模式
    /// </summary>
    public bool IsNewDepartment => _originalDepartment == null;

    /// <summary>
    /// 对话框标题
    /// </summary>
    public string Title => IsNewDepartment ? "添加科室" : "编辑科室";

    private string _name = string.Empty;
    /// <summary>
    /// 科室名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
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
                ValidationError = "保存失败，请重试";
            }
        }
        catch (Exception ex)
        {
            ValidationError = $"保存出错: {ex.Message}";
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
            ValidationError = "科室名称不能为空";
            return false;
        }

        if (Name.Length > Constants.DEPARTMENT_NAME_MAX_LENGTH)
        {
            ValidationError = $"科室名称不能超过{Constants.DEPARTMENT_NAME_MAX_LENGTH}个字符";
            return false;
        }

        // 检查名称是否已存在
        var nameExists = await _departmentService.CheckNameExistsAsync(
            Name.Trim(), 
            _originalDepartment?.Id);
        
        if (nameExists)
        {
            ValidationError = "科室名称已存在";
            return false;
        }

        // 验证电话
        if (!string.IsNullOrWhiteSpace(Phone))
        {
            if (Phone.Length < Constants.PHONE_MIN_LENGTH || Phone.Length > Constants.PHONE_MAX_LENGTH)
            {
                ValidationError = $"电话号码长度应在{Constants.PHONE_MIN_LENGTH}-{Constants.PHONE_MAX_LENGTH}位之间";
                return false;
            }
        }

        return true;
    }
}
