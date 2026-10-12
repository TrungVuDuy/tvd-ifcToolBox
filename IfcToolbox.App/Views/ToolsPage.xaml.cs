using IfcToolbox.App.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IfcToolbox.App.Views
{
    /// <summary>Landing page: one card per tool.</summary>
    public sealed partial class ToolsPage : Page
    {
        public ToolsPage()
        {
            InitializeComponent();
            ToolList.ItemsSource = ToolCatalog.All;
        }

        private void OnToolClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string key)
                App.MainWindow.NavigateTo(key);
        }
    }
}
