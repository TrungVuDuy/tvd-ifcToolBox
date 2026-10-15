using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.Core.Convert;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IfcToolbox.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly bool _loading;
        /// <summary>The language choice when the page opened, to tell whether a restart is needed.</summary>
        private readonly int _languageAtStart;

        public SettingsPage()
        {
            InitializeComponent();

            _loading = true;
            var settings = AppSettings.Current;
            ThemeChoice.SelectedIndex = settings.Theme switch
            {
                AppTheme.Light => 0,
                AppTheme.Dark => 1,
                _ => 2,
            };
            // First the Windows display language, then each translation under its own name.
            LanguageChoice.ItemsSource = Loc.Languages.Select(l => l.NativeName).Prepend(Loc.Get("Settings_LanguageWindows")).ToList();
            int chosen = settings.UseCustomLanguage ? Loc.Languages.ToList().FindIndex(l => l.Tag == settings.Language) : -1;
            _languageAtStart = chosen + 1;
            LanguageChoice.SelectedIndex = _languageAtStart;
            _loading = false;

            ShowCacheSize();
            _ = ShowIfcConvertAsync();
        }

        private async Task ShowIfcConvertAsync()
        {
            bool custom = !string.IsNullOrEmpty(IfcConvert.ExecutablePathOverride);
            BundledIfcConvertButton.IsEnabled = custom;
            IfcConvertText.Text = Loc.Get("Settings_CheckingIfcConvert");
            // Starts IfcConvert to ask its version, so off the UI thread.
            var (path, version) = await Task.Run(() =>
            {
                var executable = IfcConvert.ResolveExecutable();
                return (executable, IfcConvert.GetVersion(executable));
            });
            IfcConvertText.Text = custom
                ? Loc.Format("Settings_IfcConvertCustom", version, path)
                : Loc.Format("Settings_IfcConvertBundled", version);
        }

        private async void OnPickIfcConvert(object sender, RoutedEventArgs e)
        {
            IfcConvertError.IsOpen = false;
            var path = await Pickers.OpenFileAsync(".exe");
            if (path == null)
                return;

            var version = await Task.Run(() => IfcConvert.GetVersion(path));
            if (version.IsUnknown)
            {
                IfcConvertError.Message = Loc.Format("Settings_IfcConvertNoVersion", Path.GetFileName(path));
                IfcConvertError.IsOpen = true;
                return;
            }

            IfcConvert.ExecutablePathOverride = path;
            AppSettings.Current.IfcConvertPath = path;
            AppSettings.Current.Save();
            await ShowIfcConvertAsync();
        }

        private async void OnBundledIfcConvert(object sender, RoutedEventArgs e)
        {
            IfcConvertError.IsOpen = false;
            IfcConvert.ExecutablePathOverride = null;
            AppSettings.Current.IfcConvertPath = null;
            AppSettings.Current.Save();
            await ShowIfcConvertAsync();
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            var theme = ThemeChoice.SelectedIndex switch
            {
                0 => AppTheme.Light,
                1 => AppTheme.Dark,
                _ => AppTheme.System,
            };
            // RadioButtons reports the initial selection late, after loading is done.
            if (_loading || theme == AppSettings.Current.Theme)
                return;
            AppSettings.Current.Theme = theme;
            AppSettings.Current.Save();
            App.MainWindow.ApplyTheme(AppSettings.Current.Theme);
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || LanguageChoice.SelectedIndex < 0)
                return;
            int index = LanguageChoice.SelectedIndex;
            AppSettings.Current.UseCustomLanguage = index > 0;
            if (index > 0)
                AppSettings.Current.Language = Loc.Languages[index - 1].Tag;
            AppSettings.Current.Save();
            // The text is loaded once at start-up, so a new language shows after a restart.
            RestartPanel.Visibility = index == _languageAtStart ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnRestart(object sender, RoutedEventArgs e) => App.Restart("settings");

        private void OnClearCache(object sender, RoutedEventArgs e)
        {
            long freed = AppSettings.ClearCache();
            CacheText.Text = Loc.Format("Settings_CacheFreed", ImportedFile.FormatSize(freed));
            ClearCacheButton.IsEnabled = false;
        }

        private void ShowCacheSize()
        {
            long size = AppSettings.CacheSize();
            CacheText.Text = size == 0 ? Loc.Get("Settings_CacheEmpty") : Loc.Format("Settings_CacheSize", ImportedFile.FormatSize(size));
            ClearCacheButton.IsEnabled = size > 0;
        }

        private void OnOpenSettingsFolder(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppSettings.Folder);
            Process.Start(new ProcessStartInfo(AppSettings.Folder) { UseShellExecute = true });
        }
    }
}
