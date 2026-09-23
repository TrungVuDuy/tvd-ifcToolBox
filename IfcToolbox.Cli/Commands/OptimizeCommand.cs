using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System.CommandLine;
using System.IO;
using System.Linq;
using Xbim.Ifc;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>ifctoolbox optimize model.ifc [--precision 4]</summary>
    public static class OptimizeCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var output = CommandHelpers.OutputOption("Output file. Defaults to <model>_Optimized.ifc next to the model.");
            var precision = new Option<int?>("--precision", "-p")
            {
                Description = "Round coordinates to this many decimals (in model units) and merge the points that become equal. " +
                              "Leave out to merge only exact duplicates, which never changes the geometry.",
            };
            var merge = new Option<string>("--merge")
            {
                Description = "Merge duplicate entities: resources (default), reversed (also reverse-referenced entities, slower) or none.",
                DefaultValueFactory = _ => "resources",
            };
            merge.AcceptOnlyFromAmong("resources", "reversed", "none");

            var command = new Command("optimize", "Reduce file size by merging duplicate entities and, optionally, rounding coordinates.");
            command.Arguments.Add(model);
            command.Options.Add(output);
            command.Options.Add(precision);
            command.Options.Add(merge);

            command.SetAction(result =>
            {
                var writer = result.InvocationConfiguration.Output;
                var modelFile = result.GetValue(model);
                var digits = result.GetValue(precision);
                var mergeMode = result.GetValue(merge);

                if (digits.HasValue && (digits.Value < 0 || digits.Value > 15))
                    throw new CliUsageException("--precision must be between 0 and 15.");

                var config = ConfigFactory.CreateConfigOptimize();
                // Merging exact duplicates is lossless; rounding is only done when asked for.
                config.OptimizePoint = true;
                config.PrecisionOpen = digits.HasValue;
                config.Precision = digits ?? 15;
                config.OptimizePointList = digits.HasValue;
                config.MergeOpen = mergeMode != "none";
                config.MergeOnlyResources = mergeMode == "resources";
                config.MergeReversedEntities = mergeMode == "reversed";

                long before = modelFile.Length;
                var produced = OptimizerProcessor.Process(modelFile.FullName, config).FilePaths.Single();
                var final = CommandHelpers.PlaceOutput(produced, result.GetValue(output));

                long after = new FileInfo(final).Length;
                writer.WriteLine(final);
                writer.WriteLine($"{before:N0} -> {after:N0} bytes ({(before == 0 ? 0 : 100.0 * (before - after) / before):0.#}% smaller)");
                return ExitCodes.Success;
            });
            return command;
        }
    }
}
