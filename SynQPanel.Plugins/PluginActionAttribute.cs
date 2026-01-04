using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SynQPanel.Plugins
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PluginActionAttribute : Attribute
    {
        public string DisplayName { get; }

        public PluginActionAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
