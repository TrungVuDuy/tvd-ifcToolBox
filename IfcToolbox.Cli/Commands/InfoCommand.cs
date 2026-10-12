using IfcToolbox.Core.Geo;
using IfcToolbox.Core.Hierarchy;
using System.CommandLine;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>ifctoolbox info model.ifc - what is in a model.</summary>
    public static class InfoCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var json = CommandHelpers.JsonOption();

            var command = new Command("info", "Show the schema, spatial structure, content and georeferencing of a model.");
            command.Arguments.Add(model);
            command.Options.Add(json);
            command.SetAction(result =>
            {
                var output = result.InvocationConfiguration.Output;
                var info = Read(result.GetValue(model));
                if (result.GetValue(json))
                    CommandHelpers.WriteJson(output, info);
                else
                    WriteText(output, info);
                return ExitCodes.Success;
            });
            return command;
        }

        public class ModelInfo
        {
            public string File { get; set; }
            public string Schema { get; set; }
            public long Instances { get; set; }
            public int Products { get; set; }
            public System.Collections.Generic.Dictionary<string, int> ProductsByClass { get; set; }
            public System.Collections.Generic.List<SpatialInfo> Spatial { get; set; }
            public GeoInfo Georeferencing { get; set; }
        }

        public class SpatialInfo
        {
            public int Label { get; set; }
            public string Class { get; set; }
            public string Name { get; set; }
            public string Usage { get; set; }
            public int Depth { get; set; }
            public int Elements { get; set; }
        }

        public class GeoInfo
        {
            public string Crs { get; set; }
            public double? Eastings { get; set; }
            public double? Northings { get; set; }
            public double? OrthogonalHeight { get; set; }
            public double RotationDegrees { get; set; }
            public double Scale { get; set; }
            public double? FactorX { get; set; }
            public double? FactorY { get; set; }
            public double? FactorZ { get; set; }
        }

        public static ModelInfo Read(FileInfo file)
        {
            using var model = IfcToolbox.Core.Utilities.ToolboxFiles.Open(file.FullName);
            var products = model.Instances.OfType<IIfcProduct>().ToList();

            var info = new ModelInfo
            {
                File = file.Name,
                Schema = model.Header.FileSchema.Schemas.FirstOrDefault() ?? model.SchemaVersion.ToString(),
                Instances = model.Instances.Count,
                Products = products.Count,
                ProductsByClass = products
                    .Where(p => !(p is IIfcSpatialStructureElement))
                    .GroupBy(p => p.ExpressType.Name)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.Count()),
                Spatial = new System.Collections.Generic.List<SpatialInfo>(),
            };

            var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            if (project != null)
                foreach (var child in project.IsDecomposedBy.SelectMany(r => r.RelatedObjects).OfType<IIfcSpatialStructureElement>())
                    Walk(child, 0, info);

            var map = MapConversionService.Read(model);
            if (map != null)
            {
                var p = map.Parameters;
                info.Georeferencing = new GeoInfo
                {
                    Crs = map.TargetCrs?.Name,
                    Eastings = p.Eastings,
                    Northings = p.Northings,
                    OrthogonalHeight = p.OrthogonalHeight,
                    RotationDegrees = System.Math.Round(MapConversionService.RotationDegrees(p), 9),
                    Scale = p.Scale ?? 1,
                    FactorX = p.FactorX,
                    FactorY = p.FactorY,
                    FactorZ = p.FactorZ,
                };
            }

            return info;
        }

        private static void Walk(IIfcSpatialStructureElement element, int depth, ModelInfo info)
        {
            info.Spatial.Add(new SpatialInfo
            {
                Label = element.EntityLabel,
                Class = element.ExpressType.Name,
                Name = element.Name,
                Usage = Facilities.IsFacilityPart(element) ? Facilities.UsageOf(element) : null,
                Depth = depth,
                Elements = element.ContainsElements.SelectMany(r => r.RelatedElements).Count(),
            });
            foreach (var child in element.IsDecomposedBy.SelectMany(r => r.RelatedObjects).OfType<IIfcSpatialStructureElement>())
                Walk(child, depth + 1, info);
        }

        private static void WriteText(TextWriter output, ModelInfo info)
        {
            output.WriteLine($"{info.File}");
            output.WriteLine($"  schema      {info.Schema}");
            output.WriteLine($"  instances   {info.Instances:N0}");
            output.WriteLine($"  products    {info.Products:N0}");
            output.WriteLine();

            output.WriteLine("Spatial structure (label, class, name):");
            foreach (var s in info.Spatial)
            {
                var usage = s.Usage == null ? "" : $" [{s.Usage}]";
                var elements = s.Elements == 0 ? "" : $"  ({s.Elements} elements)";
                output.WriteLine($"  {new string(' ', s.Depth * 2)}#{s.Label} {s.Class}{usage} '{s.Name}'{elements}");
            }
            output.WriteLine();

            output.WriteLine("Elements by class:");
            foreach (var pair in info.ProductsByClass.Take(20))
                output.WriteLine($"  {pair.Value,7:N0}  {pair.Key}");
            if (info.ProductsByClass.Count > 20)
                output.WriteLine($"  ... {info.ProductsByClass.Count - 20} more classes");
            output.WriteLine();

            if (info.Georeferencing == null)
            {
                output.WriteLine("Georeferencing: none (no IfcMapConversion)");
                return;
            }
            var g = info.Georeferencing;
            output.WriteLine($"Georeferencing: {g.Crs}");
            output.WriteLine($"  origin at E {g.Eastings}, N {g.Northings}, H {g.OrthogonalHeight}");
            output.WriteLine($"  rotation {g.RotationDegrees:0.######} deg, scale {g.Scale}");
            if (g.FactorX.HasValue)
                output.WriteLine($"  factors x {g.FactorX}, y {g.FactorY}, z {g.FactorZ}");
        }
    }
}
