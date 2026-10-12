using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IfcToolbox.App.Views
{
    /// <summary>IFC Optimizer: merges duplicate entities and optionally rounds coordinates.</summary>
    public sealed partial class OptimizerPage : Page
    {
        // Set once every control exists: toggles switched on in XAML report it while the page loads.
        private readonly bool _ready;

        public OptimizerPage()
        {
            InitializeComponent();
            _ready = true;
            ShowPrecision();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("optimizer");
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

        private void OnFilesChanged(object sender, EventArgs e)
        {
            OutputSection.Visibility = Visibility.Collapsed;
            UpdateState();
        }

        private void OnOptionChanged(object sender, RoutedEventArgs e) => ShowPrecision();

        private void OnPrecisionChanged(object sender, RangeBaseValueChangedEventArgs e) => ShowPrecision();

        private void ShowPrecision()
        {
            if (!_ready)
                return;
            MergeChoice.IsEnabled = MergeSwitch.IsOn;
            PrecisionPanel.Visibility = RoundSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
            // Point lists are only rewritten when rounding: without it there is nothing to change in them.
            PointListSwitch.IsEnabled = RoundSwitch.IsOn;
            int digits = (int)PrecisionSlider.Value;
            // In the model's length unit: 0.000001 m is a micrometre, 0.001 mm likewise.
            PrecisionText.Text = $"Coordinates are rounded to {digits} decimal places in the model's length unit " +
                                 $"({1 / Math.Pow(10, digits):0.############}). Fewer places save more but move geometry; " +
                                 "for a model in millimetres 3 or more is safe, in metres 6 or more.";
            UpdateState();
        }

        private void UpdateState()
        {
            if (!_ready || RunBar.IsBusy)
                return;
            int files = ModelZone.Files.Count;
            bool anything = MergeSwitch.IsOn || PointSwitch.IsOn || (RoundSwitch.IsOn && PointListSwitch.IsOn);
            RunBar.CanRun = files > 0 && anything;
            RunBar.Status = files == 0 ? "Add the IFC models to optimize."
                : !anything ? "Switch on at least one optimization."
                : RoundSwitch.IsOn ? "Rounding changes coordinates; the original files are kept."
                : "Lossless: only exact duplicates are merged. The original files are kept.";
        }

        private IConfigOptimize BuildConfig()
        {
            var config = ConfigFactory.CreateConfigOptimize();
            config.KeepLabel = KeepLabelSwitch.IsOn;
            config.MergeOpen = MergeSwitch.IsOn;
            config.MergeOnlyResources = MergeSwitch.IsOn && MergeChoice.SelectedIndex == 0;
            config.MergeReversedEntities = MergeSwitch.IsOn && MergeChoice.SelectedIndex == 1;
            config.PrecisionOpen = RoundSwitch.IsOn;
            config.Precision = RoundSwitch.IsOn ? (int)PrecisionSlider.Value : 15;
            config.OptimizePoint = PointSwitch.IsOn;
            config.OptimizePointList = RoundSwitch.IsOn && PointListSwitch.IsOn;
            return config;
        }

        private async void OnOptimize(object sender, EventArgs e)
        {
            var paths = ModelZone.Files.Select(f => f.Path).ToList();
            if (paths.Count == 0)
                return;
            var config = BuildConfig();

            var token = RunBar.Begin($"Optimizing {Path.GetFileName(paths[0])}…", cancellable: paths.Count > 1);
            var started = DateTime.Now;
            var written = new List<string>();
            var failures = new List<string>();
            long before = 0, after = 0;
            string outcome = string.Empty;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var path = paths[i];
                    if (paths.Count > 1)
                        RunBar.Status = $"Optimizing {i + 1} of {paths.Count}: {Path.GetFileName(path)}…";
                    try
                    {
                        var result = await Task.Run(() => OptimizerProcessor.Process(path, config));
                        var output = result.FilePaths.Single();
                        written.Add(output);
                        before += new FileInfo(path).Length;
                        after += new FileInfo(output).Length;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    }
                }
                outcome = $"Done in {(DateTime.Now - started).TotalSeconds:0.0} s.";
            }
            catch (OperationCanceledException)
            {
                outcome = "Stopped; the files optimized so far are listed.";
            }
            finally
            {
                if (failures.Count > 0)
                    RunBar.ShowError($"{failures.Count} of {paths.Count} files could not be optimized", string.Join(Environment.NewLine, failures));
                if (written.Count > 0)
                {
                    double saved = before == 0 ? 0 : 100.0 * (before - after) / before;
                    OutputView.Show("Optimization finished", written,
                        $"{ImportedFile.FormatSize(before)} → {ImportedFile.FormatSize(after)}, {saved:0.#}% smaller.");
                    OutputSection.Visibility = Visibility.Visible;
                    OutputSection.IsExpanded = true;
                    Ui.ScrollIntoView(OutputSection);
                }
                RunBar.End(outcome);
            }
        }
    }
}
