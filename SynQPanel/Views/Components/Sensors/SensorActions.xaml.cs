using SynQPanel.Enums;
using SynQPanel.Models;
using SynQPanel.ViewModels.Components;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace SynQPanel.Views.Components
{
    public partial class SensorActions : UserControl
    {
        public static readonly DependencyProperty SelectedSensorItemProperty =
            DependencyProperty.Register(nameof(SelectedSensorItem), typeof(SensorTreeItem), typeof(SensorActions), new PropertyMetadata(null));

        public static readonly DependencyProperty SensorTypeProperty =
            DependencyProperty.Register(nameof(SensorType), typeof(SensorType), typeof(SensorActions), new PropertyMetadata(SensorType.Plugin));


        public SensorTreeItem SelectedSensorItem
        {
            get { return (SensorTreeItem)GetValue(SelectedSensorItemProperty); }
            set { SetValue(SelectedSensorItemProperty, value); }
        }

        public SensorType SensorType
        {
            get { return (SensorType)GetValue(SensorTypeProperty); }
            set { SetValue(SensorTypeProperty, value); }
        }

        public SensorActions()
        {
            InitializeComponent();
        }

        private void RefreshDisplay()
        {
            try
            {
                var selectedProfile = SharedModel.Instance.SelectedProfile;
                if (selectedProfile == null) return;

                var displayWindow = DisplayWindowManager.Instance.GetWindow(selectedProfile.Guid);
                if (displayWindow == null) return;

                var dwDispatcher = displayWindow.Dispatcher;
                var mi = displayWindow.GetType().GetMethod("RefreshDisplay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (dwDispatcher != null)
                {
                    if (mi != null)
                    {
                        dwDispatcher.Invoke(() =>
                        {
                            try { mi.Invoke(displayWindow, null); }
                            catch (Exception ex) { Debug.WriteLine("[RefreshDisplay] invoke RefreshDisplay failed: " + ex); }
                        });
                    }
                    else
                    {
                        dwDispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (displayWindow is System.Windows.UIElement ui)
                                {
                                    ui.InvalidateVisual();
                                    ui.UpdateLayout();
                                    Debug.WriteLine("[RefreshDisplay] fallback InvalidateVisual+UpdateLayout invoked on display window's dispatcher.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("[RefreshDisplay] fallback failed: " + ex);
                            }
                        });
                    }
                }
                else
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        try
                        {
                            if (mi != null) mi.Invoke(displayWindow, null);
                            else if (displayWindow is System.Windows.UIElement ui)
                            {
                                ui.InvalidateVisual();
                                ui.UpdateLayout();
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine("[RefreshDisplay] no dispatcher fallback failed: " + ex); }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RefreshDisplay] outer exception: " + ex);
            }
        }

        private void SafeAddAndRefresh(DisplayItem item)
        {
            if (item == null) return;

            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        Debug.WriteLine($"[SafeAddAndRefresh] Adding item: {item.GetType().Name}, Name={item.Name}");
                        SharedModel.Instance.AddDisplayItem(item);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[SafeAddAndRefresh] AddDisplayItem threw: " + ex);
                    }
                });

                // extra wiring + debug info right after add
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        // Emit the newly added item props to confirm wiring
                        LogDisplayItemProperties(item);
                    }
                    catch { }
                });

                TrySeedInitialSample(item);

                RefreshDisplay();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SafeAddAndRefresh] outer exception: " + ex);
            }
        }

        private void LogDisplayItemProperties(object item)
        {
            try
            {
                var props = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.CanRead)
                                .Select(p =>
                                {
                                    object val = null;
                                    try { val = p.GetValue(item); } catch { val = "(err)"; }
                                    return $"{p.Name}={(val ?? "null")}";
                                });
                Debug.WriteLine("[SafeAddAndRefresh] NewItemProps: " + string.Join(", ", props));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LogDisplayItemProperties] " + ex);
            }
        }

        private void TrySetPropertyIfExists(object target, string propName, object value)
        {
            if (target == null || string.IsNullOrEmpty(propName)) return;
            try
            {
                var pi = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null && pi.CanWrite)
                {
                    if (value == null) pi.SetValue(target, null);
                    else if (pi.PropertyType.IsAssignableFrom(value.GetType())) pi.SetValue(target, value);
                    else
                    {
                        try { var converted = Convert.ChangeType(value, pi.PropertyType); pi.SetValue(target, converted); }
                        catch { /* ignore */ }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrySetPropertyIfExists] {propName} set failed: {ex}");
            }
        }

        private void TryInvokeMethodIfExists(object target, string methodName, object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName)) return;
            try
            {
                var mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var dw = Application.Current?.Dispatcher;
                    if (dw != null && !dw.CheckAccess()) dw.Invoke(() => mi.Invoke(target, args));
                    else mi.Invoke(target, args);
                    Debug.WriteLine($"[TryInvokeMethodIfExists] invoked {methodName} on {target.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TryInvokeMethodIfExists] invoke {methodName} failed: {ex}");
            }
        }

        private void TrySeedInitialSample(DisplayItem item)
        {
            try
            {
                if (item == null) return;

                double? seedValue = null;
                string seedText = null;
                string unit = null;

                // attempt reading from plugin/AIDA via available properties
                try
                {
                    // check PluginSensorId / Id/Instance/EntryId 
                    var pluginId = item.GetType().GetProperty("PluginSensorId")?.GetValue(item) as string;

                    if (!string.IsNullOrEmpty(pluginId))
                    {
                        try
                        {
                            var sr = SensorReader.ReadPluginSensor(pluginId);
                            if (sr.HasValue)
                            {
                                seedValue = sr.Value.ValueNow;
                                seedText = sr.Value.ValueText;
                                unit = sr.Value.Unit;
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine("[TrySeedInitialSample] ReadPluginSensor failed: " + ex); }
                    }

                    // --- Plugin / AIDA-only seeding block---
                    if (seedValue == null)
                    {
                        try
                        {
                            // 1) Try plugin monitor (preferred)
                            var pluginIdProp = item.GetType().GetProperty("PluginSensorId");
                            var sensorNameProp = item.GetType().GetProperty("SensorName");

                            string? pluginIdCandidate = pluginIdProp?.GetValue(item)?.ToString();
                            string? sensorNameCandidate = sensorNameProp?.GetValue(item)?.ToString();

                            if (!string.IsNullOrWhiteSpace(pluginIdCandidate))
                            {
                                var pr = SensorReader.ReadPluginSensor(pluginIdCandidate);
                                if (pr.HasValue)
                                {
                                    seedValue = pr.Value.ValueNow;
                                    seedText = pr.Value.ValueText;
                                    unit = pr.Value.Unit;
                                }
                            }

                            // 2) Try AIDA lookup by sensor name/id (best-effort)
                            if (seedValue == null && !string.IsNullOrWhiteSpace(sensorNameCandidate))
                            {
                                try
                                {
                                    var aida = new SynQPanel.Aida.AidaHash();
                                    var sensors = aida.RefreshSensorData();
                                    var match = sensors.FirstOrDefault(s =>
                                        string.Equals(s.Id, sensorNameCandidate, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(s.Label, sensorNameCandidate, StringComparison.OrdinalIgnoreCase));
                                    if (match != null)
                                    {
                                        if (double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numeric))
                                        {
                                            seedValue = numeric;
                                            seedText = match.Value;
                                            unit = match.Type switch
                                            {
                                                "temp" => "°C",
                                                "volt" => "V",
                                                "fan" => "RPM",
                                                "pwr" => "W",
                                                "curr" => "A",
                                                "gpu fan" => "RPM",
                                                _ => ""
                                            };
                                        }
                                        else
                                        {
                                            // string value available
                                            seedText = match.Value;
                                        }
                                    }
                                }
                                catch { /* ignore AIDA errors — seeding is best-effort */ }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[TrySeedInitialSample] plugin/AIDA seed block failed: " + ex);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[TrySeedInitialSample] read block failed: " + ex);
                }


                // Set unit & sensor type on item if properties exist
                if (!string.IsNullOrEmpty(unit)) TrySetPropertyIfExists(item, "Unit", unit);
                TrySetPropertyIfExists(item, "SensorType", SensorType);

                // If we have seed data, try common setters + method calls
                if (seedValue.HasValue || !string.IsNullOrEmpty(seedText))
                {
                    TrySetPropertyIfExists(item, "InitialValue", seedValue ?? (object?)null);
                    TrySetPropertyIfExists(item, "SeedValue", seedValue ?? (object?)null);
                    TrySetPropertyIfExists(item, "LastValue", seedValue ?? (object?)null);

                    // attempt to add to DataPoints or call AddSample-like methods
                    try
                    {
                        var dpProp = item.GetType().GetProperty("DataPoints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (dpProp != null)
                        {
                            var dp = dpProp.GetValue(item);
                            if (dp is System.Collections.IList list)
                            {
                                list.Add(seedValue.HasValue ? (object)seedValue.Value : (object)seedText ?? string.Empty);
                                Debug.WriteLine("[TrySeedInitialSample] added seed to DataPoints collection");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[TrySeedInitialSample] DataPoints add failed: " + ex);
                    }

                    var addNames = new[] { "AddSample", "AddPoint", "AppendSample", "AppendData", "PushValue", "SeedSample", "Add" };
                    foreach (var name in addNames) TryInvokeMethodIfExists(item, name, seedValue.HasValue ? new object[] { seedValue.Value } : new object[] { seedText ?? string.Empty });

                    // Try initialization hooks
                    var initNames = new[] { "Initialize", "Init", "Start", "Begin" };
                    foreach (var n in initNames) TryInvokeMethodIfExists(item, n, new object[] { });

                    Debug.WriteLine($"[TrySeedInitialSample] seeded value={seedValue} text='{seedText}' unit='{unit}' for item {item.GetType().Name}");
                }
                else
                {
                    Debug.WriteLine("[TrySeedInitialSample] no seed value available for item " + item.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[TrySeedInitialSample] outer exception: " + ex);
            }
        }

        // ---------- Button handlers ---------------------------------------------
        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSensorItem == null || SharedModel.Instance.SelectedProfile is not Profile selectedProfile)
                return;

            switch (SensorType)
            {
                case SensorType.Plugin:
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                    {
                        // existing non-AIDA plugin path (you can keep this as you had it)
                        var item = new SensorDisplayItem(pluginItem.Name, selectedProfile, pluginItem.SensorId)
                        {
                            SensorType = SensorType.Plugin,
                            PluginSensorId = pluginItem.SensorId,
                            SensorName = pluginItem.SensorId,
                            Font = selectedProfile.Font,
                            FontSize = selectedProfile.FontSize,
                            Color = selectedProfile.Color,
                            Unit = pluginItem.Unit,
                            // mark as new so saver knows to create a fresh .sensorpanel line
                            OriginalLineIndex = -1,
                            OriginalRawXml = null
                        };

                        SharedModel.Instance.AddDisplayItem(item);
                        RefreshDisplay();
                    }
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                    {
                        string sensorId = aidaItem.Id?.ToString() ?? aidaItem.Name;

                        var item = new SensorDisplayItem(aidaItem.Name, selectedProfile, sensorId)
                        {
                            SensorType = SensorType.Plugin,
                            PluginSensorId = sensorId,
                            SensorName = sensorId,
                            Font = selectedProfile.Font,
                            FontSize = selectedProfile.FontSize,
                            Color = selectedProfile.Color,

                            OriginalLineIndex = -1,
                            OriginalRawXml = null
                        };

                        TrySetPropertyIfExists(item, "Unit",
                            aidaItem.GetType().GetProperty("Unit")?.GetValue(aidaItem));

                        SharedModel.Instance.AddDisplayItem(item);
                        RefreshDisplay();
                    }

                    break;
            }
        }


        private void ButtonReplace_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== Replace button clicked ===");

            if (SelectedSensorItem == null)
            {
                System.Diagnostics.Debug.WriteLine("SelectedSensorItem is null.");
                return;
            }

            var selectedDisplayItem = SharedModel.Instance.SelectedItem;
            if (selectedDisplayItem == null)
            {
                System.Diagnostics.Debug.WriteLine("SelectedDisplayItem is null.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"SelectedSensorItem type: {SelectedSensorItem.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"SelectedDisplayItem type: {selectedDisplayItem.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"SensorType: {SensorType}");

            switch (SensorType)
            {
                
                case SensorType.Plugin:
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                        ReplacePluginSensor(pluginItem, selectedDisplayItem);
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                        ReplaceAidaSensor(aidaItem, selectedDisplayItem); // <-- add this fallback!
                    break;
            }
            RefreshDisplay();
          
            System.Diagnostics.Debug.WriteLine("Replace handler finished.");
        }


        private void ReplaceAidaSensor(AidaSensorTreeItem sensorItem, DisplayItem displayItem)
        {
            // sensorItem.Id is stored as object on base; ensure we get string
            string idString = sensorItem.Id?.ToString() ?? string.Empty;

            if (displayItem is SensorDisplayItem sdi)
            {
                sdi.Name = sensorItem.Name;
                sdi.SensorName = sensorItem.Name;
                // Treat AIDA as plugin-style lookup (string id)
                sdi.SensorType = SensorType.Plugin;
                sdi.PluginSensorId = idString;
                // Optionally set a unit if the Aida item exposes Unit
                try
                {
                    var prop = sensorItem.GetType().GetProperty("Unit");
                    if (prop != null)
                    {
                        var u = prop.GetValue(sensorItem) as string;
                        if (!string.IsNullOrEmpty(u))
                            sdi.Unit = u;
                    }
                }
                catch { }
            }
            else if (displayItem is ChartDisplayItem chart)
            {
                chart.Name = sensorItem.Name;
                chart.SensorName = sensorItem.Name;
                chart.SensorType = SensorType.Aida;
                chart.PluginSensorId = idString;
            }
            else if (displayItem is GaugeDisplayItem gauge)
            {
                gauge.Name = sensorItem.Name;
                gauge.SensorName = sensorItem.Name;
                gauge.SensorType = SensorType.Aida;
                gauge.PluginSensorId = idString;
            }
            else if (displayItem is SensorImageDisplayItem simg)
            {
                simg.Name = sensorItem.Name;
                simg.SensorName = sensorItem.Name;
                simg.SensorType = SensorType.Aida;
                simg.PluginSensorId = idString;
            }
        }

        private void ReplacePluginSensor(object sensorItem, DisplayItem displayItem)
        {
            // Support replacing with AIDA sensors as well as PluginSensorItem
            if (sensorItem is AidaSensorTreeItem aidaItem)
            {
                string idString = aidaItem.Id?.ToString() ?? string.Empty;

                if (displayItem is SensorDisplayItem sensorDisplayItem)
                {
                    sensorDisplayItem.Name = aidaItem.Name;
                    sensorDisplayItem.SensorName = aidaItem.Name;
                    sensorDisplayItem.SensorType = SensorType.Aida;
                    sensorDisplayItem.PluginSensorId = idString;
                    try
                    {
                        var prop = aidaItem.GetType().GetProperty("Unit");
                        if (prop != null)
                        {
                            var u = prop.GetValue(aidaItem) as string;
                            if (!string.IsNullOrEmpty(u)) sensorDisplayItem.Unit = u;
                        }
                    }
                    catch { }
                }
                else if (displayItem is ChartDisplayItem chart)
                {
                    chart.Name = aidaItem.Name;
                    chart.SensorName = aidaItem.Name;
                    chart.SensorType = SensorType.Aida;
                    chart.PluginSensorId = idString;
                }
                else if (displayItem is GaugeDisplayItem gauge)
                {
                    gauge.Name = aidaItem.Name;
                    gauge.SensorName = aidaItem.Name;
                    gauge.SensorType = SensorType.Aida;
                    gauge.PluginSensorId = idString;
                }
                else if (displayItem is SensorImageDisplayItem simg)
                {
                    simg.Name = aidaItem.Name;
                    simg.SensorName = aidaItem.Name;
                    simg.SensorType = SensorType.Aida;
                    simg.PluginSensorId = idString;
                }
                else if (displayItem is HttpImageDisplayItem httpimg)
                {
                    httpimg.Name = aidaItem.Name;
                    httpimg.SensorName = aidaItem.Name;
                    httpimg.SensorType = SensorType.Aida;
                    httpimg.PluginSensorId = idString;
                }
                else if (displayItem is TableSensorDisplayItem table)
                {
                    table.Name = aidaItem.Name;
                    table.SensorName = aidaItem.Name;
                    table.SensorType = SensorType.Aida;
                    table.PluginSensorId = idString;
                    // TableFormat support if needed
                    if (SensorReader.ReadPluginSensor(idString) is SensorReading sr && sr.ValueTableFormat is string format)
                    {
                        table.TableFormat = format;
                    }
                }
                return;
            }

            // Original logic for PluginSensorItem
            if (sensorItem is PluginSensorItem pluginItem)
            {
                if (displayItem is SensorDisplayItem sensorDisplayItem)
                {
                    sensorDisplayItem.Name = pluginItem.Name;
                    sensorDisplayItem.SensorName = pluginItem.Name;
                    sensorDisplayItem.SensorType = SensorType.Plugin;
                    sensorDisplayItem.PluginSensorId = pluginItem.SensorId;
                    sensorDisplayItem.Unit = pluginItem.Unit;
                }
                else if (displayItem is ChartDisplayItem chartDisplayItem)
                {
                    chartDisplayItem.Name = pluginItem.Name;
                    chartDisplayItem.SensorName = pluginItem.Name;
                    chartDisplayItem.SensorType = SensorType.Plugin;
                    chartDisplayItem.PluginSensorId = pluginItem.SensorId;
                }
                else if (displayItem is GaugeDisplayItem gaugeDisplayItem)
                {
                    gaugeDisplayItem.Name = pluginItem.Name;
                    gaugeDisplayItem.SensorName = pluginItem.Name;
                    gaugeDisplayItem.SensorType = SensorType.Plugin;
                    gaugeDisplayItem.PluginSensorId = pluginItem.SensorId;
                }
                else if (displayItem is SensorImageDisplayItem sensorImageDisplayItem)
                {
                    sensorImageDisplayItem.Name = pluginItem.Name;
                    sensorImageDisplayItem.SensorName = pluginItem.Name;
                    sensorImageDisplayItem.SensorType = SensorType.Plugin;
                    sensorImageDisplayItem.PluginSensorId = pluginItem.SensorId;
                }
                else if (displayItem is HttpImageDisplayItem httpImageDisplayItem)
                {
                    httpImageDisplayItem.Name = pluginItem.Name;
                    httpImageDisplayItem.SensorName = pluginItem.Name;
                    httpImageDisplayItem.SensorType = SensorType.Plugin;
                    httpImageDisplayItem.PluginSensorId = pluginItem.SensorId;
                }
                else if (displayItem is TableSensorDisplayItem tableSensorDisplayItem)
                {
                    tableSensorDisplayItem.Name = pluginItem.Name;
                    tableSensorDisplayItem.SensorName = pluginItem.Name;
                    tableSensorDisplayItem.SensorType = SensorType.Plugin;
                    tableSensorDisplayItem.PluginSensorId = pluginItem.SensorId;
                    if (SensorReader.ReadPluginSensor(pluginItem.SensorId) is SensorReading sensorReading && sensorReading.ValueTableFormat is string format)
                    {
                        tableSensorDisplayItem.TableFormat = format;
                    }
                }
            }
        }


        //------------------------------------------------------

        private void ButtonAddGraph_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSensorItem == null || SharedModel.Instance.SelectedProfile is not Profile selectedProfile) return;

            switch (SensorType)
            {
                case SensorType.Plugin:
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                    {
                        var item = new GraphDisplayItem(pluginItem.Name, selectedProfile, GraphDisplayItem.GraphType.LINE)
                        {
                            PluginSensorId = pluginItem.SensorId,
                            SensorType = SensorType.Plugin
                        };
                        TrySetPropertyIfExists(item, "Unit", pluginItem.Unit);
                        SafeAddAndRefresh(item);
                    }
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                    {
                        var graphItem = new GraphDisplayItem(
                            aidaItem.Name,
                            selectedProfile,
                            GraphDisplayItem.GraphType.LINE
                        );
                        graphItem.SensorType = SensorType.Plugin;
                        graphItem.PluginSensorId = aidaItem.Id?.ToString();

                        // Add this debug output:
                        System.Diagnostics.Debug.WriteLine($"[AIDA DEBUG] Adding graph for sensorId: {graphItem.PluginSensorId}");

                        TrySetPropertyIfExists(graphItem, "Unit", aidaItem.GetType().GetProperty("Unit")?.GetValue(aidaItem));
                        SafeAddAndRefresh(graphItem);
                    }
                    break;
            }
        }

        private void ButtonAddBar_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSensorItem == null || SharedModel.Instance.SelectedProfile is not Profile selectedProfile)
                return;

            switch (SensorType)
            {
                case SensorType.Plugin:
                    // Non-AIDA plugin sensors
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                    {
                        string sensorId = pluginItem.SensorId;

                        var item = new BarDisplayItem(pluginItem.Name, selectedProfile)
                        {
                            SensorType = SensorType.Plugin,
                            PluginSensorId = sensorId,
                            SensorName = sensorId,   // helps mapping & exporter
                                                     // mark as new so SensorPanelSaver uses CreateSnippetForNewItem
                            OriginalLineIndex = -1,
                            OriginalRawXml = null
                        };

                        TrySetPropertyIfExists(item, "Unit", pluginItem.Unit);

                        SafeAddAndRefresh(item);
                    }
                    // AIDA sensors
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                    {
                        string sensorId = aidaItem.Id?.ToString() ?? aidaItem.Name;

                        var item = new BarDisplayItem(aidaItem.Name, selectedProfile)
                        {
                            SensorType = SensorType.Plugin,
                            PluginSensorId = sensorId,
                            SensorName = sensorId,   // key for AIDA mapping
                                                     // mark as new so SensorPanelSaver uses CreateSnippetForNewItem
                            OriginalLineIndex = -1,
                            OriginalRawXml = null
                        };

                        TrySetPropertyIfExists(item, "Unit",
                            aidaItem.GetType().GetProperty("Unit")?.GetValue(aidaItem));

                        SafeAddAndRefresh(item);
                    }
                    break;
            }
        }


        private void ButtonAddDonut_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSensorItem == null || SharedModel.Instance.SelectedProfile is not Profile selectedProfile) return;

            switch (SensorType)
            {
                case SensorType.Plugin:
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                    {
                        var item = new DonutDisplayItem(pluginItem.Name, selectedProfile)
                        {
                            PluginSensorId = pluginItem.SensorId,
                            SensorType = SensorType.Plugin
                        };
                        TrySetPropertyIfExists(item, "Unit", pluginItem.Unit);
                        SafeAddAndRefresh(item);
                    }
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                    {
                        var item = new DonutDisplayItem(aidaItem.Name, selectedProfile)
                        {
                            PluginSensorId = aidaItem.Id?.ToString(),
                            SensorType = SensorType.Plugin
                        };
                        TrySetPropertyIfExists(item, "Unit", aidaItem.GetType().GetProperty("Unit")?.GetValue(aidaItem));
                        SafeAddAndRefresh(item);
                    }
                    break;
            }
        }

        private void ButtonAddCustom_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSensorItem == null || SharedModel.Instance.SelectedProfile is not Profile selectedProfile) return;

            switch (SensorType)
            {
                case SensorType.Plugin:
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                    {
                        var item = new GaugeDisplayItem(pluginItem.Name, selectedProfile)
                        {
                            PluginSensorId = pluginItem.SensorId,
                            SensorType = SensorType.Plugin
                        };
                        TrySetPropertyIfExists(item, "Unit", pluginItem.Unit);
                        SafeAddAndRefresh(item);
                    }
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                    {
                        var gaugeItem = new GaugeDisplayItem(
                            aidaItem.Name,
                            selectedProfile
                        );
                        gaugeItem.SensorType = SensorType.Plugin;
                        gaugeItem.PluginSensorId = aidaItem.Id?.ToString();

                        TrySetPropertyIfExists(gaugeItem, "Unit", aidaItem.GetType().GetProperty("Unit")?.GetValue(aidaItem));
                        SafeAddAndRefresh(gaugeItem);
                    }

                    break;
            }
        }

        private void ButtonAddSensorImage_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSensorItem == null || SharedModel.Instance.SelectedProfile is not Profile selectedProfile) return;

            switch (SensorType)
            {
                case SensorType.Plugin:
                    if (SelectedSensorItem is PluginSensorItem pluginItem)
                    {
                        var item = new SensorImageDisplayItem(pluginItem.Name, selectedProfile)
                        {
                            Width = 100,
                            Height = 100,
                            PluginSensorId = pluginItem.SensorId,
                            SensorType = SensorType.Plugin
                        };
                        SafeAddAndRefresh(item);
                    }
                    // Fallback for AIDA sensor support
                    else if (SelectedSensorItem is AidaSensorTreeItem aidaItem)
                    {
                        var item = new SensorImageDisplayItem(aidaItem.Name ?? "AIDA Image", selectedProfile)
                        {
                            Width = 100,
                            Height = 100,
                            PluginSensorId = aidaItem.Id?.ToString(),
                            SensorType = SensorType.Plugin
                        };
                        SafeAddAndRefresh(item);
                    }
                    break;

            }
        }
    }
}
