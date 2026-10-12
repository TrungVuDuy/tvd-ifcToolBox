using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Reflection;

namespace IfcToolbox.App.Views
{
    public sealed partial class AboutPage : Page
    {
        // Name, what it does here, licence, link.
        private static readonly (string Name, string Use, string License, string Url)[] Dependencies =
        {
            ("xbim Essentials", "Reading and writing IFC models", "CDDL-1.0", "https://github.com/xBimTeam/XbimEssentials"),
            ("IfcOpenShell IfcConvert", "Geometry conversion", "LGPL-3.0", "https://ifcopenshell.org/"),
            ("buildingSMART Data Dictionary", "Classifications and properties", "bSDD terms of use", "https://www.buildingsmart.org/users/services/buildingsmart-data-dictionary/"),
            ("Information Delivery Specification", "IDS 1.0 requirements", "buildingSMART", "https://github.com/buildingSMART/IDS"),
            ("Windows App SDK / WinUI 3", "User interface", "MIT", "https://github.com/microsoft/WindowsAppSDK"),
            ("CommunityToolkit.Mvvm", "User interface", "MIT", "https://github.com/CommunityToolkit/dotnet"),
            ("Serilog", "Logging", "Apache-2.0", "https://serilog.net/"),
            ("CsvHelper", "CSV reports", "MS-PL / Apache-2.0", "https://joshclose.github.io/CsvHelper/"),
        };

        public AboutPage()
        {
            InitializeComponent();

            var version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            // Drop the "+commit" build metadata the SDK appends.
            int plus = version.IndexOf('+');
            VersionText.Text = "Version " + (plus < 0 ? version : version[..plus]);

            for (int i = 0; i < Dependencies.Length; i++)
            {
                var (name, use, license, url) = Dependencies[i];
                DependencyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddCell(new HyperlinkButton { Content = name, NavigateUri = new Uri(url), Style = (Style)Resources["DependencyNameStyle"] }, i, 0);
                AddCell(new TextBlock { Text = use, VerticalAlignment = VerticalAlignment.Center }, i, 1);
                AddCell(new TextBlock { Text = license, Style = (Style)Resources["SecondaryTextStyle"] }, i, 2);
            }
        }

        /// <summary>Opens a licence file shipped beside the app, or the GPL online when run from a build without it.</summary>
        private void OnOpenLicense(object sender, RoutedEventArgs e)
        {
            var name = (sender as FrameworkElement)?.Tag as string ?? "LICENSE.txt";
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, name);
            var target = System.IO.File.Exists(path) ? path : "https://www.gnu.org/licenses/gpl-3.0.html";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
        }

        private void AddCell(FrameworkElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            DependencyGrid.Children.Add(element);
        }
    }
}
