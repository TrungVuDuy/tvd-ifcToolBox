using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.Core.Convert;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IfcToolbox.App.Views
{
    /// <summary>IFC Converter: runs IfcOpenShell's IfcConvert on one or more models.</summary>
    public sealed partial class ConverterPage : Page
    {
        // Built when the page type is first used, after the interface language is set.
        private static readonly (ConvertTargetFormat Format, string Title, string Detail, string Glyph)[] Formats =
        {
            (ConvertTargetFormat.GLB, "glTF (.glb)", Loc.Get("Converter_Format_GLB"), ""),
            (ConvertTargetFormat.OBJ, "OBJ (.obj)", Loc.Get("Converter_Format_OBJ"), ""),
            (ConvertTargetFormat.DAE, "Collada (.dae)", Loc.Get("Converter_Format_DAE"), ""),
            (ConvertTargetFormat.STP, "STEP (.stp)", Loc.Get("Converter_Format_STP"), ""),
            (ConvertTargetFormat.IGS, "IGES (.igs)", Loc.Get("Converter_Format_IGS"), ""),
            (ConvertTargetFormat.XML, "XML (.xml)", Loc.Get("Converter_Format_XML"), ""),
            (ConvertTargetFormat.SVG, "SVG (.svg)", Loc.Get("Converter_Format_SVG"), ""),
            (ConvertTargetFormat.IFC, "IFC (.ifc)", Loc.Get("Converter_Format_IFC"), ""),
            (ConvertTargetFormat.JSON, "xeokit (.json)", Loc.Get("Converter_Format_JSON"), ""),
            (ConvertTargetFormat.TTL, "Turtle (.ttl)", Loc.Get("Converter_Format_TTL"), ""),
            (ConvertTargetFormat.H5, "HDF5 (.h5)", Loc.Get("Converter_Format_H5"), ""),
        };

        /// <summary>Classes the default filter leaves out; unticked when the filter is switched on.</summary>
        private static readonly HashSet<string> DefaultExcluded = new() { "IfcOpeningElement", "IfcSpace" };

        private IfcConvertVersion _version;
        private int _classRun;

        public ConverterPage()
        {
            InitializeComponent();
            BuildFormatTiles();
            SchemaWarning.ActionButton = new Button { Content = Loc.Get("Converter_OpenSettings") };
            SchemaWarning.ActionButton.Click += (_, _) => App.MainWindow.NavigateTo("settings");
        }

        private ConvertTargetFormat Format =>
            (ConvertTargetFormat)FormatGrid.Children.OfType<RadioButton>().First(t => t.IsChecked == true).Tag;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("converter");
            Header.Title = tool.Title;
            Header.Glyph = tool.Glyph;
            Header.TileBrush = new SolidColorBrush(tool.Color);
            SummaryText.Text = tool.Summary;
            RunBar.ActionText = tool.Action;

            if (App.StartupImport != null)
            {
                ModelZone.Import(new[] { App.StartupImport });
                App.StartupImport = null;
            }
            UpdateState();

            // Asking IfcConvert its version starts the executable, so off the UI thread.
            VersionText.Text = Loc.Get("Converter_CheckingIfcConvert");
            _version = await Task.Run(() => IfcConvert.GetVersion(IfcConvert.ResolveExecutable()));
            VersionText.Text = string.IsNullOrEmpty(IfcConvert.ExecutablePathOverride)
                ? Loc.Format("Converter_BundledVersion", _version)
                : Loc.Format("Converter_CustomVersion", _version);
            UpdateFormats();
            UpdateState();
        }

        private void BuildFormatTiles()
        {
            for (int i = 0; i < Formats.Length; i++)
            {
                var (format, title, detail, glyph) = Formats[i];
                var content = new Grid { ColumnSpacing = 12 };
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
                var text = new StackPanel();
                text.Children.Add(new TextBlock { Text = title, Style = StyleOf("BodyStrongTextBlockStyle") });
                text.Children.Add(new TextBlock { Text = detail, Style = StyleOf("SecondaryCaptionStyle"), TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(text, 1);
                content.Children.Add(text);

                var tile = new RadioButton
                {
                    Content = content,
                    Tag = format,
                    GroupName = "TargetFormat",
                    Style = StyleOf("ModeTileStyle"),
                    IsChecked = i == 0,
                };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tile, title);
                tile.Checked += (_, _) => UpdateState();

                if (FormatGrid.RowDefinitions.Count <= i / 3)
                    FormatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(tile, i / 3);
                Grid.SetColumn(tile, i % 3);
                FormatGrid.Children.Add(tile);
            }
        }

        /// <summary>Greys out what the IfcConvert in use cannot write.</summary>
        private void UpdateFormats()
        {
            foreach (var tile in FormatGrid.Children.OfType<RadioButton>())
            {
                var format = (ConvertTargetFormat)tile.Tag;
                bool supported = ConvertFormatInfo.IsSupportedBy(format, _version);
                tile.IsEnabled = supported;
                ToolTipService.SetToolTip(tile, supported ? null : Loc.Format("Converter_NeedsVersion", ConvertFormatInfo.GetMinimumVersion(format)));
            }
            if (FormatGrid.Children.OfType<RadioButton>().First(t => t.IsChecked == true).IsEnabled == false)
                FormatGrid.Children.OfType<RadioButton>().First().IsChecked = true;
        }

        private async void OnFilesChanged(object sender, EventArgs e)
        {
            OutputSection.Visibility = Visibility.Collapsed;
            UpdateState();
            if (FilterSwitch.IsOn)
                await LoadClassesAsync();
        }

        private async void OnFilterToggled(object sender, RoutedEventArgs e)
        {
            if (FilterSwitch.IsOn)
                await LoadClassesAsync();
            else
                ClassList.Visibility = Visibility.Collapsed;
            UpdateState();
        }

        /// <summary>Lists the classes found in all imported models, ticked except openings and spaces.</summary>
        private async Task LoadClassesAsync()
        {
            var paths = ModelZone.Files.Select(f => f.Path).ToList();
            int run = ++_classRun;
            var previouslyUnticked = ClassList.Children.OfType<CheckBox>().Where(c => c.IsChecked != true).Select(c => (string)c.Tag).ToHashSet();
            ClassList.Children.Clear();
            ClassList.Visibility = Visibility.Collapsed;
            if (paths.Count == 0)
                return;

            ClassProgress.IsActive = true;
            try
            {
                var counts = await Task.Run(() =>
                {
                    var merged = new Dictionary<string, int>();
                    foreach (var path in paths)
                        foreach (var (name, count) in ConvertAnalysis.ReadClasses(path))
                            merged[name] = merged.GetValueOrDefault(name) + count;
                    return merged;
                });
                if (run != _classRun)
                    return;

                foreach (var (name, count) in counts.OrderBy(c => c.Key, StringComparer.Ordinal))
                {
                    var box = new CheckBox
                    {
                        Content = $"{name} ({count})",
                        Tag = name,
                        IsChecked = !DefaultExcluded.Contains(name) && !previouslyUnticked.Contains(name),
                    };
                    box.Checked += (_, _) => UpdateState();
                    box.Unchecked += (_, _) => UpdateState();
                    ClassList.Children.Add(box);
                }
                ClassList.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                if (run == _classRun)
                    RunBar.ShowError(Loc.Get("Common_ModelUnreadable"), ex.Message);
            }
            finally
            {
                if (run == _classRun)
                    ClassProgress.IsActive = false;
                UpdateState();
            }
        }

        private List<string> TickedClasses =>
            ClassList.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();

        private void UpdateState()
        {
            if (RunBar == null || RunBar.IsBusy)
                return;
            UpdateSchemaWarning();

            int files = ModelZone.Files.Count;
            bool filterReady = !FilterSwitch.IsOn || (ClassList.Children.Count > 0 && TickedClasses.Count > 0);
            RunBar.CanRun = files > 0 && _version != null && filterReady && !SchemaWarning.IsOpen;
            RunBar.Status = files == 0 ? Loc.Get("Converter_NeedModels")
                : SchemaWarning.IsOpen ? string.Empty
                : !filterReady ? Loc.Get("Converter_NeedClass")
                : files == 1 ? Loc.Format("Converter_ReadyOne", Format) : Loc.Format("Converter_ReadyMany", Format, files);
        }

        /// <summary>The bundled IfcConvert 0.6 predates IFC 4.3 and cannot read those models.</summary>
        private void UpdateSchemaWarning()
        {
            if (_version == null)
                return;
            var unreadable = ModelZone.Files
                .Where(f => !IfcConvert.CanRead(_version, IfcConvert.ReadFileSchema(f.Path) ?? string.Empty))
                .Select(f => f.Name)
                .ToList();
            SchemaWarning.IsOpen = unreadable.Count > 0;
            if (unreadable.Count > 0)
            {
                SchemaWarning.Message = Loc.Format("Converter_CannotRead43", _version, string.Join(", ", unreadable));
            }
        }

        private ConvertOptionsWrap BuildOptions(ConvertTargetFormat format)
        {
            var options = new ConvertOptionsWrap
            {
                UseElementGuids = NamingChoice.SelectedIndex == 1,
                UseElementNames = NamingChoice.SelectedIndex == 2,
                SiteLocalPlacement = PlacementChoice.SelectedIndex == 1,
                BuildingLocalPlacement = PlacementChoice.SelectedIndex == 2,
                UseWorldCoords = WorldCoordsCheck.IsChecked == true,
                CenterModel = CenterCheck.IsChecked == true,
            };
            if (FilterSwitch.IsOn)
                options.IncludeEntities = string.Join(" ", TickedClasses);
            else
                options.ExcludeEntities = IfcConvert.GetExcludeEntitiesDefault(format);
            return options;
        }

        private async void OnConvert(object sender, EventArgs e)
        {
            var paths = ModelZone.Files.Select(f => f.Path).ToList();
            if (paths.Count == 0)
                return;
            var format = Format;
            var config = ConfigFactory.CreateConfigConvert(BuildOptions(format), format);

            // IfcConvert cannot be stopped half way through a model; between models it can.
            var token = RunBar.Begin(Loc.Format("Converter_Running", Path.GetFileName(paths[0]), format), cancellable: paths.Count > 1);
            var started = DateTime.Now;
            var written = new List<string>();
            var failures = new List<string>();
            string outcome = string.Empty;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var path = paths[i];
                    RunBar.Status = paths.Count == 1
                        ? Loc.Format("Converter_Running", Path.GetFileName(path), format)
                        : Loc.Format("Converter_RunningMany", i + 1, paths.Count, Path.GetFileName(path));
                    try
                    {
                        var result = await Task.Run(() => ConverterProcessor.Process(path, config));
                        written.AddRange(result.FilePaths);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    }
                }
                outcome = Loc.Format("Common_Done", (DateTime.Now - started).TotalSeconds);
            }
            catch (OperationCanceledException)
            {
                outcome = Loc.Get("Converter_Stopped");
            }
            finally
            {
                if (failures.Count > 0)
                {
                    RunBar.ShowError(failures.Count == paths.Count ? Loc.Get("Converter_NothingConverted") : Loc.Format("Converter_SomeFailed", failures.Count, paths.Count),
                        string.Join(Environment.NewLine, failures));
                }
                if (written.Count > 0)
                {
                    OutputView.Show(Loc.Get("Converter_Finished"), written,
                        written.Any(p => p.EndsWith(".log.txt", StringComparison.OrdinalIgnoreCase))
                            ? Loc.Get("Converter_Warnings")
                            : null);
                    OutputSection.Visibility = Visibility.Visible;
                    OutputSection.IsExpanded = true;
                    Ui.ScrollIntoView(OutputSection, 0);
                }
                RunBar.End(outcome);
            }
        }

        private static Style StyleOf(string key) => (Style)Application.Current.Resources[key];
    }
}
