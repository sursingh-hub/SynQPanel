using System.Collections.Generic;
using System.Linq;
using SynQPanel.Aida;

// This wrapper makes AIDA sensors 
public class AidaSensorWrapper
{
    public string Id { get; }
    public string Label { get; }
    public string Type { get; }
    public string Value { get; }
    public string Unit { get; }

    public string Name => Label;  // For compatibility with mapping code

    public AidaSensorWrapper(AidaSensorItem sensor)
    {
        Id = sensor.Id;
        Label = sensor.Label;
        Type = sensor.Type;
        Value = sensor.Value;
        Unit = sensor.Unit;
    }
}

public static class AidaMonitor
{
    // Adjust this to wherever you store your most recent list of AidaSensorItem
    // For test, let's assume you have ONE latest batch loaded somewhere accessible
    public static List<AidaSensorItem> LatestSensors { get; set; } = new List<AidaSensorItem>();

    public static IEnumerable<AidaSensorWrapper> GetOrderedList()
    {
        foreach (var sensor in LatestSensors)
        {
            // Yield every sensor, not just TCPU/SCPUUTI
            yield return new AidaSensorWrapper(sensor);
        }
    }
}
