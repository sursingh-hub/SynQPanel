using SynQPanel.Models;
using SynQPanel.Monitors;
using SynQPanel.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SynQPanel.Views.Components
{
   
    public partial class PluginSensors : System.Windows.Controls.UserControl
    {
        private PluginSensorsVM ViewModel { get; set; }

        private readonly DispatcherTimer UpdateTimer = new() { Interval = TimeSpan.FromSeconds(1) };

        public PluginSensors()
        {
            ViewModel = new PluginSensorsVM();
            DataContext = ViewModel;

            InitializeComponent();

            
            Loaded += PluginSensors_OnLoaded;
            Unloaded += PluginSensors_OnUnloaded;
        }

        private void PluginSensors_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure VM refresh happens immediately on load
            try
            {
               ViewModel.LoadPlugins(); // safe no-op if already populated
            }
            catch { /* swallow to avoid breaking UI */ }

            // Wire timer if present (keeps existing behavior)
            if (UpdateTimer != null)
            {
                UpdateTimer.Tick += Timer_Tick;
                // Fire once synchronously to show items immediately
                Timer_Tick(this, EventArgs.Empty);
                UpdateTimer.Start();
            }
        }

        private void PluginSensors_OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (UpdateTimer != null)
            {
                UpdateTimer.Stop();
                UpdateTimer.Tick -= Timer_Tick;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Keep existing UI refresh behavior but delegate to ViewModel
            //ViewModel.RefreshPlugins();
           LoadSensorTree();
            UpdateSensorDetails();
        }


        private void LoadSensorTree()
        {
            try
            {
                // snapshot to avoid iterator invalidation and nulls
                var readings = PluginMonitor.GetOrderedList()?.ToList() ?? new List<PluginMonitor.PluginReading>();

                // Ensure collection changes happen on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (PluginMonitor.PluginReading reading in readings)
                    {
                        // construct plugin (root)
                        var parent = ViewModel.FindParentSensorItem(reading.PluginId);
                        if (parent == null)
                        {
                            parent = new PluginTreeItem(reading.PluginId, reading.PluginName);
                            ViewModel.Sensors.Add(parent);
                        }
                        else
                        {
                            // update plugin name if changed
                            var newPluginName = reading.PluginName ?? reading.PluginId?.ToString() ?? "";
                            if (parent.Name != newPluginName) parent.Name = newPluginName;
                        }

                        // construct container
                        var container = parent.FindChild(reading.ContainerId);
                        if (container == null)
                        {
                            container = new PluginTreeItem(reading.ContainerId, reading.ContainerName);
                            parent.Children.Add(container);
                        }
                        else
                        {
                            var newContainerName = reading.ContainerName ?? reading.ContainerId?.ToString() ?? "";
                            if (container.Name != newContainerName) container.Name = newContainerName;
                        }

                        // construct actual sensor
                        var child = container.FindChild(reading.Id);
                        if (child == null)
                        {
                            child = new PluginSensorItem(reading.Id, reading.Name, reading.Id);
                            container.Children.Add(child);
                        }
                        else
                        {
                            // update sensor display name if it changed
                            var newSensorName = reading.Name ?? child.Name;
                            if (child.Name != newSensorName) child.Name = newSensorName;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadSensorTree error: " + ex);
            }
        }


        private void TreeViewInfo_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is PluginSensorItem sensorItem)
            {
                ViewModel.SelectedItem = sensorItem;
                sensorItem.Update();
            }
            else
            {
                ViewModel.SelectedItem = null;
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void UpdateSensorDetails()
        {
            ViewModel.SelectedItem?.Update();
        }



        private void ButtonAddHttpImage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem is PluginSensorItem sensorItem && SharedModel.Instance.SelectedProfile is Profile selectedProfile)
            {
                var item = new HttpImageDisplayItem(sensorItem.Name, selectedProfile)
                {
                    Width = 100,
                    Height = 100,
                    PluginSensorId = sensorItem.SensorId,
                    SensorType = Enums.SensorType.Plugin
                };
                SharedModel.Instance.AddDisplayItem(item);
            }
        }

        private void ButtonAddTableSensor_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedItem is PluginSensorItem sensorItem && SharedModel.Instance.SelectedProfile is Profile selectedProfile)
            {
                var item = new TableSensorDisplayItem(sensorItem.Name, selectedProfile, sensorItem.SensorId);
                if (SensorReader.ReadPluginSensor(sensorItem.SensorId) is SensorReading sensorReading && sensorReading.ValueTableFormat is string format)
                {
                    item.TableFormat = format;
                }
                SharedModel.Instance.AddDisplayItem(item);
            }
        }
    }
}
