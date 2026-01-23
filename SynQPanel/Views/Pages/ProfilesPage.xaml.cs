using SkiaSharp;
using SynQPanel.Infrastructure;
using SynQPanel.Models;
using SynQPanel.Utils;
using SynQPanel.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;


namespace SynQPanel.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfilesPage.xaml
    /// </summary>
    public partial class ProfilesPage : INavigableView<ProfilesViewModel>
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly ISnackbarService _snackbarService;

        public ObservableCollection<string> InstalledFonts { get; } = [];
        public ProfilesViewModel ViewModel { get; }

        public ProfilesPage(ProfilesViewModel viewModel, IContentDialogService contentDialogService, ISnackbarService snackbarService)
        {
            ViewModel = viewModel;
            DataContext = this;

            LoadAllFonts();
            _contentDialogService = contentDialogService;
            _snackbarService = snackbarService;

            InitializeComponent();

            Loaded += ProfilesPage_Loaded;
            Unloaded += ProfilesPage_Unloaded;
        }

        private void LoadAllFonts()
        {
            var allFonts = SKFontManager.Default.GetFontFamilies()
                .OrderBy(f => f)
                .ToList();

            foreach (var font in allFonts)
            {
                InstalledFonts.Add(font);
            }
        }

        private void ProfilesPage_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void ProfilesPage_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            var profile = new Profile()
            {
                Name = "Panel " + (ConfigModel.Instance.Profiles.Count + 1)
            };
            ConfigModel.Instance.AddProfile(profile);
            ConfigModel.Instance.SaveProfiles();
            SharedModel.Instance.SaveDisplayItems(profile);
            ViewModel.Profile = profile;
            ListViewProfiles.ScrollIntoView(profile);
        }


        private async void ButtonSave_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ViewModel.Profile is Profile profile)
            {
                ConfigModel.Instance.SaveProfiles();
                SharedModel.Instance.SaveDisplayItems(profile);
                _snackbarService.Show("Profile Saved", $"{profile.Name}", ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
            }
        }

        private void ButtonResetPosition_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var screen = Screen.PrimaryScreen;
            if (screen != null && ViewModel.Profile is Profile profile)
            {
                profile.TargetWindow = new TargetWindow(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, screen.DeviceName);
                profile.WindowX = 0;
                profile.WindowY = 0;
            }
        }

        private void ButtonMaximise_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is App app && ViewModel.Profile is Profile profile)
            {
                app.MaximiseDisplayWindow(profile);
            }
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Profile is Profile profile)
            {
                ConfigModel.Instance.ReloadProfile(ViewModel.Profile);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Profile = null;
        }






        /*
        private async void ButtonImportProfile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Multiselect = false,
                //Filter = "SensorPanel Files (*.sensorpanel)|*.sensorpanel|RemoteSensor LCD Files (*.rslcd)|*.rslcd|AIDA SPZIP Files (*.spzip)|*.spzip",
                Filter = "All Supported Files (*.sensorpanel;*.rslcd;*.spzip)|*.sensorpanel;*.rslcd;*.spzip|SensorPanel Files (*.sensorpanel)|*.sensorpanel|RemoteSensor LCD Files (*.rslcd)|*.rslcd|AIDA SPZIP Files (*.spzip)|*.spzip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };
            if (openFileDialog.ShowDialog() == true)
            {
                string file = openFileDialog.FileName;
                if (file.EndsWith(".sensorpanel", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".rslcd", StringComparison.OrdinalIgnoreCase))
                {
                    await SharedModel.ImportSensorPanel(file);
                    _snackbarService.Show("Panel Imported", file, ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                }
                else if (file.EndsWith(".spzip", StringComparison.OrdinalIgnoreCase))
                {
                    string tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SynQPanelSpzip_" + Guid.NewGuid());
                    Directory.CreateDirectory(tempFolder);

                    System.IO.Compression.ZipFile.ExtractToDirectory(file, tempFolder);

                    string sp2File = Directory.GetFiles(tempFolder, "*.sp2").FirstOrDefault();
                    if (sp2File != null)
                    {
                        await SharedModel.ImportSensorPanel(sp2File); // This creates and registers the Profile!

                        // Get newly added profile (assuming it's the most recently added one)
                        var profile = SharedModel.Instance.SelectedProfile;
                        if (profile != null)
                        {
                            var assetFolder = Path.Combine(AppPaths.Assets, profile.Guid.ToString());

                            if (!Directory.Exists(assetFolder))
                                Directory.CreateDirectory(assetFolder);

                            // Copy all image files (.png/.jpg/etc) from tempFolder to assetFolder
                            string[] validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
                            foreach (var imgFile in Directory.GetFiles(tempFolder))
                            {
                                if (validExtensions.Contains(Path.GetExtension(imgFile).ToLowerInvariant()))
                                {
                                    string destPath = Path.Combine(assetFolder, Path.GetFileName(imgFile));
                                    File.Copy(imgFile, destPath, true); // Overwrite=true
                                }
                            }
                        }

                        _snackbarService.Show("SPZIP Panel Imported", file, ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                    }
                    else
                    {
                        _snackbarService.Show("SPZIP Import Failed", "No .sp2 panel found!", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                        Directory.Delete(tempFolder, true);
                    }
                }
                else
                {
                    _snackbarService.Show("Unknown File Type", file, ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                }
            }
        }
        */

        private async void ButtonImportProfile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Multiselect = false,
                //Filter = "SensorPanel Files (*.sensorpanel)|*.sensorpanel|RemoteSensor LCD Files (*.rslcd)|*.rslcd|AIDA SPZIP Files (*.spzip)|*.spzip",
                Filter = "All Supported Files (*.sensorpanel;*.rslcd;*.spzip)|*.sensorpanel;*.rslcd;*.spzip|SensorPanel Files (*.sensorpanel)|*.sensorpanel|RemoteSensor LCD Files (*.rslcd)|*.rslcd|AIDA SPZIP Files (*.spzip)|*.spzip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            string file = openFileDialog.FileName;

            // --------------------------------------------------------------------
            // .sensorpanel or .rslcd
            // --------------------------------------------------------------------
            if (file.EndsWith(".sensorpanel", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".rslcd", StringComparison.OrdinalIgnoreCase))
            {
                // 1) Import the panel (this creates/registers the Profile)
                await SharedModel.ImportSensorPanel(file);

                // 🔎 STEP 2: trace what ImageDisplayItems exist right after import
                if (SharedModel.Instance.SelectedProfile is Profile importedProfile)
                {
                    if (ConfigModel.Instance.EnableRslcdDebug)
                    {
                        TraceRslcdImages(importedProfile, "AFTER_IMPORT");
                        TraceImportedSensors(importedProfile, "AFTER_IMPORT_SENSORS");
                    }
                }



                // Get the profile that was just imported / selected
                var profile = SharedModel.Instance.SelectedProfile;
                if (profile == null)
                {
                    RslcdDebug.Log("[RSLCD] ERROR: SelectedProfile is null after ImportSensorPanel.");
                    _snackbarService.Show("Import Failed", "No profile selected after import.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                // ----------------------------------------------------------------
                // Handle .rslcd: extract embedded images and attach a BG to profile
                // ----------------------------------------------------------------
                if (file.EndsWith(".rslcd", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Remember where this profile came from
                        if (profile != null)
                        {
                            profile.ImportedSensorPanelPath = file;
                        }

                        RslcdDebug.Log($"[RSLCD] Importing embedded images into profile {profile.Guid}");

                        string text = File.ReadAllText(file);

                        // <IMGFIL>name</IMGFIL> ... <IMGDAT>hex</IMGDAT>
                        var matches = Regex.Matches(
                            text,
                            @"<IMGFIL>(?<name>.+?)</IMGFIL>\s*<IMGDAT>(?<data>.+?)</IMGDAT>",
                            RegexOptions.Singleline | RegexOptions.IgnoreCase);

                        // ADD THIS IMMEDIATELY:
                        RslcdDebug.Log($"[RSLCD] Regex found {matches.Count} embedded <IMGFIL>/<IMGDAT> block(s) in {file}");




                        if (matches.Count == 0)
                        {
                            RslcdDebug.Log("[RSLCD] No <IMGFIL>/<IMGDAT> blocks found.");
                        }

                        // Save all embedded images
                        var imageNames = new List<string>();

                        foreach (Match match in matches)
                        {
                            string imageFileName = match.Groups["name"].Value.Trim();
                            string imageHexData = match.Groups["data"].Value.Trim();

                            if (string.IsNullOrWhiteSpace(imageFileName) ||
                                string.IsNullOrWhiteSpace(imageHexData))
                                continue;

                            // BEFORE saving - log what we're about to save
                            RslcdDebug.Log($"[RSLCD] Saving embedded image: '{imageFileName}' (length={imageHexData.Length})");

                            SaveEmbeddedImage(profile.Guid, imageFileName, imageHexData);
                            imageNames.Add(imageFileName);
                        }


                        // AFTER saving, list files in profile asset folder for visibility
                        var assetFolderLocal = Path.Combine(AppPaths.Assets, profile.Guid.ToString());

                        if (Directory.Exists(assetFolderLocal))
                        {
                            var files = Directory.GetFiles(assetFolderLocal);
                            RslcdDebug.Log($"[RSLCD] Asset folder '{assetFolderLocal}' now contains {files.Length} file(s): {string.Join(", ", files.Select(p => Path.GetFileName(p)))}");
                        }
                        else
                        {
                            RslcdDebug.Log($"[RSLCD] Asset folder '{assetFolderLocal}' does not exist after saving.");
                        }

                        RslcdDebug.Log($"[RSLCD] Total saved embedded images = {imageNames.Count}");


                        RslcdDebug.Log(
                            $"[RSLCD] Saved {imageNames.Count} embedded image(s) for profile {profile.Guid}.");

                        if (imageNames.Count > 0)
                        {
                            string assetFolder = Path.Combine(AppPaths.Assets, profile.Guid.ToString());

                            // 1) Try to find the LCARS 1024x600 BG by a "smart" filename match
                            string bestBgName = imageNames
                                .FirstOrDefault(n =>
                                    n.Contains("LcarsLite", StringComparison.OrdinalIgnoreCase) ||
                                    n.Contains("LcarsLitestep", StringComparison.OrdinalIgnoreCase) ||
                                    n.Contains("1024x600", StringComparison.OrdinalIgnoreCase));

                            if (bestBgName == null)
                            {
                                RslcdDebug.Log("[RSLCD] WARNING: Could not pick a BG image (no valid images measured).");
                                return;
                            }

                            string bestFullPath = Path.Combine(assetFolder, bestBgName);
                            int w = 0;
                            int h = 0;

                            if (File.Exists(bestFullPath))
                            {
                                using (var bmp = new System.Drawing.Bitmap(bestFullPath))
                                {
                                    w = bmp.Width;
                                    h = bmp.Height;
                                }

                                RslcdDebug.Log(
                                    $"[RSLCD] Chosen BG image: '{bestBgName}' size={w}x{h} from {bestFullPath}");
                            }
                            else
                            {
                                RslcdDebug.Log(
                                    $"[RSLCD] WARNING: chosen BG file does not exist on disk: {bestFullPath}");
                            }

                            // 🔹 NEW: attach this BG path to all profiles that came from the same .rslcd
                            try
                            {
                                var cfg = ConfigModel.Instance;

                                // Use a copy to be safe against collection modification
                                var profiles = cfg.GetProfilesCopy();
                                foreach (var p in profiles)
                                {
                                    // Only touch profiles that were imported from this exact file
                                    if (string.Equals(p.ImportedSensorPanelPath,
                                                      file,
                                                      StringComparison.OrdinalIgnoreCase))
                                    {
                                        string localCopy = EnsureImageInProfileAssets(p, bestFullPath);
                                        p.BackgroundImagePath = localCopy;


                                        RslcdDebug.Log(
                                            $"[RSLCD] Set BackgroundImagePath for profile {p.Guid} -> {bestFullPath}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                RslcdDebug.Log(
                                    "[RSLCD] Error while assigning BackgroundImagePath to profiles: " + ex);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        RslcdDebug.Log("Error extracting embedded images from .rslcd: " + ex);
                    }
                }

                // Trace again after RSLCD image extraction + BG creation
                if (file.EndsWith(".rslcd", StringComparison.OrdinalIgnoreCase)
                    && SharedModel.Instance.SelectedProfile is Profile rslcdProfile2)
                {
                    TraceRslcdImages(rslcdProfile2, "AFTER_RSLCD");
                }

                _snackbarService.Show("Panel Imported", file, ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                return;


            }



            // --------------------------------------------------------------------
            // .spzip (clean, no temp-folder accumulation; extract needed files directly into GUID asset folder)
            // --------------------------------------------------------------------
            if (file.EndsWith(".spzip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Open archive read-only
                    using var zip = ZipFile.OpenRead(file);

                    // Find the .sp2 entry (search anywhere inside the archive)
                    var sp2Entry = zip.Entries.FirstOrDefault(e => string.Equals(Path.GetExtension(e.FullName), ".sp2", StringComparison.OrdinalIgnoreCase));
                    if (sp2Entry == null)
                    {
                        _snackbarService.Show("SPZIP Import Failed", "No .sp2 panel found inside the .spzip.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                        return;
                    }

                    // We'll import into the permanent profile asset folder
                    // But first call ImportSensorPanel after extracting the .sp2 into that folder.
                    // Import will register/select the profile, so we need to extract the sp2 **before** calling ImportSensorPanel.
                    // To compute GUID path we will extract sp2 to a temporary location in-memory? No — user requested to save to GUID folder.
                    // So prepare the final profile folder first - but we don't know profile.Guid until after import.
                    // Workaround: extract the .sp2 temporarily to a small local stream file, import it, then move .sp2 to the GUID assets folder.
                    // However user requested NO temp-accumulation — we will extract .sp2 into a small temp file in system temp and immediately import then move to GUID folder and delete temp.
                    // The temp file is short-lived and removed immediately; this prevents full temp-folder extraction accumulation.

                    // Local helper functions (small and self-contained)
                    static string MakeUniqueFilename(string folder, string baseName)
                    {
                        string name = Path.GetFileNameWithoutExtension(baseName);
                        string ext = Path.GetExtension(baseName);
                        string candidate = baseName;
                        int i = 1;
                        while (File.Exists(Path.Combine(folder, candidate)))
                        {
                            candidate = $"{name}__dup{i}{ext}";
                            i++;
                        }
                        return candidate;
                    }

                    static void CopyFileWithRetryEnsure(string src, string dest, int retries = 3, int delayMs = 100)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? Path.GetDirectoryName(src) ?? ".");
                        for (int i = 0; i < retries; i++)
                        {
                            try
                            {
                                File.Copy(src, dest, overwrite: true);
                                return;
                            }
                            catch (IOException)
                            {
                                Thread.Sleep(delayMs);
                            }
                        }
                        // final attempt
                        File.Copy(src, dest, overwrite: true);
                    }

                    // Step A: extract .sp2 to a single temp file (short-lived)
                    // Use the original .sp2 filename from the archive so ImportSensorPanel gets a meaningful name
                    var originalSp2Name = Path.GetFileName(sp2Entry.FullName); // e.g. "MyPanel.sp2"
                    var tmpSp2 = Path.Combine(Path.GetTempPath(), originalSp2Name);

                    // ensure we don't accidentally keep an older temp file
                    try { if (File.Exists(tmpSp2)) File.Delete(tmpSp2); } catch { /* ignore */ }

                    // extract the sp2 entry into tmpSp2
                    using (var es = sp2Entry.Open())
                    using (var fs = File.Create(tmpSp2))
                        es.CopyTo(fs);

                    try
                    {
                        using (var es = sp2Entry.Open())
                        using (var fs = File.Create(tmpSp2))
                            es.CopyTo(fs);
                    }
                    catch (Exception ex)
                    {
                        _snackbarService.Show("SPZIP Import Failed", $"Cannot extract .sp2: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                        try { if (File.Exists(tmpSp2)) File.Delete(tmpSp2); } catch { }
                        return;
                    }

                    // Step B: import that extracted .sp2 (this will register the profile and set SelectedProfile)
                    try
                    {
                        await SharedModel.ImportSensorPanel(tmpSp2); // this registers/selects the profile
                    }
                    catch (Exception ex)
                    {
                        _snackbarService.Show("SPZIP Import Failed", $"ImportSensorPanel failed: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                        try { if (File.Exists(tmpSp2)) File.Delete(tmpSp2); } catch { }
                        return;
                    }

                    // Get the profile created by the importer
                    var profile = SharedModel.Instance.SelectedProfile;
                    if (profile == null)
                    {
                        _snackbarService.Show("SPZIP Import Failed", "ImportSensorPanel did not create a profile.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                        try { if (File.Exists(tmpSp2)) File.Delete(tmpSp2); } catch { }
                        return;
                    }

                    // Permanent asset folder for this profile (GUID-based)
                    string profileAssetRoot = Path.Combine(AppPaths.Assets, profile.Guid.ToString());
                    Directory.CreateDirectory(profileAssetRoot);

                    // Step C: move the sp2 into the profile folder (if an identical file exists, keep it)
                    string sp2FileName = Path.GetFileName(sp2Entry.FullName);
                    if (string.IsNullOrWhiteSpace(sp2FileName))
                        sp2FileName = profile.Guid + ".sp2";
                    string destSp2 = Path.Combine(profileAssetRoot, sp2FileName);

                    try
                    {
                        // If dest exists & identical size -> keep existing; else save new or unique name
                        bool writeSp2 = true;
                        if (File.Exists(destSp2))
                        {
                            var destInfo = new FileInfo(destSp2);
                            var tmpInfo = new FileInfo(tmpSp2);
                            if (destInfo.Length == tmpInfo.Length)
                            {
                                // identical file already present; we will reuse existing destSp2
                                writeSp2 = false;
                            }
                        }

                        if (writeSp2)
                        {
                            // If dest exists but different, pick unique name
                            if (File.Exists(destSp2))
                            {
                                string unique = MakeUniqueFilename(Path.GetDirectoryName(destSp2) ?? profileAssetRoot, Path.GetFileName(destSp2));
                                string uniqueDest = Path.Combine(Path.GetDirectoryName(destSp2) ?? profileAssetRoot, unique);
                                CopyFileWithRetryEnsure(tmpSp2, uniqueDest);
                                destSp2 = uniqueDest;
                            }
                            else
                            {
                                CopyFileWithRetryEnsure(tmpSp2, destSp2);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DevTrace.Write($"[SPZIP Import] Failed moving .sp2 into GUID assets: {ex.Message}");
                        // continue - we still have tmpSp2 and importer already executed using it
                    }
                    finally
                    {
                        try
                        {
                            // remove the temp sp2 we created
                            if (!string.IsNullOrWhiteSpace(tmpSp2) && File.Exists(tmpSp2))
                            {
                                try { File.Delete(tmpSp2); } catch { }
                            }

                            // also delete leftover temp .bak files created beside tmpSp2
                            try
                            {
                                TempCleanup.CleanupTmpSp2AndBak(tmpSp2);
                            }
                            catch
                            {
                                // ignore cleanup exceptions
                            }
                        }
                        catch
                        {
                            // ignore outer cleanup exceptions
                        }
                    }


                    // Step D: Extract asset entries from the zip directly into the profile assets folder (preserve relative paths)
                    var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".mp4", ".mov", ".avi", ".svg", ".apng", ".webm", ".ogg" };

                    foreach (var entry in zip.Entries)
                    {
                        try
                        {
                            // ignore directories and the .sp2 entry we handled above
                            if (string.IsNullOrEmpty(entry.Name)) continue;

                            string ext = Path.GetExtension(entry.FullName);
                            if (!validExtensions.Contains(ext)) continue;

                            // Entry full name might be like "assets/foo.png" or "images/icons/bar.png"
                            // Preserve that relative path under profileAssetRoot
                            string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                            string destFull = Path.Combine(profileAssetRoot, rel);
                            Directory.CreateDirectory(Path.GetDirectoryName(destFull) ?? profileAssetRoot);

                            // If dest exists and sizes match -> skip (assume same)
                            bool skip = false;
                            if (File.Exists(destFull))
                            {
                                try
                                {
                                    var destInfo = new FileInfo(destFull);
                                    // ZipArchiveEntry.Length may be 0 for compressed streams in some cases until extracted.
                                    // We'll extract to a temp stream and compare lengths if needed; but cheaper: compare CRC/size is not directly available.
                                    // So, if exists, extract to a temp file then compare sizes:
                                    string tmpDest = Path.Combine(Path.GetTempPath(), "SynQPanel_Spzip_EntryTmp_" + Guid.NewGuid().ToString("N") + ext);
                                    using (var es = entry.Open())
                                    using (var fs = File.Create(tmpDest))
                                    {
                                        es.CopyTo(fs);
                                    }
                                    var tmpInfo = new FileInfo(tmpDest);
                                    if (tmpInfo.Length == destInfo.Length)
                                    {
                                        // identical -> remove tmp and skip
                                        try { File.Delete(tmpDest); } catch { }
                                        skip = true;
                                    }
                                    else
                                    {
                                        // different -> move tmp to unique dest
                                        string unique = MakeUniqueFilename(Path.GetDirectoryName(destFull) ?? profileAssetRoot, Path.GetFileName(destFull));
                                        string uniqueDest = Path.Combine(Path.GetDirectoryName(destFull) ?? profileAssetRoot, unique);
                                        CopyFileWithRetryEnsure(tmpDest, uniqueDest);
                                        try { File.Delete(tmpDest); } catch { }
                                        DevTrace.Write($"[SPZIP Import] Conflict: wrote asset to unique file '{uniqueDest}' (did not overwrite '{destFull}').");
                                        continue;
                                    }
                                }
                                catch (Exception)
                                {
                                    // fallback: attempt to overwrite
                                    try
                                    {
                                        using (var es = entry.Open())
                                        using (var fs = File.Create(destFull))
                                            es.CopyTo(fs);
                                        continue;
                                    }
                                    catch { /* continue to next entry */ }
                                }
                            }

                            if (!skip)
                            {
                                // normal extraction into destFull
                                using (var es = entry.Open())
                                using (var fs = File.Create(destFull))
                                    es.CopyTo(fs);
                            }
                        }
                        catch (Exception ex)
                        {
                            DevTrace.Write($"[SPZIP Import] Failed extracting entry '{entry.FullName}': {ex.Message}");
                            // non-fatal, continue with other entries
                        }
                    }

                    // Step E: Save provenance for later round-trip saves/exports
                    try
                    {
                        profile.ImportedSensorPanelPath = destSp2; // path to .sp2 inside GUID assets (or unique name variant)
                        profile.ImportedSensorPackagePath = file;   // original .spzip path opened by user
                    }





                    catch (Exception ex)
                    {
                        DevTrace.Write($"[SPZIP Import] Warning: cannot set provenance on profile: {ex}");
                    }

                    // Done
                    _snackbarService.Show("SPZIP Panel Imported", file, ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                    
                    return;
                }
                catch (Exception ex)
                {
                    _snackbarService.Show("SPZIP Import Failed", $"Error importing .spzip: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    DevTrace.Write($"[SPZIP Import] Exception: {ex}");
                    return;
                }
            }

            // --------------------------------------------------------------------
            // Unknown file
            // --------------------------------------------------------------------
            _snackbarService.Show("Unknown File Type", file, ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));

        }


        private async void ButtonImportSqx_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = false,
                Filter = "SQX Panel (*.sqx)|*.sqx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (dialog.ShowDialog() != true)
                return;

            string sqxFile = dialog.FileName;

            // 1) Extract to a temp folder (safe)
            string tempFolder = Path.Combine(Path.GetTempPath(), "SynQPanelSqx_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            // Keep temp when DevTrace.Enabled is true (for debugging)
            bool keepTempForDebug = DevTrace.Enabled;

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(sqxFile, tempFolder);
            }
            catch (Exception ex)
            {
                _snackbarService.Show("Import Failed", $"Unable to extract SQX: {ex.Message}",
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                // extraction failed — nothing to clean beyond tempFolder which may be empty; try cleanup now
                if (!keepTempForDebug)
                {
                    try { DeleteDirectoryWithRetries(tempFolder); } catch { }
                }
                return;
            }

            // --- MAIN IMPORT WORK: wrap the rest in try/catch/finally so finally can cleanup tempFolder ---
            try
            {
                // 2) Validate expected files
                string profileXml = Path.Combine(tempFolder, "Profile.xml");
                string displayItemsXml = Path.Combine(tempFolder, "DisplayItems.xml");
                string assetsFolder = Path.Combine(tempFolder, "assets");

                if (!File.Exists(profileXml) || !File.Exists(displayItemsXml))
                {
                    _snackbarService.Show("Import Failed", "SQX missing Profile.xml or DisplayItems.xml",
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                // 3) Deserialize Profile
                Profile? profile = null;
                try
                {
                    XmlSerializer xs = new XmlSerializer(typeof(Profile));
                    using var fs = File.OpenRead(profileXml);
                    using var rd = XmlReader.Create(fs);
                    profile = xs.Deserialize(rd) as Profile;
                }
                catch (Exception ex)
                {
                    _snackbarService.Show("Import Failed", $"Error reading Profile.xml: {ex.Message}",
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                if (profile == null)
                {
                    _snackbarService.Show("Import Failed", "Invalid Profile.xml content",
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                // 4) New GUID + mark imported
                profile.Guid = Guid.NewGuid();
               //profile.Name = "[Import] " + profile.Name;
               profile.Name = profile.Name;

                // 5) Copy DisplayItems.xml into local profiles folder
                string profileFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles");
                Directory.CreateDirectory(profileFolder);
                string profilePath = Path.Combine(profileFolder, profile.Guid + ".xml");
                File.Copy(displayItemsXml, profilePath, overwrite: true);

                // 6) Load display items using public wrapper
                //    (this calls the internal reader that turns the DisplayItems.xml into DisplayItem objects)
                List<DisplayItem> displayItems;
                try
                {
                    displayItems = SharedModel.Instance.LoadDisplayItemsForProfile(profile);
                }
                catch (Exception ex)
                {
                    _snackbarService.Show("Import Failed", $"Failed to parse DisplayItems: {ex.Message}",
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    return;
                }

                // 7) AIDA mapping — set PluginSensorId + SensorType rather than assigning to numeric Id
                foreach (var di in displayItems)
                {
                    try
                    {
                        if (di is ISensorItem sensorItem) // covers sensor-like items
                        {
                            // prefer SensorName or Name depending on concrete type
                            string panelSensorKey = string.Empty;
                            try
                            {
                                var prop = sensorItem.GetType().GetProperty("SensorName");
                                if (prop != null)
                                {
                                    var val = prop.GetValue(sensorItem);
                                    if (val != null) panelSensorKey = val.ToString() ?? string.Empty;
                                }
                            }
                            catch { /* ignore reflection issues */ }

                            // fallback to DisplayItem.Name
                            if (string.IsNullOrWhiteSpace(panelSensorKey) && di is DisplayItem diBase && !string.IsNullOrWhiteSpace(diBase.Name))
                                panelSensorKey = diBase.Name;

                            if (!string.IsNullOrWhiteSpace(panelSensorKey))
                            {
                                var matched = SensorMapping.FindMatchingIdentifier(panelSensorKey);
                                if (!string.IsNullOrWhiteSpace(matched))
                                {
                                    // set plugin mapping in a safe, non-breaking way
                                    try
                                    {
                                        // Many sensor display types expose PluginSensorId and SensorType
                                        var pIdProp = sensorItem.GetType().GetProperty("PluginSensorId");
                                        if (pIdProp != null && pIdProp.PropertyType == typeof(string))
                                        {
                                            pIdProp.SetValue(sensorItem, matched);
                                        }

                                        var stProp = sensorItem.GetType().GetProperty("SensorType");
                                        if (stProp != null)
                                        {
                                            // set to Plugin enum value
                                            var enumType = stProp.PropertyType;
                                            var pluginEnum = Enum.Parse(enumType, "Plugin");
                                            stProp.SetValue(sensorItem, pluginEnum);
                                        }
                                    }
                                    catch { /* ignore reflection issues */ }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // non-fatal per-item mapping errors should not abort whole import
                    }
                }

                // 8) Save display items back via the public wrapper
                SharedModel.Instance.SaveDisplayItemsForProfile(profile, displayItems);

                // 9) Copy assets into LocalAppData\SynQPanel\assets\{guid}\
                if (Directory.Exists(assetsFolder))
                {
                    string destAssetFolder = Path.Combine(AppPaths.Assets, profile.Guid.ToString());
                    Directory.CreateDirectory(destAssetFolder);

                    foreach (var file in Directory.GetFiles(assetsFolder))
                    {
                        string dest = Path.Combine(destAssetFolder, Path.GetFileName(file));
                        File.Copy(file, dest, overwrite: true);
                    }
                }


                // 9a) ------------ Minimal provenance persistence (safe, non-invasive) -------------
                try
                {
                    // Persist a copy of Profile.xml (from tempFolder/Profile.xml) into the profiles folder (not into assets)
                    string profilesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles");
                    Directory.CreateDirectory(profilesFolder);

                    // We already wrote DisplayItems.xml into profiles earlier as profilePath (profile.Guid + ".xml")
                    // Persist Profile.xml as {GUID}.Profile.xml so both textual sources are available later.
                    string persistedProfileXml = Path.Combine(profilesFolder, profile.Guid.ToString() + ".Profile.xml");

                    try
                    {
                        if (File.Exists(profileXml))
                        {
                            File.Copy(profileXml, persistedProfileXml, overwrite: true);
                        }
                        else
                        {
                            // Fallback: serialize in-memory profile to persistedProfileXml
                            var xs = new XmlSerializer(typeof(Profile));
                            using var fs = File.Create(persistedProfileXml);
                            xs.Serialize(fs, profile);
                        }
                    }
                    catch (Exception exCopy)
                    {
                        DevTrace.Write($"[SQX Import] Warning: failed copying Profile.xml into profiles folder: {exCopy.Message}");
                    }

                    // Record provenance so later Save/Export can pick up the correct paths
                    try
                    {
                        profile.ImportedSensorPanelPath = persistedProfileXml; // textual panel source for round-trip saves
                        profile.ImportedSensorPackagePath = sqxFile;           // original .sqx package path user opened
                    }
                    catch (Exception exProv)
                    {
                        DevTrace.Write($"[SQX Import] Warning: unable to set provenance on profile: {exProv}");
                    }

                    // Quick verification output (Console + DevTrace)
                    try
                    {
                        var panelDbg = profile.ImportedSensorPanelPath ?? "<null>";
                        var pkgDbg = profile.ImportedSensorPackagePath ?? "<null>";
                        //DevTrace.Write($"[SQX Import] persisted panel XML -> {panelDbg}");
                        DevTrace.Write($"[SQX Import] recorded package path -> {pkgDbg}");
                        try { Console.WriteLine($"[SQX Import] panel='{panelDbg}', package='{pkgDbg}'"); } catch { }
                    }
                    catch { /* ignore debug failures */ }
                }
                catch (Exception ex)
                {
                    DevTrace.Write($"[SQX Import] Unexpected error while persisting provenance: {ex}");
                }
                // -----------------------------------------------------------------------------

                // 9b) ---------------------- Persist textual sources into profiles/assets (minimal, corrected) ----------------------
                try
                {
                    // We already have profileFolder above; do NOT redeclare.
                    // That folder already contains the DisplayItems.xml copy as {GUID}.xml from your step 5.

                    // Confirm DisplayItems copy exists (log only)
                    string destDisplayItemsXml = Path.Combine(profileFolder, profile.Guid + ".xml");
                    DevTrace.Write($"[SQX Import] DisplayItems XML is at -> {destDisplayItemsXml}");

                    // Create the GUID asset folder
                    string profileAssetRoot =
                        Path.Combine(
                        AppPaths.Assets, profile.Guid.ToString());
                    Directory.CreateDirectory(profileAssetRoot);

                    // Copy Profile.xml into the GUID asset folder (this one was NOT copied earlier)
                    string destProfileXml = Path.Combine(profileAssetRoot, "Profile.xml");
                    try
                    {
                        File.Copy(profileXml, destProfileXml, overwrite: true);
                        DevTrace.Write($"[SQX Import] persisted Profile.xml into assets -> {destProfileXml}");
                    }
                    catch (Exception exCopyProfile)
                    {
                        DevTrace.Write($"[SQX Import] Warning: failed copying Profile.xml into assets: {exCopyProfile.Message}");
                    }

                    // Record provenance for saving later
                    try
                    {
                        profile.ImportedSensorPanelPath = destProfileXml; // textual panel XML to update on save
                        profile.ImportedSensorPackagePath = sqxFile;      // original SQX file path
                        DevTrace.Write($"[SQX Import] stored package path -> {sqxFile}");
                    }
                    catch (Exception exProv)
                    {
                        DevTrace.Write($"[SQX Import] Warning: cannot set provenance: {exProv.Message}");
                    }
                }
                catch (Exception exPersist)
                {
                    DevTrace.Write($"[SQX Import] Unexpected persistence error: {exPersist.Message}");
                }
                // ----------------------------------------------------------------------------------------------------------------





                // 10) Register profile in ConfigModel and select it
                ConfigModel.Instance.AddProfile(profile);
                ConfigModel.Instance.SaveProfiles();
                SharedModel.Instance.SelectedProfile = profile;

                _snackbarService.Show("SQX Panel Imported", sqxFile, ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
            }
            catch (Exception exOuter)
            {
                // Any unexpected error during steps 2-10
                _snackbarService.Show("Import Failed", $"Unable to import SQX: {exOuter.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(6));
                DevTrace.Write($"[SQX Import] Exception during import steps: {exOuter}");
            }
            finally
            {
                // Clean up the temp folder unless debugging is enabled
                if (!keepTempForDebug)
                {
                    try
                    {
                        DeleteDirectoryWithRetries(tempFolder);
                    }
                    catch (Exception exDel)
                    {
                        DevTrace.Write($"[SQX Import] Failed to delete temp folder '{tempFolder}': {exDel}");
                        // If you don't have DeleteDirectoryWithRetries, you can use:
                        // try { Directory.Delete(tempFolder, true); } catch { /* ignore */ }
                    }
                }
                else
                {
                    DevTrace.Write($"[SQX Import] Keeping temp folder for debug: {tempFolder}");
                }
            }
        }







        // +++++++++++++++++++ SQX Temp folder Deletion helper +++++++++++++++++
        // --- Helper: recursive delete with retries and readonly clearing ---
        static bool DeleteDirectoryWithRetries(string folder, int retries = 3, int delayMs = 200)
        {
            if (string.IsNullOrWhiteSpace(folder)) return true; // nothing to do
            if (!Directory.Exists(folder)) return true;

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    // remove read-only attributes from files & dirs to avoid permission issues
                    try
                    {
                        foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var fi = new FileInfo(f);
                                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                                {
                                    fi.Attributes &= ~FileAttributes.ReadOnly;
                                }
                            }
                            catch { /* ignore per-file errors */ }
                        }

                        foreach (var d in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var di = new DirectoryInfo(d);
                                if ((di.Attributes & FileAttributes.ReadOnly) != 0)
                                {
                                    di.Attributes &= ~FileAttributes.ReadOnly;
                                }
                            }
                            catch { /* ignore per-dir errors */ }
                        }
                    }
                    catch { /* ignore attribute clearing errors */ }

                    Directory.Delete(folder, recursive: true);
                    return true;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(delayMs);
                }
                catch (Exception)
                {
                    // unexpected — wait and retry
                    Thread.Sleep(delayMs);
                }
            }

            // Final attempt: try to delete files individually then the folder
            try
            {
                foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    catch { /* ignore per-file */ }
                }

                // delete directories bottom-up
                var dirs = Directory.GetDirectories(folder, "*", SearchOption.AllDirectories)
                                    .OrderByDescending(d => d.Length)
                                    .ToList();
                foreach (var d in dirs)
                {
                    try
                    {
                        var di = new DirectoryInfo(d);
                        di.Attributes &= ~FileAttributes.ReadOnly;
                        Directory.Delete(d, recursive: true);
                    }
                    catch { /* ignore */ }
                }

                // final attempt to delete root
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }

                return !Directory.Exists(folder);
            }
            catch
            {
                return false;
            }
        }






        // --- Helpers used by SPZIP import (add these to the class) ---

        private static void CopyFileWithRetryEnsure(string src, string dest, int retries = 3, int delayMs = 150)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? "");
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using (var rs = File.OpenRead(src))
                    using (var ds = File.Create(dest))
                    {
                        rs.CopyTo(ds);
                    }
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
            }
            try { File.Copy(src, dest, overwrite: true); } catch { /* ignore */ }
        }

        private static string MakeUniqueFilename(string folder, string filename)
        {
            var name = Path.GetFileNameWithoutExtension(filename);
            var ext = Path.GetExtension(filename);
            int n = 1;
            string candidate;
            do
            {
                candidate = $"{name}__dup{n}{ext}";
                n++;
            } while (File.Exists(Path.Combine(folder, candidate)));
            return candidate;
        }

        private static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue)) return input;
            int index = 0;
            var sb = new System.Text.StringBuilder();
            while (true)
            {
                int i = input.IndexOf(oldValue, index, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                {
                    sb.Append(input.Substring(index));
                    break;
                }
                sb.Append(input.Substring(index, i - index));
                sb.Append(newValue);
                index = i + oldValue.Length;
            }
            return sb.ToString();

            return System.Text.RegularExpressions.Regex.Replace(input ?? "", System.Text.RegularExpressions.Regex.Escape(oldValue),
        newValue, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }






        



                

        // Write a ZipArchiveEntry to disk (create directories) with retries
        public static void ExtractEntryToFileWithRetry(ZipArchiveEntry entry, string destFile, int retries = 3, int delayMs = 150)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destFile) ?? Path.GetTempPath());

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using var entryStream = entry.Open();
                    using var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    entryStream.CopyTo(fs);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
            }

            // final attempt
            using var entryStreamFinal = entry.Open();
            using var fsFinal = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStreamFinal.CopyTo(fsFinal);
        }




        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();

            int length = hex.Length;
            byte[] bytes = new byte[length / 2];

            for (int i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }

        private static string SaveEmbeddedImage(Guid profileGuid, string fileName, string hexData)
        {
            string assetsRoot = Path.Combine(
            AppPaths.Assets, profileGuid.ToString());

            if (!Directory.Exists(assetsRoot))
                Directory.CreateDirectory(assetsRoot);

            string safeFileName = fileName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeFileName = safeFileName.Replace(c, '_');
            }

            string fullPath = Path.Combine(assetsRoot, safeFileName);

            byte[] imageBytes = HexToBytes(hexData);
            File.WriteAllBytes(fullPath, imageBytes);

            // We only return the file name, because ImageDisplayItem uses it as a relative path
            return safeFileName;
        }


        private static void AddBackgroundImageDisplayItem(
        Profile profile,
        string savedFileName,
        int x,
        int y,
        int width,
        int height)
        {
            // Use only the file name, never a full path
            string nameOnly = System.IO.Path.GetFileName(savedFileName);

            var bgItem = new ImageDisplayItem(nameOnly, profile)
            {
                FilePath = nameOnly,  // Relative file name
                RelativePath = true,  // So CalculatedPath = %LOCALAPPDATA%\SynQPanel\assets\<Guid>\<FilePath>
                X = x,
                Y = y,
                Width = width,   // 0 is OK; EvaluateSize will pull real size from image
                Height = height, // 0 is OK
                Scale = 100,
                Layer = false
            };

            // 🔑 This mirrors the pattern you already have:
            // var item = new ImageDisplayItem("Image", selectedProfile);
            // SharedModel.Instance.AddDisplayItem(item);
            SharedModel.Instance.AddDisplayItem(bgItem);
        }


        private static void AddRslcdTestBackground(Profile profile, string fileName)
        {
            // Resolve full path to the asset
            var assetFolder = Path.Combine(
            AppPaths.Assets, profile.Guid.ToString());

            var fullPath = Path.Combine(assetFolder, fileName);

            if (!File.Exists(fullPath))
            {
                RslcdDebug.Log($"[RSLCD] BG file NOT found on disk: {fullPath}");
                return;
            }

            // Try to get the real pixel size of the image
            int width = 0;
            int height = 0;

            try
            {
                using (var bmp = new System.Drawing.Bitmap(fullPath))
                {
                    width = bmp.Width;
                    height = bmp.Height;
                }
                RslcdDebug.Log($"[RSLCD] BG image size: {width}x{height} from {fullPath}");
            }
            catch (Exception ex)
            {
                RslcdDebug.Log($"[RSLCD] Failed to read image size for {fullPath}: {ex}");
                // fallback: if this fails, use panel size-ish defaults
                width = 1024;
                height = 600;
            }

            // IMPORTANT: use the constructor that includes filePath + relative flag
            var bgItem = new ImageDisplayItem("RSLCD_BG_TEST", profile, fileName, true)
            {
                X = 0,
                Y = 0,
                Width = width,
                Height = height,
                Scale = 100,
                Layer = false
            };

            SharedModel.Instance.AddDisplayItem(bgItem);

            RslcdDebug.Log(
                $"[RSLCD] Added test BG item at (0,0) size={width}x{height}, FilePath='{bgItem.FilePath}', RelativePath={bgItem.RelativePath}, Profile={profile.Guid}");
        }


        // Safe, fully-qualified Trace that uses SharedModel.Instance.DisplayItems (paste in same file as ButtonImportProfile_Click)
        private static void TraceRslcdImages(SynQPanel.Models.Profile profile, string label)
        {
            if (profile == null)
            {
                RslcdDebug.Log($"[RSLCD-TRACE] {label}: profile is null");
                return;
            }

            // 🔍 All display items live in SharedModel, not on Profile
            var allItems = SharedModel.Instance.DisplayItems;
            if (allItems == null)
            {
                RslcdDebug.Log($"[RSLCD-TRACE] {label}: SharedModel.DisplayItems is null");
                return;
            }

            // Only items belonging to this profile
            var items = allItems
                .OfType<SynQPanel.Models.ImageDisplayItem>()   // fully-qualified to avoid 'using' ambiguity
                .Where(di => di.ProfileGuid == profile.Guid)
                .ToList();

            RslcdDebug.Log($"[RSLCD-TRACE] {label}: {items.Count} ImageDisplayItem(s) for profile {profile.Guid}");

            foreach (var img in items)
            {
                RslcdDebug.Log(
                    $"[RSLCD-TRACE]   Name='{img.Name}', FilePath='{img.FilePath}', Rel={img.RelativePath}, Pos=({img.X},{img.Y}), Size={img.Width}x{img.Height}");
            }
        }

        private static void TraceImportedSensors(Profile profile, string label)
        {
            if (profile == null)
            {
                MapLogger.Trace($"[MAP-TRACE] {label}: profile is null");
                return;
            }

            // All display items live on SharedModel.Instance.DisplayItems
            var allItems = SharedModel.Instance.DisplayItems;
            if (allItems == null)
            {
                MapLogger.Trace($"[MAP-TRACE] {label}: SharedModel.DisplayItems is null");
                return;
            }

            // Select items that implement ISensorItem
            var sensorItems = allItems
                .Where(di => di is ISensorItem)
                .Cast<ISensorItem>()
                .Where(si =>
                {
                    // pick only those belonging to this profile (use ProfileGuid if present on the concrete type)
                    // Some ISensorItem implementations inherit from DisplayItem and include ProfileGuid property.
                    // Try to get ProfileGuid via reflection fallback if interface doesn't expose it.
                    var asDisplayItem = si as DisplayItem;
                    if (asDisplayItem != null)
                        return asDisplayItem.ProfileGuid == profile.Guid;

                    // reflection fallback (safe): try to read ProfileGuid property if present
                    var prop = si.GetType().GetProperty("ProfileGuid");
                    if (prop != null && prop.PropertyType == typeof(Guid))
                    {
                        var value = (Guid)prop.GetValue(si);
                        return value == profile.Guid;
                    }

                    return false;
                })
                .ToList();

            MapLogger.Trace($"[MAP-TRACE] {label}: {sensorItems.Count} ISensorItem(s) for profile {profile.Guid}");

            foreach (var si in sensorItems)
            {
                // Basic properties
                string typeName = si.GetType().Name;
                string sensorType = "AIDA";
                string idInfo = "";

                try
                {
                    sensorType = si.SensorType.ToString();
                }
                catch { /* ignore */ }

                try
                {
                    // AIDA ONLY – reflect only AidaSensorTreeItem-style fields
                    var idProp = si.GetType().GetProperty("Id");
                    var nameProp = si.GetType().GetProperty("SensorName") ??
                                   si.GetType().GetProperty("Label") ??
                                   si.GetType().GetProperty("Name");
                    var unitProp = si.GetType().GetProperty("Unit");
                    var typeProp = si.GetType().GetProperty("Type");

                    string id = idProp?.GetValue(si)?.ToString() ?? "";
                    string name = nameProp?.GetValue(si)?.ToString() ?? "";
                    string unit = unitProp?.GetValue(si)?.ToString() ?? "";
                    string aidaType = typeProp?.GetValue(si)?.ToString() ?? "";

                    idInfo = $"AIDA(Id='{id}', Type='{aidaType}', Unit='{unit}')";

                    MapLogger.Trace(
                        $"[AIDA-TRACE] Type={typeName}, SensorType={sensorType}, Name='{name}', IDs=({idInfo})"
                    );
                }
                catch (Exception ex)
                {
                    MapLogger.Trace($"[AIDA-TRACE] ERROR examining sensor item: {ex}");
                }
            }

        }





        public static class RslcdDebug
        {
            public static void Log(string message)
            {
                try
                {
                    if (ConfigModel.Instance?.EnableRslcdDebug == true)
                    {
                        // Write to debug output (Visual Studio Output window).
                        Debug.WriteLine("[RSLCD] " + message);
                    }
                }
                catch
                {
                    // Be defensive — logging must not throw
                }
            }

            // Optional helper to log exceptions with context
            public static void LogException(string context, Exception ex)
            {
                try
                {
                    if (ConfigModel.Instance?.EnableRslcdDebug == true)
                    {
                        Debug.WriteLine($"[RSLCD] EXCEPTION: {context} -- {ex.GetType().Name}: {ex.Message}");
                        Debug.WriteLine(ex.StackTrace);
                    }
                }
                catch { }
            }
        }


        private void ShareRslcdBackgroundAcrossProfiles(Profile profile, string rslcdPath, string backgroundPath)
        {
            try
            {
                var allProfiles = new[] { profile! };

                foreach (var p in allProfiles)
                {
                    if (!string.Equals(p.ImportedSensorPanelPath,
                                       rslcdPath,
                                       StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Don’t override if user already set a BG
                    if (!string.IsNullOrWhiteSpace(p.BackgroundImagePath))
                        continue;

                    p.BackgroundImagePath = backgroundPath;
                    RslcdDebug.Log(
                        $"Set BackgroundImagePath for profile {p.Guid} -> {backgroundPath}");
                }
            }
            catch (Exception ex)
            {
                RslcdDebug.Log(
                    "[RSLCD-SHARE] Error while sharing background across profiles: " + ex);
            }
        }

        // Copies a background image into the specific profile's asset folder
        // and returns the full path inside that profile's folder.
        private static string EnsureImageInProfileAssets(SynQPanel.Models.Profile profile, string sourcePath)
        {
            if (profile == null || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return sourcePath;

            // Target folder: %LOCALAPPDATA%/SynQPanel/assets/{guid}/
            string targetFolder = Path.Combine(
            AppPaths.Assets, profile.Guid.ToString());

            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            // Destination file (same filename)
            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(targetFolder, fileName);

            try
            {
                // Copy only if missing or outdated
                File.Copy(sourcePath, destPath, true); // overwrite = true (safe)
            }
            catch (Exception ex)
            {
                RslcdDebug.Log("[RSLCD] ERROR copying BG into profile assets: " + ex);
                return sourcePath;
            }

            return destPath;
        }

    }
}
