using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.App.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Linq;
using Windows.Graphics;
using Windows.UI;

namespace IfcToolbox.App
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            AppWindow.Resize(Scale(new SizeInt32(1200, 860)));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = Scale(new SizeInt32(640, 0)).Width;
                presenter.PreferredMinimumHeight = Scale(new SizeInt32(0, 480)).Height;
            }

            // One menu entry per tool, in the order of the original app.
            foreach (var tool in ToolCatalog.All)
            {
                Nav.MenuItems.Add(new NavigationViewItem
                {
                    Content = tool.Title,
                    Tag = tool.Key,
                    Icon = new FontIcon { Glyph = tool.Glyph },
                });
            }

            Root.ActualThemeChanged += (_, _) => UpdateCaptionButtons();
        }

        /// <summary>
        /// Shows a page by its tag: "tools", "settings", "about" or a tool key such as "validator".
        /// </summary>
        public void NavigateTo(string tag)
        {
            var item = Nav.MenuItems.Concat(Nav.FooterMenuItems)
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => (string)i.Tag == tag);
            Nav.SelectedItem = item ?? Nav.MenuItems[0];
        }

        public void ApplyTheme(AppTheme theme)
        {
            Root.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            UpdateCaptionButtons();
        }

        private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
            var tool = ToolCatalog.Find(tag);
            (Type page, object parameter) = tag switch
            {
                "settings" => (typeof(SettingsPage), null),
                "about" => (typeof(AboutPage), null),
                "validator" => (typeof(ValidatorPage), null),
                "splitter" => (typeof(SplitterPage), null),
                "converter" => (typeof(ConverterPage), null),
                _ when tool != null => (typeof(ToolPage), tool),
                _ => (typeof(ToolsPage), (object)null),
            };
            ContentFrame.Navigate(page, parameter, new SuppressNavigationTransitionInfo());
        }

        /// <summary>The minimise, maximise and close glyphs follow the app theme, not the system one.</summary>
        private void UpdateCaptionButtons()
        {
            var titleBar = AppWindow.TitleBar;
            bool dark = Root.ActualTheme == ElementTheme.Dark;
            var foreground = dark ? Colors.White : Colors.Black;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = dark ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x18, 0, 0, 0);
            titleBar.ButtonPressedBackgroundColor = dark ? Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x28, 0, 0, 0);
            titleBar.ButtonInactiveForegroundColor = dark ? Color.FromArgb(0xFF, 0x80, 0x80, 0x80) : Color.FromArgb(0xFF, 0x90, 0x90, 0x90);
        }

        private SizeInt32 Scale(SizeInt32 size)
        {
            // AppWindow sizes are in physical pixels.
            double scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
            return new SizeInt32((int)(size.Width * scale), (int)(size.Height * scale));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
    }
}
