using System.Data;

namespace SynQPanel.Plugins
{
    public interface IPluginTable: IPluginData
    {
        DataTable Value { get; set; }
        string DefaultFormat { get; }
    }
}
