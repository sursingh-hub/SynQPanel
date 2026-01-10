using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SynQPanel.Utils
{
    public static class LoggingUtil
    {
        public static bool DiagnosticsEnabled =>
            ConfigModel.Instance?.Settings?.DiagnosticsMode == true;
    }

}
