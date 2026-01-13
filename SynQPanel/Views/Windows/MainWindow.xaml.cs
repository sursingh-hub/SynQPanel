using SynQPanel.Models;
using SynQPanel.Utils;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SynQPanel.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: FluentWindow, INavigationWindow
    {
        //private readonly ITaskBarService _taskBarService;
        private ITaskBarService? _taskBarService;

        private bool _allowRealClose = false;

        public MainWindow(INavigationService navigationService, IPageService pageService, ITaskBarService taskBarService, ISnackbarService snackbarService, IContentDialogService contentDialogService)
        {
            // Assign the view model
            //ViewModel = viewModel;
            DataContext = this;

            // Attach the taskbar service
            _taskBarService = taskBarService;

            InitializeComponent();

            // We define a page provider for navigation
            SetPageService(pageService);

            // If you want to use INavigationService instead of INavigationWindow you can define its navigation here.
            navigationService.SetNavigationControl(RootNavigation);

            snackbarService.SetSnackbarPresenter(RootSnackbar);
            contentDialogService.SetDialogHost(RootContentDialog);

            
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

            if (version != null)
            {
                RootTitleBar.Title = $"SynQPanel - v{version}";
            }
            
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;

            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var desiredHeight = screenHeight * 0.80;

            if (desiredHeight > MinHeight)
            {
                Height = desiredHeight;
            }

        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                SharedModel.Instance.SelectedItem = null;
                
                if (ConfigModel.Instance.Settings.MinimizeToTray)
                {
                    Hide();
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (ConfigModel.Instance.Settings.StartMinimized)
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            MinWidth = 1300;
            MinHeight = 900;

            Navigate(typeof(Pages.HomePage));

            if (ConfigModel.Instance.Settings.StartMinimized && ConfigModel.Instance.Settings.MinimizeToTray)
            {
                Hide();
            }
        }


        private void TrayMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem)
                return;

            if(menuItem.Tag is string tag)
            {
                switch(tag)
                {
                    case "open":
                        RestoreWindow();
                        break;
                    case "profiles":
                        RestoreWindow();
                        Navigate(typeof(Pages.ProfilesPage));
                        break;
                    case "design":
                        RestoreWindow();
                        Navigate(typeof(Pages.DesignPage));
                        break;
                    case "plugins":
                        RestoreWindow();
                        Navigate(typeof(Pages.PluginsPage));
                        break;
                    
                    case "settings":
                        RestoreWindow();
                        Navigate(typeof(Pages.SettingsPage));
                        break;
                    case "updates":
                        RestoreWindow();
                        Navigate(typeof(Pages.UpdatesPage));
                        break;
                    case "about":
                        RestoreWindow();
                        Navigate(typeof(Pages.AboutPage));
                        break;
                    case "close":
                        _allowRealClose = true;
                        Close();
                        break;
                    default:
                        RestoreWindow();
                        break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"DEBUG | WPF UI Tray clicked: {menuItem.Tag}", "Wpf.Ui.Demo");
        }

        private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
        {
            RestoreWindow();
        }

        public void RestoreWindow()
        {
            if (WindowState != WindowState.Minimized && Visibility == Visibility.Visible)
                return;
            Show();
            WindowState = WindowState.Normal;
        }

        #region INavigationWindow methods

        public Frame GetFrame()
        {
            // In WPF-UI v3, NavigationView manages its own internal frame
            // We need to return a Frame for compatibility, so we'll create one if needed
            if (_navigationFrame == null)
            {
                _navigationFrame = new Frame();
            }
            return _navigationFrame;
        }
        
        private Frame? _navigationFrame;

        public INavigationView GetNavigation()
            => RootNavigation;

        public bool Navigate(Type pageType)
            => RootNavigation.Navigate(pageType);
            
           // => RootNavigation.Navigate(pageType) != null;

        public void SetPageService(IPageService pageService)
            => RootNavigation.SetPageService(pageService);

        public void ShowWindow()
            => Show();

        public void CloseWindow()
        {
            _allowRealClose = true;
            Close();
        }


        public MainWindow()
        {
            InitializeComponent();

            this.Closing += MainWindow_Closing;
            this.StateChanged += MainWindow_StateChanged;
        }



        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            // This is used to provide services to the navigation window
            // Implementation depends on your specific needs
        }

        #endregion INavigationWindow methods

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 🔴 CASE 1: Close-to-tray enabled AND this is NOT an explicit exit
            if (!_allowRealClose && ConfigModel.Instance.Settings.CloseToTray)
            {

                // 🔑 SAVE SETTINGS BEFORE HIDING
                ConfigModel.Instance.SaveSettings();

                e.Cancel = true;
                Hide();
                return;
            }

            // 🔵 CASE 2: REAL shutdown (tray exit / Windows shutdown / explicit close)
            e.Cancel = true; // keep async-safe pattern

            ConfigModel.Instance.SaveSettings();

            if (WindowState != WindowState.Minimized)
            {
                var loadingWindow = new LoadingWindow
                {
                    Owner = this
                };
                loadingWindow.SetText("Cleaning up..");
                loadingWindow.Show();
            }

            await FileUtil.CleanupAssets();
            await App.CleanShutDown();
        }


        public void ShowCanvasContextMenu(Point screenPoint, Profile profile)
        {
            var menu = new System.Windows.Controls.ContextMenu();


            // ───── Navigation ─────
            var hubItem = new System.Windows.Controls.MenuItem
            {
                Header = "SynQ Hub",
                Icon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Wpf.Ui.Controls.SymbolRegular.Home24,
                    Width = 16,
                    Height = 16
                }
            };

            hubItem.Click += (_, _) =>
            {
                RestoreWindow();
                Navigate(typeof(Pages.HomePage));
            };

            menu.Items.Add(hubItem);


            var galleryItem = new System.Windows.Controls.MenuItem
            {
                Header = "SynQ Gallery",
                Icon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Wpf.Ui.Controls.SymbolRegular.List20,
                    Width = 16,
                    Height = 16
                }
            };

            galleryItem.Click += (_, _) =>
            {
                RestoreWindow();
                Navigate(typeof(Pages.ProfilesPage));
            };

            menu.Items.Add(galleryItem);


            var synqManagerItem = new System.Windows.Controls.MenuItem
            {
                Header = "SynQ Manager",
                Icon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Wpf.Ui.Controls.SymbolRegular.DesktopCursor20,
                    Width = 16,
                    Height = 16
                }
            };

            synqManagerItem.Click += (_, _) =>
            {
                RestoreWindow();
                Navigate(typeof(Pages.DesignPage));
            };

            
            menu.Items.Add(synqManagerItem);

            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(CreateSeparator());

            // ───── Window actions ─────
            var minimizeItem = new System.Windows.Controls.MenuItem
            {
                Header = "Minimize",
                Icon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Wpf.Ui.Controls.SymbolRegular.ShareCloseTray20,
                    Width = 16,
                    Height = 16
                }
            };

            minimizeItem.Click += (_, _) =>
            {
                WindowState = WindowState.Minimized;
            };

            menu.Items.Add(minimizeItem);


            var closeToTrayItem = new System.Windows.Controls.MenuItem
            {
                Header = "Close to Tray",
                Icon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle20,
                    Width = 16,
                    Height = 16
                }
            };

            closeToTrayItem.Click += (_, _) =>
            {
                if (ConfigModel.Instance.Settings.MinimizeToTray)
                {
                    Hide();
                }
                else
                {
                    Close();
                }
            };

            menu.Items.Add(closeToTrayItem);


            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(CreateSeparator());

            // ───── Hide Panel ─────
            var hidePanelItem = new System.Windows.Controls.MenuItem
            {
                Header = "Hide Panel",
                Icon = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Wpf.Ui.Controls.SymbolRegular.EyeOff20,
                    Width = 16,
                    Height = 16
                }
            };

            hidePanelItem.Click += (_, _) =>
            {
                RestoreWindow();
                Navigate(typeof(Pages.ProfilesPage));

                profile.Active = false;
                ConfigModel.Instance.SaveProfiles();
            };

            menu.Items.Add(hidePanelItem);



            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Absolute;
            menu.HorizontalOffset = screenPoint.X;
            menu.VerticalOffset = screenPoint.Y;
            menu.IsOpen = true;
        }

        private System.Windows.Controls.MenuItem CreateMenuItem(string header, Action action)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = header
            };

            item.Click += (_, _) => action();

            return item;
        }

        private static System.Windows.Controls.MenuItem CreateSeparator()
        {
            return new System.Windows.Controls.MenuItem
            {
                IsEnabled = false,
                Height = 1,
                Margin = new Thickness(6, 6, 6, 6),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };
        }



    }
}
