using SynQPanel.Enums;
using System;

namespace SynQPanel.Models
{
    public enum SensorValueType
    {
        NOW, MIN, MAX, AVERAGE
    }

    // ISensorItem remains the union of the two specialized interfaces (HwInfo removed/kept as needed).
    internal interface ISensorItem : IHwInfoSensorItem, IPluginSensorItem
    {
    }

    // Common shared members
    internal interface IHwInfoSensorItem
    {
        string SensorName { get; set; }
        SensorType SensorType { get; set; }
        SensorValueType ValueType { get; set; }
        SensorReading? GetValue();

        // HwInfo-specific fields (may be unused)
        UInt32 Id { get; set; }
        UInt32 Instance { get; set; }
        UInt32 EntryId { get; set; }
    }

    // Plugin interface
    internal interface IPluginSensorItem
    {
        // Plugin-specific id
        string PluginSensorId { get; set; }
    }
}
