using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReportPreviewDialogViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is ReportPreviewDialogViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }

        _currentViewModel = e.NewValue as ReportPreviewDialogViewModel;
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

        if (_currentViewModel is not null)
        {
            _currentViewModel.CloseRequested -= OnCloseRequested;
            _currentViewModel = null;
        }
    }
}
