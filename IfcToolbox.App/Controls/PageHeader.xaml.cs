using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IfcToolbox.App.Controls
{
    public sealed partial class PageHeader : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(null));

        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(PageHeader), new PropertyMetadata(null));

        public static readonly DependencyProperty TileBrushProperty = DependencyProperty.Register(
            nameof(TileBrush), typeof(Brush), typeof(PageHeader), new PropertyMetadata(null));

        public PageHeader()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public Brush TileBrush
        {
            get => (Brush)GetValue(TileBrushProperty);
            set => SetValue(TileBrushProperty, value);
        }
    }
}
