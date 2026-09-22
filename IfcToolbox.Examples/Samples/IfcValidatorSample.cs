using IfcToolbox.Core.Bsdd;
using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Tools.Processors;
using IfcToolbox.Tools.Helper;
using Serilog;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using IfcToolbox.Core.Validate;
using IfcToolbox.Core.Utilities;

namespace IfcToolbox.Examples.Samples
{
    public class IfcValidatorSample
    {
        /// <summary>
        /// Checks a model against a requirement set written out by hand.
        /// </summary>
        public static void ValidatePropertyExistence(string filePath)
        {
            Log.Information($"IfcValidator - Start");
            var nodes = SamplePropertyHierarchyNodes();
            Report(filePath, nodes);
        }

        /// <summary>
        /// Same check, but the requirements are pulled straight from the buildingSMART Data
        /// Dictionary instead of being typed out: pass the bSDD uri of each class to check.
        /// </summary>
        /// <example>
        /// ValidateAgainstBsddAsync(path,
        ///     "https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3/class/IfcWall");
        /// </example>
        public static async Task ValidateAgainstBsddAsync(string filePath, params string[] classUris)
        {
            Log.Information($"IfcValidator (bSDD) - Start");
            using var client = new BsddClient();
            var nodes = await BsddHierarchy.BuildClassNodesAsync(client, classUris);

            foreach (var node in nodes)
                Marslogger.Mark($"Requirements from bSDD: {node.Name} - {node.Children.Count} property sets");

            Report(filePath, nodes);
        }

        private static void Report(string filePath, IList<HierarchyNode> nodes)
        {
            var result = ValidatorProcessor.Process(filePath, nodes, true);
            var tables = result.Value as IList<DataTable>;
            foreach (var table in tables)
            {
                string tableCsvPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_" + table.TableName) + ".csv";
                table.SaveAsCsv(tableCsvPath);

                Marslogger.Mark($"Total entity found : {PropertyExistenceCount.GetEntitiesCount(table)}");
                Marslogger.Mark($"Property existence checked : {PropertyExistenceCount.GetPropertiesCount(table)}");
                Marslogger.Mark($"Existence percentage : {PropertyExistenceCount.GetExistencePercentage(table)}%");
            }
        }

        private static IList<HierarchyNode> SamplePropertyHierarchyNodes()
        {
            IList<HierarchyNode> nodes = new List<HierarchyNode>();

            var ifcDoor = new HierarchyNode("IfcDoor", true);
            var ifcDoorPset = new HierarchyNode("Pset_DoorCommon", true);
            ifcDoorPset.Children.Add(new HierarchyNode("FireExit"));
            ifcDoorPset.Children.Add(new HierarchyNode("SmokeStop"));
            ifcDoorPset.Children.Add(new HierarchyNode("IsExternal"));
            ifcDoorPset.Children.Add(new HierarchyNode("SecurityRating"));
            ifcDoor.Children.Add(ifcDoorPset);

            var ifcWall = new HierarchyNode("IfcWall", true);
            var ifcWallPset = new HierarchyNode("Pset_WallCommon", true);
            ifcWallPset.Children.Add(new HierarchyNode("FireRating"));
            ifcWallPset.Children.Add(new HierarchyNode("AcousticRating"));
            ifcWallPset.Children.Add(new HierarchyNode("IsExternal"));
            ifcWallPset.Children.Add(new HierarchyNode("Reference"));
            ifcWall.Children.Add(ifcWallPset);
            var ifcWallQto = new HierarchyNode("Qto_WallBaseQuantities", true);
            ifcWallQto.Children.Add(new HierarchyNode("Height"));
            ifcWallQto.Children.Add(new HierarchyNode("Length"));
            ifcWallQto.Children.Add(new HierarchyNode("Width"));
            ifcWall.Children.Add(ifcWallQto);

            nodes.Add(ifcDoor);
            nodes.Add(ifcWall);
            return nodes;
        }
    }
}
