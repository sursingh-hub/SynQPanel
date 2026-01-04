using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Monitors;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System;
using System.Windows;

using System.Linq;

namespace SynQPanel.ViewModels.Components
{
    public partial class PluginSensorsVM : ObservableObject
    {
        public ObservableCollection<TreeItem> Sensors { get; set; }

        private PluginSensorItem? selectedItem;
        public PluginSensorItem? SelectedItem
        {
            get { return selectedItem; }
            set { SetProperty(ref selectedItem, value); }
        }

        public PluginSensorsVM()
        {
            Sensors = [];
        }

        public TreeItem? FindParentSensorItem(object id)
        {
            foreach (var sensorItem in Sensors)
            {
                if (sensorItem.Id.Equals(id))
                {
                    return sensorItem;
                }
            }

            return null;
        }

        // Add these methods inside PluginSensorsVM
        public void LoadPlugins()
        {
            // Simple wrapper — idempotent and safe
            try
            {
                RefreshPlugins();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadPlugins error: " + ex);
            }
        }

        public void RefreshPlugins()
        {
            try
            {
                // Get current plugin readings (safe: GetOrderedList() returns an empty list if none)
                var readings = PluginMonitor.GetOrderedList()?.ToList() ?? new List<PluginMonitor.PluginReading>();

                // Build a new tree model (clean and deterministic). This avoids tricky in-place diffing.
                var roots = new List<PluginTreeItem>();


                

                foreach (var reading in readings)
                {
                    // find or create plugin root
                    var root = roots.FirstOrDefault(r => string.Equals(r.Id?.ToString(), reading.PluginId?.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (root == null)
                    {
                        root = new PluginTreeItem(reading.PluginId, reading.PluginName ?? reading.PluginId);
                        roots.Add(root);
                    }

                    // find or create container child
                    var container = root.Children.OfType<PluginTreeItem>().FirstOrDefault(c => string.Equals(c.Id?.ToString(), reading.ContainerId?.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (container == null)
                    {
                        container = new PluginTreeItem(reading.ContainerId, reading.ContainerName ?? reading.ContainerId);
                        root.Children.Add(container);
                    }

                    // find or create sensor child
                    var child = container.Children.OfType<PluginSensorItem>().FirstOrDefault(ch => string.Equals(ch.Id?.ToString(), reading.Id?.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (child == null)
                    {
                        child = new PluginSensorItem(reading.Id, reading.Name ?? reading.Id, reading.Id);
                        container.Children.Add(child);
                    }
                    else
                    {
                        // update name if changed
                        child.Name = reading.Name ?? child.Name;
                    }
                }

                


                

                // Apply new roots onto VM.Sensors on the UI thread
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Clear and repopulate (keeps bindings simple)
                        this.Sensors.Clear();
                        foreach (var r in roots) this.Sensors.Add(r);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("RefreshPlugins (UI apply) error: " + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RefreshPlugins error: " + ex);
            }
        }









    }

}
