using Serilog;
using SkiaSharp;
using SynQPanel.Models;
using SynQPanel.Views.Components.Custom;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace SynQPanel.Views.Components
{
    /// <summary>
    /// Interaction logic for CustomProperties.xaml
    /// </summary>
    public partial class CustomProperties : System.Windows.Controls.UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<CustomProperties>();
        public static readonly DependencyProperty ItemProperty =
      DependencyProperty.Register("GaugeDisplayItem", typeof(GaugeDisplayItem), typeof(CustomProperties));

        public GaugeDisplayItem GaugeDisplayItem
        {
            get { return (GaugeDisplayItem)GetValue(ItemProperty); }
            set { SetValue(ItemProperty, value); }
        }

        public ObservableCollection<string> InstalledFonts { get; } = new();


        //to fix swapping view not refreshing when items empty etc
        public GaugePropertiesVM ViewModel { get; set; }

        private DispatcherTimer UpdateTimer;

        public CustomProperties()
        {
            ViewModel= new GaugePropertiesVM();

            InitializeComponent();
           LoadAllFonts();
            Unloaded += CustomProperties_Unloaded;

            UpdateTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
            UpdateTimer.Tick += Timer_Tick;
            UpdateTimer.Start();
        }

        private void CustomProperties_Unloaded(object sender, RoutedEventArgs e)
        {
            if (UpdateTimer != null)
            {
                UpdateTimer.Stop();
                UpdateTimer.Tick -= Timer_Tick;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (SharedModel.Instance.SelectedItem is GaugeDisplayItem gaugeDisplayItem)
            {
                if (gaugeDisplayItem.Images.Count > 0)
                {
                    gaugeDisplayItem.TriggerDisplayImageChange();
                }
            }
        }

        private void ButtonAddStep_Click(object sender, RoutedEventArgs e)
        {
            if (SharedModel.Instance.SelectedItem is GaugeDisplayItem customDisplayItem)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new()
                {
                    Multiselect = true,
                    Filter = "Image files (*.jpg, *.jpeg, *.png, *.gif)|*.jpg;*.jpeg;*.png;*.gif",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
                };

                if (openFileDialog.ShowDialog() == true)
                {

                    if (openFileDialog.FileNames.Length > 101)
                    {
                        System.Windows.MessageBox.Show("You can only select a maximum of 101 images.", "File Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var profile = SharedModel.Instance.SelectedProfile;

                    if (profile != null)
                    {
                        customDisplayItem.Images.Clear();

                        foreach (var file in openFileDialog.FileNames)
                        {
                            var imageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "assets", profile.Guid.ToString());
                            if (!Directory.Exists(imageFolder))
                            {
                                Directory.CreateDirectory(imageFolder);
                            }

                            try
                            {
                                var fileName = Path.GetFileName(file);
                                File.Copy(file, Path.Combine(imageFolder, fileName), true);
                                var imageDisplayItem = new ImageDisplayItem(fileName, profile, fileName, true)
                                {
                                    PersistentCache = true // Gauge images should not expire
                                };

                                customDisplayItem.Images.Add(imageDisplayItem);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, "Error copying file to assets folder");
                            }
                        }
                    }

                }
            }
        }

        private void ButtonStepUp_Click(object sender, RoutedEventArgs e)
        {
            if (SharedModel.Instance.SelectedItem is GaugeDisplayItem gaugeDisplayItem)
            {
                if (ViewModel.SelectedItem != null)
                {
                    var index = gaugeDisplayItem.Images.IndexOf(ViewModel.SelectedItem);
                    if (index > 0)
                    {
                        var selectedItem = ViewModel.SelectedItem;
                        var temp = gaugeDisplayItem.Images[index - 1];
                        gaugeDisplayItem.Images[index - 1] = gaugeDisplayItem.Images[index];
                        gaugeDisplayItem.Images[index] = temp;
                        ListViewItems.Items.Refresh();
                        ViewModel.SelectedItem = selectedItem;
                        ListViewItems.ScrollIntoView(selectedItem);
                    }
                }
            }
        }

        private void ButtonStepDown_Click(object sender, RoutedEventArgs e)
        {
            if (SharedModel.Instance.SelectedItem is GaugeDisplayItem gaugeDisplayItem)
            {
                if (ViewModel.SelectedItem != null)
                {
                    var index = gaugeDisplayItem.Images.IndexOf(ViewModel.SelectedItem);
                    if (index < gaugeDisplayItem.Images.Count - 1)
                    {
                        var selectedItem = ViewModel.SelectedItem;
                        var temp = gaugeDisplayItem.Images[index + 1];
                        gaugeDisplayItem.Images[index + 1] = gaugeDisplayItem.Images[index];
                        gaugeDisplayItem.Images[index] = temp;
                        ListViewItems.Items.Refresh();
                        ViewModel.SelectedItem = selectedItem;
                        ListViewItems.ScrollIntoView(selectedItem);
                    }
                }
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (SharedModel.Instance.SelectedItem is GaugeDisplayItem gaugeDisplayItem)
            {
                if (ViewModel.SelectedItem != null)
                {
                    //customDisplayItem.Images.Remove(ViewModel.SelectedItem);
                    for (int i = gaugeDisplayItem.Images.Count - 1; i >= 0; i--)
                    {
                        if (gaugeDisplayItem.Images[i].Selected)
                        {
                            gaugeDisplayItem.Images.RemoveAt(i);
                        }
                    }
                    ListViewItems.Items.Refresh();
                    gaugeDisplayItem.TriggerDisplayImageChange();
                }
            }
        }

        private void LoadAllFonts()
        {
            InstalledFonts.Clear();

            var fontManager = SKFontManager.Default;

            foreach (var family in fontManager.GetFontFamilies())
            {
                // ✅ ALWAYS add base family name first
                if (!InstalledFonts.Contains(family))
                    InstalledFonts.Add(family);

                var styles = fontManager.GetFontStyles(family);

                for (int i = 0; i < styles.Count; i++)
                {
                    var styleName = styles.GetStyleName(i);

                    if (string.IsNullOrWhiteSpace(styleName))
                        continue;

                    string fullName = $"{family} {styleName}";

                    if (!InstalledFonts.Contains(fullName))
                        InstalledFonts.Add(fullName);
                }
            }
        }



    }
}
