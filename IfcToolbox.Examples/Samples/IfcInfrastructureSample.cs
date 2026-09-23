using IfcToolbox.Core.Geo;
using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Core.Utilities;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using Serilog;
using System.Linq;
using Xbim.Ifc;

namespace IfcToolbox.Examples.Samples
{
    /// <summary>
    /// IFC 4.3 infrastructure models: facilities, facility parts and georeferencing.
    /// </summary>
    public class IfcInfrastructureSample
    {
        /// <summary>Lists the facilities and their parts, then writes one file per facility.</summary>
        public static void SplitByFacility(string filePath)
        {
            Log.Information("IfcInfrastructure - Start");
            string[] labels;
            using (var model = IfcStore.Open(filePath))
            {
                foreach (var facility in Facilities.AllFacilities(model))
                    Marslogger.Mark($"#{facility.EntityLabel} {facility.ExpressType.Name} '{facility.Name}'");
                foreach (var part in Facilities.AllFacilityParts(model))
                    Log.Information($"  part #{part.EntityLabel} {part.ExpressType.Name} {Facilities.UsageOf(part)} '{part.Name}'");
                labels = Facilities.AllFacilities(model).Select(f => f.EntityLabel.ToString()).ToArray();
            }

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.ByFacility;
            config.SelectedItems = labels;
            var result = SplitterProcessor.Process(filePath, config, true);
            foreach (var output in result.FilePaths)
                Marslogger.Mark($"Written {output}");
        }

        /// <summary>
        /// Reads the model's georeferencing and shows where its local origin lands on the map.
        /// </summary>
        public static void ShowGeoreferencing(string filePath)
        {
            using var model = IfcStore.Open(filePath);
            var map = MapConversionService.Read(model);
            if (map == null)
            {
                Marslogger.Mark("The model is not georeferenced with an IfcMapConversion.");
                return;
            }

            var (e, n, h) = MapConversionService.ToMap(map.Parameters, 0, 0, 0);
            Marslogger.Mark($"CRS {map.TargetCrs.Name}, rotation {MapConversionService.RotationDegrees(map.Parameters):0.###} deg, scale {map.Parameters.Scale ?? 1}");
            Marslogger.Mark($"Local origin is at E {e:0.###}, N {n:0.###}, H {h:0.###}");
        }

        /// <summary>Georeferences a model: here, UTM zone 32N with a model drawn in millimetres.</summary>
        public static void Georeference(string filePath)
        {
            var config = ConfigFactory.CreateConfigRelocate();
            config.Suffix = "Georeferenced";
            config.MapConversion = new GeoMapConversion
            {
                TargetCrs = new ProjectedCrs { Name = "EPSG:25832", Description = "ETRS89 / UTM zone 32N" },
                Parameters = new MapConversionParameters
                {
                    Eastings = 333000,
                    Northings = 5800000,
                    OrthogonalHeight = 35,
                    XAxisAbscissa = 1,
                    XAxisOrdinate = 0,
                    Scale = 0.001, // millimetres in the model, metres on the map
                },
            };
            var result = RelocatorProcessor.Process(filePath, config, true);
            Marslogger.Mark($"Written {result.FilePaths.Single()}");
        }
    }
}
