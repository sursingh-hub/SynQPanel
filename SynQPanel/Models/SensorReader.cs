using SynQPanel.Monitors;
using SynQPanel.Plugins;
using System;
using System.Globalization;
using System.Linq;

namespace SynQPanel.Models
{
    internal class SensorReader
    {
        private static bool _hwinfoLogged = false;


        /// <summary>
       
        /// Kept as an [Obsolete] silent stub to surface any remaining call sites as compiler warnings.
        /// Change the Obsolete attribute's second parameter to 'true' to make leftover calls compile-time errors.
        /// </summary>
        [Obsolete("LibreHardwareMonitor has been removed. Use PluginSensor/AIDA lookups instead.", false)]
        public static SensorReading? ReadLibreSensor(string sensorId)
        {
            // Best-effort one-time debug hint to make runtime traces easier to find if still invoked
            try
            {
                System.Diagnostics.Debug.WriteLine("ReadLibreSensor called: LibreHardwareMonitor is disabled in this build.");
            }
            catch { /* swallow - do not throw in cleanup stubs */ }

            return null;
        }


        //
        // 3) PLUGIN + AIDA SENSOR LOOKUP (kept intact)
        //
        public static SensorReading? ReadPluginSensor(string sensorId)
        {
            if (string.IsNullOrEmpty(sensorId))
                return null;


               // System.Diagnostics.Debug.WriteLine($"Sensor lookup: '{sensorId}'");

            // --- Try PluginMonitor first ---
            if (PluginSensors.TryGet(sensorId, out PluginMonitor.PluginReading reading))
            {
                if (reading.Data is IPluginSensor ps)
                {
                    return new SensorReading(
                        ps.ValueMin,
                        ps.ValueMax,
                        ps.ValueAvg,
                        ps.Value,
                        ps.Unit ?? ""
                    );
                }
                else if (reading.Data is IPluginText pt)
                {
                    return new SensorReading(pt.Value);
                }
                else if (reading.Data is IPluginTable tbl)
                {
                    return new SensorReading(tbl.Value, tbl.DefaultFormat, tbl.ToString());
                }
            }

            // --- Fallback: AIDA string-based lookup ---
            try
            {
                var aida = new SynQPanel.Aida.AidaHash();
                var sensors = aida.RefreshSensorData(); // returns List<AidaSensorItem>

                var match = sensors.FirstOrDefault(
                    s => string.Equals(s.Id, sensorId, StringComparison.OrdinalIgnoreCase)
                );

                if (match != null)
                {

                    // ---------------------------------------------
                    // Special handling: STIME used as numeric gauge
                    // ---------------------------------------------
                    if (string.Equals(sensorId, "STIME", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // Prefer discrete time sensors (always numeric)
                            var sensorsAll = sensors;

                            double hour = 0;
                            double min = 0;
                            double sec = 0;

                            var h = sensorsAll.FirstOrDefault(s => s.Id == "SHOUR24");
                            var m = sensorsAll.FirstOrDefault(s => s.Id == "SMIN");
                            var s = sensorsAll.FirstOrDefault(s => s.Id == "SSEC");

                            if (h != null && double.TryParse(h.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var hv))
                                hour = hv;

                            if (m != null && double.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var mv))
                                min = mv;

                            if (s != null && double.TryParse(s.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var sv))
                                sec = sv;

                            double secondsSinceMidnight = hour * 3600 + min * 60 + sec;

                            return new SensorReading(
                                0,
                                86399,
                                secondsSinceMidnight,
                                secondsSinceMidnight,
                                "s"
                            );
                        }
                        catch
                        {
                            // fallback to string if anything goes wrong
                            return new SensorReading(match.Value);
                        }
                    }

                    // Try numeric parsing
                    if (double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numeric))
                    {
                        string unit = match.Type?.ToLower() switch
                        {
                            "temp" => "°C",
                            "volt" => "V",
                            "fan" => "RPM",
                            "pwr" => "W",
                            "curr" => "A",
                            "gpu fan" => "RPM",
                            _ => ""
                        };

                        return new SensorReading(0, 0, 0, numeric, unit);
                    }

                    // String-only sensors
                    return new SensorReading(match.Value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadPluginSensor: AIDA lookup exception: " + ex);
            }

            return null;
        }
    }
}
