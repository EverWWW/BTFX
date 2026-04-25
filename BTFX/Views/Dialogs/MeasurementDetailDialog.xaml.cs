using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;

namespace BTFX.Views.Dialogs;

/// <summary>
/// MeasurementDetailDialog.xaml 的交互逻辑
/// </summary>
public partial class MeasurementDetailDialog
{
    private GaitAnalysisDetailViewModel? _currentAnalysisViewModel;
    private MeasurementDetailViewModel? _currentMeasurementViewModel;

    public MeasurementDetailDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MeasurementDetailViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.OldValue is GaitAnalysisDetailViewModel oldAnalysisVm)
        {
            oldAnalysisVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is MeasurementDetailViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }

        if (e.NewValue is GaitAnalysisDetailViewModel newAnalysisVm)
        {
            newAnalysisVm.CloseRequested += OnCloseRequested;
        }

        _currentMeasurementViewModel = e.NewValue as MeasurementDetailViewModel;
        _currentAnalysisViewModel = e.NewValue as GaitAnalysisDetailViewModel;
    }

    private void OnCloseRequested()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(OnCloseRequested);
            return;
        }

        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;

        if (TryCloseDialogHost())
        {
            return;
        }

        if (Parent is FrameworkElement parent)
        {
            parent.Visibility = Visibility.Collapsed;
            parent.IsHitTestVisible = false;
        }
    }

    private bool TryCloseDialogHost()
    {
        var dialogHost = FindAncestor<DialogHost>(this);
        if (dialogHost?.CurrentSession is { } session)
        {
            session.Close();
            return true;
        }

        if (!DialogHost.IsDialogOpen("RootDialog"))
        {
            return false;
        }

        DialogHost.Close("RootDialog");
        return true;
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DataContextChanged -= OnDataContextChanged;
        Unloaded -= OnUnloaded;

        if (_currentMeasurementViewModel is not null)
        {
            _currentMeasurementViewModel.CloseRequested -= OnCloseRequested;
        }

        if (_currentAnalysisViewModel is not null)
        {
            _currentAnalysisViewModel.CloseRequested -= OnCloseRequested;
        }

        _currentMeasurementViewModel = null;
        _currentAnalysisViewModel = null;
    }
}
