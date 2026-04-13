using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;

namespace BTFX.Views.Dialogs;

/// <summary>
/// PatientEditDialog.xaml Interaction Logic
/// </summary>
public partial class PatientEditDialog : Window
{
    public PatientEditDialog()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FitToOwnerScreen();

        if (DataContext is PatientEditViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Set DatePicker language based on current culture
        SetDatePickerLanguage();

        // Subscribe to language changes if localization service is available
        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged += OnLanguageChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PatientEditViewModel.ShouldClose))
        {
            if (DataContext is PatientEditViewModel viewModel && viewModel.ShouldClose)
            {
                DialogResult = viewModel.DialogResult;
                Close();
            }
        }
    }

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        SetDatePickerLanguage();
    }

    private void SetDatePickerLanguage()
    {
        // Get all DatePickers in the dialog
        var datePickers = FindVisualChildren<DatePicker>(this);

        // Set language based on current UI culture
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var xmlLanguage = XmlLanguage.GetLanguage(culture.Name);

        foreach (var datePicker in datePickers)
        {
            datePicker.Language = xmlLanguage;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
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

    // P/Invoke: 获取窗口所在显示器信息
    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// 根据 Owner 窗口所在的显示器，设置当前窗口的位置和大小
    /// </summary>
    private void FitToOwnerScreen()
    {
        var target = Owner ?? Application.Current.MainWindow;
        if (target == null) return;

        var helper = new WindowInteropHelper(target);
        var hMonitor = MonitorFromWindow(helper.Handle, MONITOR_DEFAULTTONEAREST);

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi)) return;

        // 获取 DPI 缩放因子
        var dpiScale = 1.0;
        var source = PresentationSource.FromVisual(target);
        if (source?.CompositionTarget != null)
        {
            dpiScale = source.CompositionTarget.TransformToDevice.M11;
        }

        // 用 DIP 单位计算显示器工作区
        var workLeft = mi.rcWork.Left / dpiScale;
        var workTop = mi.rcWork.Top / dpiScale;
        var workWidth = (mi.rcWork.Right - mi.rcWork.Left) / dpiScale;
        var workHeight = (mi.rcWork.Bottom - mi.rcWork.Top) / dpiScale;

        // 对话框固定 660x820，不超出工作区
        Width = Math.Min(660, workWidth);
        Height = Math.Min(820, workHeight);
        Left = workLeft + (workWidth - Width) / 2;
        Top = workTop + (workHeight - Height) / 2;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is PatientEditViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        // Unsubscribe from language changes
        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
        }

        base.OnClosed(e);
    }
}
