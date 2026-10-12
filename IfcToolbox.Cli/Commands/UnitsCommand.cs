using IfcToolbox.Core.Editors;
using IfcToolbox.Core.Geo;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using Xbim.Ifc;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>
    /// ifctoolbox units model.ifc              - show the length unit
    /// ifctoolbox units model.ifc --to m       - convert, writing model_m.ifc
    /// </summary>
    public static class UnitsCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var to = new Option<string>("--to")
            {
                Description = "Convert the model's lengths to this unit and write a new file: " + string.Join(", ", LengthUnitConverter.Units.Keys) + ".",
            };
            to.AcceptOnlyFromAmong(LengthUnitConverter.Units.Keys.ToArray());
            var output = CommandHelpers.OutputOption("Output file when converting. Defaults to <model>_<unit>.ifc.");
            var json = CommandHelpers.JsonOption();

            var command = new Command("units", "Show a model's length unit, or convert every length in it to millimetres, centimetres or metres.");
            command.Arguments.Add(model);
            command.Options.Add(to);
            command.Options.Add(output);
            command.Options.Add(json);

            command.SetAction(result =>
            {
                var writer = result.InvocationConfiguration.Output;
                var modelFile = result.GetValue(model);
                var unit = result.GetValue(to);

                using var ifcModel = IfcStore.Open(modelFile.FullName);
                double metres = MapConversionService.LengthUnitInMetres(ifcModel);
                if (unit == null)
                {
                    if (result.GetValue(json))
                        CommandHelpers.WriteJson(writer, new { lengthUnitInMetres = metres, name = Name(metres) });
                    else
                        writer.WriteLine($"{Name(metres)} (1 unit = {metres} m)");
                    return ExitCodes.Success;
                }

                LengthUnitConversion conversion;
                using (var txn = ifcModel.BeginTransaction("Convert length unit"))
                {
                    conversion = LengthUnitConverter.Convert(ifcModel, unit);
                    txn.Commit();
                }
                var target = result.GetValue(output)?.FullName
                             ?? Path.Combine(modelFile.DirectoryName ?? ".", Path.GetFileNameWithoutExtension(modelFile.Name) + "_" + unit + ".ifc");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));
                ifcModel.SaveAs(target);

                if (result.GetValue(json))
                {
                    CommandHelpers.WriteJson(writer, new
                    {
                        output = target,
                        from = Name(conversion.FromMetres),
                        to = Name(conversion.ToMetres),
                        valuesScaled = conversion.ValuesScaled,
                        valuesWithOwnUnit = conversion.ValuesWithOwnUnit,
                    });
                }
                else
                {
                    writer.WriteLine(target);
                    writer.WriteLine($"{Name(conversion.FromMetres)} to {Name(conversion.ToMetres)}: {conversion.ValuesScaled:N0} lengths rescaled" +
                                     (conversion.ValuesWithOwnUnit > 0 ? $", {conversion.ValuesWithOwnUnit} with a unit of their own left as they are." : "."));
                }
                return ExitCodes.Success;
            });
            return command;
        }

        private static string Name(double metres)
        {
            foreach (var (name, factor) in new[] { ("millimetres", 0.001), ("centimetres", 0.01), ("metres", 1.0), ("feet", 0.3048), ("inches", 0.0254) })
                if (Math.Abs(metres - factor) < factor * 1e-9)
                    return name;
            return $"units of {metres} m";
        }
    }
}
