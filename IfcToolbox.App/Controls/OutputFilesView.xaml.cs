using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IfcToolbox.App.Controls
{
    public sealed partial class OutputFilesView : UserControl
    {
        public OutputFilesView()
        {
            InitializeComponent();
        }

        /// <summary>Lists the files that exist of <paramref name="paths"/> under a one line summary.</summary>
        public void Show(string title, IEnumerable<string> paths, string message = null)
        {
            var files = paths.Where(File.Exists).Distinct().Select(p => new ImportedFile(p)).ToList();
            Summary.Title = title;
            Summary.Message = message ?? Loc.Count("Output_FilesWritten", files.Count);
            Summary.Severity = files.Count > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            FileList.ItemsSource = files;
        }

        private void OnOpen(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string path && File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void OnShowInFolder(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string path && File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
    }
}
