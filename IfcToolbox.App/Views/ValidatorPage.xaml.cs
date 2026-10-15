using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.Core.Bsdd;
using IfcToolbox.Core.Bsdd.Models;
using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Tools.Processors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IfcToolbox.App.Views
{
    /// <summary>
    /// IFC Validator: checks a model against IDS files, or against classes and properties picked
    /// from bSDD. Both end up as IDS and go through the same engine and the same results view.
    /// </summary>
    public sealed partial class ValidatorPage : Page
    {
        /// <summary>A bSDD class in the requirement tree, with the tree node of each of its properties.</summary>
        private sealed class ClassRequirement
        {
            public BsddClass Class { get; init; }
            public string DictionaryName { get; init; }
            public TreeViewNode Node { get; init; }
            public Dictionary<TreeViewNode, BsddClassProperty> Properties { get; } = new();
        }

        private readonly List<ClassRequirement> _classes = new();
        private IList<IdsValidationResult> _results;
        private string _resultsModel;
        private bool _dictionariesRequested;

        public ValidatorPage()
        {
            InitializeComponent();
            IdsZone.FileTypes = new[] { ".ids" };
            IdsZone.Describe = DescribeIds;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("validator");
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
            if (App.StartupIds.Count > 0)
            {
                IdsZone.Import(App.StartupIds);
                App.StartupIds.Clear();
            }
            UpdateState();
        }

        private static string DescribeIds(string path)
        {
            // Throws on a file that is not IDS; the row then shows why.
            int count = IdsReader.Load(path).Specifications.Count;
            return Loc.Count("Validator_Specifications", count);
        }

        #region Requirement source

        private async void OnModeChanged(object sender, RoutedEventArgs e)
        {
            // Raised while the page is still being built, for the tile checked in XAML.
            if (BsddPanel == null)
                return;
            bool bsdd = BsddMode.IsChecked == true;
            IdsZone.Visibility = bsdd ? Visibility.Collapsed : Visibility.Visible;
            BsddPanel.Visibility = bsdd ? Visibility.Visible : Visibility.Collapsed;
            UpdateState();
            if (bsdd && !_dictionariesRequested)
                await LoadDictionariesAsync();
        }

        private async Task LoadDictionariesAsync()
        {
            _dictionariesRequested = true;
            BsddError.IsOpen = false;
            DictionaryProgress.IsActive = true;
            try
            {
                var options = (await Bsdd.GetDictionariesAsync()).Select(d => new DictionaryOption(d)).ToList();
                DictionaryChoice.ItemsSource = options;
                DictionaryChoice.SelectedItem =
                    options.FirstOrDefault(o => BsddIds.IsIfcDictionary(o.Dictionary.Uri) && o.Dictionary.IsLatestVersion)
                    ?? options.FirstOrDefault();
                DictionaryChoice.IsEnabled = true;
                DictionaryChoice.PlaceholderText = Loc.Get("Validator_ChooseDictionaryPlaceholder");
            }
            catch (Exception ex) when (IsNetworkFailure(ex))
            {
                ShowBsddError(ex);
                var retry = new Button { Content = Loc.Get("Validator_Retry") };
                retry.Click += async (_, _) => await LoadDictionariesAsync();
                BsddError.ActionButton = retry;
                DictionaryChoice.PlaceholderText = Loc.Get("Validator_DictionariesFailed");
            }
            finally
            {
                DictionaryProgress.IsActive = false;
            }
        }

        private void OnDictionaryChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchResults.ItemsSource = null;
            SearchResults.Visibility = Visibility.Collapsed;
            SearchEmptyText.Visibility = Visibility.Collapsed;
        }

        private async void OnSearch(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => await SearchAsync(args.QueryText);

        private async void OnSearchClick(object sender, RoutedEventArgs e) => await SearchAsync(SearchBox.Text);

        private async Task SearchAsync(string query)
        {
            var text = query?.Trim();
            if (string.IsNullOrEmpty(text))
                return;
            var dictionary = (DictionaryChoice.SelectedItem as DictionaryOption)?.Dictionary;

            BsddError.IsOpen = false;
            SearchProgress.IsActive = true;
            try
            {
                var response = await Bsdd.Client.SearchClassesAsync(text, dictionary?.Uri, limit: 100);
                SearchResults.ItemsSource = response.Classes;
                SearchResults.Visibility = response.Classes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                SearchEmptyText.Text = Loc.Format("Validator_NoMatch", text, dictionary?.Name ?? "bSDD");
                SearchEmptyText.Visibility = response.Classes.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex) when (IsNetworkFailure(ex))
            {
                ShowBsddError(ex);
            }
            finally
            {
                SearchProgress.IsActive = false;
            }
        }

        private async void OnAddClass(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not BsddClassSearchItem item || _classes.Any(c => c.Class.Uri == item.Uri))
                return;

            BsddError.IsOpen = false;
            ClassProgress.IsActive = true;
            try
            {
                var bsddClass = await Bsdd.Client.GetClassAsync(item.Uri);
                var properties = await Bsdd.Client.GetAllClassPropertiesAsync(item.Uri);
                AddClass(bsddClass, properties, item.DictionaryName);
            }
            catch (Exception ex) when (IsNetworkFailure(ex))
            {
                ShowBsddError(ex);
            }
            finally
            {
                ClassProgress.IsActive = false;
            }
        }

        /// <summary>
        /// Adds a class with its properties grouped by property set.
        /// <para>
        /// What starts ticked depends on the dictionary. An IFC class such as IfcWall carries dozens
        /// of property sets, and requiring them all would fail every wall, so nothing is ticked and
        /// the Common set (Pset_WallCommon) is opened for the user to pick from. Other dictionaries
        /// list what their classes need, so everything is ticked except the plain IFC attributes.
        /// </para>
        /// </summary>
        private void AddClass(BsddClass bsddClass, IList<BsddClassProperty> properties, string dictionaryName)
        {
            var requirement = new ClassRequirement
            {
                Class = bsddClass,
                DictionaryName = dictionaryName,
                Node = new TreeViewNode { Content = $"{bsddClass.Name} ({bsddClass.Code})", IsExpanded = true },
            };

            var groups = properties
                .Where(p => !string.IsNullOrWhiteSpace(p.PropertySet))
                .GroupBy(p => p.PropertySet)
                .OrderBy(g => IsAttributes(g.Key) ? 2 : g.Key.EndsWith("Common", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(g => g.Key, StringComparer.Ordinal);

            bool ifcClass = BsddIds.IsIfcDictionary(bsddClass.Uri);
            var toSelect = new List<TreeViewNode>();
            foreach (var group in groups)
            {
                var distinct = group.GroupBy(BsddIds.PropertyName).Select(g => g.First()).OrderBy(BsddIds.PropertyName).ToList();
                var setNode = new TreeViewNode
                {
                    Content = $"{group.Key} ({distinct.Count})",
                    IsExpanded = ifcClass && group.Key.EndsWith("Common", StringComparison.Ordinal),
                };
                foreach (var property in distinct)
                {
                    var code = BsddIds.PropertyName(property);
                    var label = string.Equals(code, property.Name?.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(property.Name)
                        ? code
                        : $"{code} · {property.Name}";
                    var node = new TreeViewNode { Content = label };
                    setNode.Children.Add(node);
                    requirement.Properties[node] = property;
                    if (!ifcClass && !IsAttributes(group.Key))
                        toSelect.Add(node);
                }
                requirement.Node.Children.Add(setNode);
            }

            _classes.Add(requirement);
            RequirementTree.RootNodes.Add(requirement.Node);
            foreach (var node in toSelect)
                RequirementTree.SelectedNodes.Add(node);
            UpdateState();
            // The tree sits below the search results; show the user what was added.
            Ui.ScrollIntoView(RequirementTree, 0.1);
        }

        private static bool IsAttributes(string propertySet) =>
            string.Equals(propertySet, BsddHierarchy.AttributesPropertySet, StringComparison.OrdinalIgnoreCase);

        private void OnRequirementSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args) => UpdateState();

        private void OnClearClasses(object sender, RoutedEventArgs e)
        {
            _classes.Clear();
            RequirementTree.RootNodes.Clear();
            UpdateState();
        }

        /// <summary>The picked bSDD requirements as an IDS document; null when nothing is picked.</summary>
        private IdsDocument BuildBsddDocument()
        {
            var selected = RequirementTree.SelectedNodes.ToHashSet();
            var specifications = new List<IdsSpecification>();
            foreach (var requirement in _classes)
            {
                var properties = requirement.Properties.Where(p => selected.Contains(p.Key)).Select(p => p.Value).ToList();
                if (properties.Count > 0)
                {
                    specifications.Add(BsddIds.BuildSpecification(requirement.Class, properties, requirement.DictionaryName,
                        checkAllowedValues: AllowedValuesCheck.IsChecked == true));
                }
            }
            return specifications.Count == 0 ? null : BsddIds.BuildDocument(specifications);
        }

        private async void OnSaveIds(object sender, RoutedEventArgs e)
        {
            var document = BuildBsddDocument();
            if (document == null)
                return;
            var path = await Pickers.SaveFileAsync(Loc.Get("Validator_IdsFileName"), Loc.Get("Validator_IdsFileType"), ".ids");
            if (path == null)
                return;
            IdsWriter.Save(document, path);
            RunBar.Status = Loc.Format("Validator_IdsSaved", Path.GetFileName(path));
        }

        #endregion

        #region Running

        private void OnInputChanged(object sender, EventArgs e) => UpdateState();

        private bool HasBsddSelection => _classes.Any(c => c.Properties.Keys.Any(RequirementTree.SelectedNodes.Contains));

        /// <summary>Enables what can be used now and says what is still missing.</summary>
        private void UpdateState()
        {
            if (RunBar == null)
                return;
            bool hasModel = ModelZone.Files.Count > 0;
            bool bsdd = BsddMode.IsChecked == true;
            bool hasRequirements = bsdd ? HasBsddSelection : IdsZone.Files.Count > 0;

            SaveIdsButton.IsEnabled = HasBsddSelection;
            ClearClassesButton.IsEnabled = _classes.Count > 0;
            RequirementTree.Visibility = _classes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            NoClassesText.Visibility = _classes.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

            if (RunBar.IsBusy)
                return;
            RunBar.CanRun = hasModel && hasRequirements;
            RunBar.Status = !hasModel ? Loc.Get("Validator_NeedModel")
                : !hasRequirements ? Loc.Get(bsdd ? "Validator_NeedProperty" : "Validator_NeedIds")
                : string.Empty;
        }

        private async void OnValidate(object sender, EventArgs e)
        {
            var model = ModelZone.Files.FirstOrDefault()?.Path;
            if (model == null)
                return;
            RunBar.ClearError();

            var unreadable = IdsZone.Files.FirstOrDefault(f => f.Problem != null);
            if (IdsMode.IsChecked == true && unreadable != null)
            {
                RunBar.ShowError(Loc.Get("Validator_IdsUnreadable"), $"{unreadable.Name}: {unreadable.Problem}");
                return;
            }

            var idsPaths = IdsZone.Files.Select(f => f.Path).ToList();
            var document = BsddMode.IsChecked == true ? BuildBsddDocument() : null;

            var token = RunBar.Begin(Loc.Format("Validator_Running", Path.GetFileName(model)));
            var watch = Stopwatch.StartNew();
            string outcome = string.Empty;
            try
            {
                IList<IdsValidationResult> results = document == null
                    ? await Task.Run(() => IdsProcessor.Process(model, idsPaths, cancellationToken: token), token)
                    : new[] { await Task.Run(() => (IdsValidationResult)IdsProcessor.Process(model, document, cancellationToken: token).Value, token) };

                _results = results;
                _resultsModel = model;
                ResultsView.Show(results);
                ResultsSection.Visibility = Visibility.Visible;
                ResultsSection.IsExpanded = true;
                outcome = Loc.Format("Validator_Done", watch.Elapsed.TotalSeconds);
                Ui.ScrollIntoView(ResultsSection, 0);
            }
            catch (OperationCanceledException)
            {
                outcome = Loc.Get("Validator_Cancelled");
            }
            catch (Exception ex)
            {
                RunBar.ShowError(Loc.Get("Validator_Failed"), ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
                UpdateState();
                if (!string.IsNullOrEmpty(outcome))
                    RunBar.Status = outcome;
            }
        }

        #endregion

        #region Reports

        private async void OnExportCsv(object sender, RoutedEventArgs e) =>
            await ExportAsync(Loc.Get("Validator_CsvType"), ".csv", () => IdsReport.ToCsv(_results));

        private async void OnExportText(object sender, RoutedEventArgs e) =>
            await ExportAsync(Loc.Get("Validator_TextType"), ".txt", () => string.Join(Environment.NewLine, _results.Select(r => IdsReport.ToText(r))));

        private async Task ExportAsync(string typeLabel, string extension, Func<string> render)
        {
            if (_results == null)
                return;
            var path = await Pickers.SaveFileAsync(Path.GetFileNameWithoutExtension(_resultsModel) + "_IdsReport", typeLabel, extension);
            if (path == null)
                return;
            try
            {
                await File.WriteAllTextAsync(path, render(), Encoding.UTF8);
                RunBar.Status = Loc.Format("Validator_ReportSaved", path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                RunBar.ShowError(Loc.Get("Validator_ReportFailed"), ex.Message);
            }
        }

        #endregion

        private static bool IsNetworkFailure(Exception e) =>
            e is HttpRequestException or BsddException or TaskCanceledException or System.Text.Json.JsonException;

        private void ShowBsddError(Exception e)
        {
            BsddError.ActionButton = null;
            BsddError.Message = e is TaskCanceledException
                ? Loc.Get("Validator_Timeout")
                : e.Message;
            BsddError.IsOpen = true;
        }
    }
}
