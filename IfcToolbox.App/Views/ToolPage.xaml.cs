using IfcToolbox.App.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace IfcToolbox.App.Views
{
    /// <summary>
    /// Page of one tool. For now the input and layout only; each tool's options and its run
    /// button come with the stage of the rebuild named in <see cref="ToolInfo.Stage"/>.
    /// </summary>
    public sealed partial class ToolPage : Page
    {
        public ToolPage()
        {
            InitializeComponent();
        }

        public ToolInfo Tool { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Tool = (ToolInfo)e.Parameter;

            Header.Title = Tool.Title;
            Header.Glyph = Tool.Glyph;
            Header.TileBrush = new SolidColorBrush(Tool.Color);
            SummaryText.Text = Tool.Summary;
            InputSection.Header = Tool.Key == "validator" ? "File to validate" : "File to process";
            DropZone.AllowMultiple = Tool.Key is "optimizer" or "anonymizer" or "converter";
            RunButton.Content = Tool.Action;
            PendingInfo.Title = "Not wired up yet";
            PendingInfo.Message = $"The options of {Tool.Title} arrive in stage {Tool.Stage} of the desktop app rebuild. " +
                                  "Until then the ifctoolbox command line tool does the same job.";

            if (App.StartupImport != null)
            {
                DropZone.Import(new[] { App.StartupImport });
                App.StartupImport = null;
            }
        }
    }
}
