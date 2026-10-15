using IfcToolbox.App.Services;
using IfcToolbox.Core.Ids.Validation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace IfcToolbox.App.Controls
{
    public sealed partial class IdsResultsView : UserControl
    {
        /// <summary>
        /// Rows drawn per specification. A large model can fail thousands of entities; the
        /// exported report has all of them.
        /// </summary>
        private const int MaxEntitiesShown = 200;

        public IdsResultsView()
        {
            InitializeComponent();
        }

        public void Show(IList<IdsValidationResult> results)
        {
            Specifications.Children.Clear();

            var all = results.SelectMany(r => r.Specifications).ToList();
            int failed = all.Count(s => s.Status == IdsStatus.Fail);
            int passed = all.Count(s => s.Status == IdsStatus.Pass);
            int notApplicable = all.Count(s => s.Status == IdsStatus.NotApplicable);

            Verdict.Severity = failed > 0 ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            Verdict.Title = Loc.Get(failed > 0 ? "Ids_VerdictFail" : "Ids_VerdictPass");
            Verdict.Message = notApplicable > 0
                ? Loc.Format("Ids_SummaryWithNotApplicable", passed, all.Count, failed, notApplicable)
                : Loc.Format("Ids_Summary", passed, all.Count, failed);

            foreach (var result in results)
            {
                if (results.Count > 1)
                {
                    Specifications.Children.Add(new TextBlock
                    {
                        Text = result.Title ?? "IDS",
                        Style = StyleOf("BodyStrongTextBlockStyle"),
                        Margin = new Thickness(0, 12, 0, 0),
                    });
                }
                // Failures first: they are what the user came for.
                foreach (var specification in result.Specifications.OrderBy(s => s.Status == IdsStatus.Fail ? 0 : 1))
                    Specifications.Children.Add(CreateRow(specification));
            }
        }

        private static Expander CreateRow(IdsSpecificationResult specification)
        {
            var (glyph, iconStyle, label) = specification.Status switch
            {
                IdsStatus.Pass => ("", "SuccessIconStyle", Loc.Get("Ids_Passed")),
                IdsStatus.Fail => ("", "CriticalIconStyle", Loc.Get("Ids_Failed")),
                IdsStatus.NotApplicable => ("", "NeutralIconStyle", Loc.Get("Ids_NotApplicable")),
                _ => ("", "NeutralIconStyle", Loc.Get("Ids_Skipped")),
            };

            var header = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 6, 0, 6) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon { Glyph = glyph, FontSize = 14, Style = StyleOf(iconStyle) };
            AutomationProperties.SetName(icon, label);
            header.Children.Add(icon);

            var title = new StackPanel();
            title.Children.Add(new TextBlock
            {
                Text = specification.Specification?.Name ?? Loc.Get("Ids_Unnamed"),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var applicability = specification.Specification?.Applicability?.AllFacets().Select(f => f.Describe()).ToList();
            if (applicability?.Count > 0)
            {
                title.Children.Add(new TextBlock
                {
                    Text = Loc.Format("Ids_AppliesTo", string.Join(", ", applicability)),
                    Style = StyleOf("SecondaryCaptionStyle"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            var counts = new TextBlock
            {
                Text = specification.Status == IdsStatus.NotApplicable || specification.ApplicableCount == 0
                    ? label
                    : Loc.Format("Ids_PassCount", specification.PassedCount, specification.ApplicableCount),
                VerticalAlignment = VerticalAlignment.Center,
                Style = StyleOf("SecondaryTextStyle"),
            };
            Grid.SetColumn(counts, 2);
            header.Children.Add(counts);

            var expander = new Expander
            {
                Header = header,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(expander, $"{label}: {specification.Specification?.Name}");
            // Built on first open: a long result list stays quick to show.
            expander.Expanding += (_, _) => expander.Content ??= CreateDetails(specification);
            return expander;
        }

        private static UIElement CreateDetails(IdsSpecificationResult specification)
        {
            var panel = new StackPanel { Spacing = 10 };

            foreach (var text in new[] { specification.Specification?.Description, specification.Specification?.Instructions, specification.Message })
            {
                if (!string.IsNullOrWhiteSpace(text))
                    panel.Children.Add(new TextBlock { Text = text, Style = StyleOf("SecondaryTextStyle") });
            }

            var failures = specification.Failures.ToList();
            if (failures.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = specification.Status switch
                    {
                        IdsStatus.NotApplicable => Loc.Get("Ids_NothingMatches"),
                        IdsStatus.Pass when specification.ApplicableCount == 0 => Loc.Get("Ids_NothingMatchesAllowed"),
                        IdsStatus.Pass => Loc.Format("Ids_AllMeet", specification.ApplicableCount),
                        _ => Loc.Get("Ids_NoEntityFailed"),
                    },
                    Style = StyleOf("SecondaryTextStyle"),
                });
            }
            foreach (var entity in failures.Take(MaxEntitiesShown))
            {
                var row = new StackPanel { Spacing = 2 };
                var name = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
                name.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = $"#{entity.EntityLabel} {entity.EntityType}  ", FontWeight = FontWeights.SemiBold });
                name.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = entity.Name ?? string.Empty });
                row.Children.Add(name);
                if (!string.IsNullOrEmpty(entity.GlobalId))
                    row.Children.Add(new TextBlock { Text = entity.GlobalId, Style = StyleOf("SecondaryCaptionStyle"), IsTextSelectionEnabled = true });

                foreach (var facet in entity.Failures)
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = facet.Message ?? facet.Facet?.Describe(),
                        Style = StyleOf("CriticalTextStyle"),
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(16, 0, 0, 0),
                    });
                }
                panel.Children.Add(row);
            }

            if (failures.Count > MaxEntitiesShown)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = Loc.Format("Ids_More", failures.Count - MaxEntitiesShown),
                    Style = StyleOf("SecondaryTextStyle"),
                });
            }
            return panel;
        }

        private static Style StyleOf(string key) => (Style)Application.Current.Resources[key];
    }
}
