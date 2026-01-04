using SynQPanel.Monitors;
using LibreHardwareMonitor.Hardware; // kept for HardwareType enum used in SensorPanel2 mapping
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SynQPanel.Utils
{
    internal class SensorMapping
    {
        public static readonly Dictionary<string, (SensorType, HardwareType)> SensorPanel2 = new()
        {
            { "FCPU", (SensorType.Fan, HardwareType.Motherboard) },
            { "TCPU", (SensorType.Temperature, HardwareType.Cpu) },
            { "TGPU1GPU2", (SensorType.Temperature, HardwareType.GpuNvidia) },
            { "PCPUPKG", (SensorType.Power, HardwareType.Cpu) },
            { "VCPU", (SensorType.Voltage, HardwareType.Cpu) },
            { "SCPUCLK", (SensorType.Clock, HardwareType.Cpu) },
            { "TCPUDIO", (SensorType.Temperature, HardwareType.Cpu) },
            { "FGPU1", (SensorType.Fan, HardwareType.GpuNvidia) },
            { "SGPU1CLK", (SensorType.Clock, HardwareType.GpuNvidia) },
            { "SCPUUTI",(SensorType.Load, HardwareType.Cpu) },
            { "SGPU1UTI", (SensorType.Load, HardwareType.GpuNvidia) },
            { "SGPU1USEDDEMEM", (SensorType.SmallData, HardwareType.GpuNvidia)},
            { "TGPU1", (SensorType.Temperature, HardwareType.GpuNvidia) },
            { "PGPU1", (SensorType.Power, HardwareType.GpuNvidia)},
            { "SMEMUTI", (SensorType.Load, HardwareType.Memory) },
            { "SUSEDMEM", (SensorType.Data, HardwareType.Memory) },
            { "SFREEMEM", (SensorType.Data, HardwareType.Memory) },
            { "SVMEMUSAGE", (SensorType.Load, HardwareType.GpuNvidia) },
            { "TVRM", (SensorType.Temperature, HardwareType.Motherboard) },
            { "TDIMMTS2",  (SensorType.Temperature, HardwareType.Memory) }
        };

        private static bool _aidaPrefLoggedOnce = false;

        // AIDA-only matching: returns AIDA sensor Id (or null)
        public static string? FindMatchingIdentifier(string sensorPanelKey)
        {
            if (string.IsNullOrWhiteSpace(sensorPanelKey))
            {
                MapLogger.Log("[MAP-MATCH] Empty sensorPanelKey requested.");
                return null;
            }

            var key = sensorPanelKey.Trim();
            var normKey = key.ToUpperInvariant();
            var normKeyAlpha = new string(normKey.Where(c => !char.IsDigit(c)).ToArray()).Trim();

            MapLogger.Log($"[MAP-MATCH] Finding AIDA-only match for '{key}'");

            // 1) desired types from explicit map or heuristic
            SensorType? desiredSensorType = null;
            HardwareType? desiredHardwareType = null;
            if (SensorPanel2.TryGetValue(key, out (SensorType sensorType, HardwareType hardwareType) explicitMap))
            {
                desiredSensorType = explicitMap.sensorType;
                desiredHardwareType = explicitMap.hardwareType;
                MapLogger.Log($"[MAP-MATCH] Using explicit SensorPanel2 map -> SensorType={desiredSensorType}, HardwareType={desiredHardwareType}");
            }
            else
            {
                // heuristic by first char + suffix rules
                switch (key.FirstOrDefault())
                {
                    case 'T': desiredSensorType = SensorType.Temperature; break;
                    case 'F': desiredSensorType = SensorType.Fan; break;
                    case 'P': desiredSensorType = SensorType.Power; break;
                    case 'V': desiredSensorType = SensorType.Voltage; break;
                    case 'S': desiredSensorType = SensorType.Load; break;
                }
                if (key.EndsWith("CLK", StringComparison.OrdinalIgnoreCase)) desiredSensorType = SensorType.Clock;
                if (key.EndsWith("UTI", StringComparison.OrdinalIgnoreCase)) desiredSensorType = SensorType.Load;
                if (key.EndsWith("SPEED", StringComparison.OrdinalIgnoreCase)) desiredSensorType = SensorType.Frequency;
                if (key.EndsWith("MUL", StringComparison.OrdinalIgnoreCase)) desiredSensorType = SensorType.Factor;
                if (key.EndsWith("RATE", StringComparison.OrdinalIgnoreCase)) desiredSensorType = SensorType.Throughput;

                var tail = key.Length > 1 ? key[1..].ToUpperInvariant() : string.Empty;
                if (tail.StartsWith("CPU")) desiredHardwareType = HardwareType.Cpu;
                else if (tail.StartsWith("MOBO")) desiredHardwareType = HardwareType.Motherboard;
                else if (tail.StartsWith("GPU")) desiredHardwareType = HardwareType.GpuNvidia; // will accept AMD heuristically elsewhere
                else if (tail.StartsWith("MEM") || tail.StartsWith("DIMM")) desiredHardwareType = HardwareType.Memory;
                else if (tail.StartsWith("HDD")) desiredHardwareType = HardwareType.Storage;
                else if (tail.StartsWith("NIC")) desiredHardwareType = HardwareType.Network;

                MapLogger.Log($"[MAP-MATCH] Heuristic -> SensorType={desiredSensorType}, HardwareType={desiredHardwareType}");
            }

            // Read AIDA sensors
            var aidaList = AidaMonitor.GetOrderedList()?.ToList() ?? new List<AidaSensorWrapper>();
            MapLogger.Log($"[MAP-MATCH] AIDA candidates count={aidaList.Count}");

            // Helper logging (small)
            void LogIf(string message)
            {
                if (ConfigModel.Instance?.Settings?.VerboseMapLogs == true)
                    MapLogger.Log("[MAP-MATCH] " + message);
            }

            // 1) Exact id match (case-insensitive)
            var exactId = aidaList.FirstOrDefault(a => !string.IsNullOrEmpty(a.Id) && string.Equals(a.Id, key, StringComparison.OrdinalIgnoreCase));
            if (exactId != null)
            {
                LogIf($"AIDA exact id -> {exactId.Id} / {exactId.Label}");
                return exactId.Id;
            }

            // 2) Exact label match
            var exactLabel = aidaList.FirstOrDefault(a => !string.IsNullOrEmpty(a.Label) && string.Equals(a.Label.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exactLabel != null)
            {
                LogIf($"AIDA exact label -> {exactLabel.Id} / {exactLabel.Label}");
                return exactLabel.Id;
            }

            // 3) Contains / substring matches (id or label)
            var contains = aidaList.FirstOrDefault(a =>
                (!string.IsNullOrEmpty(a.Label) && a.Label.ToUpperInvariant().Contains(normKey)) ||
                (!string.IsNullOrEmpty(a.Id) && a.Id.ToUpperInvariant().Contains(normKey)));
            if (contains != null)
            {
                LogIf($"AIDA contains match -> {contains.Id} / {contains.Label}");
                return contains.Id;
            }

            // 4) Token matching: split the key into tokens and try to match
            var tokens = normKey.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0)
            {
                foreach (var t in tokens)
                {
                    var tokenMatch = aidaList.FirstOrDefault(a =>
                        (!string.IsNullOrEmpty(a.Label) && a.Label.ToUpperInvariant().Contains(t)) ||
                        (!string.IsNullOrEmpty(a.Id) && a.Id.ToUpperInvariant().Contains(t)));
                    if (tokenMatch != null)
                    {
                        LogIf($"AIDA token match '{t}' -> {tokenMatch.Id} / {tokenMatch.Label}");
                        return tokenMatch.Id;
                    }
                }
            }

            // 5) Core / numeric-index heuristic
            var digits = new string(key.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits))
            {
                foreach (var cand in aidaList)
                {
                    var candId = (cand.Id ?? "").ToUpperInvariant();
                    var candLab = (cand.Label ?? "").ToUpperInvariant();
                    if (!string.IsNullOrEmpty(candId) && candId.Contains(digits) &&
                        (candId.Contains("CPU") || candId.Contains("CORE") || candId.Contains("SCC") || candId.Contains("GPU")))
                    {
                        LogIf($"AIDA core-index heuristic by Id -> {cand.Id} / {cand.Label}");
                        return cand.Id;
                    }
                    if (!string.IsNullOrEmpty(candLab) && candLab.Contains(digits) &&
                        (candLab.Contains("CPU") || candLab.Contains("CORE") || candLab.Contains("GPU")))
                    {
                        LogIf($"AIDA core-index heuristic by Name -> {cand.Id} / {cand.Label}");
                        return cand.Id;
                    }
                }
            }

            // 6) If desired type/hardware known, prefer candidates that mention them in label or id
            if (desiredSensorType.HasValue || desiredHardwareType.HasValue)
            {
                var prefer = aidaList.Where(a =>
                {
                    var lab = (a.Label ?? "").ToUpperInvariant();
                    var id = (a.Id ?? "").ToUpperInvariant();
                    bool matchesType = false;
                    bool matchesHw = false;

                    if (desiredSensorType.HasValue)
                    {
                        switch (desiredSensorType.Value)
                        {
                            case SensorType.Temperature: matchesType = lab.Contains("TEMP") || id.Contains("T"); break;
                            case SensorType.Fan: matchesType = lab.Contains("FAN") || id.Contains("F"); break;
                            case SensorType.Load: matchesType = lab.Contains("UTIL") || lab.Contains("USAGE") || id.Contains("UTI"); break;
                            case SensorType.Power: matchesType = lab.Contains("POWER") || lab.Contains("WATT") || id.Contains("P"); break;
                            case SensorType.Voltage: matchesType = lab.Contains("VOLT") || id.StartsWith("V"); break;
                            case SensorType.Clock: matchesType = lab.Contains("CLOCK") || lab.Contains("CLK") || id.Contains("CLK"); break;
                        }
                    }

                    if (desiredHardwareType.HasValue)
                    {
                        switch (desiredHardwareType.Value)
                        {
                            case HardwareType.Cpu: matchesHw = lab.Contains("CPU") || id.Contains("CPU"); break;
                            case HardwareType.GpuNvidia: matchesHw = lab.Contains("GPU") || id.Contains("GPU"); break;
                            case HardwareType.Memory: matchesHw = lab.Contains("MEM") || id.Contains("MEM") || lab.Contains("DIMM"); break;
                            case HardwareType.Storage: matchesHw = lab.Contains("DRIVE") || lab.Contains("HDD") || id.Contains("HDD") || lab.Contains("DISK"); break;
                            case HardwareType.Motherboard: matchesHw = lab.Contains("MOBO") || lab.Contains("MOTHERBOARD") || lab.Contains("CHIPSET"); break;
                            case HardwareType.Network: matchesHw = lab.Contains("NIC") || lab.Contains("LAN") || lab.Contains("NETWORK"); break;
                        }
                    }

                    return (desiredSensorType.HasValue ? matchesType : true) && (desiredHardwareType.HasValue ? matchesHw : true);
                }).ToList();

                if (prefer.Count == 1)
                {
                    LogIf($"AIDA single preferred candidate -> {prefer[0].Id} / {prefer[0].Label}");
                    return prefer[0].Id;
                }
                if (prefer.Count > 1)
                {
                    var prefAlpha = prefer.FirstOrDefault(p => (p.Label ?? "").ToUpperInvariant().Contains(normKeyAlpha) || (p.Id ?? "").ToUpperInvariant().Contains(normKeyAlpha));
                    if (prefAlpha != null)
                    {
                        LogIf($"AIDA preferred filtered by alpha part -> {prefAlpha.Id} / {prefAlpha.Label}");
                        return prefAlpha.Id;
                    }
                }
            }

            // 7) Final fallback: return first AIDA candidate whose id or label at least contains the first alpha-token
            if (!string.IsNullOrEmpty(normKeyAlpha))
            {
                var fallback = aidaList.FirstOrDefault(a => (!string.IsNullOrEmpty(a.Label) && a.Label.ToUpperInvariant().Contains(normKeyAlpha)) || (!string.IsNullOrEmpty(a.Id) && a.Id.ToUpperInvariant().Contains(normKeyAlpha)));
                if (fallback != null)
                {
                    LogIf($"AIDA fallback by alpha -> {fallback.Id} / {fallback.Label}");
                    return fallback.Id;
                }
            }

            MapLogger.Log($"[MAP-MATCH] AIDA: no match found for '{key}'.");
            return null;
        }

        // Build plugin index (AIDA-only). Libre enumeration removed to avoid runtime dependency.
        private static List<PluginSensorDescriptor> BuildPluginSensorIndex()
        {
            var list = new List<PluginSensorDescriptor>();

            try
            {
                // 1) AIDA sensors (wrap using your AidaMonitor)
                try
                {
                    var aidaList = AidaMonitor.GetOrderedList()?.ToList() ?? new List<AidaSensorWrapper>();

                    foreach (var a in aidaList)
                    {
                        var id = (a.Id ?? "").Trim();
                        var label = (a.Label ?? a.Name ?? "").Trim();

                        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(label))
                            continue;

                        list.Add(new PluginSensorDescriptor
                        {
                            Id = id,
                            FriendlyName = label,
                            X = null, // AIDA doesn't provide coordinates here
                            Y = null,
                            IsHwBacked = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    MapLogger.Log("[MAP-BUILD] AIDA index build failed: " + ex);
                }

                // Deduplicate by Id / FriendlyName (AIDA-first)
                var dedup = new Dictionary<string, PluginSensorDescriptor>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in list)
                {
                    var key = (p.Id ?? "").Trim();
                    if (string.IsNullOrEmpty(key)) key = (p.FriendlyName ?? "").Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    if (!dedup.ContainsKey(key)) dedup[key] = p;
                }

                return dedup.Values.ToList();
            }
            catch (Exception ex)
            {
                MapLogger.Log("[MAP-BUILD] BuildPluginSensorIndex fatal: " + ex);
                return new List<PluginSensorDescriptor>();
            }
        }


        // Lightweight representation of an available AIDA/plugin sensor
        public class PluginSensorDescriptor
        {
            public string Id { get; set; } = "";          // plugin id or legacy libre id or name token
            public string FriendlyName { get; set; } = ""; // human name shown in AIDA
            public double? X { get; set; } = null;        // optional x coordinate if you can derive it
            public double? Y { get; set; } = null;        // optional y coordinate
            public bool IsHwBacked { get; set; } = false; // true if this sensor has real HW ids behind it
        }

        // Diagnostic helper — Libre diagnostics intentionally disabled in AIDA-only build
        private static void LogLibreCandidatesDiagnostic()
        {
            MapLogger.Log("[LIBRE-DIAG] Libre diagnostics are disabled in AIDA-only configuration.");
            // If you want to re-enable, implement gated logic here that calls LibreSensors.GetOrderedList()
            // and prints the same details you previously used for debugging.
        }
    }

    // Simple MapLogger: Trace is very verbose and gated by VerboseMapLogs; avoid recursion
    static class MapLogger
    {
        // Very verbose (per-sensor) — shown only when VerboseMapLogs == true
        public static void Trace(string message)
        {
            try
            {
                if (ConfigModel.Instance?.Settings?.VerboseMapLogs == true)
                {
                    System.Diagnostics.Debug.WriteLine("[MAP-TRACE] " + message);
                }
            }
            catch { /* defensive: don't crash logging */ }
        }

        // General mapping messages (less noisy). Shown only when VerboseMapLogs == true
        public static void Log(string message)
        {
            try
            {
                if (ConfigModel.Instance?.Settings?.VerboseMapLogs == true)
                    System.Diagnostics.Debug.WriteLine("[MAP] " + message);
            }
            catch { }
        }

        // Info = important short messages — always shown (use sparingly)
        public static void Info(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAP-INFO] " + message);
            }
            catch { }
        }

        public static void Error(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAP-ERROR] " + message);
            }
            catch { }
        }
    }

}
