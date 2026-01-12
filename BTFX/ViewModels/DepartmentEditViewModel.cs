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

    private int _sortOrder;
    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    private bool _isEnabled = true;
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
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
            Code = _originalDepartment.Code ?? string.Empty;
            Description = _originalDepartment.Description ?? string.Empty;
            SortOrder = _originalDepartment.SortOrder;
            IsEnabled = _originalDepartment.IsEnabled;
        }
        else
        {
            // 默认值
            IsEnabled = true;
            SortOrder = 0;
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

        if (!Validate()) return;

        IsSaving = true;
        try
        {
            var department = _originalDepartment ?? new Department();
            department.Name = Name.Trim();
            department.Code = string.IsNullOrWhiteSpace(Code) ? null : Code.Trim();
            department.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            department.SortOrder = SortOrder;
            department.IsEnabled = IsEnabled;
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
    private bool Validate()
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

        return true;
    }
}
