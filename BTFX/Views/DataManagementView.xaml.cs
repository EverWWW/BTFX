using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows;
using System.Windows.Media;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using System.ComponentModel;

namespace BTFX.Views;

/// <summary>
/// DataManagementView.xaml 的交互逻辑
/// </summary>
public partial class DataManagementView : UserControl
{
    private bool _isUpdatingSelection = false; // 防止递归更新

    private void StatusComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            comboBox.Focus();
            if (!comboBox.IsDropDownOpen)
            {
                comboBox.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }

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

    private void DataManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        // 初始同步日期显示
        SyncDateDisplay();

        // 监听 ViewModel 属性变化以同步日期显示
        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += ViewModel_PropertyChanged;
        }

        // 设置日历语言
        SetCalendarLanguage();

        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged += OnLanguageChanged;
        }
    }

    private void DataManagementView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
        }

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DataManagementViewModel.FilterStartDate)
            or nameof(DataManagementViewModel.FilterEndDate))
        {
            SyncDateDisplay();
        }
    }

    /// <summary>
    /// 同步日期文本显示和清空按钮可见性
    /// </summary>
    private void SyncDateDisplay()
    {
        if (DataContext is not DataManagementViewModel vm) return;

        if (vm.FilterStartDate.HasValue)
        {
            StartDateText.Text = vm.FilterStartDate.Value.ToString("yyyy.MM.dd");
            StartDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        }
        else
        {
            StartDateText.Text = "0000.00.00";
            StartDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB"));
        }

        if (vm.FilterEndDate.HasValue)
        {
            EndDateText.Text = vm.FilterEndDate.Value.ToString("yyyy.MM.dd");
            EndDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        }
        else
        {
            EndDateText.Text = "0000.00.00";
            EndDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB"));
        }

        // 用 Opacity + IsHitTestVisible 控制清空按钮显隐，不改变布局
        var hasDate = vm.FilterStartDate.HasValue || vm.FilterEndDate.HasValue;
        DateClearButton.Opacity = hasDate ? 1.0 : 0.0;
        DateClearButton.IsHitTestVisible = hasDate;
    }

    /// <summary>
    /// 点击起始日期区域，打开起始日期日历
    /// </summary>
    private void StartDateButton_Click(object sender, RoutedEventArgs e)
    {
        // 同步当前选中日期到日历
        if (DataContext is DataManagementViewModel vm && vm.FilterStartDate.HasValue)
        {
            StartDateCalendar.SelectedDate = vm.FilterStartDate;
            StartDateCalendar.DisplayDate = vm.FilterStartDate.Value;
        }
        else
        {
            StartDateCalendar.SelectedDate = null;
        }

        StartDatePopup.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// 点击截止日期区域，打开截止日期日历
    /// </summary>
    private void EndDateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DataManagementViewModel vm && vm.FilterEndDate.HasValue)
        {
            EndDateCalendar.SelectedDate = vm.FilterEndDate;
            EndDateCalendar.DisplayDate = vm.FilterEndDate.Value;
        }
        else
        {
            EndDateCalendar.SelectedDate = null;
        }

        EndDatePopup.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// 起始日期日历选择事件
    /// </summary>
    private void StartDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (StartDateCalendar.SelectedDate.HasValue && DataContext is DataManagementViewModel vm)
        {
            vm.FilterStartDate = StartDateCalendar.SelectedDate.Value;
            StartDatePopup.IsOpen = false;
        }
    }

    /// <summary>
    /// 截止日期日历选择事件
    /// </summary>
    private void EndDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (EndDateCalendar.SelectedDate.HasValue && DataContext is DataManagementViewModel vm)
        {
            vm.FilterEndDate = EndDateCalendar.SelectedDate.Value;
            EndDatePopup.IsOpen = false;
        }
    }

    /// <summary>
    /// 清空所有日期
    /// </summary>
    private void DateClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DataManagementViewModel vm)
        {
            vm.FilterStartDate = null;
            vm.FilterEndDate = null;
        }

        StartDateCalendar.SelectedDate = null;
        EndDateCalendar.SelectedDate = null;
        StartDatePopup.IsOpen = false;
        EndDatePopup.IsOpen = false;
        e.Handled = true;
    }

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        SetCalendarLanguage();
    }

    private void SetCalendarLanguage()
    {
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var xmlLanguage = XmlLanguage.GetLanguage(culture.Name);

        StartDateCalendar.Language = xmlLanguage;
        EndDateCalendar.Language = xmlLanguage;
    }

    /// <summary>
    /// 全选复选框状态变化事件
    /// </summary>
    private void AllSelectCheckBox_Click(object sender, System.Windows.RoutedEventArgs e)
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

    private void DataRowBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isUpdatingSelection || IsFromButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is not FrameworkElement { DataContext: MeasurementRecordItem item }
            || DataContext is not DataManagementViewModel viewModel)
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        viewModel.OnItemSelectionChanged(item);
        e.Handled = true;
    }

    private static bool IsFromButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private async void DataListViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is DataManagementViewModel viewModel)
        {
            await viewModel.UpdatePageSizeAsync(e.NewSize.Height);
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

    private void Button_Click()
    {

    }
}
