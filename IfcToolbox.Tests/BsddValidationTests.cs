using IfcToolbox.Core.Bsdd;
using IfcToolbox.Core.Validate;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// End to end check of the chain that the bSDD rewrite exists for:
    /// bSDD class -> HierarchyNode tree -> validator -> existence table.
    /// Before this, the node tree had to be written out by hand in code.
    /// </summary>
    [Trait("Category", "Network")]
    public class BsddValidationTests
    {
        private const string IfcWallUri =
            "https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3/class/IfcWall";

        private static bool SkipNetwork =>
            System.Environment.GetEnvironmentVariable("IFCTOOLBOX_SKIP_NETWORK_TESTS") == "1";

        [SkippableFact]
        public async Task BsddRequirementsFindPropertiesInModel()
        {
            Skip.If(SkipNetwork);

            using var client = new BsddClient();
            var nodes = await BsddHierarchy.BuildClassNodesAsync(client, new[] { IfcWallUri });

            // This sample deliberately has FireRating filled in, so a bSDD driven check of
            // Pset_WallCommon must report it as present.
            var result = ValidatorProcessor.Process(LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, nodes);

            Assert.True(result.Success);
            var tables = Assert.IsAssignableFrom<IList<DataTable>>(result.Value);
            var wallTable = Assert.Single(tables);
            Assert.Equal("IfcWall", wallTable.TableName);
            Assert.True(PropertyExistenceCount.GetEntitiesCount(wallTable) > 0, "no IfcWall rows were produced");

            // Columns are flattened to "Pset.Property" by the validator.
            Assert.Contains("Pset_WallCommon.FireRating", wallTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName));

            int found = wallTable.Rows.Cast<DataRow>()
                .Count(r => !string.IsNullOrEmpty(r["Pset_WallCommon.FireRating"].ToString()));
            Assert.True(found > 0, "FireRating was not detected on any wall, the bSDD property names are probably wrong");
        }
    }
}
