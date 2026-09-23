using System.CommandLine;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IfcToolbox.Cli.Commands
{
    internal static class CommandHelpers
    {
        public static Argument<FileInfo> ModelArgument(string description = "The IFC model.") =>
            new Argument<FileInfo>("model") { Description = description }.AcceptExistingOnly();

        public static Option<FileInfo> OutputOption(string description) =>
            new("--output", "-o") { Description = description };

        public static Option<bool> JsonOption() =>
            new("--json") { Description = "Write the result as JSON to standard output." };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static void WriteJson(TextWriter output, object value) =>
            output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

        /// <summary>
        /// The processors write next to the source with a suffix. When the user asked for a
        /// specific path, the result is moved there.
        /// </summary>
        public static string PlaceOutput(string produced, FileInfo requested)
        {
            if (requested == null || string.Equals(Path.GetFullPath(produced), requested.FullName, System.StringComparison.OrdinalIgnoreCase))
                return produced;

            requested.Directory?.Create();
            File.Move(produced, requested.FullName, overwrite: true);
            return requested.FullName;
        }
    }
}
