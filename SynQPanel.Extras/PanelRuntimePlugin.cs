using SynQPanel.Plugins;
using System;
using System.Diagnostics;

namespace SynQPanel.Extras
{
    /// <summary>
    /// Displays runtime information about the SynQPanel process itself.
    /// </summary>
    public sealed class PanelRuntimePlugin : BasePlugin
    {
        private readonly PluginContainer _container;

        private readonly PluginText _uptime;
        private readonly PluginText _processId;
        private readonly PluginText _threadCount;
        private readonly PluginText _memoryUsage;
        private readonly PluginText _processState;

        private PerformanceCounter? _privateWorkingSet;
        private readonly Stopwatch _uptimeWatch = Stopwatch.StartNew();

        public override string? ConfigFilePath => null;
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

        public PanelRuntimePlugin()
            : base(
                "panel-runtime-addon",
                "Panel Runtime",
                "Shows runtime information about the SynQPanel process."
            )
        {
            _container = new PluginContainer("panel-runtime", "Panel Runtime", true);


            _uptime = new PluginText("uptime", "Uptime", "-");
            _processId = new PluginText("pid", "Process ID", "-");
            _threadCount = new PluginText("threads", "Thread Count", "-");
            _memoryUsage = new PluginText("memory", "App Memory Usage", "-");
            _processState = new PluginText("state", "Process State", "-");

            _container.Entries.Add(_uptime);
            _container.Entries.Add(_processId);
            _container.Entries.Add(_threadCount);
            _container.Entries.Add(_memoryUsage);
            _container.Entries.Add(_processState);
        }

        public override void Initialize()
        {
            try
            {
                _privateWorkingSet = new PerformanceCounter(
                    "Process",
                    "Working Set - Private",
                    Process.GetCurrentProcess().ProcessName,
                    true
                );
            }
            catch
            {
                _privateWorkingSet = null;
            }

            UpdateRuntime();
        }

        public override void Load(List<IPluginContainer> containers)
            => containers.Add(_container);

        public override void Update()
            => UpdateRuntime();

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            UpdateRuntime();
            return Task.CompletedTask;
        }

        public override void Close()
        {
            _privateWorkingSet?.Dispose();
            _privateWorkingSet = null;
        }

        private void UpdateRuntime()
        {
            var process = Process.GetCurrentProcess();

            // Uptime
            var uptime = _uptimeWatch.Elapsed;
            _uptime.Value =
                $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            // Process info
            _processId.Value = process.Id.ToString();
            _threadCount.Value = process.Threads.Count.ToString();

            // Memory (matches Task Manager)
            try
            {
                if (_privateWorkingSet != null)
                {
                    float value = _privateWorkingSet.NextValue();
                    _memoryUsage.Value = $"{(int)(value / 1024 / 1024)} MB";
                }
                else
                {
                    _memoryUsage.Value = "N/A";
                }
            }
            catch
            {
                _memoryUsage.Value = "N/A";
            }

            _processState.Value = process.Responding
                ? "Running"
                : "Not Responding";
        }
    }
}
