using Serilog;
using Serilog.Events;
using SynQPanel.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Topten.RichTextKit.Utils;



namespace SynQPanel.Aida
{
    public class AidaSensorItem
    {
        public string Type { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        public override string ToString() => $"{Type} | {Id} | {Label} = {Value} {Unit}";
    }

    public class AidaHash : IDisposable
    {
        private const string SharedMemName = "AIDA64_SensorValues";
        private const uint FileMapRead = 0x0004;
        private const int MaxBufferSize = 16384; // 16 KB (adjust if you need larger)
                                                 // private const int MaxBufferSize = 32768; // 32 KB

        private static bool _loggedMappingSuccess = false;
        private static int _lastSensorCount = -1;
        private static int _lastMappedSize = -1;



        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess,
        uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private IntPtr _hMap = IntPtr.Zero;
        private IntPtr _mapView = IntPtr.Zero;

        public bool IsOpen => _hMap != IntPtr.Zero;

        public bool OpenSharedMemory()
        {
            if (LoggingUtil.DiagnosticsEnabled)
            {
                Log.Information(
                    "AIDA: Opening shared memory (MapName={MapName})",
                    SharedMemName
                );
            }


            if (_hMap != IntPtr.Zero)
                return true;

            _hMap = OpenFileMapping(FileMapRead, false, SharedMemName);
            if (_hMap == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();

                Log.Error(
                    "AIDA: OpenFileMapping failed. Win32Error={Error}, IsAdmin={IsAdmin}, MapName={MapName}",
                    err,
                    SecurityUtil.IsRunningAsAdmin(),
                    SharedMemName
                );

                return false;
            }
            if (!_loggedMappingSuccess && LoggingUtil.DiagnosticsEnabled)
            {
                Log.Information("AIDA: OpenFileMapping succeeded");
                _loggedMappingSuccess = true;
            }
            return true;

        }

        public void Close()
        {
            if (_mapView != IntPtr.Zero)
            {
                UnmapViewOfFile(_mapView);
                _mapView = IntPtr.Zero;
            }
            if (_hMap != IntPtr.Zero)
            {
                CloseHandle(_hMap);
                _hMap = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public List<AidaSensorItem> RefreshSensorData()
        {
            if (LoggingUtil.DiagnosticsEnabled)
            {
                Log.Information("AIDA: Attempting to read shared memory sensors");
            }


            if (!OpenSharedMemory())
            {
                Log.Error("AIDA: OpenSharedMemory() failed");
                throw new InvalidOperationException(
                    "Failed to open AIDA64 shared memory. Please check if AIDA64 is running and Shared Memory is enabled."
                );
            }

            if (_mapView != IntPtr.Zero)
            {
                UnmapViewOfFile(_mapView);
                _mapView = IntPtr.Zero;
            }

            //_mapView = MapViewOfFile(_hMap, FileMapRead, 0, 0, (UIntPtr)MaxBufferSize);

            _mapView = MapViewOfFile(
            _hMap,
            FileMapRead,
            0,
            0,
            UIntPtr.Zero   // map entire section
            );

            if (_mapView == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Log.Error("AIDA: MapViewOfFile failed. Win32Error={Error}", err);
                throw new InvalidOperationException($"MapViewOfFile returned NULL. Win32 Error: {err}");
            }

            try
            {
                // 🔍 Query actual mapped memory size
                MEMORY_BASIC_INFORMATION mbi;
                VirtualQuery(
                    _mapView,
                    out mbi,
                    (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()
                );

                int mappedSize = (int)mbi.RegionSize;

                if (_lastMappedSize != mappedSize)
                {
                    if (LoggingUtil.DiagnosticsEnabled)
                    {
                        Log.Information(
                            "AIDA: Mapped shared memory region size={Size} bytes",
                            mappedSize
                        );
                    }

                    _lastMappedSize = mappedSize;
                }


                // Allocate buffer exactly matching mapped region
                var buffer = new byte[mappedSize];

                // SAFE copy — no overflow possible
                Marshal.Copy(_mapView, buffer, 0, mappedSize);


                int nullPos = Array.IndexOf(buffer, (byte)0);
                int length = nullPos >= 0 ? nullPos : mappedSize;


                if (LoggingUtil.DiagnosticsEnabled)
                {
                    Log.Debug(
                        "AIDA: Raw shared memory buffer length={Length} / MaxBufferSize={Max}",
                        length,
                        MaxBufferSize
                    );
                }


                if (length >= mappedSize)
                {
                    Log.Warning(
                        "AIDA: Shared memory buffer may be truncated (Length reached mapped size={Size})",
                        mappedSize
                    );
                }


                // 🔍 NEW: log raw buffer size
                if (LoggingUtil.DiagnosticsEnabled)
                {
                    Log.Debug("AIDA: Raw shared memory buffer length={Length}", length);
                }

                string xml = Encoding.Default
                    .GetString(buffer, 0, length)
                    .Trim('\0', '\r', '\n', ' ');

                // 🔍 NEW: basic sanity check
                if (!xml.Contains("<id>", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("AIDA: XML does not contain <id> tags. Possible corruption or mismatch.");
                }

                // Ensure we don't parse broken XML
                int lastTagEnd = xml.LastIndexOf('>');
                if (lastTagEnd > 0 && lastTagEnd < xml.Length - 1)
                {
                    Log.Warning(
                        "AIDA: Trimming truncated XML from {OriginalLength} to {TrimmedLength}",
                        xml.Length,
                        lastTagEnd + 1
                    );

                    xml = xml.Substring(0, lastTagEnd + 1);
                }


                string xmlToParse = "<root>" + xml + "</root>";

                XDocument doc;
                try
                {
                    doc = XDocument.Parse(xmlToParse);
                }
                catch (XmlException ex)
                {
                    // 🔥 CRITICAL: do NOT crash, do NOT rethrow
                    Log.Error(
                        ex,
                        "AIDA: XML parsing failed after truncation handling. XML length={Length}",
                        xml.Length
                    );

                    // 🔍 Helpful diagnostics (safe, capped)
                    Log.Debug(
                        "AIDA: XML HEAD (first 500 chars): {Head}",
                        xml.Length > 500 ? xml.Substring(0, 500) : xml
                    );

                    Log.Debug(
                        "AIDA: XML TAIL (last 500 chars): {Tail}",
                        xml.Length > 500 ? xml.Substring(xml.Length - 500) : xml
                    );

                    return new List<AidaSensorItem>(); // graceful fallback
                }


                var sensors = doc.Root!
                    .Descendants()
                    .Where(x => x.Element("id") != null)
                    .Select(x =>
                    {
                        var idRaw = x.Element("id")?.Value ?? string.Empty;
                        var labelRaw = x.Element("label")?.Value ?? x.Element("name")?.Value ?? string.Empty;
                        var valueRaw = x.Element("value")?.Value ?? x.Element("val")?.Value ?? string.Empty;

                        var id = Normalize(idRaw);
                        var label = Normalize(labelRaw);
                        var value = Normalize(valueRaw);

                        string unit = InferUnit(x.Name.LocalName ?? string.Empty, id, label, value);

                        return new AidaSensorItem
                        {
                            Type = x.Name.LocalName ?? string.Empty,
                            Id = id,
                            Label = label,
                            Value = value,
                            Unit = unit
                        };
                    })
                    .Where(s => !string.IsNullOrEmpty(s.Id) || !string.IsNullOrEmpty(s.Label))
                    .ToList();

                // ✅ NEW: final result logging
                if (_lastSensorCount != sensors.Count)
                {
                    if (LoggingUtil.DiagnosticsEnabled)
                    {
                        Log.Information(
                            "AIDA: Shared memory parsed successfully. Sensors found={Count}",
                            sensors.Count
                        );
                    }

                    _lastSensorCount = sensors.Count;
                }


                if (sensors.Count == 0)
                {
                    Log.Warning("AIDA: Sensor list is EMPTY after parsing");
                }

                return sensors;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AIDA: Failed while reading or parsing shared memory");
                throw;
            }
            finally
            {
                if (_mapView != IntPtr.Zero)
                {
                    UnmapViewOfFile(_mapView);
                    _mapView = IntPtr.Zero;
                }
            }
        }

        private static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            return input.Trim();
        }

        private static string InferUnit(string xmlElementName, string id, string label, string value)
        {
            // Normalise for checks
            var idU = (id ?? string.Empty).ToUpperInvariant();
            var labelU = (label ?? string.Empty).ToUpperInvariant();
            var valU = (value ?? string.Empty).ToUpperInvariant();
            var elem = (xmlElementName ?? string.Empty).ToLowerInvariant();


            // GPU / CPU TDP percentage (e.g. PGPU1TDPP)
            if (idU.Contains("TDPP"))
                return "%";


            // 1) element-name based (leave as-is for explicit categories)
            switch (elem)
            {
                case "temp":
                    return "°C";
                case "volt":
                    return "V";
                case "fan":
                    return "RPM";
                case "pwr":
                    return "W";
                case "curr":
                    return "A";
                case "gpu fan":
                    return "%";
            }

            // 2) If the value itself already contains an explicit unit, don't override
            if (!string.IsNullOrEmpty(valU))
            {
                if (valU.Contains("%") || valU.Contains("MHZ") || valU.Contains("GHZ") || valU.Contains("°C") ||
                    valU.Contains("V") || valU.Contains("RPM") || valU.Contains("MB/S") || valU.Contains("KB/S") || valU.Contains("GB"))
                {
                    return string.Empty;
                }
            }

            // 3) Specific ID / label patterns commonly used by AIDA (conservative, targeted)
            // UTIL / UTI => percent
            if (idU.EndsWith("UTI") || idU.Contains("UTI") || idU.Contains("UTIL") || labelU.Contains("UTIL") || labelU.Contains("UTILIZATION"))
                return "%";

            // CLOCKS / FREQUENCIES => MHz
            // Example IDs: SCPUCLK, SCC-1-3, SGPU1CLK, SMEMCLK, etc.
            if (idU.Contains("CLK") || idU.Contains("MEMCLK") || idU.Contains("FREQ") || labelU.Contains("CLOCK") || labelU.Contains("FREQ"))
            {
                // If value text already has GHz/MHz we don't set
                if (valU.Contains("GHZ"))
                {
                    // optional: convert GHz->MHz at presentation layer if desired; here we avoid changing raw text
                    return string.Empty;
                }
                return "MHz";
            }

            // Disk/network speed / transfer rates
            // AIDA uses READSPD / WRITESPD / DLRATE / ULRATE tokens
            if (idU.Contains("READSPD") || idU.Contains("WRITESPD") || idU.Contains("DLRATE") || idU.Contains("ULRATE") || labelU.Contains("RATE") || labelU.Contains("SPEED"))
            {
                // Conservative default: MB/s (you can switch to KB/s if your tests show smaller magnitudes)
                return "MB/s";
            }

            // Drive sizes / used/free already contain numbers and maybe units; avoid forcing
            if (idU.Contains("USEDSPC") || idU.Contains("FREESPC") || labelU.Contains("USED SPACE") || labelU.Contains("FREE SPACE"))
            {
                // No forced unit — the value often already formatted (e.g., "52.0" meaning GB)
                return string.Empty;
            }

            // Voltage fallback
            if (idU.StartsWith("V") && idU.Length <= 6 && (labelU.Contains("VOLT") || labelU.Contains("VOLTAGE")))
                return "V";

            // RPM catch (if id looks like Fxxx but elem==sys, but label says fan)
            if (labelU.Contains("FAN") || idU.StartsWith("F") && labelU.Length < 10 && labelU.Contains("CPU"))
                return "RPM";

            // some AIDA IDs explicitly include percent-like tokens or words
            if (labelU.EndsWith("%") || valU.EndsWith("%"))
                return "%";

          
            // Power-related IDs fallback (ex: PCPUPKG, PGPU1, PGPU1PCIE)
            if (idU.Contains("PWR") || idU.Contains("PKG")  || labelU.Contains("POWER") || idU.Contains("PGPU"))
                return "W";

            // GPU / CPU TDP percentage (e.g. PGPU1TDPP, TDP%)
            if (idU.Contains("TDPP") || labelU.Contains("GPU TDP%") || labelU.Contains("TDP"))
                return "%";


            // final fallback: if nothing matched, leave unknown
            return string.Empty;


        }


        #region IDisposable
        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // nothing managed
                }

                Close();
                _disposed = true;
            }
        }

        ~AidaHash()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        [DllImport("kernel32.dll")]
        private static extern UIntPtr VirtualQuery(
        IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer,
        UIntPtr dwLength
        );



    }
}
