using CommunityToolkit.Mvvm.ComponentModel;
using IfcToolbox.Core.Convert;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace IfcToolbox.App.Models
{
    /// <summary>A file the user dropped or picked, with what the file list row shows about it.</summary>
    public sealed partial class ImportedFile : ObservableObject
    {
        public ImportedFile(string path)
        {
            var info = new FileInfo(path);
            Path = info.FullName;
            Name = info.Name;
            Folder = info.DirectoryName;
            Modified = info.LastWriteTime.ToString("g", CultureInfo.CurrentCulture);
            Size = FormatSize(info.Length);
        }

        public string Path { get; }
        public string Name { get; }
        public string Folder { get; }
        public string Modified { get; }
        public string Size { get; }

        /// <summary>FILE_SCHEMA of the header, e.g. "IFC4X3_ADD2"; filled in after the row appears.</summary>
        [ObservableProperty]
        private string _schema;

        public async Task ReadSchemaAsync()
        {
            var schema = await Task.Run(() => IfcConvert.ReadFileSchema(Path));
            Schema = schema switch
            {
                null => "?",
                _ when schema.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase) => "IFC 4.3",
                _ => schema.ToUpperInvariant(),
            };
        }

        public static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
        }
    }
}
