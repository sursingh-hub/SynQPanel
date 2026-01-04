using SynQPanel.Plugins;
using System.Net.NetworkInformation;

namespace SynQPanel.Extras
{
    /// <summary>
    /// Minimal example add-on demonstrating Bluetooth adapter status in SynQPanel.
    /// Uses NetworkInterface only for maximum stability.
    /// </summary>
    public sealed class BluetoothStatusPlugin : BasePlugin
    {
        private readonly PluginContainer _container;

        private readonly PluginText _available;
        private readonly PluginText _name;
        private readonly PluginText _description;
        private readonly PluginText _status;
        private readonly PluginText _enabled;
        private readonly PluginText _discoverableName;


        public override string? ConfigFilePath => null;

        // Bluetooth state does not change frequently – keep it lightweight
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(30);

        public BluetoothStatusPlugin()
            : base(
                "bluetooth-addon",
                "Bluetooth Add-on",
                "Example add-on showing Bluetooth adapter availability and status."
            )
        {
            _container = new PluginContainer("bluetooth", "Bluetooth Adapter");

            _available = new PluginText("available", "Available", "No");
            _name = new PluginText("name", "Name", "-");
            _description = new PluginText("description", "Description", "-");
            _status = new PluginText("status", "Status", "-");

            _enabled = new PluginText("enabled", "Enabled", "No");
            _discoverableName = new PluginText("discoverable", "Discoverable As", "-");

            


            _container.Entries.Add(_available);
            _container.Entries.Add(_name);
            _container.Entries.Add(_description);
            _container.Entries.Add(_status);
            _container.Entries.Add(_enabled);
            _container.Entries.Add(_discoverableName);
        }

        public override void Initialize()
        {
            UpdateBluetoothState();
            Serilog.Log.Information("BluetoothStatusPlugin.Initialize() called");

        }

        public override void Load(List<IPluginContainer> containers)
        {
            containers.Add(_container);
        }

        public override void Update()
        {
            UpdateBluetoothState();
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateBluetoothState();
            return Task.CompletedTask;
        }

        public override void Close()
        {
            // No unmanaged resources
        }

        private void UpdateBluetoothState()
        {
            var bluetoothInterface = NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(ni =>
                    ni.Description.Contains("bluetooth", StringComparison.OrdinalIgnoreCase) ||
                    ni.Name.Contains("bluetooth", StringComparison.OrdinalIgnoreCase));

            if (bluetoothInterface == null)
            {
                _available.Value = "No";
                _enabled.Value = "No";
                _discoverableName.Value = "-";
                _name.Value = "-";
                _description.Value = "-";
                _status.Value = "Not detected";
                return;
            }

            _available.Value = "Yes";

            // Enabled = adapter exists (NetworkInterface-only rule)
            bool enabled = bluetoothInterface.OperationalStatus != OperationalStatus.NotPresent;
            _enabled.Value = enabled ? "Yes" : "No";

            // Windows Bluetooth "Discoverable as" name
            _discoverableName.Value = Environment.MachineName;

            _name.Value = bluetoothInterface.Name;
            _description.Value = bluetoothInterface.Description;

            // Keep raw status for transparency
            _status.Value = bluetoothInterface.OperationalStatus.ToString();
        }


    }
}
