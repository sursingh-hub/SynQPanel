namespace SynQPanel.Plugins
{
    public interface IPluginData
    {
        string Id { get; }
        string Name { get; }
        string ToString();
    }
}
