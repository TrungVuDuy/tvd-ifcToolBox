using IfcToolbox.Core.Analyse;
using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Editors
{
    /// <summary>
    /// Only when ByObjectType, SelectedItems represent the name of object type.
    /// In other cases SelectedItems represent the entity lable.
    /// ByBuildingStorey, ByBuilding, BySite create suffix with entity name, remove invalid chars for fileName included.
    /// </summary>
    public class SubModelGeneration
    {
        #region Split Strategies
        /// <summary>
        /// Not include any IfcSpatialStructureElement, like IfcSpace, IfcBuilding, IfcSite, etc.
        /// </summary>
        public static List<string> SplitByObjectType(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> objectTypes, string suffix = "ByObjectType")
        {
            var generatedFilePath = ConsoleFile.AddSuffixToName(sourceFilePath, "_" + suffix);
            var requiredProducts = model.Instances.OfType<IIfcProduct>()
                .Where(x => !(x is IIfcSpatialStructureElement))
                .Where(x => objectTypes.Contains(x.ObjectType.Value.Value));
            InsertCopy.CopyProducts(model, generatedFilePath, requiredProducts, keepLable);
            return new List<string> { generatedFilePath };
        }
        /// <summary>
        /// Generate sub-model only contain specific products in the IFC hierarchy.
        /// </summary>
        public static List<string> SplitByProduct(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> entitiyLables, string suffix = "ByProduct")
        {
            var generatedFilePath = ConsoleFile.AddSuffixToName(sourceFilePath, "_" + suffix);
            var requiredProducts = ProductAnalyse.PrepareRequiredProducts(model, entitiyLables);
            InsertCopy.CopyProducts(model, generatedFilePath, requiredProducts, keepLable);
            return new List<string> { generatedFilePath };
        }
        /// <summary>
        /// Exclude all geometry information of IFC file.
        /// </summary>
        public static List<string> SplitDataOnly(IfcStore model, bool keepLable, string sourceFilePath, string suffix = "DataOnly")
        {
            var generatedFilePath = ConsoleFile.AddSuffixToName(sourceFilePath, "_" + suffix);
            InsertCopy.CopyOnlyProperties(model, generatedFilePath, keepLable);
            return new List<string> { generatedFilePath };
        }

        /// <summary>
        /// Only generate file when have IfcBuildingStorey entity.
        /// </summary>
        public static List<string> SplitByBuildingStorey(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> entitiyLables, string placeholder = "Level", string parentPlaceholder = "Building")
        {
            var newPaths = new List<string>();
            var buildings = model.Instances.OfType<IIfcBuilding>().ToList();
            for (int j = 0; j < buildings.Count(); j++)
            {
                var buildingStoreys = buildings[j].BuildingStoreys
                    .Where(x => entitiyLables.Contains(x.EntityLabel.ToString()))
                    .ToList();
                for (int i = 0; i < buildingStoreys.Count(); i++)
                {
                    string suffix = ConsoleFile.CreateIndexedSuffix(buildingStoreys[i].Name, buildingStoreys.Count() > 1, i, placeholder,
                        true, buildings[j].Name, buildings.Count() > 1, j, parentPlaceholder);
                    var generatedFilePath = ConsoleFile.AddSuffixToName(sourceFilePath, suffix);
                    var requiredProducts = ProductAnalyse.PrepareRequiredProducts(model, buildingStoreys[i]);
                    if (requiredProducts.Count == 0)
                        continue;
                    InsertCopy.CopyProducts(model, generatedFilePath, requiredProducts, keepLable);
                    newPaths.Add(generatedFilePath);
                }
            }
            return newPaths;
        }
        /// <summary>
        /// Only generate file when have IfcBuilding entity.
        /// </summary>
        public static List<string> SplitByBuilding(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> entitiyLables, string placeholder = "Building")
        {
            var buildings = model.Instances.OfType<IIfcBuilding>()
                .Where(x => entitiyLables.Contains(x.EntityLabel.ToString()))
                .ToList();
            var roots = new List<IIfcRoot>();
            foreach (var building in buildings)
                if (building is IIfcRoot root)
                    roots.Add(root);
            return SplitByIfcRoot(model, keepLable, sourceFilePath, roots, placeholder);
        }
        /// <summary>
        /// Only generate file when have IfcSite entity.
        /// </summary>
        public static List<string> SplitBySite(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> entitiyLables, string placeholder = "Site")
        {
            // Every site, not only those directly under the project: IFC 4.3 models commonly
            // nest sites (an "environment" site holding a site per structure), and
            // IfcProject.Sites only reaches the first level.
            var roots = model.Instances.OfType<IIfcSite>()
                .Where(site => entitiyLables.Contains(site.EntityLabel.ToString()))
                .Cast<IIfcRoot>()
                .ToList();
            return SplitByIfcRoot(model, keepLable, sourceFilePath, roots, placeholder);
        }

        /// <summary>
        /// One file per selected IFC 4.3 facility: a road, railway, bridge, marine facility or
        /// building, with everything aggregated into it and contained in it.
        /// </summary>
        public static List<string> SplitByFacility(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> entitiyLables, string placeholder = "Facility")
        {
            var roots = Facilities.AllFacilities(model)
                .Where(f => entitiyLables.Contains(f.EntityLabel.ToString()))
                .Cast<IIfcRoot>()
                .ToList();
            return SplitByIfcRoot(model, keepLable, sourceFilePath, roots, placeholder);
        }

        /// <summary>
        /// One file per selected IFC 4.3 facility part, e.g. a single carriageway or track.
        /// Nested parts come along with their parent.
        /// </summary>
        public static List<string> SplitByFacilityPart(IfcStore model, bool keepLable, string sourceFilePath, IEnumerable<string> entitiyLables, string placeholder = "Part")
        {
            var roots = Facilities.AllFacilityParts(model)
                .Where(p => entitiyLables.Contains(p.EntityLabel.ToString()))
                .Cast<IIfcRoot>()
                .ToList();
            return SplitByIfcRoot(model, keepLable, sourceFilePath, roots, placeholder);
        }

        /// <summary>
        /// Sub method for IfcBuilding, IfcSite and the IFC 4.3 facilities. Elements that contain
        /// nothing are skipped, so fewer paths than roots can come back.
        /// </summary>
        private static List<string> SplitByIfcRoot(IfcStore model, bool keepLable, string sourceFilePath, IList<IIfcRoot> roots, string placeholder)
        {
            var newPaths = new List<string>();
            for (int i = 0; i < roots.Count(); i++)
            {
                var suffix = ConsoleFile.CreateIndexedSuffix(roots[i].Name, roots.Count() > 1, i, placeholder);
                var generatedFilePath = ConsoleFile.AddSuffixToName(sourceFilePath, suffix);
                var requiredProducts = ProductAnalyse.PrepareRequiredProducts(model, roots[i]);
                // Nothing is contained in it (an empty shoulder, a site with no elements): the copy
                // would be a file with an empty DATA section, not even the project. Leave it out;
                // the caller sees it missing from the returned paths.
                if (requiredProducts.Count == 0)
                    continue;
                InsertCopy.CopyProducts(model, generatedFilePath, requiredProducts, keepLable);
                newPaths.Add(generatedFilePath);
            }
            return newPaths;
        }
        #endregion
    }
}
