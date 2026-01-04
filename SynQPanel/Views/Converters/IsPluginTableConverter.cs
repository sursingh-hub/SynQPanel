using SynQPanel.Models;
using SynQPanel.Plugins;
using System;
using System.Data;
using System.Globalization;
using System.Windows.Data;

namespace SynQPanel
{
    internal class IsPluginTableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is SensorReading sensorReading && sensorReading.ValueTable is DataTable;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
