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
            RunBar.ActionText = tool.Action;
            ShowModes();

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
            PickHint.Text = Loc.Format("Common_Reading", Path.GetFileName(path));
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
                RunBar.ShowError(Loc.Get("Common_ModelUnreadable"), ex.Message);
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
            Enable(ByObjectType, ByObjectTypeHint, a == null || a.ObjectTypes > 0, Loc.Get("Splitter_ByObjectTypeHint"), Loc.Get("Splitter_NoObjectTypes"));
            Enable(BySite, BySiteHint, a == null || a.Sites > 0, Count(a?.Sites, "Splitter_Sites", "Splitter_BySiteHint"), Loc.Get("Splitter_NoSites"));
            Enable(ByBuilding, ByBuildingHint, a == null || a.Buildings > 0, Count(a?.Buildings, "Splitter_Buildings", "Splitter_ByBuildingHint"), Loc.Get("Splitter_NoBuildings"));
            Enable(ByBuildingStorey, ByBuildingStoreyHint, a == null || a.Storeys > 0, Count(a?.Storeys, "Splitter_Levels", "Splitter_ByBuildingStoreyHint"), Loc.Get("Splitter_NoLevels"));
            Enable(ByFacility, ByFacilityHint, a == null || a.Facilities > 0, Count(a?.Facilities, "Splitter_Facilities", "Splitter_ByFacilityHint"), Loc.Get("Splitter_NoFacilities"));
            Enable(ByFacilityPart, ByFacilityPartHint, a == null || a.FacilityParts.Count > 0, Count(a?.FacilityParts.Count, "Splitter_FacilityParts", "Splitter_ByFacilityPartHint"), Loc.Get("Splitter_NoFacilityParts"));

            // The mode in use may just have become unavailable.
            if (ModeTiles.First(t => t.IsChecked == true).IsEnabled == false)
                ByProduct.IsChecked = true;
        }

        private static void Enable(RadioButton tile, TextBlock hint, bool enabled, string text, string reason)
        {
            tile.IsEnabled = enabled;
            hint.Text = enabled ? text : reason;
        }

        /// <summary>"3 sites in this model" from the counted resource <paramref name="key"/>, or the <paramref name="fallback"/> resource before a model is read.</summary>
        private static string Count(int? count, string key, string fallback) =>
            count == null ? Loc.Get(fallback) : Loc.Count(key, count.Value);

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
                    PickHint.Text = Loc.Get("Splitter_NeedModelToPick");
                UpdateState();
                return;
            }

            var trees = _analysis.Trees;
            PickTree.Visibility = strategy == SplitStrategy.DataOnly ? Visibility.Collapsed : Visibility.Visible;
            Func<HierarchyNode, string> leaves = n => n.IsComposition ? null : n.Id;

            switch (strategy)
            {
                case SplitStrategy.ByProduct:
                    PickHint.Text = Loc.Get("Splitter_PickProducts");
                    // Open the spatial structure, keep the per class product lists closed.
                    _selection.Show(trees.FullNodes, leaves, Label, (n, depth) => n.IsComposition && !n.IsTypeGroup && depth < 6);
                    break;
                case SplitStrategy.ByObjectType:
                    PickHint.Text = Loc.Get("Splitter_PickObjectTypes");
                    _selection.Show(trees.TypedNodes, leaves, Label, (_, _) => false);
                    break;
                case SplitStrategy.DataOnly:
                    _selection.Clear();
                    PickHint.Text = Loc.Get("Splitter_PickDataOnly");
                    break;
                case SplitStrategy.BySite:
                    PickHint.Text = Loc.Get("Splitter_PickSites");
                    _selection.Show(trees.SiteNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByBuilding:
                    PickHint.Text = Loc.Get("Splitter_PickBuildings");
                    _selection.Show(trees.BuildingNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByBuildingStorey:
                    PickHint.Text = Loc.Get("Splitter_PickLevels");
                    _selection.Show(trees.LevelNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByFacility:
                    PickHint.Text = Loc.Get("Splitter_PickFacilities");
                    _selection.Show(trees.FacilityNodes, leaves, Label, (n, _) => n.IsComposition);
                    break;
                case SplitStrategy.ByFacilityPart:
                    PickHint.Text = Loc.Get("Splitter_PickFacilityParts");
                    _selection.Show(trees.FacilityPartNodes, n => _analysis.FacilityParts.ContainsKey(n.Id ?? "") ? n.Id : null,
                        PartLabel, (_, _) => true);
                    break;
            }
            UpdateState();
        }

        /// <summary>"Ground Floor · IfcBuildingStorey", "IfcWall · 3 entities".</summary>
        private static string Label(HierarchyNode node) => Ui.NodeLabel(node);

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
            RunBar.Status = !hasModel ? Loc.Get("Splitter_NeedModel")
                : _analysis == null ? string.Empty
                : !needsPick ? Loc.Get("Splitter_ReadyDataOnly")
                : picked == 0 ? Loc.Get("Splitter_NeedPick")
                : Loc.Count("Splitter_Picked", picked);
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
            RunBar.Begin(Loc.Format("Splitter_Running", Path.GetFileName(model)), cancellable: false);
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
                string message = written.Count == 0 ? Loc.Get("Splitter_NothingToWrite")
                    : empty > 0 ? Loc.Count("Output_FilesWritten", written.Count) + " " + Loc.Count("Splitter_EmptyPicks", empty)
                    : null;
                OutputView.Show(Loc.Get(written.Count > 0 ? "Splitter_Finished" : "Common_NothingWritten"), written, message);
                OutputSection.Visibility = Visibility.Visible;
                OutputSection.IsExpanded = true;
                Ui.ScrollIntoView(OutputSection, 0);
                outcome = Loc.Format("Common_Done", (DateTime.Now - started).TotalSeconds);
            }
            catch (Exception ex)
            {
                RunBar.ShowError(Loc.Get("Splitter_Failed"), ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
            }
        }
    }
}
