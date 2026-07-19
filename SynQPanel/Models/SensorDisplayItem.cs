using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Enums;
using System;
using System.Linq;
using System.Windows;

namespace SynQPanel.Models
{
    [Serializable]
    public partial class SensorDisplayItem : TextDisplayItem, ISensorItem, IPluginSensorItem
    {
        private string _sensorName = string.Empty;
        public string SensorName
        {
            get { return _sensorName; }
            set
            {
                SetProperty(ref _sensorName, value);
            }
        }





        private Enums.SensorType _sensorType = Enums.SensorType.Plugin;
        public new Enums.SensorType SensorType
        {
            get { return _sensorType; }
            set
            {
                SetProperty(ref _sensorType, value);
                OnPropertyChanged(nameof(IsAddOn)); // <--- TELLS THE ICON TO UPDATE!
            }
        }


        private UInt32 _id;
        public UInt32 Id
        {
            get { return _id; }
            set
            {
                SetProperty(ref _id, value);
            }
        }

        private UInt32 _instance;
        public UInt32 Instance
        {
            get { return _instance; }
            set
            {
                SetProperty(ref _instance, value);
            }
        }

        private UInt32 _entryId;
        public UInt32 EntryId
        {
            get { return _entryId; }
            set
            {
                SetProperty(ref _entryId, value);
            }
        }

      

        private string _pluginSensorId = string.Empty;
        public string PluginSensorId
        {
            get => _pluginSensorId;
            set
            {
                if (_pluginSensorId != value)
                {
                    _pluginSensorId = value;
                    OnPropertyChanged(nameof(PluginSensorId)); // ensures UI updates!
                    OnPropertyChanged(nameof(IsAddOn));        // TELLS THE ICON TO UPDATE!
                }
            }
        }


        public SensorValueType _valueType = SensorValueType.NOW;
        public SensorValueType ValueType
        {
            get { return _valueType; }
            set
            {
                SetProperty(ref _valueType, value);
            }
        }

        private double _threshold1 = 0;
        public double Threshold1
        {
            get { return _threshold1; }
            set
            {
                SetProperty(ref _threshold1, value);
            }
        }

        private string _threshold1Color = "#000000";
        public string Threshold1Color
        {
            get { return _threshold1Color; }
            set
            {
                if (value == null)
                {
                    return;
                }

                if (!value.StartsWith("#"))
                {
                    value = "#" + value;
                }

                SetProperty(ref _threshold1Color, value);
            }
        }

        private double _threshold2 = 0;
        public double Threshold2
        {
            get { return _threshold2; }
            set
            {
                SetProperty(ref _threshold2, value);
            }
        }

        private string _threshold2Color = "#000000";
        public string Threshold2Color
        {
            get { return _threshold2Color; }
            set
            {
                if (value == null)
                {
                    return;
                }

                if (!value.StartsWith("#"))
                {
                    value = "#" + value;
                }

                SetProperty(ref _threshold2Color, value);
            }
        }

        private bool _showName = false;
        public bool ShowName
        {
            get { return _showName; }
            set
            {
                SetProperty(ref _showName, value);
            }
        }

        private string _unit = string.Empty;
        public string Unit
        {
            get { return _unit; }
            set
            {
                SetProperty(ref _unit, value);
            }
        }

        private bool _overrideUnit = false;
        public bool OverrideUnit
        {
            get { return _overrideUnit; }
            set
            {
                SetProperty(ref _overrideUnit, value);
            }
        }

        private bool _showUnit = true;
        public bool ShowUnit
        {
            get { return _showUnit; }
            set
            {
                SetProperty(ref _showUnit, value);
            }
        }

        private bool _overridePrecision = false;
        public bool OverridePrecision
        {
            get { return _overridePrecision; }
            set
            {
                SetProperty(ref _overridePrecision, value);
            }
        }

        private int _precision = 0;
        public int Precision
        {
            get { return _precision; }
            set
            {
                SetProperty(ref _precision, value);
            }
        }

        private double _additionModifier = 0;
        public double AdditionModifier
        {
            get { return _additionModifier; }
            set
            {
                SetProperty(ref _additionModifier, value);
            }
        }

        private bool _absoluteAddition = true;
        public bool AbsoluteAddition
        {
            get { return _absoluteAddition; }
            set
            {
                SetProperty(ref _absoluteAddition, value);
            }
        }

        private double _multiplicationModifier = 1.00;
        public double MultiplicationModifier
        {
            get { return _multiplicationModifier; }
            set
            {
                SetProperty(ref _multiplicationModifier, value);
            }
        }

        [ObservableProperty]
        private bool _divisionToggle = false;

        public SensorDisplayItem()
        {
            SensorName = string.Empty;
        }

        public SensorDisplayItem(string name, Profile profile) : base(name, profile)
        {
            SensorName = name;
        }

        public SensorDisplayItem(string name, Profile profile, string pluginSensorId) : base(name, profile)
        {
            SensorName = name;
            SensorType = SensorType.Plugin;
            PluginSensorId = pluginSensorId ?? string.Empty;
        }

        public SensorDisplayItem(string name, Profile profile, uint id, uint instance, uint entryId) : base(name, profile)
        {
            SensorName = name;
            SensorType = Enums.SensorType.Plugin;
            Id = id;
            Instance = instance;
            EntryId = entryId;
        }

       


        public SensorReading? GetValue()
        {
            // Avoid hitting sensors in the designer
            if (DesignModeHelper.IsInDesignMode)
                return null;

            return SensorType switch
            {
               
                SensorType.Plugin => SensorReader.ReadPluginSensor(PluginSensorId),
                _ => null,
            };
        }



        public override (string, string) EvaluateTextAndColor()
        {
            var value = GetValue();

            if (value.HasValue)
            {
                return (EvaluateText(value.Value), EvaluateColor(value.Value));
            }

            return ("-", Color);
        }

        public override string EvaluateColor()
        {
            var value = GetValue();

            if (value.HasValue)
            {
                return EvaluateColor(value.Value);
            }

            return Color;
        }

            private string EvaluateColor(SensorReading sensorReading)
            {
                if (Threshold1 > 0 || Threshold2 > 0)
                {
                    double sensorReadingValue;

                    switch (ValueType)
                    {
                        case SensorValueType.MIN:
                            sensorReadingValue = sensorReading.ValueMin;
                            break;
                        case SensorValueType.MAX:
                            sensorReadingValue = sensorReading.ValueMax;
                            break;
                        case SensorValueType.AVERAGE:
                            sensorReadingValue = sensorReading.ValueAvg;
                            break;
                        default:
                            sensorReadingValue = sensorReading.ValueNow;
                            break;
                    }

                    if (DivisionToggle)
                    {
                        if(MultiplicationModifier != 0)
                        {
                            sensorReadingValue = sensorReadingValue / MultiplicationModifier + AdditionModifier;
                        } else {
                            sensorReadingValue = sensorReadingValue + AdditionModifier;
                        }
                    } else
                    {
                        sensorReadingValue = sensorReadingValue * MultiplicationModifier + AdditionModifier;
                    }


                    if (AbsoluteAddition)
                    {
                        sensorReadingValue = Math.Abs(sensorReadingValue);
                    }

                    if (Threshold2 > 0 && sensorReadingValue >= Threshold2)
                    {
                        return Threshold2Color;
                    }
                    else if (Threshold1 > 0 && sensorReadingValue >= Threshold1)
                    {
                        return Threshold1Color;
                    }
                }
                return Color;
            }

            public override string EvaluateText()
            {
                var sensorReading = GetValue();

                if (sensorReading.HasValue)
                {
                    return EvaluateText(sensorReading.Value);
                }

                return "-";
            }

        private string EvaluateText(SensorReading sensorReading)
        {
            string? value;

            // =====================================================================
            // FIX: Intercept TimeFlow components FIRST before ValueText bypasses it
            // =====================================================================
            bool isTimeFlowPadded = false;
            if (SensorType == Enums.SensorType.Plugin)
            {
                string idCheck = (PluginSensorId ?? "").ToLowerInvariant().Trim();
                string nameCheck = (SensorName ?? "").ToLowerInvariant().Trim();

                // Check if the ID or Name ends with the exact TimeFlow absolute names
                // (This catches "timeflow/minute", "timeflow/second", or just "minute")
                if (idCheck.EndsWith("minute") || nameCheck.EndsWith("minute") ||
                    idCheck.EndsWith("second") || nameCheck.EndsWith("second") ||
                    idCheck.EndsWith("hour (12)") || nameCheck.EndsWith("hour (12)") ||
                    idCheck.EndsWith("hour (24)") || nameCheck.EndsWith("hour (24)") ||
                    idCheck.EndsWith("day") || nameCheck.EndsWith("day") ||
                    idCheck.EndsWith("month") || nameCheck.EndsWith("month"))
                {
                    isTimeFlowPadded = true;
                }
            }

            if (isTimeFlowPadded)
            {
                // Force exactly two digits from the raw mathematical value (e.g. 5 -> "05")
                value = string.Format("{0:00}", sensorReading.ValueNow);
            }
            // Standard string sensor handling
            else if (!string.IsNullOrEmpty(sensorReading.ValueText))
            {
                value = sensorReading.ValueText;
            }
            else
            {
                double sensorReadingValue;

                switch (ValueType)
                {
                    case SensorValueType.MIN:
                        sensorReadingValue = sensorReading.ValueMin;
                        break;
                    case SensorValueType.MAX:
                        sensorReadingValue = sensorReading.ValueMax;
                        break;
                    case SensorValueType.AVERAGE:
                        sensorReadingValue = sensorReading.ValueAvg;
                        break;
                    default:
                        sensorReadingValue = sensorReading.ValueNow;
                        break;
                }

                if (DivisionToggle)
                {
                    if (MultiplicationModifier != 0)
                    {
                        sensorReadingValue = sensorReadingValue / MultiplicationModifier + AdditionModifier;
                    }
                    else
                    {
                        sensorReadingValue = sensorReadingValue + AdditionModifier;
                    }
                }
                else
                {
                    sensorReadingValue = sensorReadingValue * MultiplicationModifier + AdditionModifier;
                }

                if (AbsoluteAddition)
                {
                    sensorReadingValue = Math.Abs(sensorReadingValue);
                }

                if (OverridePrecision)
                {
                    switch (Precision)
                    {
                        case 1:
                            value = string.Format("{0:0.0}", sensorReadingValue);
                            break;
                        case 2:
                            value = string.Format("{0:0.00}", sensorReadingValue);
                            break;
                        case 3:
                            value = string.Format("{0:0.000}", sensorReadingValue);
                            break;
                        default:
                            value = string.Format("{0:0}", Math.Floor(sensorReadingValue));
                            break;
                    }
                }
                else
                {
                    switch (sensorReading.Unit.ToLower())
                    {
                        case "gb":
                            value = string.Format("{0:0.0}", sensorReadingValue);
                            break;
                        case "kb/s":
                        case "mb/s":
                        case "mbar/min":
                        case "mbar":
                            value = string.Format("{0:0.00}", sensorReadingValue);
                            break;
                        case "v":
                            value = string.Format("{0:0.000}", sensorReadingValue);
                            break;
                        default:
                            value = string.Format("{0:0}", sensorReadingValue);
                            break;
                    }
                }
            }

            if (ShowUnit)
            {
                if (OverrideUnit)
                {
                    // Preserve exact spaces from the text box
                    string unitToShow = Unit ?? string.Empty;
                    value += unitToShow;
                }
                else
                {
                    // FALLBACK: Use live sensor unit and apply AIDA's default spacing rules
                    string unitToShow = !string.IsNullOrEmpty(sensorReading.Unit)
                        ? sensorReading.Unit.Trim()
                        : (Unit?.Trim() ?? string.Empty);

                    if (!string.IsNullOrEmpty(unitToShow))
                    {
                        if (unitToShow == "%" || unitToShow == "°C" || unitToShow == "°F")
                        {
                            value += unitToShow;
                        }
                        else
                        {
                            if (!value.EndsWith(" "))
                                value += " ";

                            value += unitToShow;
                        }
                    }
                }
            }

            if (ShowName)
            {
                value = Name + " " + value;
            }

            return value;
        }

        // ICON HELPER

        private bool? _isAddOn;

        public bool IsAddOn
        {
            get
            {
                // 1. Not a Plugin, or empty ID? Definitely not an Add-on.
                if (SensorType != Enums.SensorType.Plugin || string.IsNullOrWhiteSpace(PluginSensorId))
                    return false;

                // 2. Check the live PluginMonitor list
                var addOnReadings = SynQPanel.Monitors.PluginMonitor.GetOrderedList();
                if (addOnReadings != null)
                {
                    // If the ID exists in your Add-ons, return true!
                    return addOnReadings.Any(r => string.Equals(r.Id, PluginSensorId, StringComparison.OrdinalIgnoreCase));
                }

                return false;
            }
        }






    }
}
