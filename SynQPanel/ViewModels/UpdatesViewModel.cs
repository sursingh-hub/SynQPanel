using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Models;
using System.Collections.ObjectModel;
using System.Reflection;

namespace SynQPanel.ViewModels
{
    public class UpdatesViewModel : ObservableObject
    {
        public string Version { get; set; }

        public VersionModel? VersionModel { get; set; }

        private bool _updateCheckInProgress = false;

        public bool UpdateCheckInProgress
        {
            get { return _updateCheckInProgress; }
            set { SetProperty(ref _updateCheckInProgress, value); }
        }

        private bool _downloadInProgress = false;
        public bool DownloadInProgress
        {
            get { return _downloadInProgress; }
            set { SetProperty(ref _downloadInProgress, value); }
        }

        private double _downloadProgress = 0;

        public double DownloadProgress
        {
            get { return _downloadProgress; }
            set { SetProperty(ref _downloadProgress, value); }
        }

        private bool _updateAvailable = false;
        public bool UpdateAvailable
        {
            get { return _updateAvailable; }
            set { SetProperty(ref _updateAvailable, value); }
        }

        public ObservableCollection<UpdateVersion> UpdateVersions { get; } = [];

        public UpdatesViewModel()
        {
            Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);

            var version100 =
                new UpdateVersion
                {
                    Version = "v1.0.0",
                    Expanded = false,
                    Title = "Initial public release of SynQPanel",
                    Items =
                    [
                        new UpdateVersionItem
                {
                    Title = "Core Panel System",
                    Description =
                    [
                        "Introduces SynQPanel as a standalone panel visualization application.",
                        "Create clean, information-dense layouts using text, gauges, bars, tables, and images.",
                        "Designed for precision, stability, and long-term maintainability."
                    ]
                },

                new UpdateVersionItem
                {
                    Title = "Profile & Layout Management",
                    Description =
                    [
                        "Profile-based panel configurations with reliable save and reload behavior.",
                        "Pixel-precise positioning for all display items.",
                        "Safe background saving with no user workflow interruption."
                    ]
                },

                new UpdateVersionItem
                {
                    Title = "AIDA64 Integration",
                    Description =
                    [
                        "Reads hardware telemetry via AIDA64 Shared Memory.",
                        "Supports CPU, GPU, memory, temperatures, clocks, and utilization metrics.",
                        "AIDA64 is a registered trademark of FinalWire Ltd. SynQPanel is not affiliated with or endorsed by FinalWire Ltd."
                    ]
                },

                new UpdateVersionItem
                {
                    Title = "Import & Compatibility",
                    Description =
                    [
                        "Supports importing existing .sensorpanel configurations.",
                        "Includes controlled handling of .spzip and .sqx-based workflows.",
                        "Import features are provided for user convenience and are not officially supported by AIDA64."
                    ]
                },

                new UpdateVersionItem
                {
                    Title = "Add-ons Framework (Foundation)",
                    Description =
                    [
                        "Includes a minimal built-in add-ons framework.",
                        "Designed as a stable base for future extension without compromising core stability.",
                        "External add-on distribution will evolve in future releases."
                    ]
                },

                new UpdateVersionItem
                {
                    Title = "Stability & Reliability",
                    Description =
                    [
                        "Improved save logic to prevent re-entrancy and data corruption.",
                        "Consistent behavior across sensorpanel, spzip, and sqx workflows.",
                        "Extensive logging and diagnostics for safer troubleshooting."
                    ]
                }
                    ]
                };

            UpdateVersions.Add(version100);
        }

    }

    public class UpdateVersion()
    {
        public required string Version { get; set; }
        public required string Title { get; set; }
        public bool Expanded { get; set; } = false;
        public required ObservableCollection<UpdateVersionItem> Items { get; set; }
    }

    public class UpdateVersionItem()
    {
        public required string Title { get; set; }
        public required ObservableCollection<string> Description { get; set; }
    }

}
