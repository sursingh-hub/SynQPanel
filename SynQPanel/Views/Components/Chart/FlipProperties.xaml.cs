using SynQPanel.Infrastructure;
using SynQPanel.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SynQPanel.Views.Components
{
    public partial class FlipProperties : UserControl
    {
        public FlipProperties()
        {
            InitializeComponent();

            // ✅ Update preview when DataContext changes (for loaded files)
            DataContextChanged += FlipProperties_DataContextChanged;
        }

        private void FlipProperties_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // When a FlipDisplayItem is loaded from file, update preview
            if (e.NewValue is FlipDisplayItem flip)
            {
                LoadExistingPreview(flip);
            }
        }

        private void LoadExistingPreview(FlipDisplayItem flip)
        {
            try
            {
                string? folderPath = flip.CalculatedImageFolder;

                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    ClearPreview();
                    return;
                }

                // Try to find 00.png (or any first digit image)
                var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
                string? firstImage = null;

                // Try 00 first (most common)
                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(folderPath, $"00{ext}");
                    if (File.Exists(testPath))
                    {
                        firstImage = testPath;
                        break;
                    }
                }

                // If 00 not found, try any numbered image (0-59)
                if (firstImage == null)
                {
                    for (int i = 0; i < 60; i++)
                    {
                        foreach (var ext in extensions)
                        {
                            string testPath = Path.Combine(folderPath, $"{i:00}{ext}");
                            if (File.Exists(testPath))
                            {
                                firstImage = testPath;
                                break;
                            }
                        }
                        if (firstImage != null) break;
                    }
                }

                if (firstImage != null)
                {
                    UpdatePreview(firstImage);

                    // Show status for loaded file
                    int imageCount = CountValidImages(folderPath);
                    if (imageCount == 60)
                    {
                        ShowImportStatus($"✅ Loaded: {imageCount} images (00-59 complete)", Colors.Green);
                    }
                    else if (imageCount > 0)
                    {
                        ShowImportStatus($"⚠️ Loaded: {imageCount} images (expected 60)", Colors.Orange);
                    }
                }
                else
                {
                    ClearPreview();
                }
            }
            catch
            {
                ClearPreview();
            }
        }

        private int CountValidImages(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return 0;

            int count = 0;
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

            for (int i = 0; i < 60; i++)
            {
                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(folderPath, $"{i:00}{ext}");
                    if (File.Exists(testPath))
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private void ClearPreview()
        {
            PreviewImage.Source = null;
            PreviewText.Text = "No image selected";
            ImportStatusCard.Visibility = Visibility.Collapsed;
        }

        private void ButtonSelectFlipFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not FlipDisplayItem flip)
                return;

            if (flip.Profile == null)
                return;

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder containing flip digit images (00.png to 59.png)"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string sourceFolder = dialog.SelectedPath;
            if (!Directory.Exists(sourceFolder))
                return;

            string profileAssetRoot = Path.Combine(
                AppPaths.Assets,
                flip.Profile.Guid.ToString()
            );

            Directory.CreateDirectory(profileAssetRoot);

            // Validation variables
            string? firstImage = null;
            int copiedCount = 0;
            int expectedCount = 60;
            var missingNumbers = Enumerable.Range(0, expectedCount).ToList();

            foreach (var file in Directory.GetFiles(sourceFolder))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp"))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(file);
                string dest = Path.Combine(profileAssetRoot, Path.GetFileName(file));

                // Check if filename is a valid number (00-59)
                if (int.TryParse(fileName, out int number) && number >= 0 && number < expectedCount)
                {
                    missingNumbers.Remove(number);
                }

                if (!File.Exists(dest))
                    File.Copy(file, dest, overwrite: false);

                firstImage ??= dest;
                copiedCount++;
            }

            // ✅ Validation: No valid images found
            if (copiedCount == 0)
            {
                ShowImportStatus(
                    "⚠️ No valid images found in the selected folder.",
                    Colors.Orange
                );
                return;
            }

            // ✅ Validation: Check for missing images
            bool hasAllImages = missingNumbers.Count == 0;
            string statusMessage;
            Color statusColor;

            if (hasAllImages)
            {
                statusMessage = $"✅ Successfully imported {copiedCount} images (00-59 complete)";
                statusColor = Colors.Green;
            }
            else if (missingNumbers.Count <= 10)
            {
                string missingList = string.Join(", ", missingNumbers.Select(n => n.ToString("00")));
                statusMessage = $"⚠️ Imported {copiedCount} images. Missing: {missingList}";
                statusColor = Colors.Orange;
            }
            else
            {
                statusMessage = $"⚠️ Imported {copiedCount} images. {missingNumbers.Count} images missing (expected 60 images named 00-59)";
                statusColor = Colors.Orange;
            }

            ShowImportStatus(statusMessage, statusColor);

            // ✅ Store RELATIVE reference (exactly like ImageDisplayItem)
            flip.ImageFolder = ".";

            // ✅ Auto-size using firstImage
            if (firstImage != null && File.Exists(firstImage))
            {
                try
                {
                    using var bmp = SkiaSharp.SKBitmap.Decode(firstImage);
                    if (bmp != null)
                    {
                        flip.Width = bmp.Width;
                        flip.Height = bmp.Height;
                    }

                    // ✅ Update Preview
                    UpdatePreview(firstImage);
                }
                catch (Exception ex)
                {
                    ShowImportStatus($"⚠️ Error loading image: {ex.Message}", Colors.Red);
                }
            }
        }

        private void ShowImportStatus(string message, Color color)
        {
            ImportStatusCard.Visibility = Visibility.Visible;
            ImportStatusText.Text = message;
            ImportStatusText.Foreground = new SolidColorBrush(color);
        }

        private void UpdatePreview(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewImage.Source = bitmap;
                PreviewText.Text = $"Sample: {Path.GetFileName(imagePath)}";
            }
            catch
            {
                PreviewImage.Source = null;
                PreviewText.Text = "Preview unavailable";
            }
        }
    }
}
