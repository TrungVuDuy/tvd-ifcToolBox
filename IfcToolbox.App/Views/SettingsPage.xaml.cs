using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IfcToolbox.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        private sealed record LanguageOption(string Tag, string Name);

        // The translations of the original app (en, de, fr, pt, zh) are not brought over yet,
        // so English is the only choice that does anything today.
        private static readonly LanguageOption[] Languages =
        {
            new("en-US", "English"),
        };

        private readonly bool _loading;

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
            LanguageChoice.ItemsSource = Languages.Select(l => l.Name).ToList();
            LanguageChoice.SelectedIndex = System.Math.Max(0, System.Array.FindIndex(Languages, l => l.Tag == settings.Language));
            CustomLanguageSwitch.IsOn = settings.UseCustomLanguage;
            LanguageChoice.IsEnabled = settings.UseCustomLanguage;
            _loading = false;

            ShowCacheSize();
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

        private void OnCustomLanguageToggled(object sender, RoutedEventArgs e)
        {
            LanguageChoice.IsEnabled = CustomLanguageSwitch.IsOn;
            if (_loading || CustomLanguageSwitch.IsOn == AppSettings.Current.UseCustomLanguage)
                return;
            AppSettings.Current.UseCustomLanguage = CustomLanguageSwitch.IsOn;
            AppSettings.Current.Save();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || LanguageChoice.SelectedIndex < 0 || Languages[LanguageChoice.SelectedIndex].Tag == AppSettings.Current.Language)
                return;
            AppSettings.Current.Language = Languages[LanguageChoice.SelectedIndex].Tag;
            AppSettings.Current.Save();
        }

        private void OnClearCache(object sender, RoutedEventArgs e)
        {
            long freed = AppSettings.ClearCache();
            CacheText.Text = $"Freed {ImportedFile.FormatSize(freed)}.";
            ClearCacheButton.IsEnabled = false;
        }

        private void ShowCacheSize()
        {
            long size = AppSettings.CacheSize();
            CacheText.Text = size == 0 ? "The cache is empty." : $"{ImportedFile.FormatSize(size)} in the cache.";
            ClearCacheButton.IsEnabled = size > 0;
        }

        private void OnOpenSettingsFolder(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppSettings.Folder);
            Process.Start(new ProcessStartInfo(AppSettings.Folder) { UseShellExecute = true });
        }
    }
}
