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
                    var app = System.Windows.Application.Current;
                    var dispatcher = app?.Dispatcher;

                    if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    {
                        // App is closing – abort initialization safely
                        _initializingValueText = false;
                        return;
                    }

                    dispatcher.BeginInvoke(
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
                    OnPropertyChanged(nameof(PluginSensorId));
                    OnPropertyChanged(nameof(IsAddOn)); // <--- ADD THIS HERE TOO!
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


        private double _animationSpeed = 0;
        // Variables for smooth animation
        private double _visualIndex = 0;
        private DateTime _lastUpdate = DateTime.UtcNow;

        public double AnimationSpeed
        {
            get { return _animationSpeed; }
            set
            {
                SetProperty(ref _animationSpeed, value);
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
                if (sensorReading.HasValue)
                {

                    double rawVal = sensorReading.Value.ValueNow;

                    // Normalize to 0.0 - 1.0
                    double range = _maxValue - _minValue;
                    double fraction = range != 0 ? (rawVal - _minValue) / range : 0;
                    fraction = Math.Clamp(fraction, 0.0, 1.0);

                    // Universal Mapping: Floor(fraction * Count)
                    // This distributes values evenly across all available images
                    int index = (int)Math.Floor(fraction * _images.Count);

                    // Handle the 100% case (fraction=1.0 -> index=Count -> out of bounds)
                    if (index >= _images.Count)
                    {
                        index = _images.Count - 1;
                    }

                    // Interpolation logic
                    var intermediateIndex = Interpolate(currentImageIndex, index, interpolationDelay * 2);
                    intermediateIndex = Math.Clamp(intermediateIndex, 0, Images.Count - 1);
                    currentImageIndex = intermediateIndex;

                    result = Images[(int)Math.Round(intermediateIndex)];
                }
                else
                {
                    result = Images[0];
                }
            }

            if (result != null)
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

        //Icon Helper
        public bool IsAddOn
        {
            get
            {
                if (SensorType != Enums.SensorType.Plugin || string.IsNullOrWhiteSpace(PluginSensorId))
                    return false;

                var addOnReadings = SynQPanel.Monitors.PluginMonitor.GetOrderedList();
                if (addOnReadings != null)
                {
                    return addOnReadings.Any(r => string.Equals(r.Id, PluginSensorId, StringComparison.OrdinalIgnoreCase));
                }

                return false;
            }
        }

        //Smooth Helper
        public struct GaugeRenderFrame
        {
            public ImageDisplayItem? BaseImage;
            public ImageDisplayItem? OverlayImage; // Can be null if perfectly on a frame
            public float BlendOpacity; // 0.0 to 1.0
        }

        public GaugeRenderFrame EvaluateFluidFrame()
        {
            // Safety check: if no images, return empty
            if (_images == null || _images.Count == 0)
            {
                return new GaugeRenderFrame { BaseImage = null, OverlayImage = null, BlendOpacity = 0 };
            }

            // If there's only 1 image, we just show it
            if (_images.Count == 1)
            {
                var img = _images[0];
                img.Scale = _scale;
                return new GaugeRenderFrame { BaseImage = img, OverlayImage = null, BlendOpacity = 0 };
            }

            // 1. Figure out exactly which frame the gauge SHOULD be on (Target)
            double targetIndex = 0;
            var sensorReading = GetValue();
            if (sensorReading.HasValue)
            {
                double rawVal = sensorReading.Value.ValueNow;
                double range = _maxValue - _minValue;
                double fraction = range != 0 ? (rawVal - _minValue) / range : 0;
                fraction = Math.Clamp(fraction, 0.0, 1.0);

                targetIndex = fraction * (_images.Count - 1);
            }

            // 2. If Animation Speed is 0, snap instantly!
            if (_animationSpeed <= 0)
            {
                _visualIndex = targetIndex;
                int index = (int)Math.Round(_visualIndex);
                index = Math.Clamp(index, 0, _images.Count - 1);

                var img = _images[index];
                img.Scale = _scale;
                _lastUpdate = DateTime.UtcNow;

                return new GaugeRenderFrame { BaseImage = img, OverlayImage = null, BlendOpacity = 0 };
            }

            // 3. Smooth Animation Math with Easing and Circular Wrapping
            var now = DateTime.UtcNow;
            var deltaSeconds = (now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;
            if (deltaSeconds > 0.5) deltaSeconds = 0.5; // Lag protection

            int totalImages = _images.Count;
            double maxIndex = totalImages - 1;

            // --- CIRCULAR WRAP-AROUND LOGIC ---
            // If the distance is more than half the gauge, it means the sensor "rolled over" (e.g. 59 to 0).
            // So we push the target up past the max so it animates forward instead of backward!
            double distance = targetIndex - _visualIndex;

            if (distance < -(maxIndex / 2.0))
            {
                targetIndex += totalImages; // Roll forward over the finish line
            }
            else if (distance > (maxIndex / 2.0))
            {
                targetIndex -= totalImages; // Roll backward under the start line
            }

            // Recalculate distance after wrap adjustment
            distance = targetIndex - _visualIndex;

            // --- EASE-OUT PHYSICS (Spring damping) ---
            // Instead of linear speed, we move a percentage of the remaining distance.
            // Higher animation speed = higher tension spring.
            double easeFactor = 1.0 - Math.Exp(-_animationSpeed * deltaSeconds);

            if (Math.Abs(distance) < 0.01)
            {
                _visualIndex = targetIndex; // Snap exactly when very close
            }
            else
            {
                _visualIndex += distance * easeFactor;
            }

            // --- APPLY MODULO WRAPPING FOR DRAWING ---
            // Bring the visual index safely back into the 0-Max range
            double drawIndex = _visualIndex % totalImages;
            if (drawIndex < 0) drawIndex += totalImages;

            // 4. Determine the SINGLE closest image (Turn off crossfading to prevent ghosting)
            int frameIndex = (int)Math.Round(drawIndex);

            // If rounding pushes it exactly to the max count (e.g. 59.6 rounds to 60), 
            // wrap it perfectly back to 0 so it doesn't crash or stutter.
            if (frameIndex >= totalImages)
            {
                frameIndex = 0;
            }

            // Final safety clamp
            frameIndex = Math.Clamp(frameIndex, 0, _images.Count - 1);

            var baseImage = _images[frameIndex];
            baseImage.Scale = _scale;

            // Return ONLY the base image. Overlay is null, so the PanelDraw switch 
            // block will completely skip the second ghosted image!
            return new GaugeRenderFrame
            {
                BaseImage = baseImage,
                OverlayImage = null,
                BlendOpacity = 0f
            };
        }

    }
}
