using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;

namespace BTFX.Views;

/// <summary>
/// ReportView.xaml 的交互逻辑
/// </summary>
public partial class ReportView : UserControl
{
    private bool _isUpdatingSelection;

    public ReportView()
    {
        InitializeComponent();
        Loaded += ReportView_Loaded;
        Unloaded += ReportView_Unloaded;
    }

    /// <summary>
    /// 带ViewModel的构造函数
    /// </summary>
    public ReportView(ReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += ReportView_Loaded;
        Unloaded += ReportView_Unloaded;
    }

    private void ReportView_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.IsShuttingDown)
        {
            return;
        }

        SyncReportDateDisplay();

        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += ViewModel_PropertyChanged;
        }

        SetDatePickerLanguage();
        SetCalendarLanguage();

        try
        {
            if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
            {
                localizationService.LanguageChanged += OnLanguageChanged;
            }
        }
        catch
        {
            // 忽略关闭时的异常
        }
    }

    private void ReportView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (App.IsShuttingDown)
        {
            return;
        }

        if (DataContext is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged -= ViewModel_PropertyChanged;
        }

        try
        {
            if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
            {
                localizationService.LanguageChanged -= OnLanguageChanged;
            }
        }
        catch
        {
            // 忽略关闭时的异常
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReportViewModel.ReportFilterStartDate)
            or nameof(ReportViewModel.ReportFilterEndDate))
        {
            SyncReportDateDisplay();
        }
    }

    private void ReportListViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is ReportViewModel viewModel)
        {
            viewModel.UpdateReportPageSize(e.NewSize.Height);
        }
    }

    private void SyncReportDateDisplay()
    {
        if (DataContext is not ReportViewModel vm)
        {
            return;
        }

        if (vm.ReportFilterStartDate.HasValue)
        {
            ReportStartDateText.Text = vm.ReportFilterStartDate.Value.ToString("yyyy.MM.dd");
            ReportStartDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        }
        else
        {
            ReportStartDateText.Text = "0000.00.00";
            ReportStartDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB"));
        }

        if (vm.ReportFilterEndDate.HasValue)
        {
            ReportEndDateText.Text = vm.ReportFilterEndDate.Value.ToString("yyyy.MM.dd");
            ReportEndDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        }
        else
        {
            ReportEndDateText.Text = "0000.00.00";
            ReportEndDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB"));
        }

        var hasDate = vm.ReportFilterStartDate.HasValue || vm.ReportFilterEndDate.HasValue;
        ReportDateClearButton.Opacity = hasDate ? 1.0 : 0.0;
        ReportDateClearButton.IsHitTestVisible = hasDate;
    }

    private void ReportStartDateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportViewModel vm && vm.ReportFilterStartDate.HasValue)
        {
            ReportStartDateCalendar.SelectedDate = vm.ReportFilterStartDate;
            ReportStartDateCalendar.DisplayDate = vm.ReportFilterStartDate.Value;
        }
        else
        {
            ReportStartDateCalendar.SelectedDate = null;
        }

        ReportStartDatePopup.IsOpen = true;
        e.Handled = true;
    }

    private void ReportEndDateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportViewModel vm && vm.ReportFilterEndDate.HasValue)
        {
            ReportEndDateCalendar.SelectedDate = vm.ReportFilterEndDate;
            ReportEndDateCalendar.DisplayDate = vm.ReportFilterEndDate.Value;
        }
        else
        {
            ReportEndDateCalendar.SelectedDate = null;
        }

        ReportEndDatePopup.IsOpen = true;
        e.Handled = true;
    }

    private void ReportStartDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReportViewModel vm && ReportStartDateCalendar.SelectedDate.HasValue)
        {
            vm.ReportFilterStartDate = ReportStartDateCalendar.SelectedDate.Value;
            ReportStartDatePopup.IsOpen = false;
        }
    }

    private void ReportEndDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReportViewModel vm && ReportEndDateCalendar.SelectedDate.HasValue)
        {
            vm.ReportFilterEndDate = ReportEndDateCalendar.SelectedDate.Value;
            ReportEndDatePopup.IsOpen = false;
        }
    }

    private void ReportDateClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportViewModel vm)
        {
            vm.ReportFilterStartDate = null;
            vm.ReportFilterEndDate = null;
        }

        ReportStartDateCalendar.SelectedDate = null;
        ReportEndDateCalendar.SelectedDate = null;
        ReportStartDatePopup.IsOpen = false;
        ReportEndDatePopup.IsOpen = false;
        e.Handled = true;
    }

    private void AllSelectCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            if (DataContext is ReportViewModel viewModel)
            {
                var shouldSelectAll = viewModel.SelectAllState != 2;
                viewModel.ApplySelectAll(shouldSelectAll);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void ItemCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is ReportItem item && DataContext is ReportViewModel viewModel)
            {
                viewModel.OnReportSelectionChanged(item);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void ReportRowBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isUpdatingSelection || IsFromButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is not FrameworkElement { DataContext: ReportItem item }
            || DataContext is not ReportViewModel viewModel)
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        viewModel.OnReportSelectionChanged(item);
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

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        if (App.IsShuttingDown)
        {
            return;
        }

        SetDatePickerLanguage();
        SetCalendarLanguage();
    }

    private void SetCalendarLanguage()
    {
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var xmlLanguage = XmlLanguage.GetLanguage(culture.Name);

        ReportStartDateCalendar.Language = xmlLanguage;
        ReportEndDateCalendar.Language = xmlLanguage;
    }

    private void SetDatePickerLanguage()
    {
        var datePickers = FindVisualChildren<DatePicker>(this);
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var xmlLanguage = XmlLanguage.GetLanguage(culture.Name);

        foreach (var datePicker in datePickers)
        {
            datePicker.Language = xmlLanguage;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
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
}
