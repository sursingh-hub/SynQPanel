using SynQPanel.ViewModels.Components;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SynQPanel.Views.Components
{
    
    
    
    
    public partial class AidaSensors : UserControl
    {
        private AidaSensorsVM ViewModel { get; set; }
        private readonly DispatcherTimer UpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };


        // --- Start: Scroll tracking additions ---
        private double lastGroupedTreeOffset = 0;
        private ScrollViewer? groupedTreeScrollViewer = null;

        public static ScrollViewer? GetScrollViewer(DependencyObject dep)
        {
            if (dep is ScrollViewer) return (ScrollViewer)dep;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
        // --- End: Scroll tracking additions ---



        public AidaSensors()
        {
            InitializeComponent();

            ViewModel = new AidaSensorsVM();
            DataContext = ViewModel;

            Loaded += AidaSensors_Loaded;
            Unloaded += AidaSensors_Unloaded;
        }

        private void AidaSensors_Loaded(object sender, RoutedEventArgs e)
        {
            // Build sensors list at first
            ViewModel.LoadSensors();

            UpdateTimer.Tick += UpdateTimer_Tick;
            UpdateTimer.Start();
        }


        private void AidaSensors_Unloaded(object sender, RoutedEventArgs e)
        {
            UpdateTimer.Tick -= UpdateTimer_Tick;
            UpdateTimer.Stop();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            ViewModel.UpdateSensorValues();
        }


        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void TreeViewInfo_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SensorTreeItem sensorItem)
            {
                ViewModel.SelectedItem = sensorItem;
                ViewModel.SetLastSelected(sensorItem.Id); // <-- track selection for restore
                sensorItem.Update();
            }
            else
            {
                ViewModel.SelectedItem = null;
                ViewModel.SetLastSelected(null);
            }
        }





    }
}
