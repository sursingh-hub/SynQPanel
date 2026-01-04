// TreeItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Extensions;
using SynQPanel.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SynQPanel.ViewModels.Components
{
    // Base tree node used in both grouped and flat views.
    public class TreeItem : ObservableObject
    {
        public object Id { get; set; }
        public string Name { get; set; }
        public string? Icon { get; set; } = "pack://application:,,,/Resources/Images/Aida/empty.png";
        public ObservableCollection<TreeItem> Children { get; set; } = new ObservableCollection<TreeItem>();

        // NEW: expansion state bound to TreeViewItem.IsExpanded
        private bool _isExpanded = false;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        // NEW: selection state bound to TreeViewItem.IsSelected
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TreeItem(object id, string name)
        {
            Id = id;
            Name = name;
        }

        public TreeItem? FindChild(object id)
        {
            foreach (var child in Children)
            {
                if (child.Id.Equals(id))
                {
                    return child;
                }
                else
                {
                    var c = child.FindChild(id);

                    if (c != null)
                    {
                        return c;
                    }
                }
            }

            return null;
        }
    }

    public class LibreHardwareTreeItem : TreeItem
    {
        public LibreHardwareTreeItem(object id, string name, LibreHardwareMonitor.Hardware.HardwareType hardwareType)
            : base(id, name)
        {
            var image = GetImage(hardwareType);
            if (image is string img)
            {
                Icon = "pack://application:,,,/Resources/Images/Libre/" + img;
            }
        }

        private static string? GetImage(LibreHardwareMonitor.Hardware.HardwareType hardwareType)
        {
            return hardwareType switch
            {
                HardwareType.Cpu => "cpu.png",
                HardwareType.GpuNvidia => "nvidia.png",
                HardwareType.GpuAmd => "amd.png",
                HardwareType.GpuIntel => "intel.png",
                HardwareType.Storage => "hdd.png",
                HardwareType.Motherboard => "mainboard.png",
                HardwareType.SuperIO or HardwareType.EmbeddedController => "chip.png",
                HardwareType.Memory => "ram.png",
                HardwareType.Network => "nic.png",
                HardwareType.Cooler => "fan.png",
                HardwareType.Psu => "power-supply.png",
                HardwareType.Battery => "battery.png",
                _ => "empty.png",
            };
        }
    }

    public class LibreGroupTreeItem : TreeItem
    {
        public LibreGroupTreeItem(object id, string name, LibreHardwareMonitor.Hardware.SensorType readingType)
            : base(id, name)
        {
            Icon = "pack://application:,,,/Resources/Images/Libre/" + readingType.ToString().ToLower() + ".png";
        }
    }

    public abstract class SensorTreeItem : TreeItem
    {
        private string _value = string.Empty;
        public string Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        private string _unit = string.Empty;
        public string Unit
        {
            get { return _unit; }
            set { SetProperty(ref _unit, value); }
        }

        public SensorTreeItem(object id, string name) : base(id, name) { }

        public abstract void Update();
    }



    // LibreSensorItem (defensive)
    public class LibreSensorItem : SensorTreeItem
    {
        public string SensorId { get; set; }

        public LibreSensorItem(object id, string name, string sensorId) : base(id, name)
        {
            SensorId = sensorId;
        }

        public override void Update()
        {
            // LibreHardwareMonitor disabled in this AIDA-only build: no update possible.
            // Keep instance alive so UI/listing code still works without errors.
            // Optionally: set a placeholder
            Value = string.Empty;
            Unit = string.Empty;
        }
    }



    public class PluginTreeItem : TreeItem
    {
        public PluginTreeItem(object id, string name) : base(id, name) { }
    }

    public class PluginSensorItem : SensorTreeItem
    {
        public string SensorId { get; set; }

        public PluginSensorItem(object id, string name, string sensorId) : base(id, name)
        {
            SensorId = sensorId;
        }

        public SensorReading? SensorReading => SensorReader.ReadPluginSensor(SensorId);

        public override void Update()
        {
            var sensorReading = SensorReading;
            if (sensorReading.HasValue)
            {
                Value = sensorReading.Value.ValueText ?? sensorReading.Value.ValueNow.ToFormattedString();
                Unit = sensorReading.Value.Unit;
            }
        }
    }
}
