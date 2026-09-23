using IfcToolbox.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IfcToolbox.Core.Convert
{
    public class IfcConvert
    {
        private static IfcConvertVersion _detectedVersion;
        private static string _detectedFor;

        /// <summary>
        /// Environment variable pointing at an IfcConvert executable to use instead of the
        /// bundled one. This is how to move to IfcConvert 0.8, which adds glTF in ECEF
        /// coordinates as well as the JSON, TTL and H5 serializers.
        /// </summary>
        public const string ExecutablePathVariable = "IFCTOOLBOX_IFCCONVERT";

        /// <summary>
        /// Explicit path to an IfcConvert executable. Takes precedence over everything else.
        /// </summary>
        public static string ExecutablePathOverride { get; set; }

        public static void Convert(string source, IConvertOptionsWrap optionsWrap, ConvertTargetFormat format, bool logDetail = false)
        {
            string target = Path.ChangeExtension(source, ConvertFormatInfo.GetExtension(format));
            string logFilePath = Path.ChangeExtension(source, ".txt");
            Convert(source, target, optionsWrap, format, logFilePath, logDetail);
        }

        public static IEnumerable<string> Convert(string source, string target, IConvertOptionsWrap optionsWrap, ConvertTargetFormat format,
            string logFilePath = null, bool logDetail = false, string workingDirectory = null)
        {
            if (string.IsNullOrEmpty(workingDirectory))
                workingDirectory = ReflectionUtility.GetExecutingAssemblyDirectory;

            string executablePath = ResolveExecutable(workingDirectory);
            if (!File.Exists(executablePath))
                throw new FileNotFoundException(
                    $"IfcConvert was not found (looked for {Path.GetFileName(executablePath)}). Reference the IfcToolbox.IfcConvert " +
                    $"package, which ships the Windows x64 build, or point {ExecutablePathVariable} at an IfcConvert executable.",
                    executablePath);
            var version = GetVersion(executablePath);

            if (!ConvertFormatInfo.IsSupportedBy(format, version))
                throw new NotSupportedException(
                    $"IfcConvert {version} cannot write {format}; it needs {ConvertFormatInfo.GetMinimumVersion(format)} or newer. " +
                    $"Point {ExecutablePathVariable} at a newer IfcConvert to enable it.");

            var fileSchema = ReadFileSchema(source);
            if (!CanRead(version, fileSchema))
                throw new NotSupportedException(
                    $"IfcConvert {version} cannot read {fileSchema} models. IFC 4.3 support arrived after the " +
                    $"0.6 release bundled with IfcToolbox; point {ExecutablePathVariable} at IfcConvert 0.8 or newer.");

            var optionPrompt = ConvertOptionService.GeneratePrompt(optionsWrap, format, version, logFilePath, true);

            var run = ProcessUtility.Run(source, target, workingDirectory, executablePath, optionPrompt, logDetail);

            var finalFiles = new List<string>();
            if (File.Exists(target))
                finalFiles.Add(target);
            // .mtl file generated with obj file.
            if (format == ConvertTargetFormat.OBJ)
            {
                var mtlFile = Path.ChangeExtension(target, ".mtl");
                if (File.Exists(mtlFile))
                    finalFiles.Add(mtlFile);
            }
            // add log file if log is not empty
            if (File.Exists(logFilePath))
            {
                if (!string.IsNullOrEmpty(File.ReadAllText(logFilePath)))
                    finalFiles.Add(logFilePath);
                else
                    File.Delete(logFilePath);
            }

            if (!run.Success && finalFiles.Count == 0)
                throw new IfcConvertException(format, run.ExitCode, run.StandardError);

            return finalFiles;
        }

        /// <summary>
        /// Locates the IfcConvert executable, in order:
        /// <list type="number">
        /// <item><see cref="ExecutablePathOverride"/>, set in code;</item>
        /// <item>the <see cref="ExecutablePathVariable"/> environment variable;</item>
        /// <item><paramref name="workingDirectory"/>, which is how a caller points at its own build;</item>
        /// <item>a copy next to the application;</item>
        /// <item>the executable embedded in this assembly, written out on first use.</item>
        /// </list>
        /// </summary>
        public static string ResolveExecutable(string workingDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(ExecutablePathOverride) && File.Exists(ExecutablePathOverride))
                return ExecutablePathOverride;

            var fromEnvironment = Environment.GetEnvironmentVariable(ExecutablePathVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
                return fromEnvironment;

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                var inWorkingDirectory = Path.Combine(workingDirectory, ExecutableName);
                if (File.Exists(inWorkingDirectory))
                    return inWorkingDirectory;
            }

            var besideApplication = Path.Combine(ReflectionUtility.GetExecutingAssemblyDirectory, ExecutableName);
            if (File.Exists(besideApplication))
                return besideApplication;

            return EnsureExecutable() ?? besideApplication;
        }

        /// <summary>
        /// IfcConvert ships as an embedded resource, so it has to be written next to the
        /// application before it can be started. Previously only the GUI app did this, which
        /// left the converter unusable for any other consumer of the library.
        /// </summary>
        public static string EnsureExecutable()
        {
            return ReflectionUtility.CopyEmbeddedResource(typeof(IfcConvert).Assembly, ExecutableName);
        }

        public static string ExecutableName
        {
            get
            {
                string executableName = "IfcConvert_x64.exe";
                if (!Environment.Is64BitOperatingSystem)
                    executableName = "IfcConvert_x32.exe";
                return executableName;
            }
        }

        /// <summary>
        /// Version of the IfcConvert build in use, cached per executable path.
        /// </summary>
        public static IfcConvertVersion GetVersion(string executablePath = null)
        {
            executablePath ??= ResolveExecutable();
            if (_detectedVersion != null && _detectedFor == executablePath)
                return _detectedVersion;

            _detectedFor = executablePath;
            _detectedVersion = ReadVersion(executablePath);
            return _detectedVersion;
        }

        /// <summary>
        /// Whether the given IfcConvert build can open a file of that schema. Only what has been
        /// checked is refused: 0.6 reports "No schema named IFC4X3_ADD2". Anything newer, or an
        /// unknown version, is allowed to try, and a failure then surfaces as IfcConvertException.
        /// </summary>
        public static bool CanRead(IfcConvertVersion version, string fileSchema)
        {
            if (string.IsNullOrEmpty(fileSchema) || version == null || version.IsUnknown)
                return true;
            bool isIfc4x3 = fileSchema.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase);
            return !isIfc4x3 || version >= new IfcConvertVersion(0, 7);
        }

        /// <summary>
        /// Schema named in the file's FILE_SCHEMA header, read without parsing the model.
        /// Returns null for anything that is not a readable STEP file.
        /// </summary>
        public static string ReadFileSchema(string path)
        {
            try
            {
                foreach (var line in File.ReadLines(path).Take(50))
                {
                    int index = line.IndexOf("FILE_SCHEMA", StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                        continue;
                    var match = System.Text.RegularExpressions.Regex.Match(line.Substring(index), @"'([^']+)'");
                    return match.Success ? match.Groups[1].Value : null;
                }
            }
            catch (IOException)
            {
            }
            return null;
        }

        /// <summary>Formats the IfcConvert build in use can write.</summary>
        public static IEnumerable<ConvertTargetFormat> SupportedFormats() =>
            ConvertFormatInfo.SupportedFormats(GetVersion());

        private static IfcConvertVersion ReadVersion(string executablePath)
        {
            if (!File.Exists(executablePath))
                return IfcConvertVersion.Unknown;

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(info);
                string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                return IfcConvertVersion.Parse(output);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Could not read the IfcConvert version from {Path}", executablePath);
                return IfcConvertVersion.Unknown;
            }
        }

        public static string GetExcludeEntitiesDefault(ConvertTargetFormat format)
        {
            if (format == ConvertTargetFormat.SVG)
                return "IfcOpeningElement";
            else
                return "IfcOpeningElement IfcSpace";
        }
    }

    /// <summary>Raised when IfcConvert fails and produces no output at all.</summary>
    public class IfcConvertException : Exception
    {
        public IfcConvertException(ConvertTargetFormat format, int exitCode, string standardError)
            : base($"IfcConvert failed to produce {format} output (exit code {exitCode}). {standardError}")
        {
            Format = format;
            ExitCode = exitCode;
            StandardError = standardError;
        }

        public ConvertTargetFormat Format { get; }
        public int ExitCode { get; }
        public string StandardError { get; }
    }
}
