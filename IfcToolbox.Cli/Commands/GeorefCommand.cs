using IfcToolbox.Core.Geo;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>
    /// ifctoolbox georef model.ifc                                  - show
    /// ifctoolbox georef model.ifc --crs EPSG:25832 --eastings ...  - set
    /// ifctoolbox georef model.ifc --to-map 0 0 0                   - transform a point
    /// </summary>
    public static class GeorefCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var crs = new Option<string>("--crs") { Description = "Projected CRS to georeference to, normally an EPSG code, e.g. EPSG:25832. Setting it writes a new model." };
            var eastings = new Option<double?>("--eastings", "-e") { Description = "Map easting of the local origin." };
            var northings = new Option<double?>("--northings", "-n") { Description = "Map northing of the local origin." };
            var height = new Option<double>("--height") { Description = "Orthogonal height of the local origin. Default 0." };
            var rotation = new Option<double>("--rotation") { Description = "Anti-clockwise angle from map east to the local x axis, in degrees. Default 0." };
            var scale = new Option<double?>("--scale")
            {
                Description = "Model unit to map unit. Defaults to what the model's length unit implies for a map in metres, e.g. 0.001 for millimetres.",
            };
            var factor = new Option<double?>("--factor") { Description = "Combined scale factor for x and y (IFC 4.3 only, written as IfcMapConversionScaled)." };
            var toMap = new Option<double[]>("--to-map")
            {
                Description = "Print the map coordinates of a local point: x y z, in model units.",
                AllowMultipleArgumentsPerToken = true,
                Arity = new ArgumentArity(3, 3),
            };
            var output = CommandHelpers.OutputOption("Output file when setting georeferencing. Defaults to <model>_Georeferenced.ifc.");
            var json = CommandHelpers.JsonOption();

            var command = new Command("georef", "Show, set or apply a model's georeferencing (projected CRS and IfcMapConversion).");
            command.Arguments.Add(model);
            foreach (var option in new Option[] { crs, eastings, northings, height, rotation, scale, factor, toMap, output, json })
                command.Options.Add(option);

            command.SetAction(result =>
            {
                var writer = result.InvocationConfiguration.Output;
                var modelFile = result.GetValue(model);

                if (result.GetValue(crs) == null)
                {
                    // System.CommandLine hands back an empty array, not null, for an array
                    // option that was not given.
                    var point = result.GetValue(toMap);
                    return Show(writer, modelFile, point == null || point.Length == 0 ? null : point, result.GetValue(json));
                }

                if (!result.GetValue(eastings).HasValue || !result.GetValue(northings).HasValue)
                    throw new CliUsageException("--crs needs --eastings and --northings.");

                double radians = result.GetValue(rotation) * Math.PI / 180;
                double appliedScale = result.GetValue(scale) ?? DefaultScale(modelFile);
                var conversion = new GeoMapConversion
                {
                    TargetCrs = new ProjectedCrs { Name = result.GetValue(crs) },
                    Parameters = new MapConversionParameters
                    {
                        Eastings = result.GetValue(eastings),
                        Northings = result.GetValue(northings),
                        OrthogonalHeight = result.GetValue(height),
                        XAxisAbscissa = Math.Cos(radians),
                        XAxisOrdinate = Math.Sin(radians),
                        Scale = appliedScale,
                        FactorX = result.GetValue(factor),
                        FactorY = result.GetValue(factor),
                    },
                };

                var config = ConfigFactory.CreateConfigRelocate();
                config.Suffix = "Georeferenced";
                config.MapConversion = conversion;
                var produced = RelocatorProcessor.Process(modelFile.FullName, config).FilePaths.Single();
                writer.WriteLine(CommandHelpers.PlaceOutput(produced, result.GetValue(output)));
                if (!result.GetValue(scale).HasValue)
                    writer.WriteLine($"scale {appliedScale} taken from the model's length unit, assuming the CRS is in metres; pass --scale otherwise.");
                return ExitCodes.Success;
            });
            return command;
        }

        private static int Show(TextWriter writer, FileInfo modelFile, double[] point, bool json)
        {
            using var ifcModel = IfcToolbox.Core.Utilities.ToolboxFiles.Open(modelFile.FullName);
            var map = MapConversionService.Read(ifcModel);
            if (map == null)
            {
                if (json)
                    CommandHelpers.WriteJson(writer, new { georeferenced = false });
                else
                    writer.WriteLine("The model is not georeferenced with an IfcMapConversion.");
                // Asking to transform a point without a conversion to apply is a failure.
                return point == null ? ExitCodes.Success : ExitCodes.Error;
            }

            var p = map.Parameters;
            (double E, double N, double H)? mapped = point == null ? null : MapConversionService.ToMap(p, point[0], point[1], point[2]);

            if (json)
            {
                CommandHelpers.WriteJson(writer, new
                {
                    georeferenced = true,
                    crs = map.TargetCrs?.Name,
                    eastings = p.Eastings,
                    northings = p.Northings,
                    orthogonalHeight = p.OrthogonalHeight,
                    rotationDegrees = Math.Round(MapConversionService.RotationDegrees(p), 9),
                    scale = p.Scale ?? 1,
                    factorX = p.FactorX,
                    factorY = p.FactorY,
                    factorZ = p.FactorZ,
                    point = mapped == null ? null : new { easting = mapped.Value.E, northing = mapped.Value.N, height = mapped.Value.H },
                });
                return ExitCodes.Success;
            }

            if (mapped != null)
            {
                writer.WriteLine(FormattableString.Invariant($"{mapped.Value.E:0.######} {mapped.Value.N:0.######} {mapped.Value.H:0.######}"));
                return ExitCodes.Success;
            }

            writer.WriteLine($"CRS       {map.TargetCrs?.Name}");
            writer.WriteLine($"origin    E {p.Eastings}, N {p.Northings}, H {p.OrthogonalHeight}");
            writer.WriteLine($"rotation  {MapConversionService.RotationDegrees(p):0.######} deg");
            writer.WriteLine($"scale     {p.Scale ?? 1}");
            if (p.FactorX.HasValue)
                writer.WriteLine($"factors   x {p.FactorX}, y {p.FactorY}, z {p.FactorZ}");
            return ExitCodes.Success;
        }

        /// <summary>
        /// Model length unit expressed in metres, which is what IfcMapConversion.Scale has to
        /// be for the common case of a projected CRS in metres.
        /// </summary>
        private static double DefaultScale(FileInfo modelFile)
        {
            using var ifcModel = IfcToolbox.Core.Utilities.ToolboxFiles.Open(modelFile.FullName);
            return MapConversionService.LengthUnitInMetres(ifcModel);
        }
    }
}
