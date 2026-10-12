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

        /// <summary>Short label next to the name, e.g. the IFC schema; filled in after the row appears.</summary>
        [ObservableProperty]
        private string _badge;

        /// <summary>Set when the file cannot be used, e.g. an IDS file that does not parse.</summary>
        [ObservableProperty]
        private string _problem;

        /// <summary>
        /// Works out the badge off the UI thread. The describer returns the badge text, and throws
        /// to report a file it cannot read.
        /// </summary>
        public async Task DescribeAsync(Func<string, string> describe)
        {
            try
            {
                Badge = await Task.Run(() => (describe ?? DescribeIfc)(Path));
            }
            catch (Exception e)
            {
                Badge = "unreadable";
                Problem = e.Message;
            }
        }

        /// <summary>The IFC schema named in the file header, e.g. "IFC4" or "IFC 4.3".</summary>
        public static string DescribeIfc(string path)
        {
            var schema = IfcConvert.ReadFileSchema(path);
            return schema switch
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
