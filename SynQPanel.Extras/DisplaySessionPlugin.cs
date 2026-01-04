using SynQPanel.Plugins;
using System;
using System.Runtime.InteropServices;

namespace SynQPanel.Extras
{
    /// <summary>
    /// Displays basic information about the current display session
    /// using Win32 APIs only (no WPF dependency).
    /// </summary>
    public sealed class DisplaySessionPlugin : BasePlugin
    {
        private readonly PluginContainer _container;

        private readonly PluginText _resolution;
        private readonly PluginText _refreshRate;
        private readonly PluginText _scaling;
        private readonly PluginText _sessionType;

        public override string? ConfigFilePath => null;

        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

        public DisplaySessionPlugin()
            : base(
                "display-session-addon",
                "Display Session",
                "Shows display resolution, scaling, refresh rate, and session type."
            )
        {
            _container = new PluginContainer("display-session", "Display Session");

            _resolution = new PluginText("resolution", "Resolution", "-");
            _refreshRate = new PluginText("refresh", "Refresh Rate", "-");
            _scaling = new PluginText("scaling", "Scaling", "-");
            _sessionType = new PluginText("session", "Session", "-");

            _container.Entries.Add(_resolution);
            _container.Entries.Add(_refreshRate);
            _container.Entries.Add(_scaling);
            _container.Entries.Add(_sessionType);
        }

        public override void Initialize() => UpdateSession();
        public override void Load(List<IPluginContainer> containers) => containers.Add(_container);
        public override void Update() => UpdateSession();
        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateSession();
            return Task.CompletedTask;
        }
        public override void Close() { }

        private void UpdateSession()
        {
            // Resolution
            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);
            _resolution.Value = $"{width} × {height}";

            // Refresh rate
            DEVMODE mode = new();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode) && mode.dmDisplayFrequency > 0)
            {
                _refreshRate.Value = $"{mode.dmDisplayFrequency} Hz";
            }
            else
            {
                _refreshRate.Value = "Unknown";
            }

            // DPI scaling
            try
            {
                uint dpi = GetDpiForSystem();
                _scaling.Value = $"{Math.Round(dpi / 96.0 * 100)} %";
            }
            catch
            {
                _scaling.Value = "Unknown";
            }

            // Session type
            _sessionType.Value =
                GetSystemMetrics(SM_REMOTESESSION) != 0 ? "Remote" : "Local";
        }

        #region Win32

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_REMOTESESSION = 0x1000;
        private const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(
            string? lpszDeviceName,
            int iModeNum,
            ref DEVMODE lpDevMode);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        #endregion
    }
}
