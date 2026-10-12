using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.Core.Hierarchy;
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
    /// <summary>
    /// IFC Splitter: writes part of a model to new files, by picked products, object type,
    /// site, building, storey, or the IFC 4.3 facilities and facility parts.
    /// </summary>
    public sealed partial class SplitterPage : Page
    {
        private readonly HierarchySelection _selection;
        private SplitAnalysis _analysis;
        private string _analysedPath;
        private int _analysisRun;

        public SplitterPage()
        {
            InitializeComponent();
            _selection = new HierarchySelection(PickTree);
            _selection.SelectionChanged += (_, _) => UpdateState();
        }

        private SplitStrategy Strategy =>
            Enum.Parse<SplitStrategy>((string)ModeTiles.First(t => t.IsChecked == true).Tag);

        private IEnumerable<RadioButton> ModeTiles => ModeGrid.Children.OfType<RadioButton>();

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("splitter");
            Header.Title = tool.Title;
            Header.Glyph = tool.Glyph;
            Header.TileBrush = new SolidColorBrush(tool.Color);
            SummaryText.Text = tool.Summary;

            if (App.StartupImport != null)
            {
                ModelZone.Import(new[] { App.StartupImport });
                App.StartupImport = null;
            }
            UpdateState();
        }

        private async void OnModelChanged(object sender, EventArgs e)
        {
            OutputSection.Visibility = Visibility.Collapsed;
            var path = ModelZone.Files.FirstOrDefault()?.Path;
            if (path == _analysedPath)
                return;

            _analysis = null;
            _analysedPath = path;
            _selection.Clear();
            UpdateState();
            if (path == null)
                return;

            // A newer file may arrive while this one is read; only the latest one counts.
            int run = ++_analysisRun;
            AnalysisProgress.IsActive = true;
            PickHint.Text = $"Reading {Path.GetFileName(path)}…";
            try
            {
                var analysis = await Task.Run(() => SplitAnalysis.Read(path));
                if (run != _analysisRun)
                    return;
                _analysis = analysis;
                RunBar.ClearError();
            }
            catch (Exception ex)
            {
                if (run != _analysisRun)
                    return;
                RunBar.ShowError("The model could not be read", ex.Message);
            }
            finally
            {
                if (run == _analysisRun)
                    AnalysisProgress.IsActive = false;
            }
            ShowModes();
            ShowPicker();
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            // Raised while the page is being built, for the tile checked in XAML.
            if (PickTree == null)
                return;
            ShowPicker();
        }

        /// <summary>Greys out the modes this model has nothing for, and says why.</summary>
        private void ShowModes()
        {
            var a = _analysis;
            Enable(ByObjectType, ByObjectTypeHint, a == null || a.ObjectTypes > 0, "Products by object type", "No object types in this model");
            Enable(BySite, BySiteHint, a == null || a.Sites > 0, Count(a?.Sites, "site", "sites", "Spatial element: IfcSite"), "No IfcSite in this model");
            Enable(ByBuilding, ByBuildingHint, a == null || a.Buildings > 0, Count(a?.Buildings, "building", "buildings", "Spatial element: IfcBuilding"), "No IfcBuilding in this model");
            Enable(ByBuildingStorey, ByBuildingStoreyHint, a == null || a.Storeys > 0, Count(a?.Storeys, "level", "levels", "Spatial element: IfcBuildingStorey"), "No IfcBuildingStorey in this model");
            Enable(ByFacility, ByFacilityHint, a == null || a.Facilities > 0, Count(a?.Facilities, "facility", "facilities", "IFC 4.3: road, railway, bridge, marine"), "IFC 4.3 models with facilities");
            Enable(ByFacilityPart, ByFacilityPartHint, a == null || a.FacilityParts.Count > 0, Count(a?.FacilityParts.Count, "facility part", "facility parts", "IFC 4.3: carriageway, track, deck…"), "IFC 4.3 models with facility parts");

            // The mode in use may just have become unavailable.
            if (ModeTiles.First(t => t.IsChecked == true).IsEnabled == false)
                ByProduct.IsChecked = true;
        }

        private static void Enable(RadioButton tile, TextBlock hint, bool enabled, string text, string reason)
        {
            tile.IsEnabled = enabled;
            hint.Text = enabled ? text : reason;
        }

        private static string Count(int? count, string noun, string plural, string fallback) =>
            count == null ? fallback : count == 1 ? $"1 {noun} in this model" : $"{count} {plural} in this model";

        /// <summary>Fills the tree with what the current mode can extract.</summary>
        private void ShowPicker()
        {
            var strategy = Strategy;
            OutputSection.Visibility = Visibility.Collapsed;
            if (_analysis == null)
            {
                _selection.Clear();
                PickTree.Visibility = Visibility.Collapsed;
                if (!AnalysisProgress.IsActive)
                    PickHint.Text = "Add a model to see what it contains.";
                UpdateState();
                return;
            }

            var trees = _analysis.Trees;
            PickTree.Visibility = strategy == SplitStrategy.DataOnly ? Visibility.Collapsed : Visibility.Visible;
            Func<HierarchyNode, string> leaves = n => n.IsComposition ? null : n.Id;

            switch (strategy)
            {
                case SplitStrategy.ByProduct:
                    PickHint.Text = "Tick the products to extract, or a whole storey or class. The spatial structure above them comes along.";
                    // Open the spatial structure, keep the per class product lists closed.
                    _selection.Show(trees.FullNodes, leaves, Label, (n, depth) => n.IsComposition && !n.IsTypeGroup && depth < 6);
                    break;
                case SplitStrategy.ByObjectType:
                    PickHint.Text = "Tick the object types to extract; every product of that type goes into the file.";
                    _selection.Show(trees.TypedNodes, leaves, Label, (_, _) => false);
                    break;
                case SplitStrategy.DataOnly:
                    _selection.Clear();
                    PickHint.Text = "Writes the whole model without its geometry: properties, relationships and structure only. Nothing to pick.";
                    break;
                case SplitStrategy.BySite:
                    PickHint.Text = "Tick the sites to extract. Each one becomes a file of its own.";
                    _selection.Show(trees.SiteNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByBuilding:
                    PickHint.Text = "Tick the buildings to extract. Each one becomes a file of its own.";
                    _selection.Show(trees.BuildingNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByBuildingStorey:
                    PickHint.Text = "Tick the levels to extract. Each one becomes a file of its own.";
                    _selection.Show(trees.LevelNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByFacility:
                    PickHint.Text = "Tick the facilities to extract: a road, a railway, a bridge… Each one becomes a file of its own.";
                    _selection.Show(trees.FacilityNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByFacilityPart:
                    PickHint.Text = "Tick the facility parts to extract, such as a carriageway or a track. Each one becomes a file of its own.";
                    _selection.Show(trees.FacilityPartNodes, n => _analysis.FacilityParts.ContainsKey(n.Id ?? "") ? n.Id : null,
                        PartLabel, (_, _) => true);
                    break;
            }
            UpdateState();
        }

        private static string Files(int count) => count == 1 ? "1 file" : $"{count} files";

        /// <summary>"Ground Floor · IfcBuildingStorey", "IfcWall · 3 entities".</summary>
        private static string Label(HierarchyNode node) =>
            string.IsNullOrEmpty(node.Description) ? node.Name : $"{node.Name}  ·  {node.Description}";

        private string PartLabel(HierarchyNode node) =>
            _analysis.FacilityParts.TryGetValue(node.Id ?? "", out var usage) && !string.IsNullOrEmpty(usage)
                ? $"{Label(node)}  [{usage}]"
                : Label(node);

        private void UpdateState()
        {
            if (RunBar.IsBusy)
                return;
            bool hasModel = ModelZone.Files.Count > 0;
            bool needsPick = Strategy != SplitStrategy.DataOnly;
            int picked = needsPick && _analysis != null ? _selection.SelectedValues().Count : 0;

            RunBar.CanRun = hasModel && _analysis != null && (!needsPick || picked > 0);
            RunBar.Status = !hasModel ? "Add the IFC model to split."
                : _analysis == null ? string.Empty
                : !needsPick ? "Ready to write the model without geometry."
                : picked == 0 ? "Tick at least one item to extract."
                : picked == 1 ? "1 item picked." : $"{picked} items picked.";
        }

        private async void OnSplit(object sender, EventArgs e)
        {
            var model = ModelZone.Files.FirstOrDefault()?.Path;
            if (model == null)
                return;

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = Strategy;
            config.SelectedItems = config.SplitStrategy == SplitStrategy.DataOnly ? null : _selection.SelectedValues();
            config.Suffix = config.SplitStrategy.ToString();

            // Splitting cannot be interrupted half way through a file.
            RunBar.Begin($"Splitting {Path.GetFileName(model)}…", cancellable: false);
            var started = DateTime.Now;
            string outcome = string.Empty;
            try
            {
                var result = await Task.Run(() => SplitterProcessor.Process(model, config));
                // Files written before this run started may share a name; list only fresh ones.
                var written = result.FilePaths.Where(p => File.Exists(p) && File.GetLastWriteTime(p) >= started.AddSeconds(-2)).ToList();
                // One file per pick for these; a pick that contains no element writes nothing.
                bool filePerPick = config.SplitStrategy is SplitStrategy.BySite or SplitStrategy.ByBuilding
                    or SplitStrategy.ByBuildingStorey or SplitStrategy.ByFacility or SplitStrategy.ByFacilityPart;
                int empty = filePerPick ? config.SelectedItems.Count() - written.Count : 0;
                string message = written.Count == 0 ? "What you picked contains no elements, so there was nothing to write."
                    : empty == 1 ? $"{Files(written.Count)} written. 1 pick contains no elements and was skipped."
                    : empty > 1 ? $"{Files(written.Count)} written. {empty} picks contain no elements and were skipped."
                    : null;
                OutputView.Show(written.Count > 0 ? "Split finished" : "Nothing was written", written, message);
                OutputSection.Visibility = Visibility.Visible;
                OutputSection.IsExpanded = true;
                Ui.ScrollIntoView(OutputSection, 0);
                outcome = $"Done in {(DateTime.Now - started).TotalSeconds:0.0} s.";
            }
            catch (Exception ex)
            {
                RunBar.ShowError("The model could not be split", ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
            }
        }
    }
}
