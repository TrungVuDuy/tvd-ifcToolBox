using IfcToolbox.Cli.Commands;
using IfcToolbox.Core.Utilities;
using Serilog;
using System;
using System.CommandLine;
using System.IO;

namespace IfcToolbox.Cli
{
    /// <summary>
    /// Entry point of the ifctoolbox command. <see cref="Run"/> takes its writers as
    /// parameters so tests can drive the whole command line in process.
    /// </summary>
    public static class CliApp
    {
        public static readonly Option<bool> VerboseOption = new("--verbose", "-v")
        {
            Description = "Write progress and diagnostic messages to stderr.",
            Recursive = true,
        };

        public static RootCommand BuildRootCommand()
        {
            var root = new RootCommand(
                "IFC toolbox: validate against IDS, convert, split, optimise, anonymise and georeference IFC models " +
                "(IFC2X3, IFC4, IFC 4.3).");
            root.Options.Add(VerboseOption);

            root.Subcommands.Add(InfoCommand.Create());
            root.Subcommands.Add(ValidateCommand.Create());
            root.Subcommands.Add(ConvertCommand.Create());
            root.Subcommands.Add(SplitCommand.Create());
            root.Subcommands.Add(OptimizeCommand.Create());
            root.Subcommands.Add(AnonymizeCommand.Create());
            root.Subcommands.Add(GeorefCommand.Create());
            return root;
        }

        public static int Run(string[] args, TextWriter output = null, TextWriter error = null)
        {
            output ??= Console.Out;
            error ??= Console.Error;

            var parseResult = BuildRootCommand().Parse(args);

            if (parseResult.Errors.Count > 0)
            {
                foreach (var parseError in parseResult.Errors)
                    error.WriteLine($"error: {parseError.Message}");
                error.WriteLine("Run 'ifctoolbox --help' for usage.");
                return ExitCodes.UsageError;
            }

            // Standard output belongs to the command's results. Library progress messages go
            // to stderr with --verbose and nowhere otherwise, so --json output stays parseable.
            bool verbose = parseResult.GetValue(VerboseOption);
            var previousOutput = Marslogger.Output;
            Marslogger.Output = verbose ? error : TextWriter.Null;
            if (verbose)
                Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
                    // Every level to stderr, never stdout.
                    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                    .CreateLogger();

            try
            {
                var configuration = new InvocationConfiguration
                {
                    Output = output,
                    Error = error,
                    EnableDefaultExceptionHandler = false,
                };
                return parseResult.Invoke(configuration);
            }
            catch (CliUsageException e)
            {
                error.WriteLine($"error: {e.Message}");
                return ExitCodes.UsageError;
            }
            catch (Exception e)
            {
                error.WriteLine($"error: {e.GetBaseException().Message}");
                if (verbose)
                    error.WriteLine(e);
                return ExitCodes.Error;
            }
            finally
            {
                Marslogger.Output = previousOutput;
            }
        }
    }

    /// <summary>A problem with what the user asked for, reported as a usage error.</summary>
    public class CliUsageException : Exception
    {
        public CliUsageException(string message) : base(message) { }
    }
}
