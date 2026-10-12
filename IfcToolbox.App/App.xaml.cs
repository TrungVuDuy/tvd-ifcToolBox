using IfcToolbox.App.Services;
using Microsoft.UI.Xaml;
using System;

namespace IfcToolbox.App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        public static MainWindow MainWindow { get; private set; }

        /// <summary>A file to import into the first tool page shown, from --import or a file opened with the app.</summary>
        public static string StartupImport { get; set; }

        /// <summary>
        /// Command line:
        ///   IfcToolbox.exe [model.ifc] [--page tools|settings|about|validator|...] [--theme light|dark|system]
        /// --theme applies to this run only; the saved setting is left alone.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            string page = null;
            var theme = AppSettings.Current.Theme;
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 1; i < arguments.Length; i++)
            {
                string next = i + 1 < arguments.Length ? arguments[i + 1] : null;
                switch (arguments[i].ToLowerInvariant())
                {
                    case "--page" when next != null:
                        page = next.ToLowerInvariant();
                        i++;
                        break;
                    case "--theme" when next != null:
                        theme = Enum.TryParse<AppTheme>(next, true, out var parsed) ? parsed : theme;
                        i++;
                        break;
                    case "--import" when next != null:
                        StartupImport = next;
                        i++;
                        break;
                    default:
                        if (!arguments[i].StartsWith("--", StringComparison.Ordinal))
                            StartupImport = arguments[i];
                        break;
                }
            }

            MainWindow = new MainWindow();
            MainWindow.ApplyTheme(theme);
            // A file given without a page opens in the validator, the tool most people start with.
            MainWindow.NavigateTo(page ?? (StartupImport != null ? "validator" : "tools"));
            MainWindow.Activate();
        }
    }
}
