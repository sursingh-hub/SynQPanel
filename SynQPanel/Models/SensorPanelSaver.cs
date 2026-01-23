using SynQPanel.Models;
using SynQPanel.Drawing;// if DisplayItem / Profile are here
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using Vortice.Direct2D1.Effects;
using System.Drawing.Drawing2D;




namespace SynQPanel.Models
{
    public static class SensorPanelSaver
    {

        // tracks original line indices of items deleted in the current session
        private static readonly HashSet<int> _deletedLineIndices = new HashSet<int>();

        public static void RegisterDeletedLineIndex(int lineIndex)
        {
            if (lineIndex >= 0)
            {
                _deletedLineIndices.Add(lineIndex);
            }
        }

        private static HashSet<int> GetAndClearDeletedLineSet()
        {
            // make a copy and clear for next save
            var copy = new HashSet<int>(_deletedLineIndices);
            _deletedLineIndices.Clear();
            return copy;
        }


        public static void SaveSensorPanel(string sensorPanelPath, ICollection<DisplayItem> displayItems)
        {
            if (string.IsNullOrWhiteSpace(sensorPanelPath)) throw new ArgumentNullException(nameof(sensorPanelPath));
            if (!File.Exists(sensorPanelPath)) throw new FileNotFoundException("sensorpanel not found", sensorPanelPath);

            var encoding = Encoding.GetEncoding("iso-8859-1");
            var lines = File.ReadAllLines(sensorPanelPath, encoding);
            var outLines = (string[])lines.Clone();

            // Build a fast lookup for deleted original line indices
            //var deletedLineSet = new HashSet<int>(profile.DeletedSensorPanelLineIndices ?? new List<int>());

           
            // Apply deletions from UI to the cloned output lines before saving
            var deletedLineSetNow = GetAndClearDeletedLineSet();
            if (deletedLineSetNow.Count > 0)
            {
                foreach (var idx in deletedLineSetNow)
                {
                    if (idx >= 0 && idx < outLines.Length)
                    {
                        outLines[idx] = null;
                    }
                }
            }



            // Helper to set or create child element value in the parsed <Root> wrapper
            static void SetOrCreateChildValue(XElement root, string childName, string value)
            {
                var el = root.Elements(childName).FirstOrDefault();
                if (el != null)
                {
                    el.Value = value ?? string.Empty;
                }
                else
                {
                    root.Add(new XElement(childName, value ?? string.Empty));
                }
            }

            // Helper to update ID value preserving any prefixes like [SIMPLE], [GAUGE], leading '-' etc.
            static void UpdateIdPreservePrefix(XElement root, string newKey)
            {
                var idEl = root.Elements("ID").FirstOrDefault();
                if (idEl == null)
                {
                    root.Add(new XElement("ID", newKey ?? string.Empty));
                    return;
                }

                var orig = idEl.Value ?? string.Empty;
                var core = orig;

                // capture prefix markers
                var prefix = string.Empty;
                if (core.StartsWith("-"))
                {
                    prefix = "-";
                    core = core.Substring(1);
                }

                var prefixTags = new List<string>();
                while (core.StartsWith("["))
                {
                    int idx = core.IndexOf(']');
                    if (idx > 0)
                    {
                        prefixTags.Add(core.Substring(0, idx + 1));
                        core = core.Substring(idx + 1);
                    }
                    else break;
                }

                var newCore = newKey ?? core;
                var newId = prefix + string.Concat(prefixTags) + newCore;
                idEl.Value = newId;
            }

            // Process each display item with provenance
            foreach (var item in displayItems)
            {
                try
                {
                    if (item.OriginalLineIndex.HasValue && !string.IsNullOrWhiteSpace(item.OriginalRawXml))
                    {
                        int li = item.OriginalLineIndex.Value;
                        if (li < 0 || li >= outLines.Length) continue;

                        // Parse the original raw inner XML
                        var root = XElement.Parse($"<Root>{EscapeContentWithinLBL(item.OriginalRawXml)}</Root>");

                        // Position: ITMX / ITMY
                        SetOrCreateChildValue(root, "ITMX", ((int)item.X).ToString(CultureInfo.InvariantCulture));
                        SetOrCreateChildValue(root, "ITMY", ((int)item.Y).ToString(CultureInfo.InvariantCulture));

                        // Label: use the DisplayItem.Name (your codebase uses Name elsewhere)
                        var labelValue = item is DisplayItem diBase ? diBase.Name : null;
                        if (!string.IsNullOrWhiteSpace(labelValue))
                        {
                            SetOrCreateChildValue(root, "LBL", labelValue);
                        }

                        // For width/height or other numeric props we read concrete types
                        switch (item)
                        {


                          case GaugeDisplayItem g:
                                // --- Size and value range ---
                                try
                                {
                                    // Gauge size in AIDA is RESIZW / RESIZH (already used on import)
                                    SetOrCreateChildValue(root, "RESIZW", ((int)g.Width).ToString(CultureInfo.InvariantCulture));
                                    SetOrCreateChildValue(root, "RESIZH", ((int)g.Height).ToString(CultureInfo.InvariantCulture));

                                    // Min/Max values
                                    SetOrCreateChildValue(root, "MINVAL", ((int)g.MinValue).ToString(CultureInfo.InvariantCulture));
                                    SetOrCreateChildValue(root, "MAXVAL", ((int)g.MaxValue).ToString(CultureInfo.InvariantCulture));
                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SensorPanelSaver] Gauge size/range write error: {ex.Message}");
                                }

                                // --- STAFLS: gauge state image list ---
                                // We try to reconstruct "image1.png|image2.png|..." from GaugeDisplayItem.Images.
                                // If we can't get clean filenames for all images, we LEAVE existing STAFLS untouched.
                                try
                                {
                                    var staflsElement = root.Elements("STAFLS").FirstOrDefault();
                                    var existingStafls = staflsElement?.Value ?? string.Empty;

                                    if (g.Images != null && g.Images.Count > 0)
                                    {
                                        var fileNames = new List<string>();

                                        foreach (var img in g.Images)
                                        {
                                            if (img == null) continue;

                                            var t = img.GetType();
                                            string name = null;

                                            // Common property names we saw in similar code: AssetName, Name, FileName, ImageName
                                            var pName = t.GetProperty("AssetName")
                                                       ?? t.GetProperty("Name")
                                                       ?? t.GetProperty("FileName")
                                                       ?? t.GetProperty("ImageName");

                                            if (pName != null)
                                            {
                                                var v = pName.GetValue(img) as string;
                                                if (!string.IsNullOrWhiteSpace(v))
                                                {
                                                    name = System.IO.Path.GetFileName(v);
                                                }
                                            }

                                            // Fallback: Path / Source property
                                            if (string.IsNullOrWhiteSpace(name))
                                            {
                                                var pPath = t.GetProperty("Path") ?? t.GetProperty("Source");
                                                if (pPath != null)
                                                {
                                                    var v = pPath.GetValue(img)?.ToString();
                                                    if (!string.IsNullOrWhiteSpace(v))
                                                    {
                                                        name = System.IO.Path.GetFileName(v);
                                                    }
                                                }
                                            }

                                            if (!string.IsNullOrWhiteSpace(name))
                                                fileNames.Add(name);
                                        }

                                        // Only update STAFLS if we have a filename for *every* gauge image.
                                        if (fileNames.Count == g.Images.Count && fileNames.Count > 0)
                                        {
                                            string newStafls = string.Join("|", fileNames);
                                            SetOrCreateChildValue(root, "STAFLS", newStafls);
                                            DevTrace.Write($"[SensorPanelSaver] Gauge STAFLS updated -> {newStafls}");
                                        }
                                        else
                                        {
                                            // Cannot reliably reconstruct filenames: keep original STAFLS as-is
                                            if (staflsElement != null)
                                                staflsElement.Value = existingStafls;
                                        }


                                        // --- Gauge value text (AIDA compatible) ---
                                        try
                                        {
                                            SetOrCreateChildValue(root, "SHWVAL", g.ShowValue ? "1" : "0");

                                            if (g.ShowValue)
                                            {
                                                SetOrCreateChildValue(
                                                    root,
                                                    "TXTSIZ",
                                                    (g.ValueTextSize > 0 ? g.ValueTextSize : 12)
                                                        .ToString(CultureInfo.InvariantCulture)
                                                );

                                                SetOrCreateChildValue(
                                                    root,
                                                    "FNTNAM",
                                                    string.IsNullOrWhiteSpace(g.ValueFontName)
                                                        ? "Segoe UI"
                                                        : g.ValueFontName
                                                );

                                                int valCol = HexToDecimalBgr(g.ValueColor);
                                                SetOrCreateChildValue(root, "VALCOL", valCol.ToString());

                                                char b = g.ValueBold ? '1' : '0';
                                                char i = g.ValueItalic ? '1' : '0';
                                                SetOrCreateChildValue(root, "VALBI", $"{b}{i}");
                                            }
                                            else
                                            {
                                                // AIDA still expects defaults even when hidden
                                                SetOrCreateChildValue(root, "TXTSIZ", "12");
                                                SetOrCreateChildValue(root, "FNTNAM", "Segoe UI");
                                                SetOrCreateChildValue(root, "VALCOL", "16777215");
                                                SetOrCreateChildValue(root, "VALBI", "00");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            DevTrace.Write($"[SensorPanelSaver] Gauge value text write error: {ex.Message}");
                                        }


                                    }
                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SensorPanelSaver] Gauge STAFLS write error: {ex.Message}");
                                }

                                // --- ID: use PluginSensorId (this is the bit you already patched, kept here for clarity) ---
                                try
                                {
                                    string idValue = null;

                                    var pluginIdProp = g.GetType().GetProperty("PluginSensorId");
                                    if (pluginIdProp != null)
                                    {
                                        var val = pluginIdProp.GetValue(g) as string;
                                        if (!string.IsNullOrWhiteSpace(val))
                                            idValue = val;
                                    }

                                    if (!string.IsNullOrWhiteSpace(idValue))
                                    {
                                        UpdateIdPreservePrefix(root, idValue);
                                        DevTrace.Write($"[SensorPanelSaver] Gauge ID set to plugin ID '{idValue}'");
                                    }
                                    // else: leave existing ID unchanged for safety
                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SensorPanelSaver] Gauge ID write error: {ex.Message}");
                                }

                                // --- Decide gauge type and MAXSTA based on image count ---
                                try
                                {
                                    int imageCount = g.Images?.Count ?? 0;

                                    // Read existing TYP if present
                                    var typElement = root.Elements("TYP").FirstOrDefault();
                                    string existingTyp = typElement?.Value ?? "Custom";

                                    // If there are >16 images, we should treat it as CustomN.
                                    // Otherwise keep whatever TYP was there (Custom / CustomN / etc.).
                                    string newTyp = existingTyp;
                                    if (imageCount > 16)
                                        newTyp = "CustomN";

                                    SetOrCreateChildValue(root, "TYP", newTyp);

                                    // MAXSTA = number_of_states_minus_1 (AIDA style: 0..MAXSTA)
                                    if (imageCount > 0)
                                    {
                                        int maxSta = imageCount - 1;
                                        SetOrCreateChildValue(root, "MAXSTA", maxSta.ToString(CultureInfo.InvariantCulture));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SensorPanelSaver] Gauge TYP/MAXSTA write error: {ex.Message}");
                                }

                                break;


                            case GraphDisplayItem gr:
                                // Basic size and value range
                                SetOrCreateChildValue(root, "WID", ((int)gr.Width).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "HEI", ((int)gr.Height).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "MINVAL", ((int)gr.MinValue).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "MAXVAL", ((int)gr.MaxValue).ToString(CultureInfo.InvariantCulture));

                                // Auto scale flag (AUTSCL: 1 = auto, 0 = fixed)
                                try
                                {
                                    SetOrCreateChildValue(root, "AUTSCL", gr.AutoValue ? "1" : "0");
                                }
                                catch
                                {
                                    // if AutoValue not present, ignore
                                }

                                // Step (GPHSTP) and Thickness (GPHTCK)
                                try
                                {
                                    SetOrCreateChildValue(root, "GPHSTP", ((int)gr.Step).ToString(CultureInfo.InvariantCulture));
                                }
                                catch { /* ignore if not present */ }

                                try
                                {
                                    SetOrCreateChildValue(root, "GPHTCK", ((int)gr.Thickness).ToString(CultureInfo.InvariantCulture));
                                }
                                catch { /* ignore if not present */ }

                                // Colors: graph line, background, frame -> GPHCOL, BGCOL, FRMCOL
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(gr.Color))
                                    {
                                        var dec = HexToDecimalBgr(gr.Color);
                                        SetOrCreateChildValue(root, "GPHCOL", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Graph line color -> {gr.Color} => {dec}");
                                    }

                                    if (!string.IsNullOrWhiteSpace(gr.BackgroundColor))
                                    {
                                        var dec = HexToDecimalBgr(gr.BackgroundColor);
                                        SetOrCreateChildValue(root, "BGCOL", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Graph background color -> {gr.BackgroundColor} => {dec}");
                                    }

                                    if (!string.IsNullOrWhiteSpace(gr.FrameColor))
                                    {
                                        var dec = HexToDecimalBgr(gr.FrameColor);
                                        SetOrCreateChildValue(root, "FRMCOL", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Graph frame color -> {gr.FrameColor} => {dec}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SensorPanelSaver] Graph color write error: {ex.Message}");
                                }

                                // GPHBFG: 3 chars, typically [background][frame][grid]
                                try
                                {
                                    var existing = root.Elements("GPHBFG").FirstOrDefault()?.Value ?? "000";
                                    char c0 = existing.Length > 0 ? existing[0] : '0';
                                    char c1 = existing.Length > 1 ? existing[1] : '0';
                                    char c2 = existing.Length > 2 ? existing[2] : '0';

                                    // Use GraphDisplayItem booleans if present
                                    bool background = false;
                                    bool frame = false;

                                    try
                                    {
                                        background = gr.Background;
                                    }
                                    catch { }

                                    try
                                    {
                                        frame = gr.Frame;
                                    }
                                    catch { }

                                    char n0 = background ? '1' : c0;
                                    char n1 = frame ? '1' : c1;
                                    char n2 = c2; // grid flag untouched unless you later add a "Grid" property

                                    string newGphbfg = new string(new[] { n0, n1, n2 });
                                    SetOrCreateChildValue(root, "GPHBFG", newGphbfg);
                                }
                                catch { /* ignore */ }

                                // If plugin sensor ID is known, use it for ID (AIDA expects internal ID like SNIC6DLRATE, not label text)
                                try
                                {
                                    string idValue = null;

                                    // Prefer the real AIDA ID
                                    var pluginIdProp = gr.GetType().GetProperty("PluginSensorId");
                                    if (pluginIdProp != null)
                                    {
                                        var val = pluginIdProp.GetValue(gr) as string;
                                        if (!string.IsNullOrWhiteSpace(val))
                                            idValue = val;
                                    }

                                    // If we don't have a plugin ID, do NOT overwrite ID with label.
                                    // Just leave existing ID as-is for safety.

                                    if (!string.IsNullOrWhiteSpace(idValue))
                                    {
                                        UpdateIdPreservePrefix(root, idValue);
                                        DevTrace.Write($"[SensorPanelSaver] Graph ID set to plugin ID '{idValue}'");
                                    }
                                }
                                catch { /* ignore */ }
                                break;


                            case BarDisplayItem b:
                                // Update numeric layout fields used by .sensorpanel
                                SetOrCreateChildValue(root, "BARWID", ((int)b.Width).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "BARHEI", ((int)b.Height).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "BARMIN", ((int)b.MinValue).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "BARMAX", ((int)b.MaxValue).ToString(CultureInfo.InvariantCulture));

                                // ---- Robust color persistence ----
                                try
                                {
                                    // candidate property names to try for frame, main color, background/gradient, minfg/minbg
                                    var candidatesFrame = new[] { "FrameColor", "Frame", "FrameColour", "BarFrameColor", "BARFRMCOL", "FrameColourHex" };
                                    var candidatesMain = new[] { "Color", "BarColor", "FillColor", "ColorHex", "MainColor" };
                                    var candidatesBg = new[] { "BackgroundColor", "Background", "BackgroundColour", "GradientColor", "GradientColour", "BgColor" };
                                    var candidatesMinFg = new[] { "MinForegroundColor", "MinFgColor", "MinFg", "BARMINFGC", "BarMinForeground" };
                                    var candidatesMinBg = new[] { "MinBackgroundColor", "MinBgColor", "MinBg", "BARMINBGC", "BarMinBackground" };
                                    var candidatesLim = new[] { "GradientColor", "GradientColorHex", "LimitColor", "LimitColour", "BARLIM3BGC" };

                                    string frameStr = null;
                                    string mainStr = null;
                                    string bgStr = null;
                                    string minFgStr = null;
                                    string minBgStr = null;
                                    string limStr = null;

                                    // helper to try reading many candidate string props
                                    string tryCandidatesParams(object obj, string[] names)
                                    {
                                        foreach (var n in names)
                                        {
                                            var p = obj.GetType().GetProperty(n);
                                            if (p == null) continue;
                                            var v = p.GetValue(obj);
                                            if (v == null) continue;
                                            var s = v.ToString();
                                            if (!string.IsNullOrWhiteSpace(s)) return s;
                                        }
                                        return null;
                                    }

                                    frameStr = tryCandidatesParams(b, candidatesFrame);
                                    mainStr = tryCandidatesParams(b, candidatesMain);
                                    bgStr = tryCandidatesParams(b, candidatesBg);
                                    minFgStr = tryCandidatesParams(b, candidatesMinFg);
                                    minBgStr = tryCandidatesParams(b, candidatesMinBg);
                                    limStr = tryCandidatesParams(b, candidatesLim);

                                    // Convert+write values (if found)
                                    if (!string.IsNullOrWhiteSpace(frameStr))
                                    {
                                        var dec = HexToDecimalBgr(frameStr);
                                        SetOrCreateChildValue(root, "BARFRMCOL", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Bar frame color set -> {frameStr} => {dec}");
                                    }

                                    // main color -> try BARMINFGC & BARMINBGC mapping if no specific limit fields present
                                    if (!string.IsNullOrWhiteSpace(mainStr))
                                    {
                                        var dec = HexToDecimalBgr(mainStr);

                                        // Prefer mapping the main (user-chosen) bar color to the highest limit block:
                                        // BARLIM3FGC / BARLIM3BGC represent the color used for the top-most segment on many panels.
                                        //SetOrCreateChildValue(root, "BARLIM3FGC", dec.ToString(CultureInfo.InvariantCulture));
                                        //SetOrCreateChildValue(root, "BARLIM3BGC", dec.ToString(CultureInfo.InvariantCulture));

                                        SetOrCreateChildValue(root, "BARLIM3FGC", dec.ToString(CultureInfo.InvariantCulture));
                                        // do NOT touch BARLIM3BGC here – that's for background/gradient



                                        // Keep fallback for older panels (optional): comment out if you don't want these
                                        // SetOrCreateChildValue(root, "BARMINFGC", dec.ToString(CultureInfo.InvariantCulture));
                                        // SetOrCreateChildValue(root, "BARMINBGC", dec.ToString(CultureInfo.InvariantCulture));

                                        DevTrace.Write($"[SensorPanelSaver] Bar main color set -> {mainStr} => {dec} (written to BARLIM3*)");
                                    }


                                    if (!string.IsNullOrWhiteSpace(bgStr))
                                    {
                                        var dec = HexToDecimalBgr(bgStr);

                                        // Background/gradient → Bar 3 BG only
                                        SetOrCreateChildValue(root, "BARLIM3BGC", dec.ToString(CultureInfo.InvariantCulture));

                                        DevTrace.Write($"[SensorPanelSaver] Bar background/gradient color set -> {bgStr} => {dec} (BARLIM3BGC)");
                                    }


                                    /*

                                    if (!string.IsNullOrWhiteSpace(bgStr))
                                    {
                                        var dec = HexToDecimalBgr(bgStr);
                                        // populate both limit and background tags for broader compatibility
                                        SetOrCreateChildValue(root, "BARLIM3BGC", dec.ToString(CultureInfo.InvariantCulture));
                                        SetOrCreateChildValue(root, "BARLIM3FGC", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Bar background/gradient color set -> {bgStr} => {dec}");
                                    }

                                    */

                                    if (!string.IsNullOrWhiteSpace(minFgStr))
                                    {
                                        var dec = HexToDecimalBgr(minFgStr);
                                        SetOrCreateChildValue(root, "BARMINFGC", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Bar minFG color set -> {minFgStr} => {dec}");
                                    }
                                    if (!string.IsNullOrWhiteSpace(minBgStr))
                                    {
                                        var dec = HexToDecimalBgr(minBgStr);
                                        SetOrCreateChildValue(root, "BARMINBGC", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Bar minBG color set -> {minBgStr} => {dec}");
                                    }

                                    /*

                                    if (!string.IsNullOrWhiteSpace(limStr))
                                    {
                                        var dec = HexToDecimalBgr(limStr);
                                        SetOrCreateChildValue(root, "BARLIM3BGC", dec.ToString(CultureInfo.InvariantCulture));
                                        SetOrCreateChildValue(root, "BARLIM3FGC", dec.ToString(CultureInfo.InvariantCulture));
                                        DevTrace.Write($"[SensorPanelSaver] Bar limit color set -> {limStr} => {dec}");
                                    }

                                    */

                                }
                                catch (Exception ex)
                                {
                                    DevTrace.Write($"[SensorPanelSaver] Bar color write error: {ex.Message}");
                                }

                                // BARFS flags: frame, reserved, gradient, flipX (4 chars).
                                try
                                {
                                    var existingBarfs = root.Elements("BARFS").FirstOrDefault()?.Value ?? string.Empty;
                                    char c0 = existingBarfs.Length > 0 ? existingBarfs[0] : '0';
                                    char c1 = existingBarfs.Length > 1 ? existingBarfs[1] : '0';
                                    char c2 = existingBarfs.Length > 2 ? existingBarfs[2] : '0';
                                    char c3 = existingBarfs.Length > 3 ? existingBarfs[3] : '0';

                                    bool frame = TryGetBoolProp(b, "Frame") || TryGetBoolProp(b, "HasFrame");
                                    bool gradient = TryGetBoolProp(b, "Gradient") || TryGetBoolProp(b, "HasGradient");
                                    bool flipX = TryGetBoolProp(b, "FlipX") || TryGetBoolProp(b, "Flip") || TryGetBoolProp(b, "Flipped");

                                    char nc0 = frame ? '1' : c0;
                                    char nc1 = c1;
                                    char nc2 = gradient ? '1' : c2;
                                    char nc3 = flipX ? '1' : c3;

                                    string newBarfs = new string(new[] { nc0, nc1, nc2, nc3 });
                                    SetOrCreateChildValue(root, "BARFS", newBarfs);
                                }
                                catch { /* ignore */ }

                                // BARPLC (placement) handling if available
                                try
                                {
                                    var barplc = TryGetStringProp(b, "Placement") ?? TryGetStringProp(b, "BarPlacement") ?? TryGetStringProp(b, "BARPLC");
                                    if (!string.IsNullOrWhiteSpace(barplc))
                                    {
                                        SetOrCreateChildValue(root, "BARPLC", barplc);
                                    }
                                }
                                catch { /* ignore */ }

                                // If plugin sensor ID is known, use it for <ID> (AIDA expects SNIC..., not label text)
                                try
                                {
                                    string idValue = null;

                                    var pluginIdProp = b.GetType().GetProperty("PluginSensorId");
                                    if (pluginIdProp != null)
                                    {
                                        var val = pluginIdProp.GetValue(b) as string;
                                        if (!string.IsNullOrWhiteSpace(val))
                                            idValue = val;
                                    }

                                    if (!string.IsNullOrWhiteSpace(idValue))
                                    {
                                        UpdateIdPreservePrefix(root, idValue);
                                        DevTrace.Write($"[SensorPanelSaver] Bar ID set to plugin ID '{idValue}'");
                                    }
                                    // else: leave existing ID unchanged for safety
                                }
                                catch { /* ignore */ }
                                break;



                            case SensorDisplayItem s:
                                {

                                    // SensorDisplayItem: use PluginSensorId (AIDA internal ID) for <ID>,
                                    // not the human-readable SensorName.
                                    try
                                    {
                                        string idValue = null;

                                        var pluginIdProp = s.GetType().GetProperty("PluginSensorId");
                                        if (pluginIdProp != null)
                                        {
                                            var val = pluginIdProp.GetValue(s) as string;
                                            if (!string.IsNullOrWhiteSpace(val))
                                                idValue = val;
                                        }

                                        if (!string.IsNullOrWhiteSpace(idValue))
                                        {
                                            UpdateIdPreservePrefix(root, idValue);
                                            DevTrace.Write($"[SensorPanelSaver] Sensor ID set to plugin ID '{idValue}'");
                                        }
                                        // else: leave existing ID unchanged for safety
                                    }
                                    catch { /* ignore */ }





                                    // --- Font name & size mapping for sensor value text ---
                                    try
                                    {
                                        // Font name: SynQPanel usually stores it in s.Font
                                        var fontName = TryGetStringProp(s, "Font") ?? TryGetStringProp(s, "FontName");
                                        if (!string.IsNullOrWhiteSpace(fontName))
                                        {
                                            SetOrCreateChildValue(root, "FNTNAM", fontName);
                                            DevTrace.Write($"[SensorPanelSaver] Sensor font name set -> {fontName}");
                                        }

                                        // Font size: SynQPanel usually stores it in s.FontSize
                                        var fontSizeStr = TryGetStringProp(s, "FontSize");
                                        if (!string.IsNullOrWhiteSpace(fontSizeStr) &&
                                            int.TryParse(fontSizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out int fontSize) &&
                                            fontSize > 0)
                                        {
                                            SetOrCreateChildValue(root, "TXTSIZ", fontSize.ToString(CultureInfo.InvariantCulture));
                                            DevTrace.Write($"[SensorPanelSaver] Sensor font size set -> {fontSize}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DevTrace.Write($"[SensorPanelSaver] SensorDisplayItem font write error: {ex.Message}");
                                    }



                                    // --- NEW: Bold / Italic flags -> VALBIS ---
                                    try
                                    {
                                        // AIDA uses 3-char string: [Bold][Italic][(unused or right-align)]
                                        // We preserve the 3rd char if present.
                                        var existingValbis = root.Elements("VALBIS").FirstOrDefault()?.Value ?? "000";

                                        char boldFlag = s.Bold ? '1' : '0';
                                        char italicFlag = s.Italic ? '1' : '0';
                                        char thirdFlag = existingValbis.Length >= 3 ? existingValbis[2] : '0';

                                        string newValbis = new string(new[] { boldFlag, italicFlag, thirdFlag });

                                        SetOrCreateChildValue(root, "VALBIS", newValbis);
                                        DevTrace.Write($"[SensorPanelSaver] Sensor VALBIS updated -> {newValbis} (Bold={s.Bold}, Italic={s.Italic})");
                                    }
                                    catch (Exception ex)
                                    {
                                        DevTrace.Write($"[SensorPanelSaver] SensorDisplayItem VALBIS write error: {ex.Message}");
                                    }

                                    // --- Color mapping (already working) ---
                                    try
                                    {
                                        var colorStr = TryGetStringProp(s, "Color");
                                        if (!string.IsNullOrWhiteSpace(colorStr))
                                        {
                                            var dec = HexToDecimalBgr(colorStr);
                                            SetOrCreateChildValue(root, "VALCOL", dec.ToString(CultureInfo.InvariantCulture));
                                            SetOrCreateChildValue(root, "TXTCOL", dec.ToString(CultureInfo.InvariantCulture));
                                            DevTrace.Write($"[SensorPanelSaver] Sensor text color set -> {colorStr} => {dec}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DevTrace.Write($"[SensorPanelSaver] SensorDisplayItem color write error: {ex.Message}");
                                    }



                                    // --- Existing sensor ID / name mapping ---
                                    try
                                    {
                                        //  if (!string.IsNullOrWhiteSpace(s.SensorName))
                                        //      UpdateIdPreservePrefix(root, s.SensorName);
                                    }
                                    catch { /* ignore */ }


                                    break;

                                }
                                break;


                            case ImageDisplayItem img:
                                // update position only
                                SetOrCreateChildValue(root, "ITMX", ((int)img.X).ToString(CultureInfo.InvariantCulture));
                                SetOrCreateChildValue(root, "ITMY", ((int)img.Y).ToString(CultureInfo.InvariantCulture));
                                break;


                            case ClockDisplayItem c:
                                {
                                    // Map clock font and color back to AIDA tags
                                    try
                                    {
                                        // Font name
                                        var fontName = TryGetStringProp(c, "Font") ?? TryGetStringProp(c, "FontName");
                                        if (!string.IsNullOrWhiteSpace(fontName))
                                        {
                                            SetOrCreateChildValue(root, "FNTNAM", fontName);
                                            DevTrace.Write($"[SensorPanelSaver] Clock font name set -> {fontName}");
                                        }

                                        // Font size
                                        var fontSizeStr = TryGetStringProp(c, "FontSize");
                                        if (!string.IsNullOrWhiteSpace(fontSizeStr) &&
                                            int.TryParse(fontSizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out int fontSize) &&
                                            fontSize > 0)
                                        {
                                            SetOrCreateChildValue(root, "TXTSIZ", fontSize.ToString(CultureInfo.InvariantCulture));
                                            DevTrace.Write($"[SensorPanelSaver] Clock font size set -> {fontSize}");
                                        }

                                        // Color: we treat clock like a "value", so map to VALCOL/TXTCOL
                                        var colorStr = TryGetStringProp(c, "Color");
                                        if (!string.IsNullOrWhiteSpace(colorStr))
                                        {
                                            var dec = HexToDecimalBgr(colorStr);
                                            SetOrCreateChildValue(root, "VALCOL", dec.ToString(CultureInfo.InvariantCulture));
                                            SetOrCreateChildValue(root, "TXTCOL", dec.ToString(CultureInfo.InvariantCulture));
                                            DevTrace.Write($"[SensorPanelSaver] Clock color set -> {colorStr} => {dec}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DevTrace.Write($"[SensorPanelSaver] ClockDisplayItem write error: {ex.Message}");
                                    }

                                    break;
                                }


                            case CalendarDisplayItem cal:
                                {
                                    // Map calendar font and color back to AIDA tags
                                    try
                                    {
                                        // Font name
                                        var fontName = TryGetStringProp(cal, "Font") ?? TryGetStringProp(cal, "FontName");
                                        if (!string.IsNullOrWhiteSpace(fontName))
                                        {
                                            SetOrCreateChildValue(root, "FNTNAM", fontName);
                                            DevTrace.Write($"[SensorPanelSaver] Calendar font name set -> {fontName}");
                                        }

                                        // Font size
                                        var fontSizeStr = TryGetStringProp(cal, "FontSize");
                                        if (!string.IsNullOrWhiteSpace(fontSizeStr) &&
                                            int.TryParse(fontSizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out int fontSize) &&
                                            fontSize > 0)
                                        {
                                            SetOrCreateChildValue(root, "TXTSIZ", fontSize.ToString(CultureInfo.InvariantCulture));
                                            DevTrace.Write($"[SensorPanelSaver] Calendar font size set -> {fontSize}");
                                        }

                                        // Color
                                        var colorStr = TryGetStringProp(cal, "Color");
                                        if (!string.IsNullOrWhiteSpace(colorStr))
                                        {
                                            var dec = HexToDecimalBgr(colorStr);
                                            SetOrCreateChildValue(root, "VALCOL", dec.ToString(CultureInfo.InvariantCulture));
                                            SetOrCreateChildValue(root, "TXTCOL", dec.ToString(CultureInfo.InvariantCulture));
                                            DevTrace.Write($"[SensorPanelSaver] Calendar color set -> {colorStr} => {dec}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DevTrace.Write($"[SensorPanelSaver] CalendarDisplayItem write error: {ex.Message}");
                                    }

                                    break;
                                }






                            case TextDisplayItem tx:
                                // Update label text if Name changed
                                if (!string.IsNullOrWhiteSpace(tx.Name))
                                    SetOrCreateChildValue(root, "LBL", tx.Name);

                                // Font name (FNTNAM)
                                if (!string.IsNullOrWhiteSpace(tx.Font))
                                    SetOrCreateChildValue(root, "FNTNAM", tx.Font);

                                // Font size (TXTSIZ)
                                if (tx.FontSize > 0)
                                    SetOrCreateChildValue(root, "TXTSIZ", ((int)tx.FontSize).ToString(CultureInfo.InvariantCulture));

                                // Color: convert from UI hex (e.g. "#RRGGBB") to decimal BGR used in .sensorpanel (LBLCOL)
                                if (!string.IsNullOrWhiteSpace(tx.Color))
                                {
                                    try
                                    {
                                        int bgr = HexToDecimalBgr(tx.Color);
                                        SetOrCreateChildValue(root, "LBLCOL", bgr.ToString(CultureInfo.InvariantCulture));
                                    }
                                    catch
                                    {
                                        // ignore conversion errors, preserve original color
                                    }
                                }

                                // --- Robust detection of Bold / Italic / RightAlign using reflection ---
                                bool bold = false;
                                bool italic = false;
                                bool rightAlign = false;

                                try
                                {
                                    var ttype = tx.GetType();

                                    // 1) Direct boolean properties
                                    object TryGetBool(string name)
                                    {
                                        var p = ttype.GetProperty(name);
                                        if (p != null && p.PropertyType == typeof(bool))
                                        {
                                            return p.GetValue(tx);
                                        }
                                        return null;
                                    }

                                    var v = TryGetBool("Bold") ?? TryGetBool("IsBold") ?? TryGetBool("BoldStyle");
                                    if (v is bool vb) bold = vb;

                                    v = TryGetBool("Italic") ?? TryGetBool("IsItalic");
                                    if (v is bool vi) italic = vi;

                                    v = TryGetBool("RightAlign") ?? TryGetBool("IsRightAligned") ?? TryGetBool("Right");
                                    if (v is bool vr) rightAlign = vr;

                                    // 2) FontStyle string e.g. "Bold", "Light", "Bold Italic"
                                    if (!bold)
                                    {
                                        var pfs = ttype.GetProperty("FontStyle") ?? ttype.GetProperty("Style");
                                        if (pfs != null)
                                        {
                                            var sval = pfs.GetValue(tx)?.ToString() ?? string.Empty;
                                            if (!string.IsNullOrEmpty(sval))
                                            {
                                                if (sval.IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0) bold = true;
                                                if (sval.IndexOf("italic", StringComparison.OrdinalIgnoreCase) >= 0) italic = true;
                                                if (sval.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0) rightAlign = true;
                                            }
                                        }
                                    }

                                    // 3) FontWeight numeric or string (some frameworks expose FontWeight)
                                    if (!bold)
                                    {
                                        var pw = ttype.GetProperty("FontWeight") ?? ttype.GetProperty("Weight");
                                        if (pw != null)
                                        {
                                            var wval = pw.GetValue(tx);
                                            if (wval != null)
                                            {
                                                var s = wval.ToString();
                                                if (int.TryParse(s, out int wi))
                                                {
                                                    if (wi >= 700) bold = true; // typical threshold for bold
                                                }
                                                else
                                                {
                                                    if (s.IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0) bold = true;
                                                }
                                            }
                                        }
                                    }

                                    // 4) TextAlignment / HorizontalAlignment names for right align
                                    if (!rightAlign)
                                    {
                                        var pa = ttype.GetProperty("TextAlignment") ?? ttype.GetProperty("Alignment") ?? ttype.GetProperty("HorizontalAlignment");
                                        if (pa != null)
                                        {
                                            var aval = pa.GetValue(tx)?.ToString() ?? string.Empty;
                                            if (aval.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0)
                                                rightAlign = true;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // reflection should not break saving; ignore but log to console for debugging
                                    DevTrace.Write($"[SensorPanelSaver] Reflection detection error for TextDisplayItem: {ex.Message}");
                                }

                                // Write LBLBIS as three digits: Bold, Italic, RightAlign (1 or 0 each)
                                try
                                {
                                    string lblbis = $"{(bold ? 1 : 0)}{(italic ? 1 : 0)}{(rightAlign ? 1 : 0)}";
                                    SetOrCreateChildValue(root, "LBLBIS", lblbis);

                                    // optional debug output to persistent log (if you want to inspect)
                                    
                                    //     $"[SensorPanelSaver] TextDisplayItem '{tx.Name}' flags => Bold:{bold} Italic:{italic} Right:{rightAlign} LBLBIS={lblbis}{Environment.NewLine}");
                                }
                                catch { /* swallow */ }

                                break;
                            default:
                                // fallback: nothing more to do
                                break;
                        }

                        // Re-serialize root children into a single compact line and replace
                        var newInner = string.Concat(root.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)));
                        outLines[li] = newInner;

                        // Update provenance so future saves use the new line content
                        item.OriginalRawXml = newInner;
                    }
                    else
                    {
                        // No provenance -> append a new snippet
                        var snippet = CreateSnippetForNewItem(item);
                        var newList = outLines.ToList();
                        newList.Add(snippet);
                        outLines = newList.ToArray();
                        // set provenance
                        item.OriginalLineIndex = outLines.Length - 1;
                        item.OriginalRawXml = snippet;
                    }
                }
                catch (Exception ex)
                {
                    DevTrace.Write($"[SensorPanelSaver] Failed to update item at line {item?.OriginalLineIndex}: {ex.Message}");
                    // continue with other items
                }
            }

            // Atomic save: write tmp, backup original, then replace
            var tempPath = sensorPanelPath + ".tmp";
            var backupPath = sensorPanelPath + ".bak";
            

            // write only non-null lines
            File.WriteAllLines(
          tempPath,
          outLines.Where(l => l != null).ToArray(),
          encoding);


            try
            {
                File.Copy(sensorPanelPath, backupPath, overwrite: true);
            }
            catch
            {
                // ignore backup errors
            }

            // Keep only the 2 most recent backups
            try
            {
                var backups = Directory.GetFiles(Path.GetDirectoryName(sensorPanelPath)!, "*.bak")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.CreationTimeUtc)
                    .ToList();

                foreach (var old in backups.Skip(2)) // keep 2, remove rest
                    old.Delete();
            }
            catch (Exception ex)
            {
                DevTrace.Write($"[SensorPanelSaver] Backup cleanup error: {ex.Message}");
            }


            try
            {
                File.Delete(sensorPanelPath);
                File.Move(tempPath, sensorPanelPath);
            }
            catch (Exception ex)
            {
                DevTrace.Write($"[SensorPanelSaver] Error replacing file: {ex.Message}");
                // keep temp for inspection
                throw;
            }
        }



        // Create a snippet for a new item: generate realistic .sensorpanel tags
        private static string CreateSnippetForNewItem(DisplayItem item)
        {
            // small helper: avoid nulls
            string S(string v) => v ?? string.Empty;

            string label = S(item.Name);
            int x = (int)item.X;
            int y = (int)item.Y;

            // ✅ FIRST: SENSOR VALUE ([SIMPLE]... line)
            if (item is SensorDisplayItem sd)
            {
                string idCore = sd.PluginSensorId;
                if (string.IsNullOrWhiteSpace(idCore))
                    idCore = sd.SensorName ?? sd.Name ?? "UNKNOWN";

                int valColor = 0;
                try { valColor = HexToDecimalBgr(sd.Color); } catch { }

                var sb = new StringBuilder();
                sb.Append("<FNTNAM>").Append(S(sd.Font)).Append("</FNTNAM>");

                // TXTBIR: [bold][italic][rightAlign]
                sb.Append("<TXTBIR>")
                  .Append(sd.Bold ? "1" : "0")
                  .Append(sd.Italic ? "1" : "0")
                  .Append(sd.RightAlign ? "1" : "0")
                  .Append("</TXTBIR>");

                // Label + unit flags

                // Try to read a "ShowLabel"-like property from SensorDisplayItem; default to 0 (no label)
                bool showLabel = false;
                try
                {
                    var p = sd.GetType().GetProperty("ShowLabel")
                          ?? sd.GetType().GetProperty("DisplayLabel")
                          ?? sd.GetType().GetProperty("ShowText"); // optional fallback names
                    if (p != null)
                    {
                        var v = p.GetValue(sd);
                        if (v is bool bv) showLabel = bv;
                    }
                }
                catch
                {
                    // ignore and keep default (false)
                }

                sb.Append("<SHWLBL>").Append(showLabel ? "1" : "0").Append("</SHWLBL>");
                sb.Append("<SHWUNT>").Append(sd.ShowUnit ? "1" : "0").Append("</SHWUNT>");
                sb.Append("<UNT>").Append(S(sd.Unit)).Append("</UNT>");

                sb.Append("<TXTSIZ>").Append((int)sd.FontSize).Append("</TXTSIZ>");
                sb.Append("<TXTCOL>").Append(valColor).Append("</TXTCOL>");
                sb.Append("<LBL>").Append(label).Append("</LBL>");
                sb.Append("<ID>[SIMPLE]").Append(idCore).Append("</ID>");
                sb.Append("<ITMX>").Append(x).Append("</ITMX><ITMY>").Append(y).Append("</ITMY>");
                return sb.ToString();
            }

            // ✅ SECOND: pure TEXT LABEL (LBL line)
            if (item is TextDisplayItem txt)
            {
                int lblColor = 0;
                try { lblColor = HexToDecimalBgr(txt.Color); } catch { }

                var sb = new StringBuilder();
                sb.Append("<FNTNAM>").Append(S(txt.Font)).Append("</FNTNAM>");
                sb.Append("<LBL>").Append(label).Append("</LBL>");
                sb.Append("<LBLBIS>")
                  .Append(txt.Bold ? "1" : "0")
                  .Append(txt.Italic ? "1" : "0")
                  .Append("0</LBLBIS>");
                sb.Append("<SHDCOL>0</SHDCOL><SHDDIS>1</SHDDIS><SHDDEP>1</SHDDEP><URL></URL>");

                // ⭐ Preserve hidden/separator semantics:
                //  Visible:  <ID>LBL</ID>
                //  Hidden:   <ID>-LBL</ID>
                var idVal = "LBL";
                if (txt.Hidden)
                    idVal = "-" + idVal;

                sb.Append("<ID>").Append(idVal).Append("</ID>");
                sb.Append("<TXTSIZ>").Append((int)txt.FontSize).Append("</TXTSIZ>");
                sb.Append("<LBLCOL>").Append(lblColor).Append("</LBLCOL>");
                sb.Append("<ITMX>").Append(x).Append("</ITMX><ITMY>").Append(y).Append("</ITMY>");
                return sb.ToString();
            }



            // 3) BAR ([BAR]-style sensor line with SHWBAR=1)
            if (item is BarDisplayItem b)
            {
                // We already have: label, x, y, S(...) defined at method level.

                // Decide sensor id for the bar: prefer PluginSensorId, then SensorName, then label
                string sensorId = b.PluginSensorId;
                if (string.IsNullOrWhiteSpace(sensorId))
                    sensorId = b.SensorName ?? label ?? "UNKNOWN";

                // Dimensions with sane defaults if not set
                int barWidth = (int)(b.Width > 0 ? b.Width : 150);
                int barHeight = (int)(b.Height > 0 ? b.Height : 30);

                int barMin = (int)b.MinValue;
                int barMax = (int)b.MaxValue;
                if (barMax <= barMin) barMax = barMin + 100; // fallback safeguard

                // Colors: main bar, background, frame
                int mainCol = 0;          // default main fill
                int bgCol = 0;          // default background
                int frameCol = 16777215;   // white frame

                try
                {
                    if (!string.IsNullOrWhiteSpace(b.Color))
                        mainCol = HexToDecimalBgr(b.Color);
                }
                catch { }

                try
                {
                    // some bar items might not have BackgroundColor; ignore if missing
                    var bgProp = b.GetType().GetProperty("BackgroundColor");
                    if (bgProp != null)
                    {
                        var v = bgProp.GetValue(b) as string;
                        if (!string.IsNullOrWhiteSpace(v))
                            bgCol = HexToDecimalBgr(v);
                    }
                }
                catch { }

                try
                {
                    var frameProp = b.GetType().GetProperty("FrameColor");
                    if (frameProp != null)
                    {
                        var v = frameProp.GetValue(b) as string;
                        if (!string.IsNullOrWhiteSpace(v))
                            frameCol = HexToDecimalBgr(v);
                    }
                }
                catch { }

                // Unit: BarDisplayItem doesn't expose Unit directly, so use the helper used elsewhere
                string unit = string.Empty;
                try
                {
                    unit = TryGetStringProp(b, "Unit") ?? string.Empty;
                }
                catch
                {
                    unit = string.Empty;
                }
                bool hasUnit = !string.IsNullOrWhiteSpace(unit);

                var sb = new StringBuilder();

                // ID = sensor id used for mapping this bar to the metric
                sb.Append("<ID>").Append(sensorId).Append("</ID>");

                // Basic text & shadow info – similar to AIDA defaults
                sb.Append("<WID>").Append(barWidth).Append("</WID>");
                sb.Append("<TXTSIZ>8</TXTSIZ>");
                sb.Append("<FNTNAM>Tahoma</FNTNAM>");
                sb.Append("<SHDCOL>0</SHDCOL><SHDDIS>1</SHDDIS><SHDDEP>1</SHDDEP>");

                // Label and value flags (we keep label/value off by default;
                // you can change this later if you want visible bar labels by default)
                sb.Append("<SHWLBL>0</SHWLBL>");
                sb.Append("<LBL>").Append(label).Append("</LBL>");
                sb.Append("<LBLCOL>").Append(mainCol).Append("</LBLCOL>");
                sb.Append("<LBLBIS>000</LBLBIS>");

                sb.Append("<SHWVAL>0</SHWVAL>");
                sb.Append("<VALCOL>").Append(mainCol).Append("</VALCOL>");
                sb.Append("<VALBIS>000</VALBIS>");

                // Unit display
                sb.Append("<SHWUNT>").Append(hasUnit ? "1" : "0").Append("</SHWUNT>");
                sb.Append("<UNT>").Append(S(unit)).Append("</UNT>");
                sb.Append("<UNTCOL>").Append(mainCol).Append("</UNTCOL>");
                sb.Append("<UNTBIS>000</UNTBIS>");
                sb.Append("<UNTWID>40</UNTWID>");

                // Actual bar block
                sb.Append("<SHWBAR>1</SHWBAR>");
                sb.Append("<BARWID>").Append(barWidth).Append("</BARWID>");
                sb.Append("<BARHEI>").Append(barHeight).Append("</BARHEI>");
                sb.Append("<BARIND>0</BARIND>");

                // Placement: "SEP" = separate bar (classic AIDA)
                sb.Append("<BARPLC>SEP</BARPLC>");

                // BARFS flags: frame, reserved, gradient, flipX
                // We'll default to frame+gradient on, no flip: "1010"
                sb.Append("<BARFS>1010</BARFS>");

                sb.Append("<BARFRMCOL>").Append(frameCol).Append("</BARFRMCOL>");
                sb.Append("<BARMIN>").Append(barMin).Append("</BARMIN>");
                sb.Append("<BARLIM1></BARLIM1><BARLIM2></BARLIM2><BARLIM3></BARLIM3>");
                sb.Append("<BARMAX>").Append(barMax).Append("</BARMAX>");

                // Use mainCol/bgCol across min/limit fields so the bar color
                // is consistent and works with your existing save logic.
                sb.Append("<BARMINFGC>").Append(mainCol).Append("</BARMINFGC>");
                sb.Append("<BARMINBGC>").Append(bgCol).Append("</BARMINBGC>");

                sb.Append("<BARLIM1FGC>").Append(mainCol).Append("</BARLIM1FGC>");
                sb.Append("<BARLIM1BGC>").Append(bgCol).Append("</BARLIM1BGC>");

                sb.Append("<BARLIM2FGC>").Append(mainCol).Append("</BARLIM2FGC>");
                sb.Append("<BARLIM2BGC>").Append(bgCol).Append("</BARLIM2BGC>");

                sb.Append("<BARLIM3FGC>").Append(mainCol).Append("</BARLIM3FGC>");
                sb.Append("<BARLIM3BGC>").Append(bgCol).Append("</BARLIM3BGC>");

                sb.Append("<ITMX>").Append(x).Append("</ITMX><ITMY>").Append(y).Append("</ITMY>");

                return sb.ToString();
            }







            // 4) GRAPH ([GRAPH]... line)
            if (item is GraphDisplayItem gr)
            {
                // Decide graph TYP based on GraphDisplayItem.GraphType if available.
                // Default to "AG" (AIDA line/area graph).
                string typ = "AG";
                try
                {
                    var gtProp = gr.GetType().GetProperty("GraphType");
                    if (gtProp != null)
                    {
                        var val = gtProp.GetValue(gr);
                        if (val != null)
                        {
                            var name = val.ToString(); // e.g. "LINE", "HISTOGRAM"
                            if (name.Equals("HISTOGRAM", StringComparison.OrdinalIgnoreCase))
                            {
                                typ = "HG";   // histogram graph in AIDA
                            }
                            else
                            {
                                // For LINE or anything else, treat as "AG" (line/area style)
                                typ = "AG";
                            }
                        }
                    }
                }
                catch
                {
                    // if anything goes wrong, we just keep default "AG"
                }


                // AIDA sensor ID
                string idCore = null;
                var pluginProp = gr.GetType().GetProperty("PluginSensorId");
                if (pluginProp != null)
                    idCore = pluginProp.GetValue(gr) as string;
                if (string.IsNullOrWhiteSpace(idCore))
                    idCore = gr.SensorName ?? gr.Name ?? "UNKNOWN";

                int gphCol = 0, bgCol = 0, frmCol = 16777215, gridCol = 32768, sclCol = 16777215;
                try { if (!string.IsNullOrWhiteSpace(gr.Color)) gphCol = HexToDecimalBgr(gr.Color); } catch { }
                try { if (!string.IsNullOrWhiteSpace(gr.BackgroundColor)) bgCol = HexToDecimalBgr(gr.BackgroundColor); } catch { }
                try { if (!string.IsNullOrWhiteSpace(gr.FrameColor)) frmCol = HexToDecimalBgr(gr.FrameColor); } catch { }

                bool background = false, frame = false;
                try { background = gr.Background; } catch { }
                try { frame = gr.Frame; } catch { }

                char b0 = background ? '1' : '0';
                char b1 = frame ? '1' : '0';
                char b2 = '0'; // grid off

                var sb = new StringBuilder();
                sb.Append("<TYP>").Append(typ).Append("</TYP>");
                sb.Append("<GPHSTP>").Append((int)gr.Step).Append("</GPHSTP>");
                sb.Append("<GPHTCK>").Append((int)gr.Thickness).Append("</GPHTCK>");
                sb.Append("<GRDDNS>10</GRDDNS>");
                sb.Append("<MINVAL>").Append((int)gr.MinValue).Append("</MINVAL>");
                sb.Append("<MAXVAL>").Append((int)gr.MaxValue).Append("</MAXVAL>");
                sb.Append("<AUTSCL>").Append(gr.AutoValue ? "1" : "0").Append("</AUTSCL>");
                sb.Append("<GRDCOL>").Append(gridCol).Append("</GRDCOL>");
                sb.Append("<GPHCOL>").Append(gphCol).Append("</GPHCOL>");
                sb.Append("<BGCOL>").Append(bgCol).Append("</BGCOL>");
                sb.Append("<FRMCOL>").Append(frmCol).Append("</FRMCOL>");
                sb.Append("<SHWSCL>0</SHWSCL>");
                sb.Append("<TXTSIZ>8</TXTSIZ>");
                sb.Append("<FNTNAM>Tahoma</FNTNAM>");
                sb.Append("<SCLCOL>").Append(sclCol).Append("</SCLCOL>");
                sb.Append("<SCLBI>000</SCLBI>");
                sb.Append("<HEI>").Append((int)gr.Height).Append("</HEI>");
                sb.Append("<WID>").Append((int)gr.Width).Append("</WID>");
                sb.Append("<GPHBFG>").Append(b0).Append(b1).Append(b2).Append("</GPHBFG>");
                sb.Append("<LBL>").Append(label).Append("</LBL>");
                sb.Append("<ID>[GRAPH]").Append(idCore).Append("</ID>");
                sb.Append("<ITMX>").Append(x).Append("</ITMX><ITMY>").Append(y).Append("</ITMY>");
                return sb.ToString();
            }

            // 5) GAUGE ([GAUGE]... line with STAFLS and Custom/CustomN metadata)
            if (item is GaugeDisplayItem g)
            {
                string idCore = null;
                var pluginProp = g.GetType().GetProperty("PluginSensorId");
                if (pluginProp != null)
                    idCore = pluginProp.GetValue(g) as string;
                if (string.IsNullOrWhiteSpace(idCore))
                    idCore = g.SensorName ?? g.Name ?? "UNKNOWN";

                // Build STAFLS from Images if possible
                string stafls = string.Empty;
                int imageCount = 0;
                if (g.Images != null && g.Images.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var img in g.Images)
                    {
                        if (img == null) continue;
                        var t = img.GetType();
                        string fname = null;

                        var pName = t.GetProperty("AssetName")
                                   ?? t.GetProperty("Name")
                                   ?? t.GetProperty("FileName")
                                   ?? t.GetProperty("ImageName");
                        if (pName != null)
                        {
                            var v = pName.GetValue(img) as string;
                            if (!string.IsNullOrWhiteSpace(v))
                                fname = Path.GetFileName(v);
                        }
                        if (string.IsNullOrWhiteSpace(fname))
                        {
                            var pPath = t.GetProperty("Path") ?? t.GetProperty("Source");
                            if (pPath != null)
                            {
                                var v = pPath.GetValue(img)?.ToString();
                                if (!string.IsNullOrWhiteSpace(v))
                                    fname = Path.GetFileName(v);
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(fname))
                            names.Add(fname);
                    }

                    if (names.Count == g.Images.Count && names.Count > 0)
                    {
                        imageCount = names.Count;
                        stafls = string.Join("|", names);
                    }
                }

                // Decide gauge type: "Custom" (up to 16 states) or "CustomN" (N states)
                string gaugeTyp = (imageCount > 16) ? "CustomN" : "Custom";

                // MAXSTA = number_of_states_minus_1 (0..MAXSTA)
                int maxSta = (imageCount > 0) ? (imageCount - 1) : 0;

                // Text / font info for value (similar to working example)
                string fontName = "GeForce";
                try
                {
                    var fProp = g.GetType().GetProperty("Font");
                    if (fProp != null)
                    {
                        var v = fProp.GetValue(g) as string;
                        if (!string.IsNullOrWhiteSpace(v))
                            fontName = v;
                    }
                }
                catch { }

                int valColorDec = 65535; // default (like example), or you could use white (16777215)

                var sb = new StringBuilder();

                // Follow the pattern: <LBL>..</LBL><TYP>..</TYP><SIZ>..</SIZ>...
                sb.Append("<LBL>").Append(label).Append("</LBL>");
                sb.Append("<TYP>").Append(gaugeTyp).Append("</TYP>");

                // Size (S/M/L) – use S by default
                sb.Append("<SIZ>S</SIZ>");

                sb.Append("<MINVAL>").Append((int)g.MinValue).Append("</MINVAL>");
                sb.Append("<MAXVAL>").Append((int)g.MaxValue).Append("</MAXVAL>");

                // ───── Gauge value text (AIDA-compatible) ─────
                sb.Append("<SHWICO>0</SHWICO>");
                sb.Append("<SHWVAL>").Append(g.ShowValue ? 1 : 0).Append("</SHWVAL>");

                if (g.ShowValue)
                {
                    // Text size
                    sb.Append("<TXTSIZ>")
                      .Append(g.ValueTextSize > 0 ? g.ValueTextSize : 12)
                      .Append("</TXTSIZ>");

                    // Font name
                    sb.Append("<FNTNAM>")
                      .Append(string.IsNullOrWhiteSpace(g.ValueFontName) ? "Segoe UI" : g.ValueFontName)
                      .Append("</FNTNAM>");

                    // Color (convert hex → decimal BGR)
                    int valCol = HexToDecimalBgr(g.ValueColor);
                    sb.Append("<VALCOL>").Append(valCol).Append("</VALCOL>");

                    // Bold / Italic flags (AIDA format)
                    char bo = g.ValueBold ? '1' : '0';
                    char i = g.ValueItalic ? '1' : '0';
                    sb.Append("<VALBI>").Append(bo).Append(i).Append("</VALBI>");
                }
                else
                {
                    // AIDA still writes defaults even if hidden
                    sb.Append("<TXTSIZ>12</TXTSIZ>");
                    sb.Append("<FNTNAM>Segoe UI</FNTNAM>");
                    sb.Append("<VALCOL>16777215</VALCOL>");
                    sb.Append("<VALBI>00</VALBI>");
                }


                // Now the state count
                sb.Append("<MAXSTA>").Append(maxSta).Append("</MAXSTA>");

                // ID + position
                sb.Append("<ID>[GAUGE]").Append(idCore).Append("</ID>");
                sb.Append("<ITMX>").Append(x).Append("</ITMX><ITMY>").Append(y).Append("</ITMY>");

                // Finally the image list
                if (!string.IsNullOrEmpty(stafls))
                    sb.Append("<STAFLS>").Append(stafls).Append("</STAFLS>");

                return sb.ToString();
            }


            // 5) Fallback: minimal label line so the item doesn't vanish
            {
                var sb = new StringBuilder();
                sb.Append("<LBL>").Append(label).Append("</LBL>");
                sb.Append("<ID>LBL</ID>");
                sb.Append("<ITMX>").Append(x).Append("</ITMX><ITMY>").Append(y).Append("</ITMY>");
                return sb.ToString();
            }
        }






        // Minimal escape passthrough. Replace this with your real EscapeContentWithinLBL implementation if you have one.
        private static string EscapeContentWithinLBL(string input)
        {
            // If you have a project-level EscapeContentWithinLBL function, replace this method body with a call to it.
            return input ?? string.Empty;
        }




        // HELPER FOR TEXT
        // replace previous HexToDecimalBgr with this:
        private static int HexToDecimalBgr(string colorInput)
        {
            if (string.IsNullOrWhiteSpace(colorInput)) return 0;

            try
            {
                string s = colorInput.Trim();

                // Some UI libs store colors as "#AARRGGBB" or "#RRGGBB" or named colors like "White"
                // ColorTranslator.FromHtml handles named colors and #RRGGBB. For 8-digit (#AARRGGBB) strip alpha.
                if (s.StartsWith("#") && s.Length == 9) // #AARRGGBB
                {
                    // strip leading alpha -> keep last 6 chars
                    s = "#" + s.Substring(3); // drop #AA => leaves #RRGGBB
                }

                // ColorTranslator can throw for some formats; wrap in try/catch
                Color col = ColorTranslator.FromHtml(s);

                int r = col.R;
                int g = col.G;
                int b = col.B;

                // Pack into decimal BGR (B at highest byte)
                return (b << 16) | (g << 8) | r;
            }
            catch
            {
                // fallback 0 (black) if parsing fails
                return 0;
            }
        }



        //HELPER FOR BAR
        // returns property string value if exists, else null
        private static string TryGetStringProp(object obj, string propName)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(propName);
            if (p == null) return null;
            var v = p.GetValue(obj);
            return v?.ToString();
        }

        // returns boolean property (or false if not present or not bool)
        private static bool TryGetBoolProp(object obj, string propName)
        {
            if (obj == null) return false;
            var p = obj.GetType().GetProperty(propName);
            if (p == null) return false;
            var val = p.GetValue(obj);
            if (val is bool b) return b;
            // some frameworks use numeric weight (e.g., 0/1)
            if (val is int i) return i != 0;
            if (val is string s)
            {
                if (bool.TryParse(s, out var rb)) return rb;
                if (int.TryParse(s, out var ri)) return ri != 0;
            }
            return false;
        }


       





    }
}