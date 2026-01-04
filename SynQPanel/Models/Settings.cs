using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace SynQPanel.Models
{
    public partial class Settings : ObservableObject
    {
        [ObservableProperty]
        private float _uiWidth = 1300;

        [ObservableProperty]
        private float _uiHeight = 900;

        [ObservableProperty]
        private float _uiScale = 1.0f;

        [ObservableProperty]
        private bool _isPaneOpen = true;

        [ObservableProperty]
        private bool _autoStart = false;

        [ObservableProperty]
        private int _autoStartDelay = 5;

        [ObservableProperty]
        private bool _startMinimized = false;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private string _selectedItemColor = "#FF00FF00";

        [ObservableProperty]
        private bool _showGridLines = true;

        [ObservableProperty]
        private float _gridLinesSpacing = 20;

        [ObservableProperty]
        private string _gridLinesColor = "#1A808080";

        
        
       
       

        [ObservableProperty]
        private bool _webServer = false;

        [ObservableProperty]
        private string _webServerListenIp = "127.0.0.1";

        [ObservableProperty]
        private int _webServerListenPort = 80;

        [ObservableProperty]
        private int _webServerRefreshRate = 66;

        [ObservableProperty]
        private int _targetFrameRate = 15;

        [ObservableProperty]
        private int _targetGraphUpdateRate = 1000;

        [ObservableProperty]
        private int _version = 114;


        
        public bool PreferAidaOnly { get; set; } = true;
        // For SensorMapping Logs
        private bool _verboseMapLogs = false; // make it true while developing/debugging
        public bool VerboseMapLogs
        {
            get => _verboseMapLogs;
            set
            {
                if (_verboseMapLogs == value) return;
                _verboseMapLogs = value;
                OnPropertyChanged(nameof(VerboseMapLogs));
            }
        }





    }

}
