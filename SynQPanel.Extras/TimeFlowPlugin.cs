using SynQPanel.Plugins;
using System;

namespace SynQPanel.Extras
{
    /// <summary>
    /// TimeFlow Plugin
    /// Provides absolute, flow (normalized), and state-based time sensors
    /// for smooth clocks, animations, and visual transitions.
    /// </summary>
    public sealed class TimeFlowPlugin : BasePlugin
    {
        // -------------------------
        // Absolute (snap) sensors
        // -------------------------
        private readonly PluginSensor _year = new("Year", 0, "");
        private readonly PluginSensor _month = new("Month", 0, "");
        private readonly PluginSensor _day = new("Day", 0, "");

        private readonly PluginSensor _hour24 = new("Hour (24)", 0, "");
        private readonly PluginSensor _hour12 = new("Hour (12)", 0, "");
        private readonly PluginSensor _minute = new("Minute", 0, "");
        private readonly PluginSensor _second = new("Second", 0, "");
        private readonly PluginSensor _millisecond = new("Millisecond", 0, "");

        // -------------------------
        // Flow (continuous) sensors
        // -------------------------
        private readonly PluginSensor _secondPhase = new("Second Phase", 0, "");
        private readonly PluginSensor _minutePhase = new("Minute Phase", 0, "");
        private readonly PluginSensor _hour12Phase = new("Hour Phase (12h)", 0, "");
        private readonly PluginSensor _hour24Phase = new("Hour Phase (24h)", 0, "");
        private readonly PluginSensor _dayPhase = new("Day Phase", 0, "");

        // -------------------------
        // Absolute flow helper
        // -------------------------
        private readonly PluginSensor _secondsOfDay =
        new("Seconds Of Day", 0, null);


        // -------------------------
        // State / binary sensors
        // -------------------------
        private readonly PluginSensor _isAM = new("Is AM", 0, "");
        private readonly PluginSensor _isPM = new("Is PM", 0, "");
        private readonly PluginSensor _isNoon = new("Is Noon", 0, "");
        private readonly PluginSensor _isMidnight = new("Is Midnight", 0, "");


        public override string? ConfigFilePath => null;
        public override TimeSpan UpdateInterval => TimeSpan.Zero; // continuous sampling

        public TimeFlowPlugin()
            : base(
                "timeflow",
                "TimeFlow",
                "High-precision system time sensors with continuous flow phases for smooth clocks, animations, and transitions."
            )
        {
        }

        public override void Initialize()
        {
            // No initialization required
        }

        public override void Load(List<IPluginContainer> containers)
        {
            // -------- Absolute --------
            var absolute = new PluginContainer("TimeFlow · Absolute");
            absolute.Entries.AddRange(new[]
            {
                _year, _month, _day,
                _hour24, _hour12,
                _minute, _second, _millisecond,
                _secondsOfDay
            });

            // -------- Flow --------
            var flow = new PluginContainer("TimeFlow · Flow");
            flow.Entries.AddRange(new[]
            {
                _secondPhase,
                _minutePhase,
                _hour12Phase,
                _hour24Phase,
                _dayPhase
            });

            // -------- State --------
            var state = new PluginContainer("TimeFlow · State");
            state.Entries.AddRange(new[]
            {
                _isAM, _isPM, _isNoon, _isMidnight
            });

            containers.Add(absolute);
            containers.Add(flow);
            containers.Add(state);
        }

        public override void Update()
        {
            var now = DateTime.Now;

            // -------------------------
            // Absolute
            // -------------------------
            _year.Value = now.Year;
            _month.Value = now.Month;
            _day.Value = now.Day;

            _hour24.Value = now.Hour;
            _hour12.Value = now.Hour % 12 == 0 ? 12 : now.Hour % 12;
            _minute.Value = now.Minute;
            _second.Value = now.Second;
            _millisecond.Value = now.Millisecond;

            // -------------------------
            // Seconds of Day (absolute continuous)
            // -------------------------
            int secondsOfDayInt =
             now.Hour * 3600 +
             now.Minute * 60 +
             now.Second;

            // Clamp defensively (just in case)
            if (secondsOfDayInt < 0)
                secondsOfDayInt = 0;
            else if (secondsOfDayInt > 86399)
                secondsOfDayInt = 86399;

            _secondsOfDay.Value = secondsOfDayInt;

            // -------------------------
            // Flow phases (0..1)
            // -------------------------
            _secondPhase.Value = (float)(now.Millisecond / 1000.0);

            _minutePhase.Value = (float)(
              (now.Second + now.Millisecond / 1000.0) / 60.0);

            double hour12Fraction =
                (now.Hour % 12) +
                now.Minute / 60.0 +
                now.Second / 3600.0 +
                now.Millisecond / 3_600_000.0;

            double hour24Fraction =
                now.Hour +
                now.Minute / 60.0 +
                now.Second / 3600.0 +
                now.Millisecond / 3_600_000.0;

            _hour12Phase.Value = (float)(hour12Fraction / 12.0);
            _hour24Phase.Value = (float)(hour24Fraction / 24.0);
            _dayPhase.Value = (float)secondsOfDayInt / 86400f;


            // -------------------------
            // State sensors
            // -------------------------
            _isAM.Value = now.Hour < 12 ? 1f : 0f;
            _isPM.Value = now.Hour >= 12 ? 1f : 0f;

            // Exact instant (1 second only)
            _isNoon.Value = (now.Hour == 12 && now.Minute == 0 && now.Second == 0) ? 1f : 0f;

            // Midnight hour (00:00–00:59)
            _isMidnight.Value = now.Hour == 0 ? 1f : 0f;


        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            // Not used — synchronous sampling only
            return Task.CompletedTask;
        }

        public override void Close()
        {
            // Nothing to dispose
        }
    }
}
