using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;

namespace BTFX.Views;

/// <summary>
/// DataManagementView.xaml 的交互逻辑
/// </summary>
public partial class DataManagementView : UserControl
{
    private bool _isUpdatingSelection = false; // 防止递归更新

    public DataManagementView()
    {
        InitializeComponent();
        Loaded += DataManagementView_Loaded;
        Unloaded += DataManagementView_Unloaded;
    }

    /// <summary>
    /// 带ViewModel的构造函数
    /// </summary>
    public DataManagementView(DataManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += DataManagementView_Loaded;
        Unloaded += DataManagementView_Unloaded;
    }

    private void DataManagementView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Set DatePicker language based on current culture
        SetDatePickerLanguage();

        // Subscribe to language changes if localization service is available
        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged += OnLanguageChanged;
        }
    }

    private void DataManagementView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Unsubscribe from language changes
        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        SetDatePickerLanguage();
    }

    private void SetDatePickerLanguage()
    {
        // Get all DatePickers in the view
        var datePickers = FindVisualChildren<DatePicker>(this);

        // Set language based on current UI culture
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var xmlLanguage = XmlLanguage.GetLanguage(culture.Name);

        foreach (var datePicker in datePickers)
        {
            datePicker.Language = xmlLanguage;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject depObj) where T : System.Windows.DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);

            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    /// <summary>
    /// 全选复选框状态变化事件
    /// </summary>
    private void AllSelectCheckBox_CheckedChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        _isUpdatingSelection = true;
        try
        {
            if (DataContext is DataManagementViewModel viewModel)
            {
                // 直接调用SelectAll命令
                if (viewModel.SelectAllCommand.CanExecute(null))
                {
                    viewModel.SelectAllCommand.Execute(null);
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// 单项复选框状态变化事件
    /// </summary>
    private void ItemCheckBox_CheckedChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isUpdatingSelection) return;

        _isUpdatingSelection = true;
        try
        {
            // 获取当前项
            if (sender is System.Windows.Controls.CheckBox checkBox && 
                checkBox.DataContext is MeasurementRecordItem item &&
                DataContext is DataManagementViewModel viewModel)
            {
                // 直接调用 ViewModel 的方法，传递当前项
                viewModel.OnItemSelectionChanged(item);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// DataGrid行双击事件 - 切换选中状态
    /// </summary>
    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        // 检查双击的是否是操作按钮区域
        var originalSource = e.OriginalSource as System.Windows.DependencyObject;

        // 向上查找，看看是否点击在按钮上
        while (originalSource != null)
        {
            if (originalSource is System.Windows.Controls.Button)
            {
                // 如果点击的是按钮，不处理选中状态
                return;
            }
            originalSource = System.Windows.Media.VisualTreeHelper.GetParent(originalSource);
        }

        // 获取行数据项并切换选中状态
        if (row.Item is MeasurementRecordItem item)
        {
            item.IsSelected = !item.IsSelected;

            // 触发选中状态变化命令
            if (DataContext is DataManagementViewModel viewModel)
            {
                viewModel.ItemSelectionChangedCommand.Execute(null);
            }
        }

        // 标记事件已处理，防止其他事件触发
        e.Handled = true;
    }
}
