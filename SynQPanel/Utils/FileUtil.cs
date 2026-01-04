using SynQPanel.Models;
using SynQPanel.Views.Components;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

namespace SynQPanel.Utils
{
    class FileUtil
    {
        public static string GetBundledPluginFolder()
        {
            return Path.Combine("plugins");
        }

        public static string GetExternalPluginFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SynQPanel", "plugins");
        }

        public static string GetPluginStateFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "plugins.bin");
        }

        public static string GetRelativeAssetPath(Profile profile, string fileName)
        {
            return GetRelativeAssetPath(profile.Guid, fileName);
        }

        public static string GetRelativeAssetPath(Guid profileGuid, string fileName)
        {
            return GetRelativeAssetPath(profileGuid.ToString(), fileName);
        }

        public static string GetRelativeAssetPath(string profileGuid, string fileName)
        {
            return Path.Combine(
                           Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                           "SynQPanel", "assets", profileGuid, fileName);
        }

        public static string GetAssetPath(Profile profile)
        {
            return GetAssetPath(profile.Guid);
        }

        public static string GetAssetPath(Guid profileGuid)
        {
            return GetAssetPath(profileGuid.ToString());
        }
        public static string GetAssetPath(string profileGuid)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "assets", profileGuid);
        }
        public static string GetAssetDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "assets");
        }

        public static async Task<bool> SaveAsset(Profile profile, string fileName, byte[] data)
        {
            var assetPath = GetAssetPath(profile);

            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
            }

            var filePath = Path.Combine(assetPath, fileName);

            try
            {
                await File.WriteAllBytesAsync(filePath, data);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ensure the given image file exists inside the given profile's assets folder.
        /// If the file is already inside the profile's asset path this returns the original path.
        /// Otherwise it copies the file into the profile's assets folder and returns the new destination path.
        /// Safe, idempotent and async-friendly.
        /// </summary>
        public static async Task<string> EnsureImageInProfileAssets(Profile profile, string sourcePath, string fileName)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrEmpty(sourcePath)) throw new ArgumentNullException(nameof(sourcePath));
            if (string.IsNullOrEmpty(fileName)) fileName = Path.GetFileName(sourcePath);

            // Destination asset folder for this profile
            var assetPath = GetAssetPath(profile);

            if (!Directory.Exists(assetPath))
                Directory.CreateDirectory(assetPath);

            // Normalize full paths
            string destPath = Path.Combine(assetPath, fileName);
            string srcFull = Path.GetFullPath(sourcePath);
            string destFull = Path.GetFullPath(destPath);

            // If already the same file, return as-is
            if (string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase) && File.Exists(destFull))
            {
                return destFull;
            }

            // If destination already exists (maybe previously copied), return it
            if (File.Exists(destFull))
            {
                return destFull;
            }

            // Otherwise try a safe copy. If sourcePath not available (rare), we attempt a best-effort fallback.
            try
            {
                // If source exists, do a File.Copy first for speed
                if (File.Exists(srcFull))
                {
                    File.Copy(srcFull, destFull, overwrite: false);
                    return destFull;
                }

                // Fall back: maybe sourcePath is a data: uri or network path—try reading via File API will fail.
                // If we can't access source, return destPath (it will be missing but caller can handle).
                return destFull;
            }
            catch
            {
                // Last resort: try reading bytes and writing (handles locked reads sometimes)
                try
                {
                    var bytes = await File.ReadAllBytesAsync(srcFull);
                    await File.WriteAllBytesAsync(destFull, bytes);
                    return destFull;
                }
                catch
                {
                    // If even that fails, return destFull anyway so caller can still point to profile-local location,
                    // but the file will be missing — caller should handle gracefully.
                    return destFull;
                }
            }
        }

        /// <summary>
        /// Delete the given directory safely: waits briefly for finalizers, retries on IO/lock errors,
        /// and logs warnings instead of throwing if deletion cannot be completed.
        /// </summary>
        public static void DeleteDirectorySafely(string path, int retryCount = 3, int retryDelayMs = 250)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                    return;

                // Give the runtime a moment to release any file handles (WPF bitmaps etc.)
                GC.Collect();
                GC.WaitForPendingFinalizers();

                for (int attempt = 0; attempt <= retryCount; attempt++)
                {
                    try
                    {
                        Directory.Delete(path, true);
                        return; // success
                    }
                    catch (IOException)
                    {
                        // locked by another process - wait and retry
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // permission or file lock - wait and retry
                    }
                    catch (Exception ex)
                    {
                        // unexpected - log and stop trying
                        Log.Error(ex, "Failed deleting directory {Path}", path);
                        return;
                    }

                    Thread.Sleep(retryDelayMs);
                }

                // If deletion still fails after retries, warn and leave the folder intact.
                Log.Warning("Could not delete directory {Path} after {Retries} attempts; leaving it in place.", path, retryCount);
            }
            catch (Exception ex)
            {
                // Fail safe: log and continue
                Log.Error(ex, "Unexpected error while attempting to delete directory {Path}", path);
            }
        }

        public static async Task CleanupAssets()
        {
            await Task.Run(async () =>
            {
                //load from file as there may be unsaved changes
                if (ConfigModel.LoadProfilesFromFile() is List<Profile> profiles)
                {
                    try
                    {
                        var assetDirectory = GetAssetDirectory();

                        if (!Directory.Exists(assetDirectory))
                        {
                            return;
                        }

                        var assetFolders = Directory.GetDirectories(assetDirectory).ToList();

                        foreach (var profile in profiles)
                        {
                            var assetFolder = GetAssetPath(profile);
                            assetFolders.Remove(assetFolder);

                            if (Directory.Exists(assetFolder))
                            {
                                var assetFiles = Directory.GetFiles(assetFolder).ToList();

                                var displayItems = await SharedModel.LoadDisplayItemsAsync(profile);

                                //load from file as there may be unsaved changes
                                foreach (var item in displayItems)
                                {
                                    FilterAssetFiles(item, assetFiles);
                                }

                                //clean up removed files
                                foreach (var assetFile in assetFiles)
                                {
                                    try
                                    {
                                        File.Delete(assetFile);
                                    }
                                    catch { }
                                }
                            }
                        }

                        //clean up removed profiles
                        foreach (var assetFolder in assetFolders)
                        {
                            try
                            {
                                // Use the safe deletion helper instead of direct Directory.Delete
                                DeleteDirectorySafely(assetFolder);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            });
        }

        private static void FilterAssetFiles(DisplayItem item, List<string> assetFiles)
        {
            if (item is GroupDisplayItem groupDisplayItem)
            {
                foreach (var child in groupDisplayItem.DisplayItemsCopy)
                {
                    FilterAssetFiles(child, assetFiles);
                }
            }
            else if (item is ImageDisplayItem imageDisplayItem)
            {
                if (imageDisplayItem.CalculatedPath != null)
                {
                    assetFiles.Remove(imageDisplayItem.CalculatedPath);
                }
            }
            else if (item is GaugeDisplayItem gaugeDisplayItem)
            {
                foreach (var image in gaugeDisplayItem.Images)
                {
                    if (image.CalculatedPath != null)
                    {
                        assetFiles.Remove(image.CalculatedPath);
                    }
                }
            }
        }
    }
}
