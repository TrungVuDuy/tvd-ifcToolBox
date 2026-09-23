using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Hierarchy
{
    public class TreesReader
    {
        public static SpatialTrees GetSpatialTrees(IModel model)
        {
            var result = new SpatialTrees();
            result.FullNodes.Add(HierarchyReader.GetFullHierarchy(model));
            result.TypedNodes.AddRange(HierarchyReader.GetTypedHierarchy(model).Children);
            result.SiteNodes.Add(HierarchyReader.GetSpatialHierarchy(model, "IfcSite"));
            result.BuildingNodes.Add(HierarchyReader.GetSpatialHierarchy(model, "IfcBuilding"));
            result.LevelNodes.Add(HierarchyReader.GetSpatialHierarchy(model, "IfcBuildingStorey"));

            // IFC 4.3 only; left empty for older schemas so the UI can hide the options.
            if (Facilities.AllFacilities(model).Any())
                result.FacilityNodes.Add(HierarchyReader.GetSpatialHierarchy(model, Facilities.IsFacility));
            if (Facilities.AllFacilityParts(model).Any())
                result.FacilityPartNodes.Add(HierarchyReader.GetSpatialHierarchy(model,
                    o => !(o is IIfcSpatialStructureElement)));
            return result;
        }

        public static SpatialTrees GetOnlyTypedNodes(IModel model, bool includeIfcSpace)
        {
            var result = new SpatialTrees();
            result.TypedNodes.AddRange(HierarchyReader.GetTypedHierarchy(model, includeIfcSpace).Children);
            return result;
        }
    }
}
