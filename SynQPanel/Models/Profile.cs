using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Xml.Serialization;


namespace SynQPanel.Models
{
    public class TargetWindow
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public string Name
        {
            get
            {
                return string.Format("{0}x{1} {2},{3}", Width, Height, X, Y);
            }
        }

        public string? DeviceName { get; set; }

        public TargetWindow() { }

        public TargetWindow(int x, int y, int width, int height, string? deviceName)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            DeviceName = deviceName;
        }
    }

    public partial class Profile : ObservableObject, ICloneable
    {
        private Guid _guid = Guid.NewGuid();
        public Guid Guid
        {
            get { return _guid; }
            set
            {
                SetProperty(ref _guid, value);
            }
        }

        private string _name = "Profile";

        [XmlIgnore]
        public SKBitmap? PreviewBitmap;

        [XmlIgnore]
        [ObservableProperty]
        private bool _isSelected;

        // For Deletion
        [XmlIgnore]
        public List<int> DeletedSensorPanelLineIndices { get; } = new();


        public string Name
        {
            get { return _name; }
            set
            {
                SetProperty(ref _name, value);
            }
        }

        public string DisplayName
        {
            get
            {
                // 1️ Prefer original SPZIP name (user-facing)
                if (!string.IsNullOrWhiteSpace(ImportedSensorPackagePath))
                    return Path.GetFileNameWithoutExtension(ImportedSensorPackagePath);

                // 2️ Fallback to imported SP2 name
                if (!string.IsNullOrWhiteSpace(ImportedSensorPanelPath))
                    return Path.GetFileNameWithoutExtension(ImportedSensorPanelPath);

                // 3️ Final fallback: existing profile name
                return Name;
            }
        }


        private int _width = 600;

        public int Width
        {
            get { return _width; }
            set
            {
                if (value <= 0)
                {
                    return;
                }
                SetProperty(ref _width, value);
            }
        }

        private int _height = 400;
        public int Height
        {
            get { return _height; }
            set
            {
                if (value <= 0)
                {
                    return;
                }
                SetProperty(ref _height, value);
            }
        }

        [ObservableProperty]
        private bool _showFps = false;

        private bool _drag = true;
        public bool Drag
        {
            get { return _drag; }
            set
            {
                SetProperty(ref _drag, value);
            }
        }

        private string _backgroundColor = "#FFFFFFFF";
        public string BackgroundColor
        {
            get { return _backgroundColor; }
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

                SetProperty(ref _backgroundColor, value);
            }
        }

        private bool _active = true;
        public bool Active
        {
            get { return _active; }
            set
            {
                SetProperty(ref _active, value);
            }
        }

        [ObservableProperty]
        private bool _openGL = false;

        [ObservableProperty]
        private float _fontScale = 1.33f;

        private bool _topmost = false;
        public bool Topmost
        {
            get { return _topmost; }
            set { SetProperty(ref _topmost, value); }

        }

        private string _font = Fonts.SystemFontFamilies.First().ToString();

        public string Font
        {
            get { return _font; }
            set
            {
                if (value != null)
                {
                    SetProperty(ref _font, value);
                }
            }
        }

        private int _fontSize = 26;
        public int FontSize
        {
            get { return _fontSize; }
            set
            {
                if (value <= 0)
                {
                    return;
                }
                SetProperty(ref _fontSize, value);
            }
        }

        private string _color = "#FF000000";
        public string Color
        {
            get { return _color; }
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

                SetProperty(ref _color, value);
            }
        }

        // ADD HERE:
        public string? BackgroundImagePath { get; set; }   // <----- Add this line

        [XmlIgnore]
        public string BackgroundImageFileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BackgroundImagePath))
                    return string.Empty;

                return Path.GetFileName(BackgroundImagePath);
            }
        }




        public Profile() { }

        public Profile(string name, int width, int height)
        {
            _name = name;
            _width = width;
            _height = height;
        }

        private int _windowX = 0;
        public int WindowX
        {
            get { return _windowX; }
            set
            {
                SetProperty(ref _windowX, value);
            }
        }

        private int _windowY = 0;

        public int WindowY
        {
            get { return _windowY; }
            set
            {
                SetProperty(ref _windowY, value);
            }
        }

        private TargetWindow? _targetWindow;

        public TargetWindow? TargetWindow
        {
            get { return _targetWindow; }
            set
            {
                SetProperty(ref _targetWindow, value);
            }
        }

        private bool _resize = true;
        public bool Resize
        {
            get { return _resize; }
            set
            {
                SetProperty(ref _resize, value);
            }
        }

        private bool _strictWindowMatching = false;

        public bool StrictWindowMatching
        {
            get { return _strictWindowMatching; }
            set { SetProperty(ref _strictWindowMatching, value); }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// If this profile was imported from a .sensorpanel/.sp2 file, this stores the original file path.
        /// Used to enable round-trip saving back to the same .sensorpanel file.
        /// </summary>
        public string ImportedSensorPanelPath { get; set; }
        public string? ImportedSensorPackagePath { get; set; } // path to original .spzip or .sqx (if imported from a package)

        





    }
}
