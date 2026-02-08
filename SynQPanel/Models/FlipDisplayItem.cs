using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using SynQPanel.Infrastructure;
using SynQPanel.Models;
using SynQPanel.Rendering;
using System;
using System.IO;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;

namespace SynQPanel.Models
{
    [Serializable]
    public partial class FlipDisplayItem : DisplayItem
    {
        // -------- Size (REQUIRED) --------
        [ObservableProperty]
        private int _width = 120;

        [ObservableProperty]
        private int _height = 160;

        // -------- User-controlled --------
        [ObservableProperty]
        private string _imageFolder = string.Empty;

        [ObservableProperty]
        private int _previewValue = 0;

        [ObservableProperty]
        private int _digitCount = 2;

        [ObservableProperty]
        private float _flipProgress = 0f; // 0 → 1

        [ObservableProperty]
        private string? _flipProgressSensorId;

        [ObservableProperty]
        private float _resolvedFlipProgress;

        // Runtime cached values (authoritative)
        [XmlIgnore]
        public int RuntimeSecond { get; set; } = 0;

        [XmlIgnore]
        public float RuntimePhase { get; set; } = 0f;
    
        [ObservableProperty]
        private FlipTimeUnit _timeUnit = FlipTimeUnit.Second;

        [XmlIgnore]
        public string DisplayName =>
        TimeUnit switch
        {
            FlipTimeUnit.Second => "Flip · Seconds",
            FlipTimeUnit.Minute => "Flip · Minutes",
            FlipTimeUnit.Hour12 => "Flip · Hours (12h)",
            FlipTimeUnit.Hour24 => "Flip · Hours (24h)",
            _ => "Flip"
        };

        [XmlIgnore]
        public bool IsFlip => true;

        partial void OnTimeUnitChanged(FlipTimeUnit value)
        {
            // Force UI to refresh computed name
            OnPropertyChanged(nameof(DisplayName));
        }

        [XmlIgnore]
        public string CalculatedImageFolder
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImageFolder) || ImageFolder == ".")
                {
                    // For SQX / profile-bound flips, use the profile’s GUID asset root
                    if (Profile != null)
                        return Path.Combine(AppPaths.Assets, Profile.Guid.ToString());
                    return string.Empty;
                }

                if (Path.IsPathRooted(ImageFolder))
                    return ImageFolder;

                if (Profile != null)
                    return Path.Combine(AppPaths.Assets, Profile.Guid.ToString(), ImageFolder);

                return ImageFolder;
            }
        }



        partial void OnImageFolderChanged(string value)
        {
            OnPropertyChanged(nameof(CalculatedImageFolder));
        }




        // -------- Constructors --------
        public FlipDisplayItem() { }

        public FlipDisplayItem(string name, Profile profile)
            : base(name, profile)
        {
        }

        // -------- Helpers --------
        public string? GetCurrentImagePath()
        {
            if (string.IsNullOrWhiteSpace(ImageFolder))
                return null;

            var path = Path.Combine(ImageFolder, $"{PreviewValue}.png");
            return File.Exists(path) ? path : null;
        }

        // -------- Required Overrides --------
        public override string EvaluateText() => string.Empty;

        public override string EvaluateColor() => "#00FFFFFF";

        public override (string, string) EvaluateTextAndColor()
            => (string.Empty, "#00FFFFFF");

        public override SKSize EvaluateSize()
        {
            return new SKSize(Width, Height);
        }

        public override SKRect EvaluateBounds()
        {
            var size = EvaluateSize();
            return new SKRect(X, Y, X + size.Width, Y + size.Height);
        }

        public override object Clone()
        {
            var clone = (FlipDisplayItem)MemberwiseClone();
            clone.Guid = Guid.NewGuid();
            return clone;
        }

       
    }

    public partial class FlipDisplayItem
    {
        // Animator instance (per flip item)
        public FlipAnimator Animator { get; } = new FlipAnimator();

        // Remember last drawn value
        private int _lastValue = -1;

        [XmlIgnore]
        public int LatchedValue { get; set; } = -1;


        /// <summary>
        /// Call every frame with the current authoritative value
        /// </summary>
        public float GetFlipProgress(int currentValue)
        {
            // Seconds MUST NOT use animator
            if (TimeUnit == FlipTimeUnit.Second)
                return 0f;

            if (currentValue != _lastValue)
            {
                Animator.Trigger();
                _lastValue = currentValue;
            }

            return Animator.Update();
        }
        public override void SetProfile(Profile profile)
        {
            base.SetProfile(profile);
            OnPropertyChanged(nameof(CalculatedImageFolder));
        }

    }

}
