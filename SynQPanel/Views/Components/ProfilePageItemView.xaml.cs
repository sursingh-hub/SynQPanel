using SynQPanel.Drawing;
using SynQPanel.Models;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Profile = SynQPanel.Models.Profile;

namespace SynQPanel.Views.Components
{
    /// <summary>
    /// Interaction logic for ProfilePageItem.xaml
    /// </summary>
    public partial class ProfilePageItemView : UserControl
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly ISnackbarService _snackbarService;

        private DispatcherTimer? timer;
        private TaskCompletionSource<bool>? _paintCompletionSource;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);

        public ProfilePageItemView()
        {
            _contentDialogService = App.GetService<IContentDialogService>() ?? throw new InvalidOperationException("ContentDialogService is not registered in the service collection.");
            _snackbarService = App.GetService<ISnackbarService>() ?? throw new InvalidOperationException("SnackbarService is not registered in the service collection.");

            InitializeComponent();

            Loaded += ProfilePageItemView_Loaded;
            Unloaded += ProfilePageItemView_Unloaded;
        }
        private async void ProfilePageItemView_Loaded(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await UpdateAsync(_cancellationTokenSource.Token);

                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                timer.Tick += Timer_Tick;
                timer.Start();
            }
            catch (OperationCanceledException) { }
        }

        private void ProfilePageItemView_Unloaded(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();

            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }

            if(DataContext is Profile profile)
            {
                profile.PreviewBitmap?.Dispose();
                profile.PreviewBitmap = null;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await UpdateAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) { }
            }
        }

        private async Task UpdateAsync(CancellationToken cancellationToken)
        {
            await _updateSemaphore.WaitAsync(cancellationToken);

            try
            {
                if (DataContext is Profile profile)
                {
                    var canvasWidth = skElement.CanvasSize.Width;
                    var canvasHeight = skElement.CanvasSize.Height;

                    var scale = 1.0f;

                    if (profile.Height > canvasHeight)
                    {
                        scale = canvasHeight / profile.Height;
                    }

                    if (profile.Width > canvasWidth)
                    {
                        scale = Math.Min(scale, canvasWidth / profile.Width);
                    }

                    var width = (int)(profile.Width * scale);
                    var height = (int)(profile.Height * scale);

                    if (profile.PreviewBitmap != null && (profile.PreviewBitmap.Width != width || profile.PreviewBitmap.Height != height))
                    {
                        profile.PreviewBitmap.Dispose();
                        profile.PreviewBitmap = null;
                    }

                    profile.PreviewBitmap ??= new SKBitmap(width, height);

                    await Task.Run(() =>
                    {
                        using var g = SkiaGraphics.FromBitmap(profile.PreviewBitmap, profile.FontScale);
                        PanelDraw.Run(profile, g, true, scale, true, $"PREVIEW-{profile.Guid}");
                    }, cancellationToken);

                    _paintCompletionSource = new TaskCompletionSource<bool>();

                    skElement.InvalidateVisual();

                    await _paintCompletionSource.Task;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        private void skElement_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            if (DataContext is Profile profile)
            {
                if (profile.PreviewBitmap is SKBitmap bitmap)
                {

                    //draw bitmap to center of canvas
                    var x = (e.Info.Width - bitmap.Width) / 2;
                    var y = (e.Info.Height - bitmap.Height) / 2;


                    e.Surface.Canvas.Clear();
                    e.Surface.Canvas.DrawBitmap(bitmap, x, y);
                }
            }

            _paintCompletionSource?.TrySetResult(true);
        }

        private async void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is Profile profile)
            {
                if(ConfigModel.Instance.Profiles.Count <= 1)
                {
                    _snackbarService.Show("Cannot Delete Profile", "At least one profile must remain.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "Confirm Deletion",
                    Content = "This will permanently delete the profile and all associated items.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel"
                };
                
                var result = await _contentDialogService.ShowAsync(dialog, CancellationToken.None);

                if (result == ContentDialogResult.Primary)
                {
                    if (ConfigModel.Instance.RemoveProfile(profile))
                    {
                        var newSelectedProfile = ConfigModel.Instance.Profiles.FirstOrDefault(profile => { return profile.Active; }, ConfigModel.Instance.Profiles[0]);
                        SharedModel.Instance.SelectedProfile = newSelectedProfile;
                        ConfigModel.Instance.SaveProfiles();
                    }
                }
            }
        }

        private async void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Profile profile)
                return;

            // 1) Ensure latest data is saved to panel/sp2
            ConfigModel.Instance.SaveProfiles();
            SharedModel.Instance.SaveDisplayItems();

            // 2) Build a nice default file name
            string baseName = profile.Name ?? "panel";
            baseName = baseName.Replace("[Import]", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = profile.Guid.ToString();

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export as AIDA SPZIP",
                Filter = "AIDA SPZIP Files (*.spzip)|*.spzip",
                FileName = baseName + ".spzip"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string targetPath = saveFileDialog.FileName;

                string? spzipPath = SpzipExporter.ExportProfileAsSpzip(profile, targetPath);

                if (!string.IsNullOrWhiteSpace(spzipPath))
                {
                    _snackbarService.Show("SPZIP Exported", spzipPath,
                        ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                }
                else
                {
                    _snackbarService.Show("SPZIP Export Failed",
                        "Unable to export current profile.",
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                }
            }

        }


        //
        private async void ButtonExportSpzip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Profile profile)
                return;

            // 1) Make sure profiles + display items are saved to their XML / sensorpanel files
            ConfigModel.Instance.SaveProfiles();
            SharedModel.Instance.SaveDisplayItems();

            // 2) Ask user where to put the .spzip
            Microsoft.Win32.OpenFolderDialog openFolderDialog = new();

            if (openFolderDialog.ShowDialog() == true)
            {
                string selectedFolderPath = openFolderDialog.FolderName;

                // 3) Call our SPZIP exporter
                string? spzipPath = SpzipExporter.ExportProfileAsSpzip(profile, selectedFolderPath);

                if (!string.IsNullOrWhiteSpace(spzipPath))
                {
                    _snackbarService.Show("SPZIP Exported", spzipPath, ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                }
                else
                {
                    _snackbarService.Show("SPZIP Export Failed", "Unable to export current profile.", ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                }
            }
        }


        private async void ButtonExportSqx_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Profile profile)
                return;

            // 1) Ensure latest data is saved (same as your existing export)
            ConfigModel.Instance.SaveProfiles();
            SharedModel.Instance.SaveDisplayItems();

            // 2) Build default filename (same logic as your other export)
            string baseName = profile.Name ?? "panel";
            baseName = baseName.Replace("[Import]", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = profile.Guid.ToString();

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export SynQPanel (.sqx)",
                Filter = "SynQPanel SQX Files (*.sqx)|*.sqx",
                FileName = baseName + ".sqx",
                DefaultExt = ".sqx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string targetPath = saveFileDialog.FileName;

                try
                {
                    // For now: plain XML .sqx (no assets). SharedModel.ExportProfileAsSqx should be implemented separately.
                    string? sqxPath = SharedModel.Instance.ExportProfileAsSqx_UsingSpzip(profile, targetPath);


                    if (!string.IsNullOrWhiteSpace(sqxPath))
                    {
                        _snackbarService.Show("SQX Exported", sqxPath,
                            ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
                    }
                    else
                    {
                        _snackbarService.Show("SQX Export Failed",
                            "Unable to export current profile to .sqx.",
                            ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    }
                }
                catch (Exception ex)
                {
                    _snackbarService.Show("SQX Export Failed", ex.Message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6));
                }
            }
        }


        private void ToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {
            ConfigModel.Instance.SaveProfiles();
        }
    }
}
