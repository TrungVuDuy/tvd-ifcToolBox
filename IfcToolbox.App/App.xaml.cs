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
            // Before any XAML is loaded, or the text would come in the previous language.
            var arguments = Environment.GetCommandLineArgs();
            int language = Array.FindIndex(arguments, a => string.Equals(a, "--language", StringComparison.OrdinalIgnoreCase));
            Loc.Apply(language > 0 && language + 1 < arguments.Length ? arguments[language + 1] : null);
            InitializeComponent();
            UnhandledException += (_, e) => WriteCrashLog(e.Exception);
        }

        /// <summary>Where a crash is described, so a user can send it in.</summary>
        public static string CrashLogPath => Path.Combine(AppSettings.Folder, "crash.log");

        private static void WriteCrashLog(Exception exception)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.Folder);
                File.AppendAllText(CrashLogPath, $"{DateTime.Now:O} IFC Toolbox {typeof(App).Assembly.GetName().Version}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nothing more can be done while crashing.
            }
        }

        public static MainWindow MainWindow { get; private set; }

        /// <summary>
        /// WinUI writes "On" and "Off" next to a toggle switch in Windows' display language, not
        /// the one chosen in Settings; an implicit style gives every switch the app's words.
        /// Switches that set their own OnContent and OffContent keep them.
        /// </summary>
        private void UseLanguageForToggleSwitches()
        {
            var style = new Style(typeof(Microsoft.UI.Xaml.Controls.ToggleSwitch));
            if (Resources.TryGetValue("DefaultToggleSwitchStyle", out var basedOn) && basedOn is Style defaultStyle)
                style.BasedOn = defaultStyle;
            style.Setters.Add(new Setter(Microsoft.UI.Xaml.Controls.ToggleSwitch.OnContentProperty, Loc.Get("Common_On")));
            style.Setters.Add(new Setter(Microsoft.UI.Xaml.Controls.ToggleSwitch.OffContentProperty, Loc.Get("Common_Off")));
            Resources[typeof(Microsoft.UI.Xaml.Controls.ToggleSwitch)] = style;
        }

        /// <summary>Starts a new instance on <paramref name="page"/> and closes this one, e.g. to show a new language.</summary>
        public static void Restart(string page)
        {
            System.Diagnostics.Process.Start(Environment.ProcessPath, new[] { "--page", page });
            Current.Exit();
        }

        /// <summary>A file to import into the first tool page shown, from --import or a file opened with the app.</summary>
        public static string StartupImport { get; set; }

        /// <summary>Further --import files, for pages that take several (the Combiner).</summary>
        public static List<string> StartupMoreImports { get; } = new();

        /// <summary>IDS files to load into the validator, from --ids or .ids files opened with the app.</summary>
        public static List<string> StartupIds { get; } = new();

        /// <summary>A model for the Relocator to align to, from --reference.</summary>
        public static string StartupReference { get; set; }

        /// <summary>
        /// Command line:
        ///   IfcToolbox.exe [model.ifc] [requirements.ids] [--ids file.ids] [--reference other.ifc] [--page tools|settings|about|validator|...] [--theme light|dark|system] [--language en-US|vi-VN]
        /// --theme and --language apply to this run only; the saved settings are left alone.
        /// --language is read in the constructor, as it has to be set before any XAML loads.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            string page = null;
            var theme = AppSettings.Current.Theme;
            if (!string.IsNullOrEmpty(AppSettings.Current.IfcConvertPath) && File.Exists(AppSettings.Current.IfcConvertPath))
                IfcToolbox.Core.Convert.IfcConvert.ExecutablePathOverride = AppSettings.Current.IfcConvertPath;
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
                    case "--language" when next != null:
                        i++;
                        break;
                    case "--theme" when next != null:
                        theme = Enum.TryParse<AppTheme>(next, true, out var parsed) ? parsed : theme;
                        i++;
                        break;
                    case "--import" when next != null:
                        if (StartupImport == null)
                            StartupImport = next;
                        else
                            StartupMoreImports.Add(next);
                        i++;
                        break;
                    case "--reference" when next != null:
                        StartupReference = next;
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

            UseLanguageForToggleSwitches();
            MainWindow = new MainWindow();
            MainWindow.ApplyTheme(theme);
            // A file given without a page opens in the validator, the tool most people start with.
            MainWindow.NavigateTo(page ?? (StartupImport != null || StartupIds.Count > 0 ? "validator" : "tools"));
            MainWindow.Activate();
        }
    }
}
