using SynQPanel.Plugins;
using System;
using System.Diagnostics;

namespace SynQPanel.Extras
{
    /// <summary>
    /// Lightweight system heartbeat add-on for SynQPanel.
    /// Shows basic runtime state without heavy system probing.
    /// </summary>
    public sealed class SystemPulsePlugin : BasePlugin
    {
        private readonly PluginContainer _container;

        private readonly PluginText _uptime;
        private readonly PluginSensor _processCount;
        private readonly PluginSensor _threadCount;
        private readonly PluginSensor _handleCount;

        public override string? ConfigFilePath => null;

        // Slow, safe update cadence
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(5);

        public SystemPulsePlugin()
            : base(
                "system-pulse-addon",
                "System Pulse",
                "A lightweight snapshot of system runtime activity."
            )
        {
            _container = new PluginContainer("system-pulse", "System Pulse");

            _uptime = new PluginText("uptime", "Uptime", "-");
            _processCount = new PluginSensor("processes", "Processes", 0);
            _threadCount = new PluginSensor("threads", "Threads", 0);
            _handleCount = new PluginSensor("handles", "Handles", 0);

            _container.Entries.Add(_uptime);
            _container.Entries.Add(_processCount);
            _container.Entries.Add(_threadCount);
            _container.Entries.Add(_handleCount);
        }

        public override void Initialize()
        {
            UpdatePulse();
        }

        public override void Load(List<IPluginContainer> containers)
        {
            containers.Add(_container);
        }

        public override void Update()
        {
            UpdatePulse();
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            UpdatePulse();
            return Task.CompletedTask;
        }

        public override void Close()
        {
            // No unmanaged resources
        }

        private void UpdatePulse()
        {
            // Uptime
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            _uptime.Value =
                $"{uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";

            // Process snapshot
            var processes = Process.GetProcesses();
            _processCount.Value = processes.Length;

            int threads = 0;
            int handles = 0;

            foreach (var process in processes)
            {
                try
                {
                    threads += process.Threads.Count;
                    handles += process.HandleCount;
                }
                catch
                {
                    // Some system processes deny access — ignore safely
                }
            }

            _threadCount.Value = threads;
            _handleCount.Value = handles;
        }
    }
}
