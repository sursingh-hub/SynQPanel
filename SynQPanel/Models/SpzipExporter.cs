using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using SynQPanel.Models;

namespace SynQPanel.Models
{
    public static class SpzipExporter
    {
        /// <summary>
        /// creates a staging .spzip in a temp folder,
        /// uses File.Replace to atomically replace the original and create original.spzip.bak when the original exists,
        ///  if original doesn't exist it moves the staging file into place,
        /// preserves previous behaviour of deleting the staging folder, but keeps it when DevTrace.Enabled == true (so you can debug),
        /// existing logic (uses the profile's ImportedSensorPanelPath and GUID assets).
        /// </summary>
        public static string? ExportProfileAsSpzip(Profile profile, string targetFilePath)
        {
            try
            {
                if (profile == null)
                    return null;

                if (string.IsNullOrWhiteSpace(targetFilePath))
                    return null;

                // Ensure .spzip extension
                if (!targetFilePath.EndsWith(".spzip", StringComparison.OrdinalIgnoreCase))
                {
                    targetFilePath += ".spzip";
                }

                // Where to save
                var targetFolder = Path.GetDirectoryName(targetFilePath);
                if (string.IsNullOrWhiteSpace(targetFolder))
                {
                    targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                }
                Directory.CreateDirectory(targetFolder);

                // We rely on ProcessSensorPanelImport having set this when the panel was imported.
                var panelPath = profile.ImportedSensorPanelPath;
                if (string.IsNullOrWhiteSpace(panelPath) || !File.Exists(panelPath))
                {
                    DevTrace.Write("[SpzipExporter] No imported sensorpanel/sp2 path available for this profile.");
                    return null;
                }

                // Derive a base name purely for the internal .sp2 filename
                string baseName = profile.Name ?? "panel";
                baseName = baseName.Replace("[Import]", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = profile.Guid.ToString();

                // Final .spzip path (with user-chosen name)
                string spzipPath = targetFilePath;

                // 1) Create a temp folder to assemble SPZIP contents
                string tempRoot = Path.Combine(Path.GetTempPath(), "SynQPanelSpzipExport_" + Guid.NewGuid());
                Directory.CreateDirectory(tempRoot);

                try
                {
                    // 2) Copy panel file as *.sp2 into temp
                    // Ensure we don't accidentally copy a .bak as the panel file.
                    string sp2Name = Path.GetFileNameWithoutExtension(panelPath) + ".sp2";
                    string tempSp2Path = Path.Combine(tempRoot, sp2Name);
                    File.Copy(panelPath, tempSp2Path, overwrite: true);

                    // 3) Copy all asset images for this profile into temp
                    // Skip any .bak files so backups are NOT included in the package.
                    string assetsRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SynQPanel", "assets", profile.Guid.ToString());

                    if (Directory.Exists(assetsRoot))
                    {
                        foreach (var file in Directory.GetFiles(assetsRoot))
                        {
                            try
                            {
                                // Skip backup files
                                if (string.Equals(Path.GetExtension(file), ".bak", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var destPath = Path.Combine(tempRoot, Path.GetFileName(file));
                                File.Copy(file, destPath, overwrite: true);
                            }
                            catch (Exception exFile)
                            {
                                DevTrace.Write($"[SpzipExporter] Warning: failed copying asset '{file}' -> {exFile.Message}");
                                // continue with other files
                            }
                        }
                    }

                    // 4) Create SPZIP from the temp folder
                    // If a previous .spzip exists, make a single simple .bak copy next to it (overwrite).
                    if (File.Exists(spzipPath))
                    {
                        try
                        {
                            var bakPath = spzipPath + ".bak";
                            File.Copy(spzipPath, bakPath, overwrite: true); // create/overwrite the simple backup
                            DevTrace.Write($"[SpzipExporter] Created backup '{bakPath}'.");
                        }
                        catch (Exception exBak)
                        {
                            // non-fatal: log and continue (we will still try to replace the file)
                            DevTrace.Write($"[SpzipExporter] Warning: failed creating backup '{spzipPath}.bak': {exBak.Message}");
                        }

                        // Remove the existing spzip so we can create the new one
                        try { File.Delete(spzipPath); } catch { /* ignore deletion errors */ }
                    }

                    ZipFile.CreateFromDirectory(tempRoot, spzipPath, CompressionLevel.Optimal, includeBaseDirectory: false);


                    DevTrace.Write($"[SpzipExporter] SPZIP exported to '{spzipPath}'");
                    return spzipPath;
                }
                finally
                {
                    // 5) Cleanup temp folder
                    try { Directory.Delete(tempRoot, true); } catch { /* ignore */ }
                }
            }
            catch (Exception ex)
            {
                DevTrace.Write($"[SpzipExporter] ExportProfileAsSpzip error: {ex}");
                return null;
            }
        }




    }


}


