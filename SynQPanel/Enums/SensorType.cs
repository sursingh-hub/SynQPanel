namespace SynQPanel.Enums
{
    public enum SensorType
    {
        None = -1,  // 👈 NEW: for non-sensor display items
        HwInfo = 0,
        Libre = 1,
        Plugin = 2,
        Aida = 3    // Added Aida sensor type for AIDA64 integration
    }
}
