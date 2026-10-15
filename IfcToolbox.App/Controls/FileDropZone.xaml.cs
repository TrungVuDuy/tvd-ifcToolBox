using IfcToolbox.App.Models;
using IfcToolbox.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace IfcToolbox.App.Controls
{
    /// <summary>
    /// Where a tool page takes its input: a dashed area to click or drop files on, and the list
    /// of imported files with their schema, date and size.
    /// </summary>
    public sealed partial class FileDropZone : UserControl
    {
        public static readonly DependencyProperty AllowMultipleProperty = DependencyProperty.Register(
            nameof(AllowMultiple), typeof(bool), typeof(FileDropZone), new PropertyMetadata(false));

        public static readonly DependencyProperty PromptProperty = DependencyProperty.Register(
            nameof(Prompt), typeof(string), typeof(FileDropZone), new PropertyMetadata(Loc.Get("Drop_Prompt")));

        public FileDropZone()
        {
            InitializeComponent();
            FileList.ItemsSource = Files;
            Files.CollectionChanged += (_, _) =>
            {
                ResetButton.Visibility = Files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                for (int i = 0; i < Files.Count; i++)
                    Files[i].CanMoveUp = AllowReorder && i > 0;
                FilesChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        /// <summary>Whether the order matters, so rows offer to move up. Set before files are added.</summary>
        public bool AllowReorder { get; set; }

        /// <summary>Whether more than one file can be imported; otherwise a new file replaces the old one.</summary>
        public bool AllowMultiple
        {
            get => (bool)GetValue(AllowMultipleProperty);
            set => SetValue(AllowMultipleProperty, value);
        }

        /// <summary>Text in the dashed area.</summary>
        public string Prompt
        {
            get => (string)GetValue(PromptProperty);
            set => SetValue(PromptProperty, value);
        }

        /// <summary>Extensions accepted, with the dot.</summary>
        public IReadOnlyList<string> FileTypes { get; set; } = new[] { ".ifc" };

        /// <summary>
        /// Badge text for a file, run off the UI thread; throw to flag the file as unusable.
        /// Defaults to the IFC schema.
        /// </summary>
        public Func<string, string> Describe { get; set; }

        public ObservableCollection<ImportedFile> Files { get; } = new();

        public event EventHandler FilesChanged;

        public void Import(IEnumerable<string> paths)
        {
            var accepted = paths
                .Where(p => FileTypes.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                .Where(File.Exists)
                .ToList();
            if (accepted.Count == 0)
                return;

            if (!AllowMultiple)
            {
                Files.Clear();
                accepted = accepted.Take(1).ToList();
            }
            foreach (var path in accepted)
            {
                if (Files.Any(f => string.Equals(f.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
                    continue;
                var file = new ImportedFile(path);
                Files.Add(file);
                _ = file.DescribeAsync(Describe);
            }
        }

        private async void OnChooseFiles(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            foreach (var type in FileTypes)
                picker.FileTypeFilter.Add(type);
            // Unpackaged apps have to tell the picker which window owns it.
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

            if (AllowMultiple)
            {
                var files = await picker.PickMultipleFilesAsync();
                Import(files.Select(f => f.Path));
            }
            else
            {
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                    Import(new[] { file.Path });
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Loc.Get("Drop_Caption");
            PromptText.Text = Loc.Get("Drop_Release");
        }

        private void OnDragLeave(object sender, DragEventArgs e) => PromptText.Text = Prompt;

        private async void OnDrop(object sender, DragEventArgs e)
        {
            PromptText.Text = Prompt;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;
            var deferral = e.GetDeferral();
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                Import(items.OfType<StorageFile>().Select(f => f.Path).Where(p => !string.IsNullOrEmpty(p)));
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnMoveUp(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ImportedFile file)
            {
                int index = Files.IndexOf(file);
                if (index > 0)
                    Files.Move(index, index - 1);
            }
        }

        private void OnRemoveFile(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ImportedFile file)
                Files.Remove(file);
        }

        private void OnReset(object sender, RoutedEventArgs e) => Files.Clear();

        public static Visibility ShowIf(string text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

        public static Visibility ShowIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    }
}
