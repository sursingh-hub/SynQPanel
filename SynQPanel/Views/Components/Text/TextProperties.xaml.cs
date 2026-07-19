using Serilog;
using SkiaSharp;
using SynQPanel.Drawing;
using SynQPanel.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace SynQPanel.Views.Components
{
    /// <summary>
    /// Interaction logic for TextProperties.xaml
    /// </summary>
    /// 


    public partial class TextProperties : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<TextProperties>();
        public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register("TextDisplayItem", typeof(TextDisplayItem), typeof(TextProperties));

        public static readonly DependencyProperty CurrentFontProperty =
        DependencyProperty.Register("CurrentFont", typeof(string), typeof(TextProperties),
            new PropertyMetadata(null, OnCurrentFontChanged));

        public static readonly DependencyProperty CurrentFontStyleProperty =
        DependencyProperty.Register("CurrentFontStyle", typeof(string), typeof(TextProperties),
            new PropertyMetadata(null, OnCurrentFontStyleChanged));

        public ObservableCollection<string> InstalledFonts { get; } = [];

        public ObservableCollection<string> FontStyles { get; } = [];

        public TextDisplayItem TextDisplayItem
        {
            get { return (TextDisplayItem)GetValue(ItemProperty); }
            set { SetValue(ItemProperty, value); }
        }

        public string CurrentFont
        {
            get { return (string)GetValue(CurrentFontProperty); }
            set { SetValue(CurrentFontProperty, value); }
        }
        public string CurrentFontStyle
        {
            get { return (string)GetValue(CurrentFontStyleProperty); }
            set { SetValue(CurrentFontStyleProperty, value); }
        }


        public TextProperties()
        {
            LoadAllFonts();
            InitializeComponent();

            SetBinding(CurrentFontProperty, new Binding
            {
                Path = new PropertyPath("TextDisplayItem.Font"),
                Source = this,
                Mode = BindingMode.OneWay
            });

            SetBinding(CurrentFontStyleProperty, new Binding
            {
                Path = new PropertyPath("TextDisplayItem.FontStyle"),
                Source = this,
                Mode = BindingMode.OneWay
            });

        }

        private static void OnCurrentFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Logger.Debug("OnCurrentFontChanged newValue: {NewValue}", e.NewValue);
            var control = (TextProperties)d;
            var item = (TextDisplayItem)control.GetValue(ItemProperty);

            if (item == null)
            {
                return;
            }

            if (e.NewValue is string fontName)
            {
                // 1. Check if the exact name exists. If not, clean it!
                if (!control.InstalledFonts.Contains(fontName))
                {
                    var familyName = SkiaGraphics.ExtractBaseFamilyName(fontName);

                    // If we successfully cleaned it into a real font...
                    if (!string.IsNullOrEmpty(familyName) && control.InstalledFonts.Contains(familyName))
                    {
                        // Update the model and our local variable, but DO NOT RETURN!
                        item.Font = familyName;
                        fontName = familyName; // <-- Update the variable so the rest of the method works!
                    }
                    else
                    {
                        // If it STILL isn't a valid font after cleaning, then we abort.
                        return;
                    }
                }

                // 2. NOW continue exactly as before, using the cleaned fontName!

                // Save current FontStyle before clearing to prevent it from being nullified
                string savedFontStyle = item.FontStyle;

                control.FontStyles.Clear();
                var styles = SKFontManager.Default.GetFontStyles(fontName);

                for (int i = 0; i < styles.Count; i++)
                {
                    control.FontStyles.Add(styles.GetStyleName(i));
                }


                if (control.FontStyles.Count > 0)
                {
                    // Try to restore saved style if it's valid for the new font
                    if (!string.IsNullOrEmpty(savedFontStyle) && control.FontStyles.Contains(savedFontStyle))
                    {
                        item.FontStyle = savedFontStyle;
                    }
                    else if (string.IsNullOrEmpty(item.FontStyle) || !control.FontStyles.Contains(item.FontStyle))
                    {
                        string requestedFont = "";

                        // Legacy AIDA64 mapping
                        if (item.Bold) requestedFont = "Bold";
                        if (item.Italic) requestedFont = string.IsNullOrEmpty(requestedFont) ? "Italic" : "Bold Italic";

                        if (!string.IsNullOrEmpty(requestedFont) && control.FontStyles.Contains(requestedFont))
                        {
                            item.FontStyle = requestedFont;
                        }
                        else
                        {
                            // NEW FIX: Only use "Regular" or "Normal" if the font ACTUALLY supports it.
                            // Otherwise, default to the 0th index so we don't break the font metrics!
                            string? defaultStyle = control.FontStyles.FirstOrDefault(s =>
                                s.Equals("Regular", StringComparison.OrdinalIgnoreCase) ||
                                s.Equals("Normal", StringComparison.OrdinalIgnoreCase));

                            item.FontStyle = defaultStyle ?? control.FontStyles[0];
                        }
                    }
                }

            }
        }

        private static void OnCurrentFontStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Logger.Debug("OnCurrentFontStyleChanged newValue: {NewValue}", e.NewValue);
            var control = (TextProperties)d;
            var item = (TextDisplayItem)control.GetValue(ItemProperty);

            if (item == null)
            {
                return;
            }

            if (control.FontStyles.Count > 0)
            {
                if (string.IsNullOrEmpty(item.FontStyle) || !control.FontStyles.Contains(item.FontStyle))
                {
                    string requestedFont = "";
                    //legacy
                    if (item.Bold)
                    {
                        requestedFont = "Bold";
                    }

                    if (item.Italic)
                    {
                        if (!string.IsNullOrEmpty(requestedFont))
                        {
                            requestedFont += " ";
                        }

                        requestedFont += "Italic";
                    }

                    if (!string.IsNullOrEmpty(requestedFont))
                    {
                        if (control.FontStyles.Contains(requestedFont))
                        {
                            item.FontStyle = requestedFont;
                            return;
                        }
                    }

                    // OLD
                    //item.FontStyle = control.FontStyles[0];

                    // FIX: Prioritize Regular/Normal over index 0
                    string? defaultStyle = control.FontStyles.FirstOrDefault(s =>
                        s.Equals("Regular", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("Normal", StringComparison.OrdinalIgnoreCase));

                    item.FontStyle = defaultStyle ?? control.FontStyles[0];
                }
            }
        }

        private void LoadAllFonts()
        {
            var allFonts = SKFontManager.Default.GetFontFamilies()
                .OrderBy(f => f)
                .ToList();

            foreach (var font in allFonts)
            {
                InstalledFonts.Add(font);
            }
        }

        private void NumberBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextDisplayItem == null)
            {
                return;
            }

            var numBox = ((NumberBox)sender);
            double newValue;
            if (double.TryParse(numBox.Text, out newValue))
            {
                numBox.Value = newValue;
                TextDisplayItem.FontSize = (int)newValue;
            }
        }
    }
}
