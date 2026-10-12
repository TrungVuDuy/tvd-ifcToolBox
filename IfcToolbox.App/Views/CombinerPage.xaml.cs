using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.Core.Convert;
using IfcToolbox.Core.Editors;
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
    /// <summary>IFC Combiner: several models into one, with one project.</summary>
    public sealed partial class CombinerPage : Page
    {
        public CombinerPage()
        {
            InitializeComponent();
            ModelZone.AllowReorder = true;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("combiner");
            Header.Title = tool.Title;
            Header.Glyph = tool.Glyph;
            Header.TileBrush = new SolidColorBrush(tool.Color);
            SummaryText.Text = tool.Summary;
            if (App.StartupImport != null)
            {
                ModelZone.Import(new[] { App.StartupImport }.Concat(App.StartupMoreImports));
                App.StartupImport = null;
                App.StartupMoreImports.Clear();
            }
            UpdateState();
        }

        private void OnFilesChanged(object sender, EventArgs e)
        {
            OutputSection.Visibility = Visibility.Collapsed;
            UpdateState();
        }

        /// <summary>
        /// Different schemas show straight away from the file headers. Different units need the
        /// models opened, so those are reported when combining.
        /// </summary>
        private void UpdateState()
        {
            if (RunBar.IsBusy)
                return;
            var schemas = ModelZone.Files
                .Select(f => (f.Name, Schema: IfcConvert.ReadFileSchema(f.Path) ?? "unknown"))
                .ToList();
            var distinct = schemas.Select(s => s.Schema).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            SchemaWarning.IsOpen = distinct.Count > 1;
            if (SchemaWarning.IsOpen)
                SchemaWarning.Message = "Only files of the same IFC schema can be combined: " +
                                        string.Join(", ", schemas.Select(s => $"{s.Name} is {s.Schema}")) + ".";

            int count = ModelZone.Files.Count;
            RunBar.CanRun = count >= 2 && !SchemaWarning.IsOpen;
            RunBar.Status = count == 0 ? "Add the IFC models to combine."
                : count == 1 ? "Add at least one more model."
                : SchemaWarning.IsOpen ? string.Empty
                : $"Ready to combine {count} models into {Path.GetFileNameWithoutExtension(ModelZone.Files[0].Name)}_Combined.ifc.";
        }

        private async void OnCombine(object sender, EventArgs e)
        {
            var paths = ModelZone.Files.Select(f => f.Path).ToList();
            if (paths.Count < 2)
                return;
            var output = Path.Combine(Path.GetDirectoryName(paths[0]) ?? ".", Path.GetFileNameWithoutExtension(paths[0]) + "_Combined.ifc");
            var options = new CombineOptions { MergeSpatialByName = ByNameSwitch.IsOn, SkipDuplicateElements = SkipSwitch.IsOn };

            var token = RunBar.Begin($"Combining {paths.Count} models…");
            var started = DateTime.Now;
            string outcome = string.Empty;
            try
            {
                var result = await Task.Run(() => ModelCombiner.Combine(paths, output, options, token), token);
                OutputView.Show("Combined", new[] { result.OutputPath },
                    $"{result.ElementsCopied} elements from {paths.Count} files in one model.");
                ShowReport(result);
                OutputSection.Visibility = Visibility.Visible;
                OutputSection.IsExpanded = true;
                Ui.ScrollIntoView(OutputSection);
                outcome = $"Done in {(DateTime.Now - started).TotalSeconds:0.0} s.";
            }
            catch (OperationCanceledException)
            {
                outcome = "Cancelled; nothing was written.";
            }
            catch (CombineException ex)
            {
                RunBar.ShowError("These files cannot be combined", ex.Message);
            }
            catch (Exception ex)
            {
                RunBar.ShowError("The models could not be combined", ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
            }
        }

        /// <summary>What was merged, kept apart, skipped and renewed, so the result can be checked.</summary>
        private void ShowReport(CombineResult result)
        {
            ReportPanel.Children.Clear();
            ReportPanel.Children.Add(new Border
            {
                Style = StyleOf("CardTitleStyle"),
                Child = new TextBlock { Text = "What happened", Style = StyleOf("SubtitleTextBlockStyle"), FontSize = 18 },
            });

            AddLine("Merged", result.SpatialMerged.Count == 0 ? "nothing: every site, building and storey was different" : Join(result.SpatialMerged));
            if (result.SpatialKeptApart.Count > 0)
                AddLine("Kept apart, placed differently", Join(result.SpatialKeptApart));
            if (result.ElementsSkipped > 0)
                AddLine("Copied once, found in an earlier file",
                    $"{result.ElementsSkipped}: " + string.Join(", ", result.SkippedByClass.Select(c => $"{c.Key} {c.Value}")));
            if (result.TypesMerged.Count > 0)
                AddLine("Types found in several files, now one", Join(result.TypesMerged));
            if (result.GlobalIdsRenewed > 0)
                AddLine("GlobalIds renewed, to keep each one unique",
                    $"{result.GlobalIdsRenewed}: " + string.Join(", ", result.RenewedByClass.Select(c => $"{c.Key} {c.Value}")));
            foreach (var warning in result.Warnings)
                AddLine("Warning", warning, critical: true);
        }

        private void AddLine(string label, string text, bool critical = false)
        {
            var block = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            block.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = label + ": ", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            block.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = text });
            if (critical)
                block.Style = StyleOf("CriticalTextStyle");
            ReportPanel.Children.Add(block);
        }

        /// <summary>Short lists in full; long ones (many rooms) summarised by class.</summary>
        private static string Join(IList<string> items) =>
            items.Count <= 12
                ? string.Join(", ", items)
                : string.Join(", ", items.GroupBy(i => i.Split(' ')[0]).Select(g => $"{g.Count()} × {g.Key}"));

        private static Style StyleOf(string key) => (Style)Application.Current.Resources[key];
    }
}
