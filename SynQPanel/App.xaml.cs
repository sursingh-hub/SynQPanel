using FlyleafLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Sentry;
using Serilog;
using Serilog.Events;
using SynQPanel.Models;
using SynQPanel.Monitors;
using SynQPanel.Services;
using SynQPanel.Utils;
using SynQPanel.ViewModels;
using SynQPanel.Views.Common;
using SynQPanel.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;


namespace SynQPanel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static readonly ILogger Logger = Log.ForContext<App>();
        private static readonly IHost _host = Host
       .CreateDefaultBuilder()
       //.ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)); })
       .UseSerilog((context, services, configuration) => configuration
#if DEBUG
           .MinimumLevel.Debug()
#else
           .MinimumLevel.Information()
          // .MinimumLevel.Warning()
#endif
           .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
           .Enrich.WithThreadId()
           .Enrich.WithThreadName()
           .Enrich.WithMachineName()
           .Enrich.FromLogContext()
           .WriteTo.Debug()
           .WriteTo.File(
               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "logs", "SynQPanel-.log"),
               rollingInterval: RollingInterval.Day,


               //retainedFileCountLimit: 7,
               //fileSizeLimitBytes: 104857600, // 100MB

               retainedFileCountLimit: 3,
               fileSizeLimitBytes: 10 * 1024 * 1024,

               rollOnFileSizeLimit: true,
               outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] [{ThreadName}] - [{SourceContext}] {Message:lj}{NewLine}{Exception}"
           ))
       .ConfigureServices((context, services) =>
       {
           // App Host
           services.AddHostedService<ApplicationHostService>();

           // Theme manipulation
           services.AddSingleton<IThemeService, ThemeService>();

           // Taskbar manipulation
           services.AddSingleton<ITaskBarService, TaskBarService>();

           // Snackbar service
           services.AddSingleton<ISnackbarService, SnackbarService>();


           // Dialog service
           services.AddSingleton<IContentDialogService, ContentDialogService>();

           //// Page resolver service
           services.AddSingleton<IPageService, PageService>();

           //// Page resolver service
           //services.AddSingleton<ITestWindowService, TestWindowService>();

           // Service containing navigation, same as INavigationWindow... but without window
           services.AddSingleton<INavigationService, NavigationService>();

           // Main window container with navigation
           services.AddScoped<INavigationWindow, MainWindow>();
           //services.AddScoped<ContainerViewModel>();

           // Views and ViewModels
           services.AddScoped<Views.Pages.HomePage>();
           services.AddScoped<HomeViewModel>();
           services.AddScoped<Views.Pages.ProfilesPage>();
           services.AddScoped<ProfilesViewModel>();
           services.AddScoped<Views.Pages.DesignPage>();
           services.AddScoped<DesignViewModel>();
           services.AddScoped<Views.Pages.PluginsPage>();
           services.AddScoped<PluginsViewModel>();
           services.AddScoped<Views.Pages.AboutPage>();
           services.AddScoped<AboutViewModel>();
           services.AddScoped<Views.Pages.SettingsPage>();
           services.AddScoped<SettingsViewModel>();
           services.AddScoped<Views.Pages.UpdatesPage>();
           services.AddScoped<UpdatesViewModel>();
           

           // Configuration
           //services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));
       }).Build();

        public Dictionary<Guid, DisplayWindow> DisplayWindows = [];

        public static T? GetService<T>()
        where T : class
        {
            return _host.Services.GetService(typeof(T)) as T;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public App()
        {
            // IMPORTANT: Set Dark theme before any UI resources are loaded
            // This prevents the ThemesDictionary constructor from defaulting to Light theme
            ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, false);

            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 1. Handle exceptions from background threads and Task.Run
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 2. Handle unobserved task exceptions (async/await without proper handling)
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            SentrySdk.Init(o =>
            {
                o.Dsn = "https://5ca30f9d2faba70d50918db10cee0d26@o4508414465146880.ingest.us.sentry.io/4508414467833856";
                o.Debug = true;
                o.AutoSessionTracking = true;

                // Add Sentry-specific options
                o.SendDefaultPii = true; // Include user info
                o.AttachStacktrace = true; // Always attach stack traces
                o.Environment = "production"; // or "development"
                o.Release = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            });
        }

        void App_DispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SentrySdk.AddBreadcrumb("WPF Dispatcher unhandled exception", "error");
            SentrySdk.CaptureException(e.Exception);

            // Log locally as well
            Logger.Error(e.Exception, "DispatcherUnhandledException occurred");

            // Decide whether to crash or continue
            e.Handled = true; // Uncomment to prevent crash
        }

        void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new Exception($"Non-exception thrown: {e.ExceptionObject}");

            SentrySdk.AddBreadcrumb($"AppDomain unhandled exception (IsTerminating: {e.IsTerminating})", "error");
            SentrySdk.CaptureException(exception);

            Logger.Fatal(exception, "CurrentDomain_UnhandledException occurred. IsTerminating: {IsTerminating}", e.IsTerminating);

            // Flush Sentry before potential termination
            SentrySdk.FlushAsync(TimeSpan.FromSeconds(5)).Wait();
        }

        void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            SentrySdk.AddBreadcrumb("Unobserved task exception", "error");

            // Handle aggregate exceptions
            if (e.Exception is AggregateException agg)
            {
                foreach (var innerEx in agg.InnerExceptions)
                {
                    SentrySdk.CaptureException(innerEx);
                    Logger.Error(innerEx, "TaskScheduler_UnobservedTaskException: Unobserved task exception in aggregate");
                }
            }
            else
            {
                SentrySdk.CaptureException(e.Exception);
                Logger.Error(e.Exception, "TaskScheduler_UnobservedTaskException: Unobserved task exception");
            }

            // Mark as observed to prevent app termination
            e.SetObserved();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Information("Application exiting");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            Logger.Information("SynQPanel starting up");

            
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

            Process proc = Process.GetCurrentProcess();
            var otherInstances = Process.GetProcesses()
                .Where(p => p.ProcessName == proc.ProcessName && p.Id != proc.Id)
                .ToList();

            if (otherInstances.Count > 0)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Another instance of SynQPanel is running (Process ID: {string.Join(", ", otherInstances.Select(p => p.Id))}).\n\n" +
                    "This may be from a previous crash or hung process.\n\n" +
                    "Would you like to terminate the other instance(s) and continue?",
                    "SynQPanel Already Running",
                    System.Windows.MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    Logger.Information("User chose to terminate {Count} other SynQPanel instance(s)", otherInstances.Count);
                    foreach (var otherProcess in otherInstances)
                    {
                        try
                        {
                            Logger.Information("Terminating process ID: {ProcessId}", otherProcess.Id);
                            otherProcess.Kill();
                            otherProcess.WaitForExit(5000); // Wait up to 5 seconds for process to exit
                            Logger.Information("Successfully terminated process ID: {ProcessId}", otherProcess.Id);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Failed to terminate process ID: {ProcessId}", otherProcess.Id);
                        }
                    }
                    Logger.Information("Continuing with SynQPanel startup after terminating other instances");
                    // Continue with startup
                }
                else
                {
                    Logger.Information("User chose not to terminate other instances, exiting");
                    Environment.Exit(0);
                    return;
                }
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var cwd = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);

            if (cwd != null)
            {
                Environment.CurrentDirectory = cwd;
            }

            base.OnStartup(e);

            var updateFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynQPanel", "updates", "SynQPanelSetup.exe");

            if (File.Exists(updateFilePath))
            {
                try
                {
                    File.Delete(updateFilePath);
                }
                catch { }
            }

            _host.Start();
            Logger.Debug("Application host started");

            try
            {
                bool isAdmin = SecurityUtil.IsRunningAsAdmin();

                using var identity = WindowsIdentity.GetCurrent();

                Logger.Information(
                    "Security context: IsAdmin={IsAdmin}, User={User}, IsSystem={IsSystem}, Is64Bit={Is64Bit}",
                    isAdmin,
                    identity.Name,
                    identity.IsSystem,
                    Environment.Is64BitProcess
                );
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to determine process elevation status");
            }


            Engine.Start(new EngineConfig()
            {
#if DEBUG
                LogOutput = ":debug",
                LogLevel = LogLevel.Debug,
                FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel.Warn,
#endif
                PluginsPath = ":FlyleafPlugins",
                FFmpegPath = ":FFmpeg",
            });
#if DEBUG
            Logger.Debug("Flyleaf engine started");
#endif

            ConfigModel.Instance.Initialize();
            Logger.Information("Configuration initialized");

            if (ConfigModel.Instance.Profiles.Count == 0)
            {
                Logger.Information("No profiles found. Creating default profile.");
                var profile = new Profile()
                {
                    Name = "Panel 1",
                    Active = true,
                };

                ConfigModel.Instance.AddProfile(profile);
                SharedModel.Instance.SelectedProfile = profile;
                ConfigModel.Instance.SaveProfiles();

                var textDisplayItem = new TextDisplayItem("Go to 'SynQ Manager' tab to start your journey.", profile);
                textDisplayItem.X = 50;
                textDisplayItem.Y = 100;
                textDisplayItem.Font = "Arial";
                textDisplayItem.Italic = true;

                SharedModel.Instance.AddDisplayItem(textDisplayItem);

                textDisplayItem = new TextDisplayItem("Drag this panel to reposition.", profile);
                textDisplayItem.X = 50;
                textDisplayItem.Y = 150;
                textDisplayItem.Font = "Arial";
                textDisplayItem.Italic = true;
                SharedModel.Instance.AddDisplayItem(textDisplayItem);
                SharedModel.Instance.SaveDisplayItems();
            }
            
            // No runtime startup performed. Keeping stub calls removed to avoid loading Libre.
            try
            {
                Logger.Information("LibreHardwareMonitor is disabled in this build (AIDA-only).");
            }
            catch { /* swallow logging errors */ }


            await PluginMonitor.Instance.StartAsync();
            SystemEvents.PowerModeChanged += OnPowerChange;

            //var window = new SkiaDisplayWindow();
            //window.Show();
        }

        void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Task.Run(async () =>
            {
                
            }).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void OnPowerChange(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    Task.Run(async () =>
                    {
                      
                    }).ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
                case PowerModes.Suspend:
                    Task.Run(async () =>
                    {
                        
                    }).ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
            }
        }


       




        public static async Task CleanShutDown()
        {
            DisplayWindowManager.Instance.CloseAll();
           
            
            await PluginMonitor.Instance.StopAsync();
            //shutdown

            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }

        public void ShowDesign(Profile profile)
        {
            SharedModel.Instance.SelectedProfile = profile;
            var window = _host.Services.GetRequiredService<INavigationWindow>() as MainWindow;
            window?.RestoreWindow();
            window?.Navigate(typeof(Views.Pages.DesignPage));
        }

        public void MaximiseDisplayWindow(Profile profile)
        {
            DisplayWindowManager.Instance.GetWindow(profile.Guid)?.Fullscreen();
        }

        public void ShowDisplayWindow(Profile profile)
        {
            DisplayWindowManager.Instance.ShowDisplayWindow(profile);
        }

        public void CloseDisplayWindow(Profile profile)
        {
            DisplayWindowManager.Instance.CloseDisplayWindow(profile.Guid);
            //var window = GetDisplayWindow(profile);
            //window?.Close();
        }

        private void DisplayWindow_Closed(object? sender, EventArgs e)
        {
            if (sender is DisplayWindow displayWindow)
            {
                displayWindow.Closed -= DisplayWindow_Closed;
                DisplayWindows.Remove(displayWindow.Profile.Guid);
            }
        }
    
        
    
    }
}
