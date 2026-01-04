using SynQPanel.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SynQPanel
{
    public static class AidaBridge
    {
        // Convert an AIDA sensor id string into a SensorReading used by the UI
        public static SensorReading? GetAidaSensorReading(string aidaId)
        {
            if (string.IsNullOrEmpty(aidaId)) return null;

            try
            {
                // Construct the AidaHash reader using the namespace that contains it.
                // Use fully-qualified name to avoid "not found" errors.
                var aida = new SynQPanel.Aida.AidaHash();
                var sensors = aida.RefreshSensorData(); // expected to return IEnumerable<AidaSensorItem>

                // Find sensor by id (AidaSensorItem.Id is stored as an object/string in your tree items)
                var sensor = sensors.FirstOrDefault(s => string.Equals(s.Id?.ToString(), aidaId, StringComparison.OrdinalIgnoreCase));
                if (sensor == null) return null;

                var rawValue = sensor.Value ?? string.Empty;
                string valueText = rawValue;
                string unit = string.Empty;
                double numeric = 0.0;

                // Attempt to parse "1234 MHz" into numeric + unit
                var m = Regex.Match(rawValue.Trim(), @"^\s*([+\-]?[0-9\.,]+)\s*(.*)$");
                if (m.Success)
                {
                    var numStr = m.Groups[1].Value;
                    // normalize decimal separator
                    numStr = numStr.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
                    if (double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    {
                        numeric = parsed;
                    }
                    unit = m.Groups[2].Value?.Trim() ?? string.Empty;
                }

                // Build SensorReading
                var reading = new SensorReading
                {
                    ValueNow = numeric,
                    ValueText = valueText,
                    Unit = string.IsNullOrEmpty(unit) ? (sensor.Type ?? string.Empty) : unit
                };

                return reading;
            }
            catch
            {
                return null;
            }
        }
    }
}
