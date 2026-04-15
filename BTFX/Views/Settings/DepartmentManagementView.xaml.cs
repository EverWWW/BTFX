using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BTFX.ViewModels;
using BTFX.ViewModels.Settings;

namespace BTFX.Views.Settings
{
    /// <summary>
    /// DepartmentManagementView.xaml 的交互逻辑
    /// </summary>
    public partial class DepartmentManagementView : UserControl
    {
        public DepartmentManagementView()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdatePageSizeFromViewport();
            Dispatcher.BeginInvoke(UpdatePageSizeFromViewport, DispatcherPriority.Loaded);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Abs(e.PreviousSize.Height - e.NewSize.Height) < 0.5)
            {
                return;
            }

            UpdatePageSizeFromViewport();
            Dispatcher.BeginInvoke(UpdatePageSizeFromViewport, DispatcherPriority.Loaded);
        }

        private void RowBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not DepartmentManagementViewModel viewModel)
            {
                return;
            }

            if (IsClickFromButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (sender is Border { DataContext: DepartmentItem item })
            {
                viewModel.SelectedDepartment = item;
            }
        }

        private static bool IsClickFromButton(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button)
                {
                    return true;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void UpdatePageSizeFromViewport()
        {
            if (DataContext is not DepartmentManagementViewModel viewModel)
            {
                return;
            }

            var viewport = FindName("DepartmentListViewport") as FrameworkElement;
            var viewportHeight = viewport?.ActualHeight ?? 0;
            if (viewportHeight <= 0)
            {
                return;
            }

            viewModel.UpdatePageSize(viewportHeight);
        }
    }
}
