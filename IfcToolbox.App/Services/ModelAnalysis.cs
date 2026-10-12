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
            using var model = IfcStore.Open(path);
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

    /// <summary>IFC classes of a model's products, for the Converter's type filter.</summary>
    public static class ConvertAnalysis
    {
        /// <summary>Class name to count, spaces included, spatial containers (site, storey, ...) left out.</summary>
        public static Dictionary<string, int> ReadClasses(string path)
        {
            using var model = IfcStore.Open(path);
            return model.Instances.OfType<IIfcProduct>()
                .Where(p => p is IIfcSpace || p is not IIfcSpatialStructureElement)
                .GroupBy(p => p.ExpressType.Name)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
