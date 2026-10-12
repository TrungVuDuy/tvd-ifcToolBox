using IfcToolbox.App.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;

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

        /// <summary>IDS files to load into the validator, from --ids or .ids files opened with the app.</summary>
        public static List<string> StartupIds { get; } = new();

        /// <summary>
        /// Command line:
        ///   IfcToolbox.exe [model.ifc] [requirements.ids] [--ids file.ids] [--page tools|settings|about|validator|...] [--theme light|dark|system]
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
                    case "--ids" when next != null:
                        StartupIds.Add(next);
                        i++;
                        break;
                    default:
                        if (arguments[i].StartsWith("--", StringComparison.Ordinal))
                            break;
                        if (string.Equals(Path.GetExtension(arguments[i]), ".ids", StringComparison.OrdinalIgnoreCase))
                            StartupIds.Add(arguments[i]);
                        else
                            StartupImport = arguments[i];
                        break;
                }
            }

            MainWindow = new MainWindow();
            MainWindow.ApplyTheme(theme);
            // A file given without a page opens in the validator, the tool most people start with.
            MainWindow.NavigateTo(page ?? (StartupImport != null || StartupIds.Count > 0 ? "validator" : "tools"));
            MainWindow.Activate();
        }
    }
}
