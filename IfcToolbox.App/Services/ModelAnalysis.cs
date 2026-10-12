using IfcToolbox.Core.Hierarchy;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.App.Services
{
    /// <summary>What the Splitter shows for a model, read once in the background.</summary>
    public sealed class SplitAnalysis
    {
        public SpatialTrees Trees { get; init; }
        public string Schema { get; init; }

        /// <summary>Entity labels of the IFC 4.3 facility parts, with their usage (CARRIAGEWAY, ...).</summary>
        public Dictionary<string, string> FacilityParts { get; init; } = new();

        public int Sites { get; init; }
        public int Buildings { get; init; }
        public int Storeys { get; init; }
        public int Facilities { get; init; }
        public int ObjectTypes { get; init; }

        public static SplitAnalysis Read(string path)
        {
            using var model = IfcToolbox.Core.Utilities.ToolboxFiles.Open(path);
            var products = model.Instances.OfType<IIfcProduct>().ToList();
            return new SplitAnalysis
            {
                Trees = TreesReader.GetSpatialTrees(model),
                Schema = model.SchemaVersion.ToString(),
                FacilityParts = Core.Hierarchy.Facilities.AllFacilityParts(model)
                    .ToDictionary(p => p.EntityLabel.ToString(), p => Core.Hierarchy.Facilities.UsageOf(p)),
                Sites = products.OfType<IIfcSite>().Count(),
                Buildings = products.OfType<IIfcBuilding>().Count(),
                Storeys = products.OfType<IIfcBuildingStorey>().Count(),
                Facilities = Core.Hierarchy.Facilities.AllFacilities(model).Count(),
                ObjectTypes = products.Where(p => p is not IIfcSpatialStructureElement && p.ObjectType.HasValue)
                    .Select(p => p.ObjectType.Value.ToString()).Distinct().Count(),
            };
        }
    }

    /// <summary>Where a model sits: what the Relocator shows and changes.</summary>
    public sealed class RelocateAnalysis
    {
        public string Schema { get; init; }
        public bool IsIfc2x3 { get; init; }
        public bool IsIfc4x3 { get; init; }

        /// <summary>Label of the IfcProject, whose model context holds the world coordinate system.</summary>
        public int ProjectLabel { get; init; }
        /// <summary>World coordinate system of the 3D model context; null when there is none.</summary>
        public Core.Geo.IGeoPlacement World { get; init; }
        /// <summary>True north in the world coordinate system, (x, y); null when not given.</summary>
        public (double X, double Y)? TrueNorth { get; init; }

        /// <summary>Sites directly under the project, the project coordinate systems the Relocator moves.</summary>
        public List<(int Label, string Name, Core.Geo.IGeoPlacement Placement)> Sites { get; init; } = new();

        public Core.Geo.GeoMapConversion MapConversion { get; init; }

        /// <summary>One model length unit in metres, e.g. 0.001.</summary>
        public double UnitInMetres { get; init; }

        public string UnitName
        {
            get
            {
                // Unit factors come out of a conversion, so 0.001 may be 0.0010000000000000002.
                foreach (var (factor, name) in new[] { (0.001, "mm"), (0.01, "cm"), (1.0, "m"), (0.3048, "ft"), (0.0254, "in") })
                    if (System.Math.Abs(UnitInMetres - factor) < factor * 1e-9)
                        return name;
                return $"× {UnitInMetres:G6} m";
            }
        }

        public static RelocateAnalysis Read(string path)
        {
            using var model = IfcToolbox.Core.Utilities.ToolboxFiles.Open(path);
            var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            // The same context the alignment moves: the one 3D "Model" context.
            var context = project?.RepresentationContexts.OfType<IIfcGeometricRepresentationContext>()
                .Where(c => c is not IIfcGeometricRepresentationSubContext)
                .FirstOrDefault(c => c.ContextType == "Model");
            var north = context?.TrueNorth?.DirectionRatios;

            var sites = (project?.IsDecomposedBy.SelectMany(r => r.RelatedObjects).OfType<IIfcSite>() ?? Enumerable.Empty<IIfcSite>())
                .Where(s => s.ObjectPlacement is IIfcLocalPlacement)
                .Select(s => (s.EntityLabel, s.Name?.ToString() ?? "Site",
                    Core.Geo.GeoFactory.CreateGeoPlacement(((IIfcLocalPlacement)s.ObjectPlacement).RelativePlacement)))
                .ToList();

            return new RelocateAnalysis
            {
                Schema = model.SchemaVersion.ToString(),
                IsIfc2x3 = model.SchemaVersion == Xbim.Common.Step21.XbimSchemaVersion.Ifc2X3,
                IsIfc4x3 = model.SchemaVersion == Xbim.Common.Step21.XbimSchemaVersion.Ifc4x3,
                ProjectLabel = project?.EntityLabel ?? 0,
                World = context?.WorldCoordinateSystem == null ? null : Core.Geo.GeoFactory.CreateGeoPlacement(context.WorldCoordinateSystem),
                TrueNorth = north == null || north.Count < 2 ? null : (north[0], north[1]),
                Sites = sites,
                MapConversion = model.SchemaVersion == Xbim.Common.Step21.XbimSchemaVersion.Ifc2X3 ? null : Core.Geo.MapConversionService.Read(model),
                UnitInMetres = Core.Geo.MapConversionService.LengthUnitInMetres(model),
            };
        }
    }

    /// <summary>IFC classes of a model's products, for the Converter's type filter.</summary>
    public static class ConvertAnalysis
    {
        /// <summary>Class name to count, spaces included, spatial containers (site, storey, ...) left out.</summary>
        public static Dictionary<string, int> ReadClasses(string path)
        {
            using var model = IfcToolbox.Core.Utilities.ToolboxFiles.Open(path);
            return model.Instances.OfType<IIfcProduct>()
                .Where(p => p is IIfcSpace || p is not IIfcSpatialStructureElement)
                .GroupBy(p => p.ExpressType.Name)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
