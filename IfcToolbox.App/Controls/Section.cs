using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace IfcToolbox.App.Controls
{
    /// <summary>
    /// A titled block of a tool page, like "File to validate" in the original app: a grey
    /// header bar that collapses and expands the content below it.
    /// </summary>
    [TemplatePart(Name = "HeaderButton", Type = typeof(ButtonBase))]
    public sealed class Section : ContentControl
    {
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header), typeof(string), typeof(Section), new PropertyMetadata(null));

        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            nameof(IsExpanded), typeof(bool), typeof(Section), new PropertyMetadata(true, (d, _) => ((Section)d).UpdateState(true)));

        private ButtonBase _headerButton;

        public Section()
        {
            DefaultStyleKey = typeof(Section);
        }

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            if (_headerButton != null)
                _headerButton.Click -= OnHeaderClick;
            base.OnApplyTemplate();
            _headerButton = GetTemplateChild("HeaderButton") as ButtonBase;
            if (_headerButton != null)
                _headerButton.Click += OnHeaderClick;
            UpdateState(false);
        }

        private void OnHeaderClick(object sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

        private void UpdateState(bool animate) =>
            VisualStateManager.GoToState(this, IsExpanded ? "Expanded" : "Collapsed", animate);
    }
}
