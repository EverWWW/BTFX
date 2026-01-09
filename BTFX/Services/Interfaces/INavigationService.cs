using CommunityToolkit.Mvvm.ComponentModel;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 导航服务接口
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// 当前视图
    /// </summary>
    object? CurrentView { get; }

    /// <summary>
    /// 当前视图键名
    /// </summary>
    string CurrentViewKey { get; }

    /// <summary>
    /// 是否可以返回
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// 导航到指定ViewModel
    /// </summary>
    /// <typeparam name="TViewModel">ViewModel类型</typeparam>
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;

    /// <summary>
    /// 导航到指定视图键名
    /// </summary>
    /// <param name="viewKey">视图键名</param>
    void NavigateTo(string viewKey);

    /// <summary>
    /// 返回上一页
    /// </summary>
    void GoBack();
}
