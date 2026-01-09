using System.Windows;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Services.Implementations;

/// <summary>
/// 导航服务实现
/// </summary>
public class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, Type> _viewModelToViewMap = new();
    private readonly Stack<object> _navigationStack = new();

    private object? _currentView;
    private string _currentViewKey = string.Empty;

    /// <summary>
    /// 当前视图
    /// </summary>
    public object? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    /// <summary>
    /// 当前视图键名
    /// </summary>
    public string CurrentViewKey
    {
        get => _currentViewKey;
        private set => SetProperty(ref _currentViewKey, value);
    }

    /// <summary>
    /// 是否可以返回
    /// </summary>
    public bool CanGoBack => _navigationStack.Count > 1;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 注册视图映射
    /// </summary>
    /// <typeparam name="TViewModel">ViewModel类型</typeparam>
    /// <typeparam name="TView">View类型</typeparam>
    public void RegisterView<TViewModel, TView>()
        where TViewModel : ObservableObject
        where TView : FrameworkElement
    {
        _viewModelToViewMap[typeof(TViewModel)] = typeof(TView);
    }

    /// <summary>
    /// 导航到指定ViewModel
    /// </summary>
    /// <typeparam name="TViewModel">ViewModel类型</typeparam>
    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        var viewModelType = typeof(TViewModel);

        if (!_viewModelToViewMap.TryGetValue(viewModelType, out var viewType))
        {
            throw new InvalidOperationException($"未注册视图映射: {viewModelType.Name}");
        }

        // 获取ViewModel和View实例
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        var view = (FrameworkElement)_serviceProvider.GetRequiredService(viewType);

        // 设置DataContext
        view.DataContext = viewModel;

        // 保存当前视图到导航栈
        if (CurrentView != null)
        {
            _navigationStack.Push(CurrentView);
        }

        // 更新当前视图
        CurrentView = view;
        CurrentViewKey = viewModelType.Name;

        OnPropertyChanged(nameof(CanGoBack));
    }

    /// <summary>
    /// 导航到指定视图键名
    /// </summary>
    /// <param name="viewKey">视图键名</param>
    public void NavigateTo(string viewKey)
    {
        var viewModelType = _viewModelToViewMap.Keys
            .FirstOrDefault(t => t.Name == viewKey || t.Name == $"{viewKey}ViewModel");

        if (viewModelType == null)
        {
            throw new InvalidOperationException($"未找到视图: {viewKey}");
        }

        var viewType = _viewModelToViewMap[viewModelType];

        // 获取ViewModel和View实例
        var viewModel = _serviceProvider.GetRequiredService(viewModelType);
        var view = (FrameworkElement)_serviceProvider.GetRequiredService(viewType);

        // 设置DataContext
        view.DataContext = viewModel;

        // 保存当前视图到导航栈
        if (CurrentView != null)
        {
            _navigationStack.Push(CurrentView);
        }

        // 更新当前视图
        CurrentView = view;
        CurrentViewKey = viewModelType.Name;

        OnPropertyChanged(nameof(CanGoBack));
    }

    /// <summary>
    /// 返回上一页
    /// </summary>
    public void GoBack()
    {
        if (!CanGoBack) return;

        CurrentView = _navigationStack.Pop();

        if (CurrentView is FrameworkElement element && element.DataContext != null)
        {
            CurrentViewKey = element.DataContext.GetType().Name;
        }

        OnPropertyChanged(nameof(CanGoBack));
    }

    /// <summary>
    /// 清除导航栈
    /// </summary>
    public void ClearNavigationStack()
    {
        _navigationStack.Clear();
        OnPropertyChanged(nameof(CanGoBack));
    }
}
