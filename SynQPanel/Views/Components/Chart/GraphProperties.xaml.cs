using SynQPanel.Models;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static SynQPanel.Models.GraphDisplayItem;

namespace SynQPanel.Views.Components
{
    /// <summary>
    /// Interaction logic for GraphProperties.xaml
    /// </summary>
    public partial class GraphProperties : UserControl
    {
        public static readonly DependencyProperty ItemProperty =
       DependencyProperty.Register("GraphDisplayItem", typeof(GraphDisplayItem), typeof(GraphProperties));

        public GraphDisplayItem GraphDisplayItem
        {
            get { return (GraphDisplayItem)GetValue(ItemProperty); }
            set { SetValue(ItemProperty, value); }
        }

        public GraphProperties()
        {
            InitializeComponent();
            ComboBoxType.ItemsSource = Enum.GetValues(typeof(GraphType)).Cast<GraphType>();
        }

    }
}
