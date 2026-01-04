using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Enums;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;

namespace SynQPanel.Models
{
    [Serializable]
    public partial class ArcDisplayItem : DisplayItem, ISensorItem
    {
        // Constructor(s)
        public ArcDisplayItem()
        {
            Name = "Arc";
        }

        public ArcDisplayItem(string name, Profile profile, string pluginSensorId = "")
            : base(name, profile)
        {
            Name = name;
            PluginSensorId = pluginSensorId ?? string.Empty;
            SensorType = SensorType.Plugin;
        }

        // --- Sensor identity similar to GaugeDisplayItem / project conventions ---
        // Hw-backed ids
        private UInt32 _id;
        public UInt32 Id
        {
            get { return _id; }
            set { SetProperty(ref _id, value); }
        }

        private UInt32 _instance;
        public UInt32 Instance
        {
            get { return _instance; }
            set { SetProperty(ref _instance, value); }
        }

        private UInt32 _entryId;
        public UInt32 EntryId
        {
            get { return _entryId; }
            set { SetProperty(ref _entryId, value); }
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
                    OnPropertyChanged(nameof(PluginSensorId));
                }
            }
        }

        private string _sensorName = string.Empty;
        public string SensorName
        {
            get { return _sensorName; }
            set { SetProperty(ref _sensorName, value); }
        }

        private SensorValueType _valueType = SensorValueType.NOW;
        public SensorValueType ValueType
        {
            get { return _valueType; }
            set { SetProperty(ref _valueType, value); }
        }

        private SensorType _sensorType = SensorType.Plugin;
        public override SensorType SensorType
        {
            get { return _sensorType; }
            set { SetProperty(ref _sensorType, value); }
        }

        // --- Visual / arc properties ---
        // Use the same ObservableProperty pattern you used for Gauge to avoid duplicate members
        [ObservableProperty]
        private int _width = 150;

        [ObservableProperty]
        private int _height = 150;

        private string _color = "#FFFFFF";
        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        private string _backgroundColor = "#333333";
        public string BackgroundColor
        {
            get => _backgroundColor;
            set => SetProperty(ref _backgroundColor, value);
        }

        private int _thickness = 12;
        public int Thickness
        {
            get => _thickness;
            set => SetProperty(ref _thickness, value);
        }

        private float _startAngle = -135f;
        public float StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value);
        }

        private float _sweepAngle = 270f;
        public float SweepAngle
        {
            get => _sweepAngle;
            set => SetProperty(ref _sweepAngle, value);
        }

        private double _minValue = 0.0;
        public double Min
        {
            get => _minValue;
            set => SetProperty(ref _minValue, value);
        }

        private double _maxValue = 100.0;
        public double Max
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value);
        }

        private double _limit1 = double.NaN;
        public double Limit1
        {
            get => _limit1;
            set => SetProperty(ref _limit1, value);
        }

        private double _limit2 = double.NaN;
        public double Limit2
        {
            get => _limit2;
            set => SetProperty(ref _limit2, value);
        }

        private double _limit3 = double.NaN;
        public double Limit3
        {
            get => _limit3;
            set => SetProperty(ref _limit3, value);
        }

        // optional: an images collection if you plan to animate arcs (kept for parity)
        private ObservableCollection<ImageDisplayItem> _images = new();
        public ObservableCollection<ImageDisplayItem> Images
        {
            get => _images;
            set => SetProperty(ref _images, value);
        }

        // --- Sensor read helper (same style as Gauge) ---
        public SensorReading? GetValue()
        {
            if (DesignModeHelper.IsInDesignMode)
                return null;

            return SensorType switch
            {
               
                SensorType.Plugin => SensorReader.ReadPluginSensor(PluginSensorId),
                _ => null,
            };
        }

        // --- DisplayItem overrides (types exactly matching base) ---
        public override SKRect EvaluateBounds()
        {
            var size = EvaluateSize();
            return new SKRect(X, Y, X + size.Width, Y + size.Height);
        }

        public override SKSize EvaluateSize()
        {
            return new SKSize(Width, Height);
        }

        public override string EvaluateText() => Name;

        public override string EvaluateColor() => Color;

        public override (string, string) EvaluateTextAndColor() => (Name, Color);

        public override void SetProfile(Profile profile)
        {
            base.SetProfile(profile);
            foreach (var img in Images)
            {
                img.SetProfile(profile);
                img.PersistentCache = true;
            }
        }

        public override object Clone()
        {
            var clone = (ArcDisplayItem)MemberwiseClone();
            clone.Guid = Guid.NewGuid();

            clone.Images = new ObservableCollection<ImageDisplayItem>();
            foreach (var img in Images)
            {
                var c = (ImageDisplayItem)img.Clone();
                c.Guid = Guid.NewGuid();
                c.PersistentCache = true;
                clone.Images.Add(c);
            }

            return clone;
        }
    }
}
