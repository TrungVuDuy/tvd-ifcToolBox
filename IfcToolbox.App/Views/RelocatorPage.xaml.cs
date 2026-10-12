using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.Core.Geo;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IfcToolbox.App.Views
{
    /// <summary>
    /// IFC Relocator: moves the world and project coordinate systems, to another model's or to
    /// typed values, or georeferences the model with a projected CRS.
    /// </summary>
    public sealed partial class RelocatorPage : Page
    {
        private RelocateAnalysis _model;
        private RelocateAnalysis _reference;
        private string _modelPath, _referencePath;
        private int _modelRun, _referenceRun;

        // Set once every control exists: boxes ticked in XAML report it while the page loads.
        private readonly bool _ready;

        public RelocatorPage()
        {
            InitializeComponent();
            _ready = true;
            foreach (var box in new[] { WorldX, WorldY, WorldZ, WorldRotation, ProjectX, ProjectY, ProjectZ, ProjectRotation, HeightBox, MapRotationBox, ScaleBox, FactorBox })
                box.ValueChanged += (_, _) => UpdateState();
            OnTargetChanged(null, null);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("relocator");
            Header.Title = tool.Title;
            Header.Glyph = tool.Glyph;
            Header.TileBrush = new SolidColorBrush(tool.Color);
            SummaryText.Text = tool.Summary;

            if (App.StartupImport != null)
            {
                ModelZone.Import(new[] { App.StartupImport });
                App.StartupImport = null;
            }
            if (App.StartupReference != null)
            {
                ReferenceZone.Import(new[] { App.StartupReference });
                App.StartupReference = null;
            }
            UpdateState();
        }

        #region Reading models

        private async void OnModelChanged(object sender, EventArgs e)
        {
            OutputSection.Visibility = Visibility.Collapsed;
            var path = ModelZone.Files.FirstOrDefault()?.Path;
            if (path == _modelPath)
                return;
            _modelPath = path;
            _model = null;
            CurrentPanel.Visibility = Visibility.Collapsed;
            UpdateState();
            if (path == null)
                return;

            int run = ++_modelRun;
            AnalysisProgress.IsActive = true;
            try
            {
                var analysis = await Task.Run(() => RelocateAnalysis.Read(path));
                if (run != _modelRun)
                    return;
                _model = analysis;
                ShowPlacements(CurrentPanel, analysis, "This model");
                FillInputs(analysis);
            }
            catch (Exception ex)
            {
                if (run == _modelRun)
                    RunBar.ShowError("The model could not be read", ex.Message);
            }
            finally
            {
                if (run == _modelRun)
                    AnalysisProgress.IsActive = false;
                UpdateState();
            }
        }

        private async void OnReferenceChanged(object sender, EventArgs e)
        {
            var path = ReferenceZone.Files.FirstOrDefault()?.Path;
            if (path == _referencePath)
                return;
            _referencePath = path;
            _reference = null;
            ReferencePanel.Visibility = Visibility.Collapsed;
            UpdateState();
            if (path == null)
                return;

            int run = ++_referenceRun;
            ReferenceProgress.IsActive = true;
            try
            {
                var analysis = await Task.Run(() => RelocateAnalysis.Read(path));
                if (run != _referenceRun)
                    return;
                _reference = analysis;
                ShowPlacements(ReferencePanel, analysis, "Model to align to");
            }
            catch (Exception ex)
            {
                if (run == _referenceRun)
                    RunBar.ShowError("The model to align to could not be read", ex.Message);
            }
            finally
            {
                if (run == _referenceRun)
                    ReferenceProgress.IsActive = false;
                UpdateState();
            }
        }

        #endregion

        #region Showing placements

        /// <summary>Cards with the world and project coordinate systems and the georeferencing, like the original app.</summary>
        private static void ShowPlacements(Grid panel, RelocateAnalysis a, string whose)
        {
            panel.Children.Clear();
            panel.ColumnDefinitions.Clear();
            panel.RowDefinitions.Clear();
            panel.ColumnDefinitions.Add(new ColumnDefinition());
            panel.ColumnDefinitions.Add(new ColumnDefinition());
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var unit = $"in {a.UnitName}";
            var world = Card($"World coordinates location", $"#{a.ProjectLabel} IfcProject", whose);
            if (a.World == null)
                AddRow(world, "No 3D model context", string.Empty);
            else
            {
                AddRow(world, "Placement X, Y, Z", Format(a.World.LocationXYZ), unit);
                AddRow(world, "RefDirection", Format(a.World.RotationX), "rotation of axis X");
                AddRow(world, "Axis", Format(a.World.RotationZ), "rotation of axis Z");
                AddRow(world, "True north", a.TrueNorth == null ? "not given" : Format(new[] { a.TrueNorth.Value.X, a.TrueNorth.Value.Y }));
            }
            panel.Children.Add(world.Card);

            var site = a.Sites.FirstOrDefault();
            var project = Card("Project coordinates location", site.Label == 0 ? "no IfcSite" : $"#{site.Label} IfcSite", whose);
            if (site.Label == 0)
                AddRow(project, "No site directly under the project", string.Empty);
            else
            {
                AddRow(project, "Placement X, Y, Z", Format(site.Placement.LocationXYZ), unit);
                AddRow(project, "RefDirection", Format(site.Placement.RotationX), "rotation of axis X");
                AddRow(project, "Axis", Format(site.Placement.RotationZ), "rotation of axis Z");
                if (a.Sites.Count > 1)
                    AddRow(project, $"{a.Sites.Count} sites", "all are moved alike");
            }
            Grid.SetColumn(project.Card, 1);
            panel.Children.Add(project.Card);

            var map = Card("Georeferencing", a.Schema, whose);
            if (a.MapConversion == null)
                AddRow(map, a.IsIfc2x3 ? "IFC2X3 has no IfcMapConversion" : "Not georeferenced", "no IfcMapConversion");
            else
            {
                var p = a.MapConversion.Parameters;
                AddRow(map, "Projected CRS", a.MapConversion.TargetCrs?.Name ?? "unnamed");
                AddRow(map, "Eastings, Northings, Height", Format(new[] { p.Eastings ?? 0, p.Northings ?? 0, p.OrthogonalHeight ?? 0 }));
                AddRow(map, "Rotation", $"{MapConversionService.RotationDegrees(p).ToString("0.######", CultureInfo.InvariantCulture)}°", "of the x axis from east");
                AddRow(map, "Scale", (p.Scale ?? 1).ToString("G6", CultureInfo.InvariantCulture), p.FactorX.HasValue ? $"factors {Format(new[] { p.FactorX ?? 1, p.FactorY ?? 1, p.FactorZ ?? 1 })}" : null);
            }
            Grid.SetRow(map.Card, 1);
            Grid.SetColumnSpan(map.Card, 2);
            panel.Children.Add(map.Card);
            panel.Visibility = Visibility.Visible;
        }

        private static (Border Card, StackPanel Rows) Card(string title, string tag, string whose)
        {
            var rows = new StackPanel { Spacing = 6 };
            var head = new Grid();
            var titleBorder = new Border { Style = StyleOf("CardTitleStyle"), Child = new TextBlock { Text = title, Style = StyleOf("SubtitleTextBlockStyle"), FontSize = 18 } };
            head.Children.Add(titleBorder);
            head.Children.Add(new TextBlock { Text = tag, Style = StyleOf("SecondaryCaptionStyle"), HorizontalAlignment = HorizontalAlignment.Right });
            rows.Children.Add(head);
            var card = new Border { Style = StyleOf("CardStyle"), Child = rows };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, $"{whose}: {title}");
            return (card, rows);
        }

        private static void AddRow((Border Card, StackPanel Rows) card, string label, string value, string note = null)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new TextBlock();
            left.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = label });
            if (!string.IsNullOrEmpty(note))
                left.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "   " + note, FontSize = 12 });
            row.Children.Add(left);
            var right = new TextBlock { Text = value, HorizontalAlignment = HorizontalAlignment.Right, IsTextSelectionEnabled = true };
            Grid.SetColumn(right, 1);
            row.Children.Add(right);
            card.Rows.Children.Add(row);
        }

        private static string Format(IEnumerable<double> values) =>
            values == null ? "-" : string.Join(", ", values.Select(v => v.ToString("0.######", CultureInfo.InvariantCulture)));

        private static double Angle(IList<double> refDirection) =>
            refDirection == null || refDirection.Count < 2 ? 0 : Math.Round(Math.Atan2(refDirection[1], refDirection[0]) * 180 / Math.PI, 9);

        #endregion

        #region Target

        /// <summary>Starts the typed values and the georeferencing from where the model is now.</summary>
        private void FillInputs(RelocateAnalysis a)
        {
            var world = a.World?.LocationXYZ;
            WorldX.Value = world?[0] ?? 0;
            WorldY.Value = world?[1] ?? 0;
            WorldZ.Value = world?[2] ?? 0;
            WorldRotation.Value = Angle(a.World?.RotationX);
            WorldCheck.IsEnabled = a.World != null;

            var site = a.Sites.FirstOrDefault().Placement;
            ProjectX.Value = site?.LocationXYZ[0] ?? 0;
            ProjectY.Value = site?.LocationXYZ[1] ?? 0;
            ProjectZ.Value = site?.LocationXYZ[2] ?? 0;
            ProjectRotation.Value = Angle(site?.RotationX);
            ProjectCheck.IsEnabled = site != null;
            if (site == null)
                ProjectCheck.IsChecked = false;

            GeorefMode.IsEnabled = !a.IsIfc2x3;
            GeorefHint.Text = a.IsIfc2x3 ? "IFC4 and IFC 4.3 models only" : "Projected CRS and map conversion";
            if (a.IsIfc2x3 && GeorefMode.IsChecked == true)
                AlignMode.IsChecked = true;
            FactorBox.IsEnabled = a.IsIfc4x3;

            var map = a.MapConversion;
            CrsBox.Text = map?.TargetCrs?.Name ?? string.Empty;
            EastingsBox.Value = map?.Parameters.Eastings ?? double.NaN;
            NorthingsBox.Value = map?.Parameters.Northings ?? double.NaN;
            HeightBox.Value = map?.Parameters.OrthogonalHeight ?? 0;
            MapRotationBox.Value = map == null ? 0 : Math.Round(MapConversionService.RotationDegrees(map.Parameters), 9);
            ScaleBox.Value = map?.Parameters.Scale ?? a.UnitInMetres;
            FactorBox.Value = map?.Parameters.FactorX ?? double.NaN;
            UnitHint.Text = $"The model measures lengths in {a.UnitName}.";
            ScaleHint.Text = $"The model is in {a.UnitName}, so a CRS in metres needs a scale of {a.UnitInMetres.ToString("G6", CultureInfo.InvariantCulture)}. " +
                             "Eastings, northings and height are in the CRS unit.";
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready)
                return;
            AlignPanel.Visibility = AlignMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ManualPanel.Visibility = ManualMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            GeorefPanel.Visibility = GeorefMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UnitPanel.Visibility = UnitMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            RunBar.ActionText = GeorefMode.IsChecked == true ? "Georeference IFC" : UnitMode.IsChecked == true ? "Change unit" : "Relocate IFC";
            UpdateState();
        }

        private void OnTargetChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready)
                return;
            WorldInputs.Opacity = WorldCheck.IsChecked == true ? 1 : 0.5;
            WorldRotation.IsEnabled = WorldX.IsEnabled = WorldY.IsEnabled = WorldZ.IsEnabled = WorldCheck.IsChecked == true;
            ProjectRotation.IsEnabled = ProjectX.IsEnabled = ProjectY.IsEnabled = ProjectZ.IsEnabled = ProjectCheck.IsChecked == true;
            UpdateState();
        }

        private void OnCrsChanged(object sender, TextChangedEventArgs e) => UpdateState();

        private void OnUnitChanged(object sender, SelectionChangedEventArgs e) => UpdateState();

        /// <summary>The unit picked under "Change length unit": its name for the converter, and its size in metres.</summary>
        private (string Name, double Metres) ChosenUnit
        {
            get
            {
                var name = UnitChoice.SelectedIndex switch { 0 => "mm", 1 => "cm", _ => "m" };
                return (name, Core.Editors.LengthUnitConverter.Units[name].Metres);
            }
        }

        /// <summary>Writes a copy of the model with every length in the chosen unit, and shows where it sits.</summary>
        private async Task ChangeUnitAsync(string path)
        {
            var (unit, _) = ChosenUnit;
            var output = Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_" + unit + ".ifc");

            RunBar.Begin($"Writing {Path.GetFileName(output)}…", cancellable: false);
            var started = DateTime.Now;
            string outcome = string.Empty;
            try
            {
                var conversion = await Task.Run(() =>
                {
                    using var model = Xbim.Ifc.IfcStore.Open(path);
                    Core.Editors.LengthUnitConversion result;
                    using (var txn = model.BeginTransaction("Convert length unit"))
                    {
                        result = Core.Editors.LengthUnitConverter.Convert(model, unit);
                        txn.Commit();
                    }
                    model.SaveAs(output);
                    return result;
                });
                OutputView.Show("Done", new[] { output },
                    $"{conversion.ValuesScaled:N0} lengths rescaled" + (conversion.ValuesWithOwnUnit > 0 ? $"; {conversion.ValuesWithOwnUnit} with a unit of their own left as they are." : "."));
                OutputSection.Visibility = Visibility.Visible;
                OutputSection.IsExpanded = true;
                var after = await Task.Run(() => RelocateAnalysis.Read(output));
                ShowPlacements(ResultPanel, after, "Result");
                Ui.ScrollIntoView(OutputSection);
                outcome = $"Done in {(DateTime.Now - started).TotalSeconds:0.0} s.";
            }
            catch (Exception ex)
            {
                RunBar.ShowError("The unit could not be changed", ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
            }
        }

        private void OnGeorefValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateState();

        /// <summary>What is missing for the chosen mode, or null when it can run.</summary>
        private string Missing()
        {
            if (ModelZone.Files.Count == 0)
                return "Add the IFC model to relocate.";
            if (_model == null)
                return string.Empty;
            if (UnitMode.IsChecked == true)
            {
                var (_, metres) = ChosenUnit;
                return Math.Abs(metres - _model.UnitInMetres) < metres * 1e-9 ? $"The model is already in {_model.UnitName}." : null;
            }
            if (AlignMode.IsChecked == true)
            {
                if (ReferenceZone.Files.Count == 0)
                    return "Add the model to align to.";
                if (_reference == null)
                    return string.Empty;
                bool world = AlignWorldCheck.IsChecked == true && _reference.World != null && _model.World != null;
                bool project = AlignProjectCheck.IsChecked == true && _reference.Sites.Count > 0 && _model.Sites.Count > 0;
                return world || project ? null : "Nothing to take: tick a coordinate system that both models have.";
            }
            if (ManualMode.IsChecked == true)
            {
                if (WorldCheck.IsChecked != true && ProjectCheck.IsChecked != true)
                    return "Tick the coordinate system to change.";
                var boxes = new List<NumberBox>();
                if (WorldCheck.IsChecked == true) boxes.AddRange(new[] { WorldX, WorldY, WorldZ, WorldRotation });
                if (ProjectCheck.IsChecked == true) boxes.AddRange(new[] { ProjectX, ProjectY, ProjectZ, ProjectRotation });
                return boxes.Any(b => double.IsNaN(b.Value)) ? "Fill in every coordinate and rotation." : null;
            }
            if (string.IsNullOrWhiteSpace(CrsBox.Text))
                return "Name the projected CRS, normally its EPSG code.";
            if (double.IsNaN(EastingsBox.Value) || double.IsNaN(NorthingsBox.Value))
                return "Fill in the eastings and northings of the origin.";
            if (double.IsNaN(ScaleBox.Value) || ScaleBox.Value <= 0)
                return "The scale has to be greater than 0.";
            return null;
        }

        private void UpdateState()
        {
            if (!_ready || RunBar.IsBusy)
                return;
            var missing = Missing();
            RunBar.CanRun = missing == null;
            RunBar.Status = missing ?? (GeorefMode.IsChecked == true
                ? "Writes a new, georeferenced file; the geometry is not moved."
                : UnitMode.IsChecked == true
                    ? "Writes a new file with every length in the chosen unit; the original is kept."
                    : "Writes a new file with the moved coordinate systems; the original is kept.");
        }

        private static IGeoPlacement Placement(IGeoPlacement from, int siteLabel) =>
            GeoFactory.CreateGeoPlacement(from.LocationXYZ.ToList(), from.RotationX.ToList(), from.RotationZ.ToList(), siteLabel);

        /// <summary>A placement at a point, turned about Z; z stays up.</summary>
        private static IGeoPlacement Placement(double x, double y, double z, double degrees, int label = 0)
        {
            double radians = degrees * Math.PI / 180;
            return GeoFactory.CreateGeoPlacement(new List<double> { x, y, z },
                new List<double> { Math.Round(Math.Cos(radians), 12), Math.Round(Math.Sin(radians), 12), 0 },
                new List<double> { 0, 0, 1 }, label);
        }

        private ConfigRelocate BuildConfig()
        {
            var config = (ConfigRelocate)ConfigFactory.CreateConfigRelocate();
            if (AlignMode.IsChecked == true)
            {
                config.AlignWorldCoordinates = AlignWorldCheck.IsChecked == true && _reference.World != null;
                config.WorldPlacement = _reference.World;
                // The reference's first site placement, applied to each site of this model.
                config.AlignProjectCoordinates = AlignProjectCheck.IsChecked == true && _reference.Sites.Count > 0;
                if (config.AlignProjectCoordinates)
                    config.ProjectPlacements = _model.Sites.Select(s => Placement(_reference.Sites[0].Placement, s.Label)).ToList();
                config.Suffix = "Aligned";
            }
            else if (ManualMode.IsChecked == true)
            {
                config.AlignWorldCoordinates = WorldCheck.IsChecked == true;
                config.WorldPlacement = Placement(WorldX.Value, WorldY.Value, WorldZ.Value, WorldRotation.Value);
                config.AlignProjectCoordinates = ProjectCheck.IsChecked == true;
                config.ProjectPlacements = _model.Sites
                    .Select(s => Placement(ProjectX.Value, ProjectY.Value, ProjectZ.Value, ProjectRotation.Value, s.Label)).ToList();
                config.Suffix = "Moved";
            }
            else
            {
                double radians = MapRotationBox.Value * Math.PI / 180;
                double? factor = double.IsNaN(FactorBox.Value) || !_model.IsIfc4x3 ? null : FactorBox.Value;
                config.MapConversion = new GeoMapConversion
                {
                    TargetCrs = new ProjectedCrs { Name = CrsBox.Text.Trim() },
                    Parameters = new MapConversionParameters
                    {
                        Eastings = EastingsBox.Value,
                        Northings = NorthingsBox.Value,
                        OrthogonalHeight = double.IsNaN(HeightBox.Value) ? 0 : HeightBox.Value,
                        XAxisAbscissa = Math.Round(Math.Cos(radians), 12),
                        XAxisOrdinate = Math.Round(Math.Sin(radians), 12),
                        Scale = ScaleBox.Value,
                        FactorX = factor,
                        FactorY = factor,
                    },
                };
                config.Suffix = "Georeferenced";
            }
            return config;
        }

        private async void OnRelocate(object sender, EventArgs e)
        {
            var path = ModelZone.Files.FirstOrDefault()?.Path;
            if (path == null || _model == null)
                return;
            if (UnitMode.IsChecked == true)
            {
                await ChangeUnitAsync(path);
                return;
            }
            var config = BuildConfig();

            RunBar.Begin($"Writing {Path.GetFileNameWithoutExtension(path)}_{config.Suffix}.ifc…", cancellable: false);
            var started = DateTime.Now;
            string outcome = string.Empty;
            try
            {
                var result = await Task.Run(() => RelocatorProcessor.Process(path, config));
                var output = result.FilePaths.Single();
                OutputView.Show("Done", new[] { output });
                OutputSection.Visibility = Visibility.Visible;
                OutputSection.IsExpanded = true;
                // Read the result back, so the user sees where the model ended up.
                var after = await Task.Run(() => RelocateAnalysis.Read(output));
                ShowPlacements(ResultPanel, after, "Result");
                Ui.ScrollIntoView(OutputSection);
                outcome = $"Done in {(DateTime.Now - started).TotalSeconds:0.0} s.";
            }
            catch (Exception ex)
            {
                RunBar.ShowError("The model could not be relocated", ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
            }
        }

        #endregion

        private static Style StyleOf(string key) => (Style)Application.Current.Resources[key];
    }
}
