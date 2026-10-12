using IfcToolbox.Core.Editors;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>ifctoolbox combine architecture.ifc structure.ifc mep.ifc -o building.ifc</summary>
    public static class CombineCommand
    {
        public static Command Create()
        {
            var models = new Argument<FileInfo[]>("models")
            {
                Description = "The IFC models to combine, two or more, of the same schema and length unit.",
                Arity = new ArgumentArity(2, ArgumentArity.ZeroOrMore.MaximumNumberOfValues),
            }.AcceptExistingOnly();
            var output = CommandHelpers.OutputOption("The combined model. Defaults to <first model>_Combined.ifc.");
            var noNames = new Option<bool>("--no-name-matching")
            {
                Description = "Merge sites, buildings and storeys only when they share a GlobalId, not when they share a name.",
            };
            var keepDuplicates = new Option<bool>("--keep-duplicates")
            {
                Description = "Copy elements whose GlobalId came in from an earlier file again, with a new GlobalId, instead of once.",
            };
            var noUnits = new Option<bool>("--no-unit-conversion")
            {
                Description = "Refuse files in another length unit instead of converting them to the first file's.",
            };
            var json = CommandHelpers.JsonOption();

            var command = new Command("combine",
                "Combine IFC models into one, with one project. Sites, buildings and storeys that are the same, and placed alike, are merged.");
            command.Arguments.Add(models);
            command.Options.Add(output);
            command.Options.Add(noNames);
            command.Options.Add(keepDuplicates);
            command.Options.Add(noUnits);
            command.Options.Add(json);

            command.SetAction(result =>
            {
                var writer = result.InvocationConfiguration.Output;
                var files = result.GetValue(models);
                var target = result.GetValue(output)?.FullName
                             ?? Path.Combine(files[0].DirectoryName ?? ".", Path.GetFileNameWithoutExtension(files[0].Name) + "_Combined.ifc");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));

                CombineResult combined;
                try
                {
                    combined = ModelCombiner.Combine(files.Select(f => f.FullName).ToList(), target, new CombineOptions
                    {
                        MergeSpatialByName = !result.GetValue(noNames),
                        SkipDuplicateElements = !result.GetValue(keepDuplicates),
                        ConvertUnits = !result.GetValue(noUnits),
                    });
                }
                catch (CombineException e)
                {
                    // Not something to retry with other options: the inputs do not fit together.
                    throw new CliUsageException(e.Message);
                }

                if (result.GetValue(json))
                {
                    CommandHelpers.WriteJson(writer, new
                    {
                        output = combined.OutputPath,
                        elementsCopied = combined.ElementsCopied,
                        elementsSkipped = combined.ElementsSkipped,
                        skippedByClass = combined.SkippedByClass,
                        spatialMerged = combined.SpatialMerged,
                        spatialKeptApart = combined.SpatialKeptApart,
                        typesMerged = combined.TypesMerged,
                        unitsConverted = combined.UnitsConverted,
                        renewedByClass = combined.RenewedByClass,
                        globalIdsRenewed = combined.GlobalIdsRenewed,
                        warnings = combined.Warnings,
                    });
                    return ExitCodes.Success;
                }

                writer.WriteLine(combined.OutputPath);
                writer.WriteLine($"{combined.ElementsCopied} elements from {files.Length} files" +
                                 (combined.ElementsSkipped > 0 ? $"; {combined.ElementsSkipped} already in an earlier file were copied once ({string.Join(", ", combined.SkippedByClass.Select(c => $"{c.Key} {c.Value}"))})" : "") + ".");
                foreach (var converted in combined.UnitsConverted)
                    writer.WriteLine("lengths converted: " + converted);
                if (combined.SpatialMerged.Count > 0)
                    writer.WriteLine("merged: " + string.Join(", ", combined.SpatialMerged));
                if (combined.SpatialKeptApart.Count > 0)
                    writer.WriteLine("kept apart, placed differently: " + string.Join(", ", combined.SpatialKeptApart));
                if (combined.TypesMerged.Count > 0)
                    writer.WriteLine("types merged: " + string.Join(", ", combined.TypesMerged));
                if (combined.GlobalIdsRenewed > 0)
                    writer.WriteLine($"{combined.GlobalIdsRenewed} duplicate GlobalIds renewed (" + string.Join(", ", combined.RenewedByClass.Select(c => $"{c.Key} {c.Value}")) + ").");
                foreach (var warning in combined.Warnings)
                    writer.WriteLine("warning: " + warning);
                return ExitCodes.Success;
            });
            return command;
        }
    }
}
