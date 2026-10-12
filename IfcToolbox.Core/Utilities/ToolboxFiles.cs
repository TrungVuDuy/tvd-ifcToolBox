using System;
using System.IO;
using System.Reflection;
using Xbim.Common;
using Xbim.Ifc;

namespace IfcToolbox.Core.Utilities
{
    /// <summary>
    /// How the toolbox identifies itself in the files it writes, and how it writes them, so that
    /// no output carries details of the machine or the person that produced it.
    /// </summary>
    public static class ToolboxFiles
    {
        /// <summary>"2.0.0-preview.1": the release, without the commit it was built from.</summary>
        public static string Version { get; } = ReadVersion();

        /// <summary>"IFC Toolbox 2.0.0-preview.1", for the header's preprocessor field.</summary>
        public static string Description => $"IFC Toolbox {Version}";

        /// <summary>
        /// The editor xbim records in the owner history of what it creates. Deliberately not the
        /// Windows user name, which the original wrote into every split and optimized file.
        /// </summary>
        public static XbimEditorCredentials Credentials() => new()
        {
            ApplicationDevelopersName = "IfcToolbox contributors",
            ApplicationFullName = "IFC Toolbox",
            ApplicationIdentifier = "IfcToolbox",
            ApplicationVersion = Version,
            EditorsFamilyName = "IFC Toolbox",
            EditorsOrganisationName = "IFC Toolbox",
        };

        /// <summary>
        /// Opens a model with the toolbox as editor. Opened without one, xbim records any change
        /// as made by the Windows user, writing their user name into the file as an IfcPerson.
        /// </summary>
        public static IfcStore Open(string path) => IfcStore.Open(path, Credentials());

        /// <summary>
        /// Saves <paramref name="model"/> to <paramref name="path"/>. The header names the file
        /// only by its file name (xbim writes the full local path, user folder and all, when the
        /// name is empty), the time it was written and this toolbox as preprocessor; the author,
        /// organisation and originating system of the model are kept as they were.
        /// </summary>
        public static void Save(IfcStore model, string path)
        {
            var header = model.Header.FileName;
            if (header != null)
            {
                header.Name = Path.GetFileName(path);
                header.TimeStamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                header.PreprocessorVersion = Description;
            }
            model.SaveAs(path);
        }

        private static string ReadVersion()
        {
            var informational = typeof(ToolboxFiles).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(informational))
                return typeof(ToolboxFiles).Assembly.GetName().Version?.ToString(3) ?? "2.0";
            int plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational[..plus];
        }
    }
}
