using System.Windows.Controls;
using System.Windows.Markup;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;

namespace BTFX.Views;

/// <summary>
/// ReportView.xaml 的交互逻辑
/// </summary>
public partial class ReportView : UserControl
{
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

    private void ReportView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 如果应用正在关闭，不执行任何操作
        if (App.IsShuttingDown) return;

        // Set DatePicker language based on current culture
        SetDatePickerLanguage();

        // Subscribe to language changes if localization service is available
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

    private void ReportView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 如果应用正在关闭，不执行任何操作
        if (App.IsShuttingDown) return;

        // Unsubscribe from language changes
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

        // 注意：不在这里调用 Dispose，因为 Unloaded 在导航时也会触发
        // ViewModel 的生命周期应由 DI 容器或应用程序关闭时统一管理
        // Dispose 会在应用程序退出时通过 App.OnExit 进行处理
    }

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        // 如果应用正在关闭，不执行任何操作
        if (App.IsShuttingDown) return;
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
}
