using System;
using System.IO;

public static class DevTrace
{
    // toggleable via configuration
    public static bool Enabled { get; set; } = false; // set true temporarily to debug

    private static readonly string _dbgPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SynQPanel", "SynQPanel_debug.log");

    public static void Write(string text)
    {
        if (!Enabled) return;
        try
        {
            System.Diagnostics.Debug.WriteLine(text);
            Directory.CreateDirectory(Path.GetDirectoryName(_dbgPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            File.AppendAllText(_dbgPath, text + Environment.NewLine);
        }
        catch { /* swallow */ }
    }
}
