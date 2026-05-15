using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BTFX.Views.Dialogs;

/// <summary>
/// ReportPreviewDialog.xaml 的交互逻辑。
/// </summary>
public partial class ReportPreviewDialog : UserControl
{
    private ReportPreviewDialogViewModel? _currentViewModel;

    public ReportPreviewDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReportPreviewDialogViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ReportPreviewDialogViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        _currentViewModel = e.NewValue as ReportPreviewDialogViewModel;
        Dispatcher.BeginInvoke(FitWidthAndUpdatePageIndicator, DispatcherPriority.Loaded);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(FitWidthAndUpdatePageIndicator, DispatcherPriority.Loaded);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportPreviewDialogViewModel.PreviewDocument))
        {
            Dispatcher.BeginInvoke(FitWidthAndUpdatePageIndicator, DispatcherPriority.Loaded);
        }
    }

    private void OnCloseRequested(ReportPreviewDialogResult result)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => OnCloseRequested(result));
            return;
        }

        var dialogHost = FindAncestor<DialogHost>(this);
        if (dialogHost?.CurrentSession is { } session)
        {
            session.Close(result);
            return;
        }

        if (DialogHost.IsDialogOpen("RootDialog"))
        {
            DialogHost.Close("RootDialog", result);
            return;
        }

        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    private void ZoomOutButton_OnClick(object sender, RoutedEventArgs e)
    {
        SetDocumentZoom(ReportDocumentViewer.Zoom - 10);
    }

    private void ZoomInButton_OnClick(object sender, RoutedEventArgs e)
    {
        SetDocumentZoom(ReportDocumentViewer.Zoom + 10);
    }

    private void FitWidthButton_OnClick(object sender, RoutedEventArgs e)
    {
        FitDocumentToWidth();
    }

    private void PreviousPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        FindVisualChild<ScrollViewer>(ReportDocumentViewer)?.PageUp();
        Dispatcher.BeginInvoke(UpdatePageIndicator);
    }

    private void NextPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        FindVisualChild<ScrollViewer>(ReportDocumentViewer)?.PageDown();
        Dispatcher.BeginInvoke(UpdatePageIndicator);
    }

    private void SetDocumentZoom(double zoom)
    {
        var targetZoom = Math.Clamp(zoom, 50, 220);
        ReportDocumentViewer.Zoom = targetZoom;
        ZoomPercentTextBlock.Text = $"{targetZoom:F0}%";
    }

    private void UpdatePageIndicator()
    {
        PageIndicatorTextBlock.Text = "连续预览";
    }

    private void FitWidthAndUpdatePageIndicator()
    {
        FitDocumentToWidth();
        UpdatePageIndicator();
    }

    private void FitDocumentToWidth()
    {
        var documentWidth = ReportDocumentViewer.Document?.PageWidth ?? 793.7;
        var availableWidth = Math.Max(200, ReportDocumentViewer.ActualWidth - 44);
        SetDocumentZoom(availableWidth / documentWidth * 100.0);
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject) where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T target)
            {
                return target;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? dependencyObject) where T : DependencyObject
    {
        if (dependencyObject is null)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, i);
            if (child is T target)
            {
                return target;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DataContextChanged -= OnDataContextChanged;
        Unloaded -= OnUnloaded;
        Loaded -= OnLoaded;

        if (_currentViewModel is not null)
        {
            _currentViewModel.CloseRequested -= OnCloseRequested;
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _currentViewModel = null;
        }
    }
}
