// Purpose: AIDA-only viewmodel for sensor lists. Keeps grouped + flat views,


using CommunityToolkit.Mvvm.ComponentModel;
using SynQPanel.Aida;
using SynQPanel.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace SynQPanel.ViewModels.Components
{
    public partial class AidaSensorsVM : ObservableObject
    {
        // Exposed grouped collection (drop-in, safe)
        public ObservableCollection<TreeItem> GroupedSensors { get; } = new ObservableCollection<TreeItem>();

        // Toggle to control whether UI should bind to GroupedSensors (if you change XAML later)
        private bool _useGroupedView = false;
        public bool UseGroupedView
        {
            get => _useGroupedView;
            set => SetProperty(ref _useGroupedView, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private object? lastSelectedId;

        // Flat hierarchical source (root nodes)
        public ObservableCollection<TreeItem> Sensors { get; set; } = new ObservableCollection<TreeItem>();

        // Flattened collection (useful for lists/search)
        public ObservableCollection<SensorTreeItem> FlatSensors { get; } = new ObservableCollection<SensorTreeItem>();

        private SensorTreeItem? selectedItem;
        public SensorTreeItem? SelectedItem
        {
            get => selectedItem;
            set => SetProperty(ref selectedItem, value);
        }

        public string Type { get; private set; } = string.Empty;

        private readonly AidaHash aidaHash = new AidaHash();

        // -- Public API --

        public void LoadSensors()
        {
            try
            {
                var prevExpanded = CaptureExpandedIds();

                var sensorItems = aidaHash.RefreshSensorData();

                // ensure global latest sensors is kept up-to-date for mapping code elsewhere
                AidaMonitor.LatestSensors = sensorItems;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sensors.Clear();

                    foreach (var sensor in sensorItems)
                    {
                        var existing = Sensors.OfType<AidaSensorTreeItem>().FirstOrDefault(s => s.Id.Equals(sensor.Id));
                        if (existing != null)
                        {
                            existing.Update();
                        }
                        else
                        {
                            Sensors.Add(new AidaSensorTreeItem(sensor));
                        }
                    }

                    // Remove old ones
                    for (int i = Sensors.Count - 1; i >= 0; i--)
                    {
                        var s = Sensors[i];
                        if (!sensorItems.Any(x => x.Id.Equals(s.Id)))
                        {
                            Sensors.RemoveAt(i);
                        }
                    }
                });

                // Rebuild flat and grouped views
                RebuildFlatSensors();
                BuildGroupedSensors();

                // Reapply expansion state
                ApplyExpandedIds(prevExpanded);

                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                Sensors.Clear();
                ErrorMessage = "No Sensors are fetched!\n\n Possible Reason: AIDA64 not running or shared memory disabled.\n\nDetails: " + ex.Message;
            }
        }

        public void UpdateSensorValues()
        {
            foreach (var root in Sensors)
            {
                foreach (var child in root.Children)
                {
                    if (child is AidaSensorTreeItem aidaSensor)
                        aidaSensor.Update();
                }
            }
        }

        public void SetLastSelected(object? id) => lastSelectedId = id;

        // -- Grouping / Flat helpers --

        public void BuildGroupedSensors()
        {
            try
            {
                // remember current selection
                object? currentlySelected = null;
                foreach (var g in GroupedSensors)
                    foreach (var c in g.Children)
                        if (c is TreeItem ti && ti.IsSelected)
                            currentlySelected = ti.Id;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    GroupedSensors.Clear();

                    var groups = new Dictionary<string, TreeItem>(StringComparer.OrdinalIgnoreCase);

                    foreach (var item in Sensors)
                    {
                        string typeKey = "other";

                        try
                        {
                            var tprop = item.GetType().GetProperty("Type");
                            if (tprop != null)
                            {
                                var tval = tprop.GetValue(item) as string;
                                if (!string.IsNullOrWhiteSpace(tval))
                                    typeKey = tval.Trim().ToLowerInvariant();
                            }
                        }
                        catch { }

                        if (string.IsNullOrWhiteSpace(typeKey) || typeKey == "other")
                        {
                            var name = item.Name ?? string.Empty;
                            var first = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(first))
                                typeKey = first.Trim().ToLowerInvariant();
                        }

                        if (!groups.TryGetValue(typeKey, out var groupNode))
                        {
                            //var title = string.IsNullOrEmpty(typeKey) ? "Other" : char.ToUpper(typeKey[0]) + (typeKey.Length > 1 ? typeKey.Substring(1) : "");
                            var title = NormalizeGroupTitle(typeKey);
                            groupNode = new TreeItem(typeKey, title);
                            groups[typeKey] = groupNode;
                            GroupedSensors.Add(groupNode);
                        }

                        groupNode.Children.Add(item);
                    }

                    // preferred ordering
                    var preferredOrder = new[] { "temp", "fan", "volt", "pwr", "curr", "gpu fan", "sys", "other" };
                    var sorted = GroupedSensors.OrderBy(g =>
                    {
                        var key = g.Id?.ToString()?.ToLowerInvariant() ?? "other";
                        var idx = Array.IndexOf(preferredOrder, key);
                        return idx >= 0 ? idx : preferredOrder.Length;
                    }).ToList();

                    GroupedSensors.Clear();
                    foreach (var g in sorted) GroupedSensors.Add(g);

                    // restore selection if possible
                    if (lastSelectedId != null)
                    {
                        foreach (var group in GroupedSensors)
                            foreach (var child in group.Children)
                                if (child is TreeItem ti && Equals(ti.Id, lastSelectedId))
                                    ti.IsSelected = true;
                    }
                });

                // deferred restore for UI layout safety
                if (currentlySelected != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (var group in GroupedSensors)
                            foreach (var child in group.Children)
                                if (child is TreeItem ti && Equals(ti.Id, currentlySelected))
                                    ti.IsSelected = true;
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BuildGroupedSensors error: " + ex);
            }
        }

        private void RebuildFlatSensors()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                object? selectedId = SelectedItem?.Id;

                var incoming = new List<SensorTreeItem>();
                foreach (var root in Sensors) CollectRecursively(root, incoming);

                void CollectRecursively(TreeItem node, List<SensorTreeItem> dest)
                {
                    if (node is SensorTreeItem s) dest.Add(s);
                    foreach (var c in node.Children) CollectRecursively(c, dest);
                }

                var incomingById = incoming.ToDictionary(x => x.Id);

                // update/add
                foreach (var inc in incoming)
                {
                    var existing = FlatSensors.FirstOrDefault(x => Equals(x.Id, inc.Id));
                    if (existing != null)
                    {
                        existing.Name = inc.Name;
                        existing.Value = inc.Value;
                        existing.Unit = (inc is AidaSensorTreeItem a) ? a.Unit : inc.Unit;
                    }
                    else
                    {
                        FlatSensors.Add(inc);
                    }
                }

                // remove stale
                for (int i = FlatSensors.Count - 1; i >= 0; i--)
                {
                    var f = FlatSensors[i];
                    if (!incomingById.ContainsKey(f.Id))
                        FlatSensors.RemoveAt(i);
                }

                // restore selection
                if (selectedId != null)
                {
                    var newSelected = FlatSensors.FirstOrDefault(x => Equals(x.Id, selectedId));
                    SelectedItem = newSelected;
                }
            });
        }

        // Expansion-state helpers
        private IEnumerable<TreeItem> ActiveSensors
        {
            get
            {
                var list = new List<TreeItem>();
                void Collect(TreeItem node)
                {
                    list.Add(node);
                    foreach (var c in node.Children) Collect(c);
                }
                foreach (var root in Sensors) Collect(root);
                return list;
            }
        }

        private HashSet<string> CaptureExpandedIds()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var g in GroupedSensors)
                {
                    var gid = g.Id?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(gid) && g.IsExpanded) set.Add("G:" + gid);
                    foreach (var c in g.Children)
                    {
                        var cid = c.Id?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(cid) && c.IsExpanded) set.Add("I:" + cid);
                    }
                }
            }
            catch { }
            return set;
        }

        private void ApplyExpandedIds(HashSet<string> set)
        {
            if (set == null || set.Count == 0) return;
            try
            {
                foreach (var g in GroupedSensors)
                {
                    var gid = g.Id?.ToString() ?? string.Empty;
                    g.IsExpanded = !string.IsNullOrEmpty(gid) && set.Contains("G:" + gid);
                    foreach (var c in g.Children)
                    {
                        var cid = c.Id?.ToString() ?? string.Empty;
                        c.IsExpanded = !string.IsNullOrEmpty(cid) && set.Contains("I:" + cid);
                    }
                }
            }
            catch { }
        }


        //-----Helper
        private static string NormalizeGroupTitle(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Other";

            raw = raw.Trim();

            // Acronyms
            if (raw.Equals("cpu", StringComparison.OrdinalIgnoreCase))
                return "CPU";

            if (raw.Equals("gpu", StringComparison.OrdinalIgnoreCase))
                return "GPU";

            // Plurals / friendly names
            if (raw.Equals("fan", StringComparison.OrdinalIgnoreCase))
                return "Cooling Fans";

            if (raw.Equals("gpu fan", StringComparison.OrdinalIgnoreCase))
                return "Cooling Fans";

            if (raw.Equals("temp", StringComparison.OrdinalIgnoreCase))
                return "Temperatures";

            if (raw.Equals("volt", StringComparison.OrdinalIgnoreCase))
                return "Voltages";

            if (raw.Equals("pwr", StringComparison.OrdinalIgnoreCase))
                return "Power";

            if (raw.Equals("curr", StringComparison.OrdinalIgnoreCase))
                return "Current";

            if (raw.Equals("sys", StringComparison.OrdinalIgnoreCase))
                return "System";

            // Default title-case
            return char.ToUpper(raw[0]) + raw.Substring(1).ToLowerInvariant();
        }




    }
}
