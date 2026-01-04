using System;
using SynQPanel.Aida;



namespace SynQPanel.ViewModels.Components
{
    public class AidaSensorTreeItem : SensorTreeItem
    {
        private readonly AidaSensorItem _sensor;

        public uint? ParentId { get; set; } = null;
        public uint? ParentInstance { get; set; } = null;
        public uint? SensorId { get; set; } = null;



        public AidaSensorTreeItem(AidaSensorItem sensor) : base(sensor.Id, sensor.Label)
        {
            _sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));



            // Prefer explicit unit returned by the AIDA reader (if present).
            // If it isn't set, we fallback to parsing value to try and extract a unit.
            if (!string.IsNullOrWhiteSpace(sensor.Unit))
            {
                Value = sensor.Value ?? string.Empty;
                Unit = sensor.Unit;
            }
            else
            {
                // use existing parse logic (keeps your tuned behaviour)
                ParseAndApplyValue(sensor.Value);
            }
            if (sensor.Type.Equals("duty", StringComparison.OrdinalIgnoreCase))
            {
                Unit = "%";
            }



            //Type = sensor.Type ?? string.Empty;
            Type = NormalizeType(sensor.Type, sensor.Label);

            var key = Type.ToLowerInvariant();
            Icon = "pack://application:,,,/Resources/Images/AIDA/" + GetIconFileForKey(key, Name);
        }

        private static string GetIconFileForKey(string key, string label = null)
        {
            // Lower-case everything for safer matching
            key = key?.ToLowerInvariant() ?? "";
            label = label?.ToLowerInvariant() ?? "";

            switch (key)
            {
                // Temperatures
                case "temp":
                case "temperature":
                case "temperatures":
                    return "temperature.png";

                // Fans
                case "fan":
                case "gpufan":
                case "gpu fan":
                case "duty":
                case "cooling fans":
                case "fan speeds":
                    return "fan.png";

                // Voltages
                case "voltage":
                case "volt":
                case "voltages":
                    return "voltage.png";

                // Power
                case "power":
                case "pwr":
                    return "power-supply.png";

                // Current
                case "current":
                case "curr":
                    return "current.png";

                // GPU / CPU / RAM
                case "gpu":
                    return "gpu.png";
                case "cpu":
                    return "cpu.png";
                case "memory":
                case "ram":
                    return "ram.png";

                case "mainboard":
                    return "mainboard.png";

                case "battery":
                    return "battery.png";

                case "storage":
                    return "hdd.png";
                
                case "network":
                    return "nic.png";

                case "sys":
                case "system":
                    // label-based refinement (unchanged)
                    if (label.Contains("cpu")) return "cpu.png";
                    if (label.Contains("gpu")) return "gpu.png";
                    if (label.Contains("memory") || label.Contains("ram")) return "ram.png";
                   // if (label.Contains("disk") || label.Contains("drive") || label.Contains("ssd") || label.Contains("hdd") || label.Contains("storage")) return "hdd.png";
                    if (label.Contains("temperature") || label.Contains("temp")) return "temperature.png";
                    if (label.Contains("fan")) return "fan.png";
                    if (label.Contains("power")) return "power-supply.png";
                    if (label.Contains("voltage") || label.Contains("volt")) return "voltage.png";
                    if (label.Contains("battery")) return "battery.png";
                    if (label.Contains("mainboard") || label.Contains("motherboard")) return "mainboard.png";
                    if (label.Contains("nic") || label.Contains("network") || label.Contains("ip")) return "nic.png";
                    if (label.Contains("bios")) return "chip.png";
                    if (label.Contains("date") || label.Contains("time")) return "time.png";
                    if (label.Contains("user") || label.Contains("users")) return "user.png";
                    if (label.Contains("process")) return "chip.png";
                    if (label.Contains("clock")) return "clock.png";
                    return "computer.png";

                default:
                    return "empty.png";
            }

        }


        public string Type { get; private set; } = string.Empty;

        public override void Update()
        {
            // AidaHash should keep _sensor.Value (and optionally .Unit) updated.
            // Prefer sensor.Unit when available; else parse text again.
            if (!string.IsNullOrWhiteSpace(_sensor.Unit))
            {
                Value = _sensor.Value ?? string.Empty;
                Unit = _sensor.Unit;
            }
            else
            {
                ParseAndApplyValue(_sensor.Value);
            }
        }

        /// <summary>
        /// Conservative parser: if the value already contains a space-separated trailing token that looks non-numeric,
        /// treat that as the unit (e.g. "4300 MHz" -> Value="4300", Unit="MHz").
        /// Otherwise keep the whole value as Value and leave Unit empty.
        /// This preserves previous behaviour while still extracting obvious units from value strings.
        /// </summary>
        private void ParseAndApplyValue(string? raw)
        {
            var s = (raw ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(s))
            {
                Value = string.Empty;
                Unit = string.Empty;
                return;
            }

            // If AIDA emits something like "DDR5-4800" or "No Battery" (non-numeric), we keep full text as Value and Unit empty.
            // If s contains whitespace and the last token is clearly a unit (non-numeric), extract it.
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                var last = parts[parts.Length - 1];

                // Try numeric parse on last token to detect if it's numeric. If parse fails -> treat as unit.
                if (!double.TryParse(last, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    // Accept common short units: MHz, % , °C, V, W, RPM, GB, MB/s, kb/s etc.
                    // If it's a single or a few chars, very likely a unit. We'll apply it.
                    Unit = last;
                    Value = string.Join(" ", parts, 0, parts.Length - 1);
                    return;
                }
            }

            // If last token was numeric (or we couldn't infer unit), keep as-is.
            Value = s;
            Unit = string.Empty;
        }

        //------Helper------

        /*
        private static string NormalizeType(string rawType, string label)
        {
            var t = (rawType ?? string.Empty).ToLowerInvariant();
            var l = (label ?? string.Empty).ToLowerInvariant();

            // Fan duty / PWM → Cooling Fans
            if (t == "duty" || (t == "fan" && l.Contains("duty")))
                return "Fan Speeds";

            // Fan RPMs
            if (t == "fan")
                return "Cooling Fans";

            // Temperatures
            if (t == "temp" || t == "temperature")
                return "Temperatures";

            // Voltages
            if (t == "volt" || t == "voltage")
                return "Voltages";

            // Power
            if (t == "pwr" || t == "power")
                return "Power";

            return rawType; // fallback
        }
        */

        private static string NormalizeType(string rawType, string label)
        {
            var t = (rawType ?? string.Empty).ToLowerInvariant();
            var l = (label ?? string.Empty).ToLowerInvariant();

            // Fans / duty
            if (t == "fan" || t == "duty")
                return "Cooling Fans";

            // Temperatures
            if (t == "temp" || l.Contains("temperature"))
                return "Temperatures";

            // Voltages
            if (t == "volt" || l.Contains("voltage"))
                return "Voltages";

            // Power
            if (t == "pwr" || l.Contains("power") || l.Contains("tdp"))
                return "Power";

            // Current
            if (t == "curr")
                return "Current";

            // --- SYSTEM SPLIT ---
            if (t == "sys" || t == "system")
            {
                if (l.Contains("cpu")) return "CPU";
                if (l.Contains("gpu")) return "GPU";
                if (l.Contains("memory") || l.Contains("ram")) return "Memory";
                if (l.Contains("disk") || l.Contains("drive") || l.Contains("ssd") || l.Contains("hdd")) return "Storage";
                if (l.Contains("network") || l.Contains("nic") || l.Contains("ethernet")) return "Network";
                if (l.Contains("battery")) return "Battery";
                if (l.Contains("fan")) return "Cooling Fans";
                if (l.Contains("voltage")) return "Voltages";
                if (l.Contains("power") || l.Contains("tdp")) return "Power";
                if (l.Contains("temp")) return "Temperatures";

                return "System";
            }

            // CPU / GPU explicit
            if (t == "cpu") return "CPU";
            if (t == "gpu") return "GPU";

            // Memory
            if (t == "memory" || t == "ram")
                return "Memory";

            // Storage
            if (t == "disk")
                return "Storage";

            // Explicit acronyms
            if (rawType.Equals("cpu", StringComparison.OrdinalIgnoreCase))
                return "CPU";

            if (rawType.Equals("gpu", StringComparison.OrdinalIgnoreCase))
                return "GPU";


            // Fallback — prettify raw token
            return char.ToUpper(rawType[0]) + rawType.Substring(1).ToLowerInvariant();
        }

        








    }
}


