using Serilog;
using System.Diagnostics;
using System.IO;

namespace IfcToolbox.Core.Utilities
{
    public class ProcessUtility
    {
        public static void ProcessStart(string source, string target, string workingDirectory, string executableName, string optionPrompt = null, bool logDetail = false)
        {
            Run(source, target, workingDirectory, executableName, optionPrompt, logDetail);
        }

        /// <summary>
        /// Runs the converter and waits for it.
        /// <para>
        /// The previous version never called WaitForExit, ignored stderr and dropped the exit
        /// code, so a failed conversion looked exactly like a successful one.
        /// </para>
        /// </summary>
        public static ProcessRunResult Run(string source, string target, string workingDirectory,
            string executableName, string optionPrompt = null, bool logDetail = false)
        {
            var executablePath = Path.IsPathRooted(executableName)
                ? executableName
                : Path.Combine(workingDirectory, executableName);

            var info = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workingDirectory,
                Arguments = $"\"{source}\" \"{target}\" {optionPrompt}",
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var proc = new Process())
            {
                proc.StartInfo = info;
                proc.Start();

                // Read both streams before waiting: a child that fills one of the pipes while
                // we block on WaitForExit would deadlock.
                string standardOutput = proc.StandardOutput.ReadToEnd();
                string standardError = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                var result = new ProcessRunResult
                {
                    ExitCode = proc.ExitCode,
                    StandardOutput = standardOutput,
                    StandardError = standardError,
                };

                if (logDetail && !string.IsNullOrWhiteSpace(standardOutput))
                    Log.Information(standardOutput);
                if (!result.Success)
                    Log.Warning("{Executable} exited with code {ExitCode}. {Error}",
                        Path.GetFileName(executablePath), result.ExitCode, result.StandardError);

                return result;
            }
        }
    }

    public class ProcessRunResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public bool Success => ExitCode == 0;
    }
}
