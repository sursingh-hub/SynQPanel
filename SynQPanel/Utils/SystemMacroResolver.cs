using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;


namespace SynQPanel.Utils
{
    public static class SystemMacroResolver
    {
        public static string Resolve(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // ✅ NOT a macro → return as-is
            if (!text.StartsWith("$", StringComparison.Ordinal))
                return text;

            // ✅ Known macros
            if (text.Equals("$CPUMODEL", StringComparison.OrdinalIgnoreCase))
                return NormalizeCpuName(GetCpuModel());

            // GPU models
            if (text.Equals("$GPU1MODEL", StringComparison.OrdinalIgnoreCase))
                return GetGpuModel(0);

            if (text.Equals("$GPU2MODEL", StringComparison.OrdinalIgnoreCase))
                return GetGpuModel(1);

            if (text.Equals("$MOBOMODEL", StringComparison.OrdinalIgnoreCase))
                return GetMotherboardProduct();

            if (text.Equals("$CHIPSET", StringComparison.OrdinalIgnoreCase))
                return ExtractChipsetFromBoard(GetMotherboardProduct());

            if (text.Equals("$OSPRODUCT", StringComparison.OrdinalIgnoreCase))
                return GetOsProduct();

            if (text.Equals("$HOSTNAME", StringComparison.OrdinalIgnoreCase))
                return GetHostName();

            if (text.Equals("$USERNAME", StringComparison.OrdinalIgnoreCase))
                return GetUserName();

            if (text.Equals("$DNSHOSTNAME", StringComparison.OrdinalIgnoreCase))
                return GetDnsHostName();

            if (text.Equals("$LOCALIP", StringComparison.OrdinalIgnoreCase))
                return GetLocalIpAddress();

            if (text.Equals("$DXVER", StringComparison.OrdinalIgnoreCase))
                return GetDirectXVersion();



            // ✅ Unknown macro → show literally (safe, AIDA-like)
            return text;
        }

        private static string NormalizeCpuName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            // Remove common trailing junk added by Windows/WMI
            name = Regex.Replace(
                name,
                @"\s+\d+-Core\s+Processor$",
                "",
                RegexOptions.IgnoreCase
            );

            name = Regex.Replace(
                name,
                @"\s+Processor$",
                "",
                RegexOptions.IgnoreCase
            );

            return name.Trim();
        }

        private static string GetCpuModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Processor"
                );

                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name.Trim();
                }
            }
            catch
            {
                // swallow — fallback handled by caller
            }

            return "Unknown CPU";
        }

        private static string GetGpuModel(int index)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController"
                );

                var gpus = searcher
                    .Get()
                    .Cast<ManagementObject>()
                    .Select(mo => mo["Name"]?.ToString()?.Trim())
                    .Where(name =>
                        !string.IsNullOrWhiteSpace(name) &&
                        !IsVirtualGpu(name)
                    )
                    .ToList();

                if (index >= 0 && index < gpus.Count)
                    return gpus[index] ?? string.Empty;
            }
            catch
            {
                // swallow
            }

            // ✅ AIDA behavior: empty string if missing
            return string.Empty;
        }


        private static bool IsVirtualGpu(string name)
        {
            var n = name.ToLowerInvariant();

            return
                n.Contains("iddcx") ||
                n.Contains("microsoft") ||
                n.Contains("remote") ||
                n.Contains("virtual") ||
                n.Contains("indirect") ||
                n.Contains("basic display");
        }

        private static string GetMotherboardProduct()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Product FROM Win32_BaseBoard"
                );

                foreach (ManagementObject obj in searcher.Get())
                {
                    var product = obj["Product"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(product))
                        return product.Trim();
                }
            }
            catch { }

            return "Unknown Motherboard";
        }


        private static string ExtractChipsetFromBoard(string board)
        {
            if (string.IsNullOrWhiteSpace(board))
                return "Unknown Chipset";

            // AMD: B650, B650M, X670E, etc.
            var amdMatch = Regex.Match(
                board,
                @"\b(B\d{3}|X\d{3})([A-Z])?\b",
                RegexOptions.IgnoreCase
            );

            if (amdMatch.Success)
                return $"AMD {amdMatch.Groups[1].Value.ToUpper()}";

            // Intel: Z790, Z790-A, H610M, etc.
            var intelMatch = Regex.Match(
                board,
                @"\b(Z\d{3}|H\d{3}|B\d{3})([A-Z])?\b",
                RegexOptions.IgnoreCase
            );

            if (intelMatch.Success)
                return $"Intel {intelMatch.Groups[1].Value.ToUpper()}";

            return "Unknown Chipset";
        }

        private static string GetOsProduct()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption FROM Win32_OperatingSystem"
                );

                foreach (ManagementObject obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(caption))
                        return caption.Trim();
                }
            }
            catch
            {
                // swallow
            }

            return "Windows";
        }

        private static string GetHostName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch
            {
                return "Unknown Host";
            }
        }

        private static string GetUserName()
        {
            try
            {
                return Environment.UserName;
            }
            catch
            {
                return "User";
            }
        }

        private static string GetDnsHostName()
        {
            try
            {
                return System.Net.Dns.GetHostEntry(Environment.MachineName).HostName;
            }
            catch
            {
                return Environment.MachineName; // safe fallback (AIDA-like)
            }
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // ignore
            }

            return string.Empty; // AIDA shows blank if unavailable
        }

        private static string GetDirectXVersion()
        {
            try
            {
                // Windows 10 / 11 always support DX12
                if (OperatingSystem.IsWindowsVersionAtLeast(10))
                    return "DirectX 12.0";

                // Windows 8.x
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                    return "DirectX 11.1";

                // Windows 7
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                    return "DirectX 11";

                return "DirectX 9.0c";
            }
            catch
            {
                return "Unknown";
            }
        }
                

    }

}

