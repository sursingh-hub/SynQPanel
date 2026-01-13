using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Aida;
using SynQPanel.Drawing;
using SynQPanel.Extensions;
using SynQPanel.Models;
using SynQPanel.Utils;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Wpf.Ui.Controls;
using static SynQPanel.Views.Pages.ProfilesPage;


namespace SynQPanel

{
    public partial class SharedModel : ObservableObject
    {
        private static readonly ILogger Logger = Log.ForContext<SharedModel>();
        private static readonly Lazy<SharedModel> lazy = new(() => new SharedModel());

        public static SharedModel Instance { get { return lazy.Value; } }

        
        private int _webserverFrameRate = 0;
        public int WebserverFrameRate
        {
            get { return _webserverFrameRate; }
            set
            {
                SetProperty(ref _webserverFrameRate, value);
            }
        }

        private int _webserverFrameTime = 0;
        public int WebserverFrameTime
        {
            get { return _webserverFrameTime; }
            set
            {
                SetProperty(ref _webserverFrameTime, value);
            }
        }

        private bool _placementControlExpanded = false;
        public bool PlacementControlExpanded
        {
            get { return _placementControlExpanded; }
            set
            {
                SetProperty(ref _placementControlExpanded, value);
            }
        }

        private Profile? _selectedProfile;
        


        public Profile? SelectedProfile
        {
            get { return _selectedProfile; }
            set
            {
                SetProperty(ref _selectedProfile, value);

                OnPropertyChanged(nameof(DisplayItems));
                NotifySelectedItemChange();
            }


        }

        private readonly ConcurrentDictionary<Guid, ObservableCollection<DisplayItem>> ProfileDisplayItems = [];
        private readonly ConcurrentDictionary<Guid, Debouncer> _debouncers = [];
        private readonly ConcurrentDictionary<Guid, ImmutableList<DisplayItem>> ProfileDisplayItemsCopy = [];

        public ObservableCollection<DisplayItem> DisplayItems => GetProfileDisplayItems();

        private ObservableCollection<DisplayItem> GetProfileDisplayItems()
        {
            if(SelectedProfile is Profile profile)
            {
                return GetProfileDisplayItems(profile);
            }

            return [];
        }

        private ObservableCollection<DisplayItem> GetProfileDisplayItems(Profile profile)
        {
            return ProfileDisplayItems.GetOrAdd(profile.Guid, guid =>
            {
                var collection = new ObservableCollection<DisplayItem>();
                collection.CollectionChanged += (s, e) =>
                {
                    if (s is ObservableCollection<DisplayItem> observableCollection)
                    {
                        var debouncer = _debouncers.GetOrAdd(guid, guid => new Debouncer());
                        debouncer.Debounce(() => ProfileDisplayItemsCopy[guid] = [.. observableCollection]);
                    }
                };
                _ = ReloadDisplayItems(profile);

                return collection;
            });
        }

        public async Task ReloadDisplayItems()
        {
            if (SelectedProfile is Profile profile)
            {
                await ReloadDisplayItems(profile);
            }
        }

        /// <summary>
        /// Provides thread-safe access to the DisplayItems collection for the currently selected profile.
        /// The action is executed on the UI thread if available.
        /// </summary>
        /// <param name="action">The action to perform with the DisplayItems collection</param>
        public void AccessDisplayItems(Action<ObservableCollection<DisplayItem>> action)
        {
            if (SelectedProfile is not Profile profile)
            {
                return;
            }

            AccessDisplayItems(profile, action);
        }

        /// <summary>
        /// Provides thread-safe access to the DisplayItems collection for a specific profile.
        /// The action is executed on the UI thread if available.
        /// </summary>
        /// <param name="profile">The profile whose DisplayItems to access</param>
        /// <param name="action">The action to perform with the DisplayItems collection</param>
        public void AccessDisplayItems(Profile profile, Action<ObservableCollection<DisplayItem>> action)
        {
            var collection = GetProfileDisplayItems(profile);

            if (collection == null)
            {
                return;
            }

            if (Application.Current.Dispatcher is Dispatcher dispatcher)
            {
                if (dispatcher.CheckAccess())
                {
                    action(collection);
                }
                else
                {
                    dispatcher.Invoke(() =>
                    {
                        action(collection);
                    });
                }
            }
        }

        private async Task ReloadDisplayItems(Profile profile)
        {
            var displayItems = await LoadDisplayItemsAsync(profile);

            AccessDisplayItems(profile, collection =>
            {
                collection.Clear();

                foreach (var item in displayItems)
                    collection.Add(item);
            });
        }

        public DisplayItem? SelectedItem
        {
            get
            {
                return SelectedItems.FirstOrDefault();
            }
            set
            {
                foreach (var selectedItem in SelectedItems)
                {
                    if (selectedItem != value)
                    {
                        selectedItem.Selected = false;
                    }
                }

                if (value is DisplayItem displayItem)
                {
                    displayItem.Selected = true;
                }

                NotifySelectedItemChange();
            }
        }

        public void NotifySelectedItemChange()
        {
            OnPropertyChanged(nameof(SelectedItem));
            OnPropertyChanged(nameof(IsItemSelected));
            OnPropertyChanged(nameof(IsSingleItemSelected));
            OnPropertyChanged(nameof(IsSelectedItemMovable));
            OnPropertyChanged(nameof(IsSelectedItemsMovable));
        }

        public ImmutableList<DisplayItem> SelectedItems
        {
            get
            {
                ImmutableList<DisplayItem> result = [];
                AccessDisplayItems(items =>
                {
                    result = result.AddRange(items
                        .SelectMany<DisplayItem, DisplayItem>(item =>
                            item is GroupDisplayItem group && group.DisplayItems is { } groupItems
                                ? [group, .. groupItems]
                                : [item])
                        .Where(item => item.Selected));
                });
                return result;
            }
        }

        public ImmutableList<DisplayItem> SelectedVisibleItems
        {
            get
            {
                return [.. SelectedItems.Where(item => item.Selected && !item.Hidden)];
            }
        }
        public bool IsSelectedItemsMovable => SelectedItems.FindAll(item => item is not GroupDisplayItem).Count > 0;

        public bool IsSelectedItemMovable => SelectedItem is not null && SelectedItem is not GroupDisplayItem;

        public bool IsItemSelected => SelectedItem != null;
        public bool IsSingleItemSelected => SelectedItems.Count == 1;

        [ObservableProperty]
        private int _moveValue = 5;

        private SharedModel()
        { }

        public void AddDisplayItem(DisplayItem newDisplayItem)
        {
            if (SelectedProfile is Profile profile)
            {
                AccessDisplayItems(profile, displayItems =>
                {
                    bool addedInGroup = false;

                    if (newDisplayItem is not GroupDisplayItem && SelectedItem is DisplayItem selectedItem)
                    {
                        // SelectedItem is a group — add directly to it
                        if (selectedItem is GroupDisplayItem group && !group.IsLocked)
                        {
                            group.DisplayItems.Add(newDisplayItem);
                            addedInGroup = true;
                        }
                        else
                        {
                            //SelectedItem is inside a group — find its parent
                            _ = FindParentCollection(selectedItem, out var parentGroup);
                            if (parentGroup is not null && !parentGroup.IsLocked)
                            {
                                parentGroup.DisplayItems.Add(newDisplayItem);
                                addedInGroup = true;
                            }
                        }
                    }

                    if (!addedInGroup)
                    {
                        displayItems.Add(newDisplayItem);
                    }

                    SelectedItem = newDisplayItem;
                });
            }
        }


        public void RemoveDisplayItem(DisplayItem displayItem)
        {
            if (SelectedProfile is Profile profile)
            {
                // Remember its original .sensorpanel line index (if known)
                if (displayItem.OriginalLineIndex.HasValue && displayItem.OriginalLineIndex.Value >= 0)
                {
                    SensorPanelSaver.RegisterDeletedLineIndex(displayItem.OriginalLineIndex.Value);
                }




                AccessDisplayItems(profile, displayItems =>
                {
                    _ = FindParentCollection(displayItem, out var parentGroup);
                    if (parentGroup is not null)
                    {
                        int index = parentGroup.DisplayItems.IndexOf(displayItem);
                        if (index >= 0)
                        {
                            parentGroup.DisplayItems.RemoveAt(index);

                            if (parentGroup.DisplayItems.Count > 0)
                            {
                                parentGroup.DisplayItems[Math.Clamp(index, 0, parentGroup.DisplayItems.Count - 1)].Selected = true;
                            }
                            else
                            {
                                parentGroup.Selected = true;
                            }
                        }
                    }
                    else
                    {
                        // Top-level item
                        int index = displayItems.IndexOf(displayItem);
                        if (index >= 0)
                        {
                            displayItems.RemoveAt(index);

                            if (displayItems.Count > 0)
                            {
                                displayItems[Math.Clamp(index, 0, displayItems.Count - 1)].Selected = true;
                            }
                        }
                    }
                });
            }
        }

        
        // in SharedModel (add near other public APIs)
        public List<DisplayItem> LoadDisplayItemsForProfile(Profile profile)
        {
            // Calls your internal loader (LoadDisplayItemsFromFile) that already exists in the same class.
            // If the underlying method is private, this wrapper will compile because it's inside SharedModel.
            return LoadDisplayItemsFromFile(profile);
        }

        public void SaveDisplayItemsForProfile(Profile profile, List<DisplayItem> items)
        {
            // Calls your existing SaveDisplayItems(profile, items) method (present in this class).
            SaveDisplayItems(profile, items);
        }

        public GroupDisplayItem? GetParent(DisplayItem displayItem)
        {
            FindParentCollection(displayItem, out var result);
            return result;
        }

        private ObservableCollection<DisplayItem>? FindParentCollection(DisplayItem item, out GroupDisplayItem? parentGroup)
        {
            parentGroup = null;

            if (DisplayItems == null)
                return null;

            if (DisplayItems.Contains(item))
                return DisplayItems;

            foreach (var group in DisplayItems.OfType<GroupDisplayItem>())
            {
                if (group.DisplayItems != null && group.DisplayItems.Contains(item))
                {
                    parentGroup = group;
                    return group.DisplayItems;
                }
            }

            return null;
        }

        public void PushDisplayItemBy(DisplayItem displayItem, int count)
        {
            if (SelectedProfile is Profile profile)
            {
                AccessDisplayItems(profile, displayItems =>
                {
                    // Find the parent collection and group (if any)
                    var parentCollection = FindParentCollection(displayItem, out GroupDisplayItem? parentGroup);

                    if (parentCollection == null)
                        return;

                    int index = parentCollection.IndexOf(displayItem);
                    int newIndex = index + count;

                    // Moving out of group (up or down)
                    if (parentGroup != null && !parentGroup.IsLocked)
                    {
                        if (newIndex < 0)
                        {
                            int groupIndex = displayItems.IndexOf(parentGroup);
                            if (groupIndex >= 0)
                            {
                                parentCollection.RemoveAt(index);
                                displayItems.Insert(groupIndex, displayItem);
                            }
                            return;
                        }

                        if (newIndex >= parentCollection.Count)
                        {
                            int groupIndex = displayItems.IndexOf(parentGroup);
                            if (groupIndex >= 0)
                            {
                                parentCollection.RemoveAt(index);
                                displayItems.Insert(groupIndex + 1, displayItem);
                            }
                            return;
                        }
                    }

                    // Moving into a group
                    if (displayItem is not GroupDisplayItem && parentGroup == null)
                    {
                        int targetIndex = index + count;
                        if (targetIndex >= 0 && targetIndex < displayItems.Count)
                        {
                            var target = displayItems[targetIndex];
                            if (target is GroupDisplayItem targetGroup && !targetGroup.IsLocked && targetGroup.DisplayItems != null)
                            {
                                parentCollection.RemoveAt(index);
                                targetGroup.DisplayItems.Insert(count > 0 ? 0 : targetGroup.DisplayItems.Count, displayItem);
                                targetGroup.IsExpanded = true;
                                return;
                            }
                        }
                    }

                    // Normal move within the same collection
                    if (newIndex >= 0 && newIndex < parentCollection.Count)
                    {
                        parentCollection.Move(index, newIndex);
                        if (parentGroup is GroupDisplayItem)
                        {
                            parentGroup.IsExpanded = true;
                        }
                    }
                });
            }
        }

        public void PushDisplayItemTo(DisplayItem displayItem, DisplayItem target)
        {
            if (displayItem == null || target == null || displayItem == target)
                return;


            if (SelectedProfile is Profile profile)
            {
                AccessDisplayItems(profile, displayItems =>
                {
                    var sourceCollection = FindParentCollection(displayItem, out var sourceGroupDisplayItem);
                    var targetCollection = FindParentCollection(target, out var targetGroupDisplayItem);

                    if (sourceCollection == null || targetCollection == null || sourceCollection != targetCollection)
                        return;

                    int sourceIndex = sourceCollection.IndexOf(displayItem);
                    int targetIndex = targetCollection.IndexOf(target);

                    if (sourceCollection == targetCollection)
                    {
                        // Same collection: simple move
                        sourceCollection.Move(sourceIndex, targetIndex + 1);
                    }
                });
            }
        }

        public void PushDisplayItemToTop(DisplayItem displayItem)
        {
            if (SelectedProfile is Profile profile)
            {
                AccessDisplayItems(profile, displayItems =>
                {
                    var sourceCollection = FindParentCollection(displayItem, out var groupDisplayItem);
                    if (sourceCollection == null)
                        return;

                    int currentIndex = sourceCollection.IndexOf(displayItem);

                    if (sourceCollection == displayItems)
                    {
                        if (currentIndex > 0)
                        {
                            displayItems.Move(currentIndex, 0);
                        }
                    }
                    else
                    {
                        if (groupDisplayItem != null && !groupDisplayItem.IsLocked)
                        {
                            sourceCollection.RemoveAt(currentIndex);
                            displayItems.Insert(0, displayItem);
                        }
                        else
                        {
                            if (currentIndex != 0)
                            {
                                sourceCollection.Move(currentIndex, 0);
                            }
                        }
                    }
                });
            }
        }

        public void PushDisplayItemToEnd(DisplayItem displayItem)
        {
            if (SelectedProfile is Profile profile)
            {
                AccessDisplayItems(profile, displayItems =>
                {
                    var sourceCollection = FindParentCollection(displayItem, out var groupDisplayItem);
                    if (sourceCollection == null)
                        return;

                    int currentIndex = sourceCollection.IndexOf(displayItem);

                    if (sourceCollection == displayItems)
                    {
                        if (currentIndex < displayItems.Count - 1)
                        {
                            displayItems.Move(currentIndex, displayItems.Count - 1);
                        }
                    }
                    else
                    {
                        if (groupDisplayItem != null && !groupDisplayItem.IsLocked)
                        {
                            sourceCollection.RemoveAt(currentIndex);
                            displayItems.Add(displayItem);
                        }
                        else
                        {
                            if (currentIndex != sourceCollection.Count - 1)
                            {
                                sourceCollection.Move(currentIndex, sourceCollection.Count - 1);
                            }
                        }
                    }
                });
            }
        }




        
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _roundTripInProgress = new();

        private static void SaveDisplayItems(Profile profile, ICollection<DisplayItem> displayItems)
        {
            if (profile == null) return;

            // Reentrancy guard: if this profile is already being saved/exported, skip to avoid loops.
            if (!_roundTripInProgress.TryAdd(profile.Guid, 0))
            {
                DevTrace.Write($"[SaveDisplayItems] Reentrancy detected for profile {profile.Guid} - skipping nested round-trip.");
                return;
            }

            try
            {
                // Minimal entry log (file append). Keep as small as possible to avoid heavy IO.
                try
                {
                    var dbgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "synqpanel_debug.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(dbgPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                    File.AppendAllText(dbgPath, $"[SaveDisplayItems] Enter - profile='{profile?.Name}' GUID={profile?.Guid} items={displayItems?.Count ?? 0} {DateTime.Now:O}{Environment.NewLine}");
                }
                catch { /* ignore logging errors */ }

                // 0) Save local profile displayitems xml (unchanged behavior)
                var profileFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles");
                if (!Directory.Exists(profileFolder)) Directory.CreateDirectory(profileFolder);

                var fileName = Path.Combine(profileFolder, profile.Guid + ".xml");

                XmlSerializer xs = new(typeof(List<DisplayItem>),
                    new Type[] {
                typeof(GroupDisplayItem), typeof(BarDisplayItem), typeof(GraphDisplayItem),
                typeof(DonutDisplayItem), typeof(TableSensorDisplayItem), typeof(SensorDisplayItem),
                typeof(TextDisplayItem), typeof(ClockDisplayItem), typeof(CalendarDisplayItem),
                typeof(SensorImageDisplayItem), typeof(ImageDisplayItem), typeof(HttpImageDisplayItem),
                typeof(GaugeDisplayItem), typeof(ShapeDisplayItem)
                    });

                var settings = new XmlWriterSettings() { Encoding = Encoding.UTF8, Indent = true };
                using (var wr = XmlWriter.Create(fileName, settings))
                {
                    xs.Serialize(wr, displayItems.ToList()); // ToList() avoids ICollection serialization pitfalls
                }

                DevTrace.Write($"[SaveDisplayItems] Saved DisplayItems local XML for profile {profile.Guid}.");

                // --- round-trip: panel save + package re-export for .spzip and .sqx ---
                try
                {
                    // 1) Save back to the original .sensorpanel / .sp2 if present
                    if (!string.IsNullOrWhiteSpace(profile?.ImportedSensorPanelPath))
                    {
                        try
                        {
                            string panelPath = profile.ImportedSensorPanelPath;
                            if (File.Exists(panelPath))
                            {
                                var ext = Path.GetExtension(panelPath).ToLowerInvariant();

                                // Only save when expected
                                if (ext == ".sensorpanel" || ext == ".sp2" || ext == ".xml")
                                {
                                    SensorPanelSaver.SaveSensorPanel(panelPath, displayItems);
                                    DevTrace.Write($"[SaveDisplayItems] Saved panel to '{panelPath}'.");
                                }
                                else
                                {
                                    DevTrace.Write($"[SaveDisplayItems] ImportedSensorPanelPath ext '{ext}' not handled for direct panel save (skipping).");
                                }
                            }
                            else
                            {
                                DevTrace.Write($"[SaveDisplayItems] Panel path not found: '{panelPath}' (skipping panel save).");
                            }
                        }
                        catch (Exception exPanel)
                        {
                            DevTrace.Write($"[SaveDisplayItems] Error saving panel: {exPanel.Message}");
                        }
                    }

                    // 2) If package path exists, handle package-specific re-exports (SPZIP, SQX)
                    if (!string.IsNullOrWhiteSpace(profile?.ImportedSensorPackagePath))
                    {
                        string pkgPath = profile.ImportedSensorPackagePath;
                        string pkgExt = Path.GetExtension(pkgPath).ToLowerInvariant();

                        // Helper: create single simple .bak (overwrite) next to target
                        //static void CreateSimpleBak(string target)
                        //{
                        //    try
                        //    {
                        //        if (File.Exists(target))
                        //        {
                        //            var dir = Path.GetDirectoryName(target) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        //            var baseFile = Path.GetFileName(target);
                        //            var bakSimple = Path.Combine(dir, baseFile + ".bak");
                        //            File.Copy(target, bakSimple, overwrite: true);
                        //            DevTrace.Write($"[SaveDisplayItems] Created backup '{bakSimple}'.");
                        //        }
                        //        else
                        //        {
                        //            DevTrace.Write($"[SaveDisplayItems] Backup skipped; target does not exist: '{target}'.");
                        //        }
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        DevTrace.Write($"[SaveDisplayItems] Backup creation failed (continuing): {ex.Message}");
                        //    }
                        //}

                        if (pkgExt == ".spzip")
                        {
                            try
                            {
                                //CreateSimpleBak(pkgPath);

                                // Use your working SPZIP exporter (expected to overwrite pkgPath)
                                try
                                {
                                    string? result = SpzipExporter.ExportProfileAsSpzip(profile, pkgPath);
                                    if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
                                        DevTrace.Write($"[SaveDisplayItems] SPZIP re-exported to '{result}'.");
                                    else
                                        DevTrace.Write($"[SaveDisplayItems] SPZIP re-export failed for '{pkgPath}'.");
                                }
                                catch (Exception exSpzip)
                                {
                                    DevTrace.Write($"[SaveDisplayItems] SPZIP re-export exception: {exSpzip.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                DevTrace.Write($"[SaveDisplayItems] SPZIP branch error: {ex.Message}");
                            }
                        }
                        else if (pkgExt == ".sqx")
                        {
                            DevTrace.Write("[SaveDisplayItems] Detected .sqx. Performing local-only save to GUID assets (no package overwrite).");

                            try
                            {
                                // Ensure GUID asset folder exists
                                string profileAssetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "assets", profile.Guid.ToString());
                                Directory.CreateDirectory(profileAssetRoot);

                                // 1) If ImportedSensorPanelPath points to a GUID-local Profile.xml, write the Profile.xml there.
                                //    If it points to a .sp2/.sensorpanel file, save via SensorPanelSaver (existing behavior).
                                var panelPath = profile.ImportedSensorPanelPath;
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(panelPath))
                                    {
                                        var ext = Path.GetExtension(panelPath).ToLowerInvariant();
                                        if (ext == ".sp2" || ext == ".sensorpanel")
                                        {
                                            // Save panel textual content back to the existing panel path (GUID-local .sp2/.sensorpanel expected)
                                            DevTrace.Write($"[SaveDisplayItems] Saving panel to '{panelPath}' via SensorPanelSaver.");
                                            try
                                            {
                                                SensorPanelSaver.SaveSensorPanel(panelPath, displayItems);
                                            }
                                            catch (Exception exPanelSave)
                                            {
                                                DevTrace.Write($"[SaveDisplayItems] Warning: SensorPanelSaver failed for '{panelPath}': {exPanelSave.Message}");
                                            }
                                        }
                                        else if (ext == ".xml")
                                        {
                                            // Save Profile.xml (serialize) into the indicated XML path
                                            DevTrace.Write($"[SaveDisplayItems] Writing Profile.xml to '{panelPath}'.");
                                            try
                                            {
                                                var xsProfile = new XmlSerializer(typeof(Profile));
                                                using var fsProfile = File.Create(panelPath);
                                                xsProfile.Serialize(fsProfile, profile);
                                            }
                                            catch (Exception exXml)
                                            {
                                                DevTrace.Write($"[SaveDisplayItems] Warning: failed writing Profile.xml to '{panelPath}': {exXml.Message}");
                                            }
                                        }
                                        else
                                        {
                                            DevTrace.Write($"[SaveDisplayItems] ImportedSensorPanelPath extension '{ext}' not handled for direct save (skipping direct panel write).");
                                        }
                                    }
                                    else
                                    {
                                        DevTrace.Write("[SaveDisplayItems] ImportedSensorPanelPath empty for .sqx – will persist Profile.xml into GUID assets instead.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SaveDisplayItems] Error while saving panel path: {ex}");
                                }

                                // 2) Ensure DisplayItems.xml (local canonical copy) is saved into profiles folder (already done earlier in method).
                                //    Also persist a copy of DisplayItems.xml into the GUID assets for provenance and later use.
                                try
                                {
                                    var profilesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles");
                                    string canonicalDisplayItems = Path.Combine(profilesFolder, profile.Guid + ".xml");
                                    if (File.Exists(canonicalDisplayItems))
                                    {
                                        var destDisplayItemsInAssets = Path.Combine(profileAssetRoot, "DisplayItems.xml");
                                        File.Copy(canonicalDisplayItems, destDisplayItemsInAssets, overwrite: true);
                                        DevTrace.Write($"[SaveDisplayItems] Copied DisplayItems.xml -> '{destDisplayItemsInAssets}'");
                                    }
                                    else
                                    {
                                        DevTrace.Write($"[SaveDisplayItems] Canonical DisplayItems.xml not found at '{canonicalDisplayItems}' (this should not happen).");
                                    }
                                }
                                catch (Exception exCopyDI)
                                {
                                    DevTrace.Write($"[SaveDisplayItems] Warning: failed copying DisplayItems.xml into assets: {exCopyDI.Message}");
                                }

                                // 3) Persist Profile.xml into the GUID assets folder (do not modify original package)
                                //    Prefer writing to an existing GUID-local profile file if ImportedSensorPanelPath pointed to it,
                                //    otherwise write a fresh Profile.xml into GUID assets and update ImportedSensorPanelPath to point to it.
                                try
                                {
                                    string destProfileXml = Path.Combine(profileAssetRoot, "Profile.xml");
                                    var xsx = new XmlSerializer(typeof(Profile));
                                    using (var fs = File.Create(destProfileXml))
                                    {
                                        xsx.Serialize(fs, profile);
                                    }
                                    DevTrace.Write($"[SaveDisplayItems] Persisted Profile.xml into assets -> '{destProfileXml}'");

                                    // ensure ImportedSensorPanelPath points to the GUID-local Profile.xml so subsequent saves know where to write
                                    try
                                    {
                                        profile.ImportedSensorPanelPath = destProfileXml;
                                        DevTrace.Write($"[SaveDisplayItems] Set ImportedSensorPanelPath -> '{destProfileXml}'");
                                    }
                                    catch (Exception exProv)
                                    {
                                        DevTrace.Write($"[SaveDisplayItems] Warning: unable to set ImportedSensorPanelPath: {exProv.Message}");
                                    }
                                }
                                catch (Exception exProfilePersist)
                                {
                                    DevTrace.Write($"[SaveDisplayItems] Warning: failed persisting Profile.xml into assets: {exProfilePersist.Message}");
                                }

                                // 4) IMPORTANT: Do NOT overwrite original .sqx package here — we intentionally avoid touching package files.
                                DevTrace.Write("[SaveDisplayItems] Local-only save for .sqx complete (no package overwrite).");

                                // Calling SQX Export after saving method

                                


                                //++++++++++++




                            }
                            catch (Exception ex)
                            {
                                DevTrace.Write("[SaveDisplayItems] .sqx local-save branch error: " + ex.Message);
                            }
                        }


                        else
                        {
                            DevTrace.Write($"[SaveDisplayItems] Package ext '{pkgExt}' not handled for round-trip re-export.");
                        }
                    }
                }
                catch (Exception exRound)
                {
                    DevTrace.Write($"[SaveDisplayItems] Unexpected round-trip exception: {exRound.Message}");
                }
            }
            finally
            {
                // Always clear the in-progress marker so subsequent saves can run.
                _roundTripInProgress.TryRemove(profile.Guid, out _);
            }
        }

        public void SaveDisplayItems(Profile profile)
        {
            if (profile != null)
            {
                var displayItems = GetProfileDisplayItemsCopy(profile);
                SaveDisplayItems(profile, displayItems);
            }
        }

        public void SaveDisplayItems()
        {
            if (SelectedProfile != null)
                SaveDisplayItems(SelectedProfile);
        }



        /// <summary>
        /// Export a Profile as .sqx by reusing the SPZIP exporter to gather assets reliably.
        /// Steps:
        ///  1) Create a temporary .spzip using SpzipExporter.ExportProfileAsSpzip(...)
        ///  2) Extract assets (images/videos) from that .spzip into a temp assets folder
        ///  3) Write Profile.xml and copy DisplayItems.xml (from saved profiles) into temp folder
        ///  4) Zip Profile.xml, DisplayItems.xml and assets/ into targetPath (.sqx)
        ///  5) Cleanup temps
        /// </summary>
        public string? ExportProfileAsSqx_UsingSpzip(Profile profile, string targetPath)
        {
            if (profile == null) return null;

            string tempRoot = Path.Combine(Path.GetTempPath(), "SynQPanel_SQX_Export_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            string tempSpzip = Path.Combine(tempRoot, "staging.spzip");
            string stagingExtract = Path.Combine(tempRoot, "staging_extracted");
            string stagingAssets = Path.Combine(tempRoot, "assets");
            Directory.CreateDirectory(stagingExtract);
            Directory.CreateDirectory(stagingAssets);

            try
            {
                // 0) Ensure display items saved (caller should already do this; kept for safety)
                try { ConfigModel.Instance.SaveProfiles(); } catch { /* ignore */ }
                try { SharedModel.Instance.SaveDisplayItems(); } catch { /* ignore */ }

                // 1) Use existing SPZIP exporter to create a temp .spzip that contains sp2 + assets
                //    SpzipExporter.ExportProfileAsSpzip(profile, path) returns path (as per your earlier code)
                string? createdSpzip = SpzipExporter.ExportProfileAsSpzip(profile, tempSpzip);
                if (string.IsNullOrWhiteSpace(createdSpzip) || !File.Exists(createdSpzip))
                {
                    // Fallback: nothing created - try to continue by grabbing assets directly from local app data
                    // (we will still attempt to build sqx from whatever is present)
                }
                else
                {
                    // 2) Extract spzip into stagingExtract
                    System.IO.Compression.ZipFile.ExtractToDirectory(createdSpzip, stagingExtract);

                    // 3) Gather assets: SPZIP usually puts the .sp2 plus image files next to it in the archive root.
                    //    Copy any files with known asset extensions into stagingAssets
                    var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp",
                ".mp4", ".mov", ".avi", ".mkv", ".webm", ".ogg", ".apng", ".svg"
            };

                    foreach (var file in Directory.GetFiles(stagingExtract, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        var ext = Path.GetExtension(file);
                        if (validExts.Contains(ext))
                        {
                            var dest = Path.Combine(stagingAssets, Path.GetFileName(file));
                            File.Copy(file, dest, overwrite: true);
                        }
                    }

                    // Also check subfolders just in case (some spzip variants may nest assets)
                    foreach (var file in Directory.GetFiles(stagingExtract, "*.*", SearchOption.AllDirectories))
                    {
                        var ext = Path.GetExtension(file);
                        if (validExts.Contains(ext))
                        {
                            var dest = Path.Combine(stagingAssets, Path.GetFileName(file));
                            if (!File.Exists(dest))
                                File.Copy(file, dest, overwrite: true);
                        }
                    }
                }

                // 4b) Copy from profile's asset folder into stagingAssets, but FILTER out textual files & backups
                try
                {
                    string profileAssetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                             "SynQPanel", "assets", profile.Guid.ToString());
                    if (Directory.Exists(profileAssetFolder))
                    {
                        // Accept only known asset extensions (images/videos/etc.)
                        var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp",
                            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".ogg", ".apng", ".svg"
                        };

                        foreach (var file in Directory.GetFiles(profileAssetFolder))
                        {
                            try
                            {
                                var ext = Path.GetExtension(file);
                                // Skip textual and backup extensions and also skip .sp2 (keep .sp2 in GUID but do not include in exported .sqx)
                                if (string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase)) continue;
                                if (string.Equals(ext, ".bak", StringComparison.OrdinalIgnoreCase)) continue;
                                if (ext != null && ext.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue; // any *.bak
                                if (string.Equals(ext, ".sp2", StringComparison.OrdinalIgnoreCase)) continue;

                                if (!validExts.Contains(ext)) continue; // skip unknown types

                                var dest = Path.Combine(stagingAssets, Path.GetFileName(file));
                                File.Copy(file, dest, overwrite: true);
                            }
                            catch (Exception exCopy)
                            {
                                DevTrace.Write($"[ExportProfileAsSqx] Warning copying asset '{file}': {exCopy.Message}");
                            }
                        }
                    }
                }
                catch { /* ignore profile-asset copy errors - fallback handled earlier */ }


                // 5) Create Profile.xml in staging root
                string profileXmlPath = Path.Combine(stagingExtract, "Profile.xml");
                {
                    var xs = new XmlSerializer(typeof(Profile));
                    using var fs = File.Create(profileXmlPath);
                    xs.Serialize(fs, profile);
                }

                // 6) Copy existing DisplayItems.xml into staging root (if it exists), otherwise create an empty one
                string profilesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles");
                string existingDisplayItemsPath = Path.Combine(profilesFolder, profile.Guid.ToString() + ".xml");
                string displayItemsTargetPath = Path.Combine(stagingExtract, "DisplayItems.xml");
                if (File.Exists(existingDisplayItemsPath))
                {
                    File.Copy(existingDisplayItemsPath, displayItemsTargetPath, overwrite: true);
                }
                else
                {
                    File.WriteAllText(displayItemsTargetPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?><DisplayItems></DisplayItems>");
                }

                // 7) Build the .sqx zip (Profile.xml + DisplayItems.xml + assets/*)
                if (File.Exists(targetPath)) File.Delete(targetPath);
                using (var zipFs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                using (var archive = new ZipArchive(zipFs, ZipArchiveMode.Create))
                {
                    // add Profile.xml
                    var pe = archive.CreateEntry("Profile.xml", CompressionLevel.Optimal);
                    using (var es = pe.Open())
                    using (var fs = File.OpenRead(profileXmlPath)) fs.CopyTo(es);

                    // add DisplayItems.xml
                    var de = archive.CreateEntry("DisplayItems.xml", CompressionLevel.Optimal);
                    using (var es = de.Open())
                    using (var fs = File.OpenRead(displayItemsTargetPath)) fs.CopyTo(es);

                    // add assets (if any)
                    if (Directory.Exists(stagingAssets))
                    {
                        foreach (var assetFile in Directory.GetFiles(stagingAssets))
                        {
                            var entryName = Path.Combine("assets", Path.GetFileName(assetFile)).Replace('\\', '/');
                            var ae = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                            using (var es = ae.Open())
                            using (var fs = File.OpenRead(assetFile)) fs.CopyTo(es);
                        }
                    }
                }

                return targetPath;
            }
            catch (Exception ex)
            {
                // Optionally log ex somewhere
                return null;
            }
            finally
            {
                // cleanup temp
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }






        public string? ExportProfile(Profile profile, string outputFolder)
        {
            var SelectedProfile = profile;

            if (SelectedProfile != null)
            {
                var exportFilePath = Path.Combine(outputFolder, SelectedProfile.Name.SanitizeFileName().Replace(" ", "_") + "-" + DateTimeOffset.Now.ToUnixTimeSeconds() + ".SynQPanel");


                if (File.Exists(exportFilePath))
                {
                    File.Delete(exportFilePath);
                }

                using (ZipArchive archive = ZipFile.Open(exportFilePath, ZipArchiveMode.Create))
                {
                    //add profile settings
                    var exportProfile = new Profile(SelectedProfile.Name, SelectedProfile.Width, SelectedProfile.Height)
                    {
                        ShowFps = SelectedProfile.ShowFps,
                        BackgroundColor = SelectedProfile.BackgroundColor,
                        Font = SelectedProfile.Font,
                        FontSize = SelectedProfile.FontSize,
                        Color = SelectedProfile.Color,
                        OpenGL = SelectedProfile.OpenGL,
                        FontScale = SelectedProfile.FontScale,
                    };

                    var entry = archive.CreateEntry("Profile.xml");

                    using (Stream entryStream = entry.Open())
                    {
                        XmlSerializer xs = new(typeof(Profile));
                        var settings = new XmlWriterSettings() { Encoding = Encoding.UTF8, Indent = true };
                        using var wr = XmlWriter.Create(entryStream, settings);
                        xs.Serialize(wr, exportProfile);
                    }

                    //add displayitems
                    var profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles", SelectedProfile.Guid + ".xml");
                    if (File.Exists(profilePath))
                    {
                        archive.CreateEntryFromFile(profilePath, "DisplayItems.xml");
                    }

                    //add assets
                    var assetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "assets", SelectedProfile.Guid.ToString());

                    if (Directory.Exists(assetFolder))
                    {
                        foreach (var file in Directory.GetFiles(assetFolder))
                        {
                            string entryName = file.Substring(assetFolder.Length + 1);
                            archive.CreateEntryFromFile(file, Path.Combine("assets", entryName));
                        }
                    }
                }

                return exportFilePath;
            }

            return null;
        }



        public static async Task ImportSensorPanel(string importPath)
        {
            if (!File.Exists(importPath))
            {
                return;
            }

            // Automatically detect asset folder: works for .sensorpanel (.rslcd) and extracted .sp2
            string assetFolder = System.IO.Path.GetDirectoryName(importPath);

            var lines = File.ReadAllLines(importPath, Encoding.GetEncoding("iso-8859-1"));

            if (lines.Length < 2)
            {
                Console.WriteLine("Invalid file format");
                return;
            }

            int page = 0;
            var items = new List<Dictionary<string, string>>();
            string importBaseName = Path.GetFileNameWithoutExtension(importPath);

            Regex openTagRegex = new(@"<LCDPAGE(\d+)>", RegexOptions.Compiled);
            Regex closeTagRegex = new(@"</LCDPAGE(\d+)>", RegexOptions.Compiled);

            for (int i = 0; i < lines.Length; i++)
            {
                var openMatch = openTagRegex.Match(lines[i]);
                if (openMatch.Success)
                {
                    page = int.Parse(openMatch.Groups[1].Value);
                    continue;
                }

                var closeMatch = closeTagRegex.Match(lines[i]);
                if (closeMatch.Success)
                {
                    var aidaHash = new AidaHash();
                    //await ProcessSensorPanelImport(aidaHash, $"[Import] {importBaseName} - Page {page}", items, assetFolder);
                    await ProcessSensorPanelImport(aidaHash, $"[Import] {importBaseName} - Page {page}", items, assetFolder, importPath);
                    items.Clear();
                    continue;
                }

                try
                {
                    // preserve the original line (for round-trip)
                    string originalLine = lines[i];

                    var rootElement = XElement.Parse($"<Root>{EscapeContentWithinLBL(originalLine)}</Root>");
                    var item = new Dictionary<string, string>();

                    foreach (XElement element in rootElement.Elements())
                    {
                        item[element.Name.LocalName] = element.Value;
                    }

                    // --- NEW: preserve the raw XML snippet (as string) and the line index ---
                    // Keep the original inner XML exactly (no formatting changes)
                    var rawInnerXml = string.Concat(rootElement.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)));
                    item["__RAW_XML__"] = rawInnerXml;
                    item["__LINE_INDEX__"] = i.ToString(CultureInfo.InvariantCulture);

                    items.Add(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line {i}: {ex.Message}");
                }
            }


            if (items.Count > 2)
            {
                var aidaHash = new AidaHash();
                //await ProcessSensorPanelImport(aidaHash, $"[Import] {Path.GetFileNameWithoutExtension(importPath)}", items, assetFolder);
                await ProcessSensorPanelImport(aidaHash, $"[Import] {Path.GetFileNameWithoutExtension(importPath)}", items, assetFolder, importPath);
            }
        }


        // add near the top of the class (once)
        private static readonly HashSet<string> _mapTraceSeen = new();
        private static readonly object _mapTraceSeenLock = new();


        //public static async Task ProcessSensorPanelImport(AidaHash aidaHash, string name, List<Dictionary<string, string>> items, string assetFolder)

        public static async Task ProcessSensorPanelImport(AidaHash aidaHash, string name, List<Dictionary<string, string>> items, string assetFolder, string importedSensorPanelPath)
        {
            if (items.Count > 2)
            {
                var SPWIDTH = items[1].GetIntValue("SPWIDTH", 1024);
                var SPHEIGHT = items[1].GetIntValue("SPHEIGHT", 600);
                var LCDBGCOLOR = items[1].GetIntValue("LCDBGCOLOR", 0);
                var SPBGCOLOR = items[1].GetIntValue("SPBGCOLOR", LCDBGCOLOR);

                Profile profile = new(name, SPWIDTH, SPHEIGHT)
                {
                    BackgroundColor = DecimalBgrToHex(SPBGCOLOR)
                };
                // store original import path so we can save back to the same .sensorpanel later
                profile.ImportedSensorPanelPath = importedSensorPanelPath;

                using var bitmap = new SKBitmap(1, 1);
                using var graphics = SkiaGraphics.FromBitmap(bitmap, profile.FontScale);

                AidaMonitor.LatestSensors = aidaHash.RefreshSensorData();
                Log.Information(
                "AIDA: LatestSensors updated. Count={Count}",
                AidaMonitor.LatestSensors?.Count ?? 0
                 );


                // fix: initialize list properly
                List<DisplayItem> displayItems = new List<DisplayItem>();




                
                // helper to attach provenance information (line index + raw xml) to each created DisplayItem
                void AttachProvenance(DisplayItem di, Dictionary<string, string> src)
                {
                    if (di == null || src == null) return;

                    // preserve original raw XML + line index (if present)
                    if (src.TryGetValue("__RAW_XML__", out var rawXml))
                    {
                        di.OriginalRawXml = rawXml;
                    }
                    if (src.TryGetValue("__LINE_INDEX__", out var lineIndexStr) && int.TryParse(lineIndexStr, out var li))
                    {
                        di.OriginalLineIndex = li;
                    }

                    // Determine whether to emit verbose map logs.
                    // If Settings/VerboseMapLogs isn't present, default to true (so behavior is unchanged).
                    bool canLog = true;
                    try
                    {
                        var cfgSettings = ConfigModel.Instance?.Settings;
                        if (cfgSettings != null)
                        {
                            // if the user added VerboseMapLogs bool in Settings, use it
                            var prop = cfgSettings.GetType().GetProperty("VerboseMapLogs");
                            if (prop != null && prop.PropertyType == typeof(bool))
                            {
                                canLog = (bool)prop.GetValue(cfgSettings);
                            }
                        }
                    }
                    catch
                    {
                        canLog = true; // fail-safe: don't hide logs if we couldn't read settings
                    }

                    // Build a stable unique key to prevent duplicate MAP-TRACE logs for the same item.
                    // Use ProfileGuid (if DisplayItem has it) + Name + OriginalLineIndex + GetHashCode
                    string profileGuid = (di is DisplayItem d && d.ProfileGuid != Guid.Empty) ? d.ProfileGuid.ToString() : "(noprof)";
                    string diName = (di as DisplayItem)?.Name ?? di.GetType().Name;
                    string lineIndex = di.OriginalLineIndex.HasValue ? di.OriginalLineIndex.Value.ToString() : "noidx";
                    string uniqueKey = $"{profileGuid}|{diName}|{lineIndex}|{di.GetHashCode()}";

                    // --- MAP TRACE: best-effort logging for sensor-like items ---
                    try
                    {
                        if (canLog)
                        {
                            // Prevent duplicate logging for the same display item
                            lock (_mapTraceSeenLock)
                            {
                                if (_mapTraceSeen.Contains(uniqueKey))
                                {
                                    // already logged once — skip to avoid flooding duplicate lines
                                    // continue execution (we still want to run auto-correct below)
                                    goto SKIP_LOGGING;
                                }
                                _mapTraceSeen.Add(uniqueKey);
                                // simple trimming strategy: clear when too large
                                if (_mapTraceSeen.Count > 20000)
                                    _mapTraceSeen.Clear();
                            }

                            if (di is ISensorItem si)
                            {
                                string typeName = si.GetType().Name;
                                string sensorType = "Unknown";
                                string name = string.Empty;
                                string idInfo = string.Empty;

                                // SensorType (try-safe)
                                try
                                {
                                    sensorType = si.SensorType.ToString();
                                }
                                catch { /* ignore */ }

                                // Name: prefer explicit SensorName property where present, fallback to DisplayItem.Name
                                try
                                {
                                    // try to get SensorName property (may be on interface or concrete)
                                    var snProp = si.GetType().GetProperty("SensorName");
                                    if (snProp != null)
                                    {
                                        var snVal = snProp.GetValue(si);
                                        if (snVal != null) name = snVal.ToString();
                                    }
                                }
                                catch { /* ignore */ }

                                if (string.IsNullOrWhiteSpace(name) && di is DisplayItem diBase && !string.IsNullOrWhiteSpace(diBase.Name))
                                {
                                    name = diBase.Name;
                                }

                                // Helper to append property if found
                                void TryAppendProp(string propName, string label)
                                {
                                    try
                                    {
                                        var p = si.GetType().GetProperty(propName);
                                        if (p != null)
                                        {
                                            var v = p.GetValue(si);
                                            if (v != null)
                                            {
                                                idInfo += $"{label}={v} ";
                                            }
                                        }
                                    }
                                    catch { /* ignore reflection issues */ }
                                }

                                TryAppendProp("Id", "HwId");
                                TryAppendProp("Instance", "Inst");
                                TryAppendProp("EntryId", "Entry");
                                TryAppendProp("PluginSensorId", "PluginId");

                                // ValueType
                                try
                                {
                                    var vtProp = si.GetType().GetProperty("ValueType");
                                    if (vtProp != null)
                                    {
                                        var vtVal = vtProp.GetValue(si);
                                        if (vtVal != null) idInfo += $"ValueType={vtVal} ";
                                    }
                                }
                                catch { /* ignore */ }

                                MapLogger.Trace(
                                    $"[MAP-TRACE] AttachedSensor: Type={typeName}, SensorType={sensorType}, Name='{name}', IDs=[{idInfo.Trim()}], ProfileGuid={profileGuid}");
                            }
                            else
                            {
                                // Non-sensor display item trace (useful for diagnosing missing labels etc.)
                                var typeName = di.GetType().Name;
                                var diNameLocal = (di as DisplayItem)?.Name ?? "(no-name)";
                                MapLogger.Trace($"[MAP-TRACE] AttachedDisplayItem: Type={typeName}, Name='{diNameLocal}', ProfileGuid={profileGuid}, LineIndex={lineIndex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MapLogger.Trace("[MAP-TRACE] AttachProvenance logging error: " + ex);
                    }

                SKIP_LOGGING: return;

                    
                }




                for (int i = 2; i < items.Count; i++)
                {
                    var item = items[i];
                    var key = item.GetStringValue("ID", string.Empty);

                    var TYP = item.GetStringValue("TYP", string.Empty);

                    var hidden = false;
                    var simple = false;
                    var gauge = false;
                    var graph = false;

                    if (key.StartsWith('-'))
                    {
                        hidden = true;
                        key = key[1..];
                    }

                    if (key.StartsWith("[SIMPLE]"))
                    {
                        simple = true;
                        key = key[8..];
                    }

                    if (key.StartsWith("[GAUGE]"))
                    {
                        gauge = true;
                        key = key[7..];
                    }
                   
                    if (key.StartsWith("[GRAPH]"))
                    {
                        graph = true;
                        key = key[7..];
                    }

                    //global items
                    var ITMX = item.GetIntValue("ITMX", 0);
                    var ITMY = item.GetIntValue("ITMY", 0);

                    //var LBL = item.GetStringValue("LBL", key);

                    var rawLabel = item.GetStringValue("LBL", key);
                    var LBL = SystemMacroResolver.Resolve(rawLabel);
                   




                    var TXTBIR = item.GetStringValue("TXTBIR", string.Empty);
                    var FNTNAM = item.GetStringValue("FNTNAM", "Arial");
                    var WID = item.GetIntValue("WID", 0);
                    var HEI = item.GetIntValue("HEI", 0);
                    
                    var MINVAL = item.GetIntValue("MINVAL", 0);
                    var MAXVAL = item.GetIntValue("MAXVAL", 100);

                    var UNT = item.GetStringValue("UNT", string.Empty);
                    var SHWUNT = item.GetIntValue("SHWUNT", 1);
                    var UNTWID = item.GetIntValue("UNTWID", 0);
                    var TXTSIZ = item.GetIntValue("TXTSIZ", 12);
                    var LBLCOL = item.GetIntValue("LBLCOL", 0);
                    var TXTCOL = item.GetIntValue("TXTCOL", LBLCOL);
                    var VALCOL = item.GetIntValue("VALCOL", TXTCOL);

                    var bold = false;
                    var italic = false;
                    var rightAlign = false;

                    if (simple)
                    {
                        if (TXTBIR.Length == 3)
                        {
                            if (int.TryParse(TXTBIR.AsSpan(0, 1), out int _bold))
                            {
                                bold = _bold == 1;
                            }
                            if (int.TryParse(TXTBIR.AsSpan(1, 1), out int _italic))
                            {
                                italic = _italic == 1;
                            }
                            if (int.TryParse(TXTBIR.AsSpan(2, 1), out int _rightAlign))
                            {
                                rightAlign = _rightAlign == 1;
                            }
                        }
                    }
                    else
                    {
                        //all other non-simple items are right align
                        if (key != "LBL")
                        {
                            rightAlign = true;
                        }
                    }

                    if (graph) // DONE DIRECT MAPPING FOR AIDA
                    {
                        if (WID != 0 && HEI != 0)
                        {
                            GraphDisplayItem.GraphType? graphType = null;
                            switch (TYP)
                            {
                                case "AG":
                                case "LG":
                                    graphType = GraphDisplayItem.GraphType.LINE;
                                    break;
                                case "HG":
                                    graphType = GraphDisplayItem.GraphType.HISTOGRAM;
                                    break;
                            }

                            if (graphType.HasValue)
                            {
                                var AUTSCL = item.GetIntValue("AUTSCL", 0);
                                var GPHCOL = item.GetIntValue("GPHCOL", 0);
                                var BGCOL = item.GetIntValue("BGCOL", 0);
                                var FRMCOL = item.GetIntValue("FRMCOL", 0);

                                // graph step
                                var GPHSTP = item.GetIntValue("GPHSTP", 1);

                                // graph thickness
                                var GPHTCK = item.GetIntValue("GPHTCK", 1);

                                // graph background, frame, grid
                                var GPHBFG = item.GetStringValue("GPHBFG", "000");

                                var background = false;
                                var frame = false;
                                if (GPHBFG.Length == 3)
                                {
                                    if (int.TryParse(GPHBFG.AsSpan(0, 1), out int _background))
                                    {
                                        background = _background == 1;
                                    }
                                    if (int.TryParse(GPHBFG.AsSpan(1, 1), out int _frame))
                                    {
                                        frame = _frame == 1;
                                    }
                                }

                                // --- AIDA mapping block ---
                                 string rawPanelKey = item.GetStringValue("ID", "").Trim();
                                 string panelSensorKey = rawPanelKey
                                     .Replace("[GRAPH]", "")
                                     .TrimStart('-')
                                     .Trim();

                                string aidaSensorId = "unknown";
                                var aidaSensor = AidaMonitor.LatestSensors
                                    .FirstOrDefault(s => string.Equals(s.Id, panelSensorKey, StringComparison.OrdinalIgnoreCase)
                                                      || string.Equals(s.Label, panelSensorKey, StringComparison.OrdinalIgnoreCase));
                                if (aidaSensor != null)
                                {
                                    aidaSensorId = aidaSensor.Id;
                                }

                                GraphDisplayItem graphDisplayItem = new(LBL, profile, graphType.Value, aidaSensorId)
                                {
                                    SensorName = panelSensorKey,
                                    Width = WID,
                                    Height = HEI,
                                    MinValue = MINVAL,
                                    MaxValue = MAXVAL,
                                    AutoValue = AUTSCL == 1,
                                    Step = GPHSTP,
                                    Thickness = GPHTCK,
                                    Background = background,
                                    Frame = frame,
                                    Fill = TYP != "LG",
                                    FillColor = TYP == "AG" ? $"#7F{DecimalBgrToHex(GPHCOL).Substring(1)}" : DecimalBgrToHex(GPHCOL),
                                    Color = DecimalBgrToHex(GPHCOL),
                                    BackgroundColor = DecimalBgrToHex(BGCOL),
                                    FrameColor = DecimalBgrToHex(FRMCOL),
                                    X = ITMX,
                                    Y = ITMY,
                                    Hidden = hidden,
                                };

                                if (aidaSensor != null)
                                {
                                    graphDisplayItem.SensorType = SynQPanel.Enums.SensorType.Plugin;
                                    graphDisplayItem.PluginSensorId = aidaSensorId;
                                }

                                // attach provenance
                                AttachProvenance(graphDisplayItem, item);

                                displayItems.Add(graphDisplayItem);
                            }
                        }
                    }

                    else if (gauge) // DONE DIRECT MAPPING FOR AIDA - generalized to handle non-custom types too
                    {
                        var STAFLS = item.GetStringValue("STAFLS", string.Empty);

                        var RESIZW = item.GetIntValue("RESIZW", 0);
                        var RESIZH = item.GetIntValue("RESIZH", 0);

                        // Extract and normalize panel sensor key from ID
                        string rawPanelKey = item.GetStringValue("ID", "").Trim();
                        string panelSensorKey = rawPanelKey
                            .Replace("[GAUGE]", "")
                            .Replace("[SIMPLE]", "")
                            .TrimStart('-')
                            .Trim();

                        // Robust AIDA sensor finding: match ID or LABEL, case-insensitive
                        string aidaSensorId = "unknown";
                        var aidaSensor = AidaMonitor.LatestSensors
                            .FirstOrDefault(s => string.Equals(s.Id, panelSensorKey, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(s.Label, panelSensorKey, StringComparison.OrdinalIgnoreCase));
                        if (aidaSensor != null)
                        {
                            aidaSensorId = aidaSensor.Id;
                        }

                        // Create gauge display item in all cases (custom images OR fallback visual)
                        GaugeDisplayItem gaugeDisplayItem = new(LBL, profile, aidaSensorId)
                        {
                            SensorName = panelSensorKey, // pass normalized key for clarity
                            MinValue = MINVAL,
                            MaxValue = MAXVAL,
                            X = ITMX,
                            Y = ITMY,
                            Width = RESIZW,
                            Height = RESIZH,
                            Hidden = hidden
                        };

                        
                        
                        // ───── AIDA Gauge Value Text  ─────
                        gaugeDisplayItem.ShowValue = item.GetIntValue("SHWVAL", 0) == 1;

                        if (gaugeDisplayItem.ShowValue)
                        {
                            gaugeDisplayItem.ValueTextSize =
                                item.GetIntValue("TXTSIZ", 0);

                             gaugeDisplayItem.ValueFontName = item.GetStringValue("FNTNAM", string.Empty);

                            gaugeDisplayItem.ValueColor =
                                DecimalBgrToHex(item.GetIntValue("VALCOL", 0));

                            var VALBI = item.GetStringValue("VALBI", "00");
                            if (VALBI.Length >= 2)
                            {
                                gaugeDisplayItem.ValueBold = VALBI[0] == '1';
                                gaugeDisplayItem.ValueItalic = VALBI[1] == '1';
                            }
                        }





                        // If this is the "Custom/CustomN + STAFLS has assets" case, load images into the gaugeImages set
                        if ((string.Equals(TYP, "Custom", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(TYP, "CustomN", StringComparison.OrdinalIgnoreCase)) &&
                             !string.IsNullOrEmpty(STAFLS))
                        {
                            try
                            {

                                
                                foreach (var image in STAFLS.Split('|', StringSplitOptions.RemoveEmptyEntries))
                                {


                                    string imagePath = System.IO.Path.Combine(assetFolder, image.Trim()); // <-- use assetFolder passed to import

                                    ImageDisplayItem imageDisplayItem = new(imagePath, profile, image.Trim(), true);

                                   
                                    
                                    /*
                                    string assetName = image.Trim()
                                   .Replace("&", "_");   // normalize identifier ONLY

                                    ImageDisplayItem imageDisplayItem =
                                        new(imagePath, profile, assetName, true);
                                    */



                                    gaugeDisplayItem.Images.Add(imageDisplayItem);
                                }
                                
                            }
                            catch (Exception ex)
                            {
                                // Non-fatal; keep the gauge but log if you want
                                MapLogger.Error("[MAP-BUILD] Failed to add gauge images: " + ex);
                            }
                        }
                        else
                        {
                            // Non-custom gauge (e.g. TYP="White") - keep defaults, renderer will draw fallback visuals
                        }


                        // Allow STIME to behave as a virtual numeric sensor
                        if (aidaSensor != null)
                        {
                            gaugeDisplayItem.SensorType = SynQPanel.Enums.SensorType.Plugin;
                            gaugeDisplayItem.PluginSensorId = aidaSensorId;
                        }
                        
                        // attach provenance
                        AttachProvenance(gaugeDisplayItem, item);

                        displayItems.Add(gaugeDisplayItem);
                    }


                    else if (key == string.Empty)
                    {
                        var GAUSTAFNM = item.GetStringValue("GAUSTAFNM", string.Empty);
                        var GAUSTADAT = item.GetStringValue("GAUSTADAT", string.Empty);

                        if (GAUSTAFNM != string.Empty && GAUSTADAT != string.Empty)
                        {
                            var data = ConvertHexStringToByteArray(GAUSTADAT);
                            await FileUtil.SaveAsset(profile, GAUSTAFNM, data);
                        }
                    }


                    else if (key == "IMG")
                    {
                        var IMGFIL = item.GetStringValue("IMGFIL", string.Empty);
                        var IMGDAT = item.GetStringValue("IMGDAT", string.Empty);
                        var BGIMG = item.GetIntValue("BGIMG", 0);
                        var RESIZW = item.GetIntValue("RESIZW", 0);
                        var RESIZH = item.GetIntValue("RESIZH", 0);
                        // ITMX / ITMY already read above

                        if (string.IsNullOrWhiteSpace(IMGFIL))
                            return; // nothing to draw

                        // Resolve the on-disk path (embedded vs external)
                        string resolvedImgPath;

                        if (!string.IsNullOrEmpty(IMGDAT))
                        {
                            // Image is embedded (typical for .rslcd and some .sp2)
                            var data = ConvertHexStringToByteArray(IMGDAT);

                            // Save into SynQPanel assets folder for this profile (existing behavior)
                            await FileUtil.SaveAsset(profile, IMGFIL, data);

                            // Compute where that file lives on disk (SaveAsset should have used per-profile folder)
                            string assetsRoot = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "SynQPanel", "assets", profile.Guid.ToString());

                            if (!Directory.Exists(assetsRoot))
                                Directory.CreateDirectory(assetsRoot);

                            resolvedImgPath = Path.Combine(assetsRoot, IMGFIL);
                        }
                        else
                        {
                            // Classic .sensorpanel style: IMGFIL references an existing PNG/JPG near the panel file
                            // resolvedImgPath initially points to the source near the panel (assetFolder)
                            resolvedImgPath = Path.Combine(assetFolder, IMGFIL);
                        }

                        // IMPORTANT: Always make sure the image is copied into the current profile's assets folder.
                        // This ensures each profile references its own copy and deleting another profile won't remove this one.
                        var perProfileImagePath = EnsureImageInProfileAssets(profile, resolvedImgPath, IMGFIL);

                        bool isRslcd = !string.IsNullOrEmpty(importedSensorPanelPath) &&
                                       importedSensorPanelPath.EndsWith(".rslcd", StringComparison.OrdinalIgnoreCase);

                        bool isBackground = false;

                        if (isRslcd)
                        {
                            // Our relaxed .rslcd background heuristic
                            if (profile.BackgroundImagePath == null &&
                                !IMGFIL.ToLowerInvariant().Contains("preview"))
                            {
                                bool isLarge =
                                    (RESIZW > 0 && RESIZH > 0 &&
                                     RESIZW >= SPWIDTH / 2 &&
                                     RESIZH >= SPHEIGHT / 2);

                                if (BGIMG == 1)
                                {
                                    isBackground = true;
                                }
                                else if (BGIMG == 0 && ITMX == 0 && ITMY == 0)
                                {
                                    isBackground = true;
                                }
                                else if (BGIMG == 0 && isLarge)
                                {
                                    isBackground = true;
                                }
                            }
                        }
                        else
                        {
                            // Authoritative AIDA rule:
                            // BGIMG == 1 means background, always (except preview)
                            if (BGIMG == 1 && !IMGFIL.ToLowerInvariant().Contains("preview"))
                            {
                                isBackground = profile.BackgroundImagePath == null;
                            }
                            else
                            {
                                // Legacy heuristic fallback (older panels)
                                isBackground =
                                    (BGIMG == 0) &&
                                    (ITMX == 0) &&
                                    (ITMY == 0) &&
                                    profile.BackgroundImagePath == null &&
                                    !IMGFIL.ToLowerInvariant().Contains("preview");
                            }
                        }

                        if (isBackground)
                        {
                            // Guarantee the background file lives inside this profile's assets folder
                            var bgProfilePath = await FileUtil.EnsureImageInProfileAssets(profile, resolvedImgPath, IMGFIL);

                            // Point profile to the profile-local copy
                            profile.BackgroundImagePath = bgProfilePath;

                            var bgItem = new ImageDisplayItem(bgProfilePath, profile, IMGFIL, true)
                            {
                                X = 0,
                                Y = 0,
                                Width = SPWIDTH,
                                Height = SPHEIGHT,
                                Hidden = false
                            };

                            // preserve provenance so saver can reuse original IMG block
                            AttachProvenance(bgItem, item);

                            displayItems.Insert(0, bgItem);
                        }
                        else if (!IMGFIL.ToLowerInvariant().Contains("preview") && BGIMG == 0)
                        {
                            // For overlay images we copy into the profile asset folder too (so overlays survive profile deletes)
                            var imageProfilePath = await FileUtil.EnsureImageInProfileAssets(profile, resolvedImgPath, IMGFIL);

                            var imageDisplayItem = new ImageDisplayItem(imageProfilePath, profile, IMGFIL, true)
                            {
                                X = ITMX,
                                Y = ITMY,
                                Width = RESIZW,
                                Height = RESIZH,
                                Hidden = hidden
                            };

                            AttachProvenance(imageDisplayItem, item);
                            displayItems.Add(imageDisplayItem);
                        }

                    }


                    else
                    {
                        switch (key)
                        {
                            case "PROPERTIES":
                                //do nothing
                                break;


                            case "LBL":
                                {
                                    // compute effective width: treat very small WID as "auto" (0)
                                    int effectiveWID = WID;
                                    try
                                    {
                                        // multiplier is configurable (default 3) - see ConfigModel change below
                                        int multiplier = (ConfigModel.Instance?.LabelWidthMultiplier) ?? 3;
                                        if (WID > 0 && WID < TXTSIZ * multiplier)
                                        {
                                            if (ConfigModel.Instance?.EnableRslcdDebug == true)
                                                
                                                //Debug.WriteLine($"[LBL-WIDTH-ADJUST] Ignoring tiny WID={WID} for label '{LBL}' with TXTSIZ={TXTSIZ}. Setting Width->0.");

                                            effectiveWID = 0;
                                        }
                                    }
                                    catch
                                    {
                                        // defensive: if anything goes wrong, just keep the original WID
                                        effectiveWID = WID;
                                    }

                                    bold = false;
                                    italic = false;
                                    FontWeight fontWeight =
                                    bold ? FontWeights.Bold : FontWeights.Normal;



                                    var LBLBIS = item.GetStringValue("LBLBIS", string.Empty);

                                    if (LBLBIS.Length == 3)
                                    {
                                        if (int.TryParse(LBLBIS.AsSpan(0, 1), out int _bold))
                                            bold = _bold == 1;

                                        if (int.TryParse(LBLBIS.AsSpan(1, 1), out int _italic))
                                            italic = _italic == 1;
                                    }


                                    // Create text item
                                    TextDisplayItem textDisplayItem = new(LBL, profile)
                                    {
                                        Font = ChooseAvailableFont(FNTNAM),
                                        FontSize = TXTSIZ,
                                        Color = DecimalBgrToHex(VALCOL),
                                        Bold = bold,
                                        Italic = italic,

                                        FontWeight = fontWeight,

                                        RightAlign = rightAlign,
                                        X = ITMX,
                                        Y = ITMY,
                                        Width = effectiveWID,
                                        Hidden = hidden
                                    };

                                    AttachProvenance(textDisplayItem, item);
                                    displayItems.Add(textDisplayItem);
                                }
                                break;                         


                            case "SDATE":
                                {
                                    CalendarDisplayItem calendarDisplayItem = new(LBL, profile)
                                    {
                                        Font = FNTNAM,
                                        FontSize = TXTSIZ,
                                        Color = DecimalBgrToHex(VALCOL),
                                        Bold = bold,
                                        Italic = italic,
                                        RightAlign = rightAlign,
                                        X = ITMX,
                                        Y = ITMY,
                                        Width = WID,
                                        Hidden = hidden
                                    };

                                    AttachProvenance(calendarDisplayItem, item);
                                    displayItems.Add(calendarDisplayItem);
                                }
                                break;
                            
                            case "STIME":
                            case "STIMENS":
                                {
                                    ClockDisplayItem clockDisplayItem = new(LBL, profile)
                                    {
                                        Font = FNTNAM,
                                        FontSize = TXTSIZ,
                                        Format = key == "STIME" ? "H:mm:ss" : "H:mm",
                                        Color = DecimalBgrToHex(VALCOL),
                                        Bold = bold,
                                        Italic = italic,
                                        RightAlign = rightAlign,
                                        X = ITMX,
                                        Y = ITMY,
                                        Width = WID,
                                        Hidden = hidden
                                    };

                                    AttachProvenance(clockDisplayItem, item);
                                    displayItems.Add(clockDisplayItem);
                                }
                                break;

                            default:
                                {

                                    var SHWLBL = item.GetIntValue("SHWLBL", 0);

                                    
                                    if (SHWLBL == 1)
                                    {
                                        var LBLBIS = item.GetStringValue("LBLBIS", string.Empty);

                                        if (LBLBIS.Length == 3)
                                        {
                                            if (int.TryParse(LBLBIS.AsSpan(0, 1), out int _bold))
                                            {
                                                bold = _bold == 1;
                                            }
                                            if (int.TryParse(LBLBIS.AsSpan(1, 1), out int _italic))
                                            {
                                                italic = _italic == 1;
                                            }
                                        }

                                        TextDisplayItem label = new TextDisplayItem(LBL, profile)
                                        {
                                            Font = FNTNAM,
                                            FontSize = TXTSIZ,
                                            Color = DecimalBgrToHex(LBLCOL),
                                            Bold = bold,
                                            Italic = italic,
                                            X = ITMX,
                                            Y = ITMY,
                                            Width = WID,
                                            Hidden = hidden,
                                        };

                                        AttachProvenance(label, item);
                                        displayItems.Add(label);
                                    }

                                    
                                    var SHWVAL = item.GetIntValue("SHWVAL", 0);

                                    if (simple || SHWVAL == 1)   // DONE DIRECT MAPPING FOR AIDA
                                    {
                                        var VALBIS = item.GetStringValue("VALBIS", string.Empty);

                                        if (VALBIS.Length == 3)
                                        {
                                            if (int.TryParse(VALBIS.AsSpan(0, 1), out int _bold))
                                            {
                                                bold = _bold == 1;
                                            }
                                            if (int.TryParse(VALBIS.AsSpan(1, 1), out int _italic))
                                            {
                                                italic = _italic == 1;
                                            }
                                        }

                                        // AIDA mapping: only for this widget!
                                        string valueKey = item.GetStringValue("ID", "")
                                            .Replace("[SIMPLE]", "")
                                            .TrimStart('-')
                                            .Trim();

                                        // Try to match ID to AIDA sensor (or label as fallback)
                                        string aidaId = "unknown";
                                        var aida = AidaMonitor.LatestSensors
                                            .FirstOrDefault(s =>
                                                string.Equals(s.Id, valueKey, StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(s.Label, valueKey, StringComparison.OrdinalIgnoreCase)
                                            );
                                        if (aida != null)
                                            aidaId = aida.Id;

                                        SensorDisplayItem sensorDisplayItem = new(LBL, profile, aidaId)
                                        {
                                            SensorName = valueKey,
                                            Font = FNTNAM,
                                            FontSize = TXTSIZ,
                                            Color = DecimalBgrToHex(VALCOL),
                                            Unit = UNT,
                                            ShowUnit = SHWUNT == 1,
                                            OverrideUnit = SHWUNT == 1,

                                            Name = LBL,
                                            ShowName = SHWLBL == 1,



                                            Bold = bold,
                                            Italic = italic,
                                            RightAlign = rightAlign,
                                            X = ITMX,
                                            Y = ITMY,
                                            Width = WID,
                                            Hidden = hidden
                                        };

                                        if (aida != null)
                                        {
                                            sensorDisplayItem.SensorType = SynQPanel.Enums.SensorType.Plugin;
                                            sensorDisplayItem.PluginSensorId = aidaId;
                                        }

                                        AttachProvenance(sensorDisplayItem, item);
                                        displayItems.Add(sensorDisplayItem);
                                    }

                                    var SHWBAR = item.GetIntValue("SHWBAR", 0);

                                    if (SHWBAR == 1)
                                    {
                                        var BARWID = item.GetIntValue("BARWID", 400);
                                        var BARHEI = item.GetIntValue("BARHEI", 50);
                                        var BARMIN = item.GetIntValue("BARMIN", 0);
                                        var BARMAX = item.GetIntValue("BARMAX", 100);
                                        var BARFRMCOL = item.GetIntValue("BARFRMCOL", 0);
                                        var BARMINFGC = item.GetIntValue("BARMINFGC", 0);
                                        var BARMINBGC = item.GetIntValue("BARMINBGC", 0);

                                        var BARLIM3FGC = item.GetIntValue("BARLIM3FGC", 0);
                                        var BARLIM3BGC = item.GetIntValue("BARLIM3BGC", 0);

                                        // frame, shadow, 3d, right to left
                                        var BARFS = item.GetStringValue("BARFS", "0000");

                                        // bar placement
                                        var BARPLC = item.GetStringValue("BARPLC", "SEP");

                                        var offset = 0;

                                        if (BARPLC == "SEP" && SHWVAL == 1)
                                        {
                                            var size2 = graphics.MeasureString("HELLO WORLD", FNTNAM, "", TXTSIZ);
                                            offset = (int)size2.height;
                                        }

                                        var size = graphics.MeasureString(UNT, FNTNAM, "", TXTSIZ);

                                        var frame = false;
                                        var gradient = false;
                                        var flipX = false;

                                        if (BARFS.Length == 4)
                                        {
                                            if (int.TryParse(BARFS.AsSpan(0, 1), out int _frame))
                                            {
                                                frame = _frame == 1;
                                            }
                                            if (int.TryParse(BARFS.AsSpan(2, 1), out int _gradient))
                                            {
                                                gradient = _gradient == 1;
                                            }
                                            if (int.TryParse(BARFS.AsSpan(3, 1), out int _flipX))
                                            {
                                                flipX = _flipX == 1;
                                            }
                                        }

                                        // --- LOCAL AIDA mapping for BarDisplayItem ---
                                        string barKey = item.GetStringValue("ID", "")
                                            .Replace("[BAR]", "")
                                            .Replace("[SIMPLE]", "")
                                            .TrimStart('-')
                                            .Trim();

                                        string aidaBarId = "unknown";
                                        var aidaBar = AidaMonitor.LatestSensors
                                            .FirstOrDefault(s =>
                                                string.Equals(s.Id, barKey, StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(s.Label, barKey, StringComparison.OrdinalIgnoreCase)
                                            );
                                        if (aidaBar != null)
                                            aidaBarId = aidaBar.Id;

                                        BarDisplayItem barDisplayItem = new(LBL, profile, aidaBarId)
                                        {
                                            SensorName = barKey,
                                            Width = BARWID,
                                            Height = BARHEI,
                                            MinValue = BARMIN,
                                            MaxValue = BARMAX,
                                            Frame = frame,
                                            FrameColor = DecimalBgrToHex(BARFRMCOL),
                                            Color = DecimalBgrToHex(BARLIM3FGC),
                                            Background = true,
                                            BackgroundColor = DecimalBgrToHex(BARLIM3BGC),
                                            Gradient = gradient,
                                            GradientColor = DecimalBgrToHex(BARLIM3BGC),
                                            FlipX = flipX,
                                            X = ITMX,
                                            Y = ITMY + offset,
                                            Hidden = hidden,
                                        };

                                        if (aidaBar != null)
                                        {
                                            barDisplayItem.SensorType = SynQPanel.Enums.SensorType.Plugin;
                                            barDisplayItem.PluginSensorId = aidaBarId;
                                        }

                                        AttachProvenance(barDisplayItem, item);
                                        displayItems.Add(barDisplayItem);
                                    }
                                }
                                break;
                        }
                    }
                }

                SaveDisplayItems(profile, displayItems);

                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    ConfigModel.Instance.AddProfile(profile);
                    ConfigModel.Instance.SaveProfiles();
                    SharedModel.Instance.SelectedProfile = profile;
                });
            }
           // return ProcessSensorPanelImport(aidaHash, name, items, assetFolder, null);

        }

        
        private static byte[] ConvertHexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length.");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }
        private static string EscapeContentWithinLBL(string xmlContent)
        {
            // Regular expression to match content within <LBL>...</LBL>
            string pattern = @"<LBL>(.*?)</LBL>";

            // Use Regex.Replace to find each match and escape its content
            string result = Regex.Replace(xmlContent, pattern, match =>
            {
                // Escape the inner content
                string innerContent = match.Groups[1].Value;
                string escapedContent = innerContent
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
                return $"<LBL>{escapedContent}</LBL>";
            }, RegexOptions.Singleline);

            return result;
        }

        private static string DecimalBgrToHex(int bgrValue)
        {
            // Handle negative values explicitly
            if (bgrValue < 0)
            {
                return "#000000";
            }

            // Extract the individual B, G, R components from the BGR integer
            int blue = (bgrValue & 0xFF0000) >> 16;
            int green = (bgrValue & 0x00FF00) >> 8;
            int red = (bgrValue & 0x0000FF);

            // Convert to hexadecimal string with leading #
            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        public void ImportProfile(string importPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(importPath))
            {
                ZipArchiveEntry? profileEntry = null;
                ZipArchiveEntry? displayItemsEntry = null;
                List<ZipArchiveEntry> assets = new List<ZipArchiveEntry>();

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Equals("Profile.xml"))
                    {
                        profileEntry = entry;
                    }
                    else if (entry.FullName.Equals("DisplayItems.xml"))
                    {
                        displayItemsEntry = entry;
                    }
                    else if (entry.FullName.StartsWith("assets\\"))
                    {
                        assets.Add(entry);
                    }
                }

                if (profileEntry != null && displayItemsEntry != null)
                {
                    //read profile settings
                    Profile? profile = null;
                    using (Stream entryStream = profileEntry.Open())
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(Profile));
                        using (var rd = XmlReader.Create(entryStream))
                        {
                            profile = xs.Deserialize(rd) as Profile;
                        }
                    }

                    if (profile != null)
                    {
                        //change profile GUID & Name
                        profile.Guid = Guid.NewGuid();
                        profile.Name = "[Import] " + profile.Name;
                        //profile.Name = profile.Name;

                        //extract displayitems
                        var profileFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles");
                        if (!Directory.Exists(profileFolder))
                        {
                            Directory.CreateDirectory(profileFolder);
                        }
                        var profilePath = Path.Combine(profileFolder, profile.Guid + ".xml");
                        displayItemsEntry.ExtractToFile(profilePath);

                        //smart import
                        var displayItems = LoadDisplayItemsFromFile(profile);
                        foreach (DisplayItem displayItem in displayItems)
                        {
                            return;
                        }
                        //save it back
                        SaveDisplayItems(profile, displayItems);

                        string tempFolder = Path.Combine(Path.GetTempPath(), "SynQPanelSpzip_" + Guid.NewGuid());
                        Directory.CreateDirectory(tempFolder);
                        System.IO.Compression.ZipFile.ExtractToDirectory(importPath, tempFolder);

                        var assetFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "SynQPanel", "assets", Guid.NewGuid().ToString() // Use profile.Guid.ToString() if profile exists!
                        );
                        if (!Directory.Exists(assetFolder))
                        {
                            Directory.CreateDirectory(assetFolder);
                        }

                        string[] validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
                        // Copy ALL files except .sp2
                        foreach (var file in Directory.GetFiles(tempFolder))
                        {
                            string extension = Path.GetExtension(file).ToLowerInvariant();
                            string name = Path.GetFileName(file).ToLowerInvariant();

                            // Skip panel config(s)
                            if (extension == ".sp2") continue;

                            // Copy likely images, including those missing typical extensions
                            if (validExtensions.Contains(extension) || name.Contains("preview") || name.Contains("background"))
                            {
                                string destPath = Path.Combine(assetFolder, Path.GetFileName(file));
                                File.Copy(file, destPath, true);
                            }
                        }







                        //add profile
                        ConfigModel.Instance.AddProfile(profile);
                        ConfigModel.Instance.SaveProfiles();
                        SharedModel.Instance.SelectedProfile = profile;
                    }
                }
            }
        }

        public ImmutableList<DisplayItem> GetProfileDisplayItemsCopy()
        {
            if (SelectedProfile is Profile profile)
            {
                return GetProfileDisplayItemsCopy(profile);
            }

            return [];
        }

        public ImmutableList<DisplayItem> GetProfileDisplayItemsCopy(Profile profile)
        {
            if(!ProfileDisplayItemsCopy.TryGetValue(profile.Guid, out var displayItemsCopy))
            {
                AccessDisplayItems(profile, new Action<ObservableCollection<DisplayItem>>((displayItems) =>
                {
                    displayItemsCopy = [.. displayItems];
                    ProfileDisplayItemsCopy[profile.Guid] = displayItemsCopy;
                }));
            }

            return displayItemsCopy ?? [];
        }

        private static string ChooseAvailableFont(string requestedFont)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestedFont))
                    return "Segoe UI";

                // WPF font families check
                var exists = System.Windows.Media.Fonts.SystemFontFamilies
                              .Any(ff => string.Equals(ff.Source, requestedFont, StringComparison.OrdinalIgnoreCase));

                if (exists) return requestedFont;

                // optional: try partial match (some fonts have different names)
                var partial = System.Windows.Media.Fonts.SystemFontFamilies
                              .FirstOrDefault(ff => ff.Source.IndexOf(requestedFont, StringComparison.OrdinalIgnoreCase) >= 0);
                if (partial != null) return partial.Source;

                // fallback
                return "Segoe UI";
            }
            catch
            {
                return "Segoe UI";
            }
        }






        public static async Task<ICollection<DisplayItem>> LoadDisplayItemsAsync(Profile profile)
        {
            return await Task.Run(() => LoadDisplayItemsFromFile(profile));
        }

        private static List<DisplayItem> LoadDisplayItemsFromFile(Profile profile)
        {
            var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "profiles", profile.Guid + ".xml");
            if (File.Exists(fileName))
            {
                XmlSerializer xs = new(typeof(List<DisplayItem>),
                    [typeof(GroupDisplayItem), typeof(BarDisplayItem), typeof(GraphDisplayItem), typeof(DonutDisplayItem), typeof(TableSensorDisplayItem), typeof(SensorDisplayItem), typeof(ClockDisplayItem), typeof(CalendarDisplayItem), typeof(TextDisplayItem), typeof(SensorImageDisplayItem), typeof(ImageDisplayItem), typeof(HttpImageDisplayItem), typeof(GaugeDisplayItem), typeof(ShapeDisplayItem)]);

                using var rd = XmlReader.Create(fileName);
                try
                {
                    if (xs.Deserialize(rd) is List<DisplayItem> displayItems)
                    {
                        foreach (var displayItem in displayItems)
                        {
                            displayItem.SetProfile(profile);
                        }

                        return displayItems;
                    }
                }
                catch { }
            }

            return [];
        }

        public abstract class CanvasItem
        {
            public Guid Id { get; } = Guid.NewGuid();

            // existing properties
            public double X { get; set; }
            public double Y { get; set; }
            public string Label { get; set; }
            public string BoundSensorId { get; set; }

            // --- new fields for round-trip persistence ---
            public int? OriginalLineIndex { get; set; }           // index in file lines[] (if imported)
            public string OriginalRawXml { get; set; }           // the exact XML snippet(s) parsed for this item

            public abstract void ApplyToAidaElement(XElement aidaElement);
        }

        // Ensure the image file lives in the given profile's assets folder.
        // Returns the absolute path to the profile-local image (destination).
        private static string EnsureImageInProfileAssets(Profile profile, string sourcePath, string fileName)
        {
            if (profile == null) return sourcePath ?? string.Empty;
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(fileName)) return sourcePath;

            try
            {
                var assetsRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SynQPanel", "assets", profile.Guid.ToString());

                if (!Directory.Exists(assetsRoot))
                    Directory.CreateDirectory(assetsRoot);

                var destPath = Path.Combine(assetsRoot, fileName);

                // If source already points at per-profile asset, just return it
                if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase)
                    && File.Exists(destPath))
                {
                    return destPath;
                }

                // If source exists on disk, copy into profile assets (overwrite)
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath, true);
                    return destPath;
                }

                // If source doesn't exist (shouldn't happen when called correctly), just return destPath (caller can decide)
                return destPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureImageInProfileAssets failed: {ex.Message}");
                // fallback: return original sourcePath so nothing breaks
                return sourcePath ?? string.Empty;
            }
        }

        // HELPER to check sensors MAPPING
        // safe diagnostic; add to SharedModel or a debug util
        private static void TraceImportedSensors(Profile profile, string label)
        {
            if (profile == null)
            {
                RslcdDebug.Log($"{label}: profile is null");
                return;
            }

            var allItems = SharedModel.Instance?.DisplayItems;
            if (allItems == null)
            {
                RslcdDebug.Log($"{label}: SharedModel.DisplayItems is null");
                return;
            }

            // Only items that are ISensorItem AND DisplayItem (so we can read ProfileGuid)
            var items = allItems
                .OfType<ISensorItem>()
                .Where(i => i is DisplayItem di && di.ProfileGuid == profile.Guid)
                .ToList();

            RslcdDebug.Log($"{label}: {items.Count} sensor items for profile {profile.Guid}");

            foreach (var si in items)
            {
                // safe to cast now because of the filter above
                var di = (DisplayItem)si;

                var typ = si.GetType().Name;
                string sname = si.GetType().GetProperty("SensorName")?.GetValue(si)?.ToString() ?? "";
                string stype = si.GetType().GetProperty("SensorType")?.GetValue(si)?.ToString() ?? "";
                string id = si.GetType().GetProperty("Id")?.GetValue(si)?.ToString() ?? "";
                string inst = si.GetType().GetProperty("Instance")?.GetValue(si)?.ToString() ?? "";
                string entry = si.GetType().GetProperty("EntryId")?.GetValue(si)?.ToString() ?? "";
                string plugin = si.GetType().GetProperty("PluginSensorId")?.GetValue(si)?.ToString() ?? "";

                RslcdDebug.Log(
                    $"  Type={typ} Name='{sname}' SensorType={stype} Id={id} Instance={inst} Entry={entry} Plugin='{plugin}' ProfileGuid={di.ProfileGuid}");
            }
        }


       




    }
}
