using IfcToolbox.App.Controls;
using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using IfcToolbox.Core.Editors;
using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IfcToolbox.App.Views
{
    /// <summary>
    /// IFC Anonymizer: removes who made a model and, by rule, product names and brands.
    /// </summary>
    public sealed partial class AnonymizerPage : Page
    {
        // Separates class and object type in a tree value; neither contains it.
        private const char Separator = '\u001F';

        private readonly HierarchySelection _types;
        private readonly ObservableCollection<ReplaceRule> _rules = new();
        private string _analysedPath;
        private int _analysisRun;

        public AnonymizerPage()
        {
            InitializeComponent();
            _types = new HierarchySelection(TypeTree);
            _types.SelectionChanged += (_, _) => AddRulesButton.IsEnabled = _types.SelectedValues().Count > 0;
            RuleList.ItemsSource = _rules;
            _rules.CollectionChanged += (_, _) => UpdateState();
            UpdateOptions();
            TypesHint.Text = Loc.Get("Anonymizer_NeedModelForTypes");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var tool = ToolCatalog.Find("anonymizer");
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
        }

        private async void OnModelChanged(object sender, EventArgs e)
        {
            OutputSection.Visibility = Visibility.Collapsed;
            var path = ModelZone.Files.FirstOrDefault()?.Path;
            if (path == _analysedPath)
                return;
            _analysedPath = path;
            _types.Clear();
            // Rules belong to the model they were made for.
            _rules.Clear();
            UpdateState();
            if (path == null)
            {
                TypesHint.Text = Loc.Get("Anonymizer_NeedModelForTypes");
                return;
            }

            int run = ++_analysisRun;
            TypesProgress.IsActive = true;
            TypesHint.Text = Loc.Format("Common_Reading", Path.GetFileName(path));
            try
            {
                var trees = await Task.Run(() => AnonymizerProcessor.Analyse(path));
                if (run != _analysisRun)
                    return;
                _types.Show(trees.TypedNodes,
                    n => n.IsComposition ? null : n.ParentId + Separator + n.Id,
                    Ui.NodeLabel,
                    (_, _) => false);
                TypesHint.Text = trees.TypedNodes.Count == 0
                    ? Loc.Get("Anonymizer_NoTypes")
                    : Loc.Get("Anonymizer_TickTypes");
            }
            catch (Exception ex)
            {
                if (run == _analysisRun)
                    RunBar.ShowError(Loc.Get("Common_ModelUnreadable"), ex.Message);
            }
            finally
            {
                if (run == _analysisRun)
                    TypesProgress.IsActive = false;
            }
        }

        private void OnOptionChanged(object sender, RoutedEventArgs e) => UpdateOptions();

        private void UpdateOptions()
        {
            if (RunBar == null)
                return;
            foreach (var box in UserOptions.Children.OfType<CheckBox>())
                box.IsEnabled = UserSwitch.IsOn;
            ReplaceWhere.Visibility = ProductSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
            ProductPanel.Visibility = ProductSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
            UpdateState();
        }

        private void OnAddRules(object sender, RoutedEventArgs e)
        {
            foreach (var value in _types.SelectedValues())
            {
                var parts = value.Split(Separator);
                if (parts.Length != 2 || _rules.Any(r => r.EntityType == parts[0] && r.Keyword == parts[1]))
                    continue;
                _rules.Add(new ReplaceRule(parts[0], parts[1]));
            }
        }

        private void OnRemoveRule(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ReplaceRule rule)
                _rules.Remove(rule);
        }

        private void OnClearRules(object sender, RoutedEventArgs e) => _rules.Clear();

        private void UpdateState()
        {
            if (RunBar == null || RunBar.IsBusy)
                return;
            ClearRulesButton.IsEnabled = _rules.Count > 0;
            bool hasModel = ModelZone.Files.Count > 0;
            bool products = ProductSwitch.IsOn && _rules.Count > 0;
            RunBar.CanRun = hasModel && (UserSwitch.IsOn || products);
            RunBar.Status = !hasModel ? Loc.Get("Anonymizer_NeedModel")
                : !UserSwitch.IsOn && !ProductSwitch.IsOn ? Loc.Get("Anonymizer_NeedOption")
                : ProductSwitch.IsOn && _rules.Count == 0 ? Loc.Get("Anonymizer_NeedRules")
                : Loc.Get("Anonymizer_Ready");
        }

        private IConfigAnonymize BuildConfig()
        {
            var config = ConfigFactory.CreateConfigAnonymize();
            config.AnonymeUserInfo = UserSwitch.IsOn;
            config.RemovePerson = PersonCheck.IsChecked == true;
            config.RemoveOrganization = OrganizationCheck.IsChecked == true;
            config.RemovePersonAndOrganization = PersonAndOrganizationCheck.IsChecked == true;
            config.RemoveActorRole = ActorRoleCheck.IsChecked == true;
            config.RemovePostalAddress = PostalCheck.IsChecked == true;
            config.RemoveTelecomAddress = TelecomCheck.IsChecked == true;
            config.RemoveApplication = ApplicationCheck.IsChecked == true;
            config.ClearHeaderFileName = FileNameCheck.IsChecked == true;

            config.AnonymeProductInfo = ProductSwitch.IsOn && _rules.Count > 0;
            config.Rules = _rules
                .Where(r => !string.IsNullOrEmpty(r.Keyword))
                .Select(r => new AnonymeRule(r.EntityType, r.Keyword, r.Replacement ?? string.Empty))
                .ToList();
            config.ReplaceInName = InNameCheck.IsChecked == true;
            config.ReplaceInObjectType = InObjectTypeCheck.IsChecked == true;
            config.ReplaceInTypeProps = InTypePropsCheck.IsChecked == true;
            config.ReplaceInProductProps = InProductPropsCheck.IsChecked == true;
            return config;
        }

        private async void OnAnonymize(object sender, EventArgs e)
        {
            var path = ModelZone.Files.FirstOrDefault()?.Path;
            if (path == null)
                return;
            var config = BuildConfig();

            RunBar.Begin(Loc.Format("Anonymizer_Running", Path.GetFileName(path)), cancellable: false);
            var started = DateTime.Now;
            string outcome = string.Empty;
            try
            {
                var result = await Task.Run(() => AnonymizerProcessor.Process(path, config));
                // The model first, then the log of changes.
                var files = result.FilePaths.OrderBy(p => p.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
                OutputView.Show(Loc.Get("Anonymizer_Finished"), files, Loc.Get("Anonymizer_FinishedNote"));
                OutputSection.Visibility = Visibility.Visible;
                OutputSection.IsExpanded = true;
                Ui.ScrollIntoView(OutputSection);
                outcome = Loc.Format("Common_Done", (DateTime.Now - started).TotalSeconds);
            }
            catch (Exception ex)
            {
                RunBar.ShowError(Loc.Get("Anonymizer_Failed"), ex.Message);
            }
            finally
            {
                RunBar.End(outcome);
            }
        }
    }
}
