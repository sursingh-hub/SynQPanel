// TempCleanup.cs
using System;
using System.Collections.Generic;
using System.IO;

namespace SynQPanel.Models
{
    public static class TempCleanup
    {
        /// <summary>
        /// Attempt to delete the supplied tmp sp2 and its .bak file and any stray SynQPanel_Spzip_Tmp_*.sp2.bak entries.
        /// Will not delete when DevTrace.Enabled == true.
        /// Returns list of deleted paths (useful for logs).
        /// </summary>
        public static List<string> CleanupTmpSp2AndBak(string? tmpSp2)
        {
            var deleted = new List<string>();
            try
            {
                if (DevTrace.Enabled) return deleted;

                void TryDelete(string path)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(path)) return;
                        if (!File.Exists(path)) return;

                        var fi = new FileInfo(path);

                        // Safety: only delete small-ish files (avoid removing large user files)
                        // and fairly recent temp files (you can tune thresholds).
                        if (fi.Length > 50 * 1024 * 1024) return; // skip > 50MB
                        if (DateTime.UtcNow - fi.CreationTimeUtc > TimeSpan.FromHours(6)) return; // skip old files

                        File.Delete(path);
                        deleted.Add(path);
                    }
                    catch { /* ignore */ }
                }

                if (!string.IsNullOrWhiteSpace(tmpSp2))
                {
                    TryDelete(tmpSp2);
                    TryDelete(tmpSp2 + ".bak");
                }

                // remove any stray temp bak files created by our pattern
                try
                {
                    var tmpRoot = Path.GetTempPath();
                    foreach (var f in Directory.EnumerateFiles(tmpRoot, "SynQPanel_Spzip_Tmp_*.sp2.bak"))
                    {
                        TryDelete(f);
                    }
                    foreach (var f in Directory.EnumerateFiles(tmpRoot, "*.sp2.bak"))
                    {
                        TryDelete(f);
                    }



                }
                catch { /* ignore */ }
            }
            catch { /* ignore */ }

            return deleted;
        }
    }
}
