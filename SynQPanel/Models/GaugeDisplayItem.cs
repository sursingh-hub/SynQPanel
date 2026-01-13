using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Enums;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SynQPanel.Models
{
    [Serializable]
    public partial class GaugeDisplayItem : DisplayItem, ISensorItem
    {



        // ─────────────────────────────────────────────
        // AIDA Gauge Value Text (Model-only, Phase 1)
        // ─────────────────────────────────────────────

        // AIDA Gauge "Show Value" support
        //public bool ShowValue { get; set; }              // SHWVAL
        public int ValueTextSize { get; set; } = 12;     // TXTSIZ
        public bool ValueBold { get; set; }               // VALBI[0]
        public bool ValueItalic { get; set; }             // VALBI[1]
        public string ValueColor { get; set; } = "#FFFFFF";   // VALCOL (hex)

        private string _valueFontName = string.Empty;
        public string ValueFontName     // FNTNAM
        {
            get => _valueFontName;
            set => SetProperty(ref _valueFontName, value);
        }


        private bool _showValue;
        private bool _valueTextInitialized;
        private bool _initializingValueText;


        public bool ShowValue
        {
            get => _showValue;
            set
            {
                if (_showValue == value)
                    return;

                _showValue = value;
                OnPropertyChanged();

                // IMPORTANT: do NOT mutate other bound properties synchronously
                if (_showValue && !_valueTextInitialized && !_initializingValueText)
                {
                    _initializingValueText = true;

                    // Defer to UI dispatcher to avoid layout re-entrancy
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(ValueFontName))
                                    ValueFontName = "Segoe UI";

                                if (ValueTextSize <= 0)
                                    ValueTextSize = 12;

                                _valueTextInitialized = true;
                            }
                            finally
                            {
                                _initializingValueText = false;
                            }
                        }),
                        System.Windows.Threading.DispatcherPriority.Background
                    );
                }
            }
        }





        private string _sensorName = String.Empty;
        public string SensorName
        {
            get { return _sensorName; }
            set
            {
                SetProperty(ref _sensorName, value);
            }
        }

        private SensorType _sensorType = SensorType.Plugin;
        public override SensorType SensorType
        {
            get { return _sensorType; }
            set
            {
                SetProperty(ref _sensorType, value);
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

        private double _minValue = 0.0;
        public double MinValue
        {
            get { return _minValue; }
            set
            {
                SetProperty(ref _minValue, value);
            }
        }

        private double _maxValue = 100.0;
        public double MaxValue
        {
            get { return _maxValue; }
            set
            {
                SetProperty(ref _maxValue, value);
            }
        }

        private int _scale = 100;
        public int Scale
        {
            get { return _scale; }
            set
            {
                SetProperty(ref _scale, value);
            }
        }

        [ObservableProperty]
        private int _width = 0;

        [ObservableProperty]
        private int _height = 0;


        private ObservableCollection<ImageDisplayItem> _images = [];

        public ObservableCollection<ImageDisplayItem> Images
        {
            get { return _images; }
            set
            {
                SetProperty(ref _images, value);
            }
        }

        private bool forward = true;
        private int counter = 0;

        public ImageDisplayItem? DisplayImage
        {
            get
            {
                if (_images.Count == 0)
                {
                    return null;
                }

                if (counter >= _images.Count || counter < 0)
                {
                    counter = 0;
                }

                if (counter >= _images.Count - 1)
                {
                    forward = false;
                }
                else if (counter <= 0)
                {
                    forward = true;
                }

                var result = _images.ElementAt(counter);
                if (forward)
                {
                    counter++;
                }
                else
                {
                    counter--;
                }

                return result;
            }
        }

        public void TriggerDisplayImageChange()
        {
            OnPropertyChanged(nameof(DisplayImage));
        }

        public GaugeDisplayItem()
        {
            Name = "Gauge";
        }

        public GaugeDisplayItem(string name, Profile profile) : base(name, profile)
        {
            SensorName = name;
        }

        public GaugeDisplayItem(string name, Profile profile, string pluginSensorId) : base(name, profile)
        {
            SensorName = name;
            SensorType = SensorType.Plugin;
            PluginSensorId = pluginSensorId ?? string.Empty;
        }

        public GaugeDisplayItem(string name, Profile profile, UInt32 id, UInt32 instance, UInt32 entryId) : base(name, profile)
        {
            SensorName = name;
            SensorType = SensorType.Plugin;
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


        private double currentImageIndex = 0;
        public ImageDisplayItem? EvaluateImage(double interpolationDelay = 1)
        {
            ImageDisplayItem? result = null;
            if (_images.Count == 1)
            {
                result = Images[0];
            }

            if (_images.Count > 1)
            {
                var sensorReading = GetValue();
                if(sensorReading.HasValue) {
                    var step = 100.0 / (_images.Count - 1);

                    var value = sensorReading.Value.ValueNow;
                    value = ((value - _minValue) / (_maxValue - _minValue)) * 100;

                    var index = (int)(value / step);

                    var intermediateIndex = Interpolate(currentImageIndex, index, interpolationDelay * 2);
                    intermediateIndex = Math.Clamp(intermediateIndex, 0, Images.Count - 1);
                    currentImageIndex = intermediateIndex;

                    result = Images[(int)Math.Round(intermediateIndex)];
                } else
                {
                    result = Images[0];
                }
            }

            if(result != null)
            {
                result.Scale = _scale;
            }

            return result;
        }

        public ImageDisplayItem? CurrentImage
        {
            get
            {
                if(_images.Count > 0)
                {
                    currentImageIndex = Math.Clamp(currentImageIndex, 0, Images.Count - 1);
                    var imageDisplayItem = Images[(int)Math.Round(currentImageIndex)];
                    imageDisplayItem.Scale = _scale;
                    return imageDisplayItem;
                }

                return null;
            }
        }

        private static double Interpolate(double startValue, int endValue, double position)
        {
            // Ensure position is within the range of 0 to 100
            position = Math.Clamp(position, 0, 1);

            // Handle case where start and target positions are equal
            if (startValue == endValue)
            {
                return startValue;
            }

            // Calculate the interpolated value
            double interpolatedValue = startValue + (endValue - startValue) * position;

            return interpolatedValue;
        }

        public override SKRect EvaluateBounds()
        {
            var size = EvaluateSize();
            return new SKRect(X, Y, X + size.Width, Y + size.Height);
        }

        public override SKSize EvaluateSize()
        {
            if(Width != 0 && Height != 0)
            {
                return new SKSize(Width, Height);
            }

            var result = new SKSize(0, 0);

            if(CurrentImage != null)
            {
                return CurrentImage.EvaluateSize();
            }

            return result;
        }

        public override string EvaluateText()
        {
            return Name;
        }

        public override string EvaluateColor()
        {
            return "#000000";
        }

        public override (string, string) EvaluateTextAndColor()
        {
            return (Name, "#000000");
        }

        public override void SetProfile(Profile profile)
        {
            base.SetProfile(profile);

            foreach (var imageDisplayItem in Images)
            {
                imageDisplayItem.SetProfile(profile);
                imageDisplayItem.PersistentCache = true; // Ensure gauge images never expire
            }
        }

        public override object Clone()
        {
            var clone = (GaugeDisplayItem)MemberwiseClone();
            clone.Guid = Guid.NewGuid();

            clone.Images = new ObservableCollection<ImageDisplayItem>();

            foreach(var imageDisplayItem in Images)
            {
                var cloneImage = (ImageDisplayItem) imageDisplayItem.Clone();
                cloneImage.Guid = Guid.NewGuid();
                cloneImage.PersistentCache = true; // Ensure gauge images never expire
                clone.Images.Add(cloneImage);
            }

            return clone;
        }
    }
}
