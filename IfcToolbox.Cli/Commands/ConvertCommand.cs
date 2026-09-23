using IfcToolbox.Core.Convert;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>ifctoolbox convert model.ifc --format glb</summary>
    public static class ConvertCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var format = new Option<string>("--format", "-f")
            {
                Description = "Target format: " + string.Join(", ", Enum.GetNames(typeof(ConvertTargetFormat)).Select(n => n.ToLowerInvariant())) +
                              ". json, ttl and h5 need IfcConvert 0.8 (see IFCTOOLBOX_IFCCONVERT).",
                Required = true,
            };
            var output = CommandHelpers.OutputOption("Output file. Defaults to the model's name with the format's extension.");
            var worldCoords = new Option<bool>("--world-coords") { Description = "Apply placements to the geometry instead of keeping them as transforms." };
            var center = new Option<bool>("--center") { Description = "Move the model so its placements are centred on the origin." };
            var guids = new Option<bool>("--element-guids") { Description = "Name elements by GlobalId (OBJ, DAE, SVG, STP)." };
            var exclude = new Option<string>("--exclude")
            {
                Description = "Space separated IFC classes to leave out. Defaults to openings, and spaces for 3D formats.",
            };

            var command = new Command("convert", "Convert a model to glTF, OBJ, DAE, STEP, IGES, SVG, XML or IFC with IfcConvert.");
            command.Arguments.Add(model);
            command.Options.Add(format);
            command.Options.Add(output);
            command.Options.Add(worldCoords);
            command.Options.Add(center);
            command.Options.Add(guids);
            command.Options.Add(exclude);

            command.SetAction(result =>
            {
                var writer = result.InvocationConfiguration.Output;
                var modelFile = result.GetValue(model);

                var formatName = result.GetValue(format);
                if (!Enum.TryParse<ConvertTargetFormat>(formatName, ignoreCase: true, out var targetFormat))
                    throw new CliUsageException($"Unknown format '{formatName}'. Use one of: " +
                        string.Join(", ", Enum.GetNames(typeof(ConvertTargetFormat)).Select(n => n.ToLowerInvariant())) + ".");

                var target = result.GetValue(output)?.FullName
                             ?? Path.ChangeExtension(modelFile.FullName, ConvertFormatInfo.GetExtension(targetFormat));
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                var options = new ConvertOptionsWrap
                {
                    UseWorldCoords = result.GetValue(worldCoords),
                    CenterModel = result.GetValue(center),
                    UseElementGuids = result.GetValue(guids),
                    ExcludeEntities = result.GetValue(exclude) ?? IfcConvert.GetExcludeEntitiesDefault(targetFormat),
                };

                var logFile = Path.ChangeExtension(target, ".log.txt");
                var produced = IfcConvert.Convert(modelFile.FullName, target, options, targetFormat, logFile).ToList();

                foreach (var file in produced)
                    writer.WriteLine(file);
                return ExitCodes.Success;
            });
            return command;
        }
    }
}
