using IfcToolbox.Core.Bsdd;
using IfcToolbox.Core.Hierarchy;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// Exercises the bSDD client against the live service. These are integration tests on
    /// purpose: the point of the rewrite was that the old endpoints no longer exist, so a
    /// mocked test would prove nothing about whether the routes are right.
    /// Set IFCTOOLBOX_SKIP_NETWORK_TESTS=1 to skip them when offline.
    /// </summary>
    [Trait("Category", "Network")]
    public class BsddClientTests
    {
        // The IFC 4.3 dictionary, i.e. ISO 16739-1:2024.
        private const string IfcDictionaryUri = "https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3";
        private const string IfcWallUri = IfcDictionaryUri + "/class/IfcWall";

        private static bool SkipNetwork =>
            System.Environment.GetEnvironmentVariable("IFCTOOLBOX_SKIP_NETWORK_TESTS") == "1";

        [SkippableFact]
        public async Task DictionaryListContainsIfc()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient();

            var response = await client.GetDictionariesAsync(IfcDictionaryUri);

            var dictionary = Assert.Single(response.Dictionaries);
            Assert.Equal("ifc", dictionary.Code);
            Assert.Equal("4.3", dictionary.Version);
            Assert.NotEmpty(dictionary.AvailableLanguages);
        }

        [SkippableFact]
        public async Task GetClassReturnsIfcWall()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient();

            var bsddClass = await client.GetClassAsync(IfcWallUri);

            Assert.Equal("IfcWall", bsddClass.Code);
            Assert.Equal("Wall", bsddClass.Name);
            Assert.False(string.IsNullOrWhiteSpace(bsddClass.Definition));

            // parentClassReference and hierarchy are objects, not strings; guard the mapping.
            Assert.Equal("IfcBuiltElement", bsddClass.ParentClassReference.Code);
            Assert.Equal("IfcRoot", bsddClass.Hierarchy.First().Code);
            Assert.Equal("IfcWall", bsddClass.Hierarchy.Last().Code);
            Assert.Equal(bsddClass.Hierarchy.Count, bsddClass.Hierarchy.Last().Level);
        }

        [SkippableFact]
        public async Task DictionaryClassesArePagedToCompletion()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient(new BsddOptions { PageSize = 100 });

            var firstPage = await client.GetDictionaryClassesAsync(IfcDictionaryUri);
            Assert.True(firstPage.TotalCount > firstPage.Count, "expected the IFC dictionary to span several pages");

            // Walk far enough to cross a page boundary without pulling the whole schema.
            var seen = new System.Collections.Generic.List<string>();
            await foreach (var item in client.EnumerateDictionaryClassesAsync(IfcDictionaryUri))
            {
                seen.Add(item.Code);
                if (seen.Count >= 150)
                    break;
            }

            Assert.Equal(150, seen.Count);
            Assert.Equal(seen.Count, seen.Distinct().Count());
        }

        [SkippableFact]
        public async Task ClassPropertiesArePagedToCompletion()
        {
            Skip.If(SkipNetwork);
            // A small page size forces more than one round trip, which is what we want to cover:
            // IfcWall carries a couple of hundred properties in IFC 4.3.
            using var client = new BsddClient(new BsddOptions { PageSize = 50 });

            var firstPage = await client.GetClassPropertiesAsync(IfcWallUri);
            var all = await client.GetAllClassPropertiesAsync(IfcWallUri);

            Assert.True(firstPage.TotalCount > firstPage.Count, "expected more properties than fit on one page");
            Assert.Equal(firstPage.TotalCount, all.Count);
            Assert.All(all, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        }

        [SkippableFact]
        public async Task SearchFindsWallClasses()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient();

            var response = await client.SearchClassesAsync("wall", IfcDictionaryUri, limit: 10);

            Assert.NotEmpty(response.Classes);
            Assert.Contains(response.Classes, c => c.ReferenceCode == "IfcWall");
        }

        [SkippableFact]
        public async Task HierarchyGroupsPropertiesByPropertySet()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient();

            HierarchyNode node = await BsddHierarchy.BuildClassNodeAsync(client, IfcWallUri);

            Assert.Equal("IfcWall", node.Name);
            Assert.True(node.IsComposition);

            // Every child is a property set holding at least one property.
            Assert.NotEmpty(node.Children);
            Assert.All(node.Children, pset =>
            {
                Assert.True(pset.IsComposition);
                Assert.NotEmpty(pset.Children);
                Assert.All(pset.Children, p => Assert.False(p.IsComposition));
            });

            // Pset_WallCommon is the set the validator sample checks, so it has to be there.
            var wallCommon = node.Children.SingleOrDefault(c => c.Name == "Pset_WallCommon");
            Assert.NotNull(wallCommon);

            // Nodes must carry the property CODE ("FireRating"), not the bSDD display name
            // ("Fire Rating"): the validator compares these against IfcProperty.Name, so the
            // display name would make every check report "not found".
            Assert.Contains(wallCommon.Children, p => p.Name == "FireRating");
            Assert.Contains(wallCommon.Children, p => p.Name == "IsExternal");
            Assert.DoesNotContain(wallCommon.Children, p => p.Name == "Fire Rating");

            // IFC attributes are excluded by default: they are not in an IfcPropertySet,
            // so the validator could never find them in a model.
            Assert.DoesNotContain(node.Children, c => c.Name == BsddHierarchy.AttributesPropertySet);
        }

        [SkippableFact]
        public async Task AttributesCanBeIncludedExplicitly()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient();

            var node = await BsddHierarchy.BuildClassNodeAsync(client, IfcWallUri, includeAttributes: true);

            Assert.Contains(node.Children, c => c.Name == BsddHierarchy.AttributesPropertySet);
        }

        [SkippableFact]
        public async Task UnknownClassUriRaisesBsddException()
        {
            Skip.If(SkipNetwork);
            using var client = new BsddClient();

            var exception = await Assert.ThrowsAsync<BsddException>(
                () => client.GetClassAsync(IfcDictionaryUri + "/class/IfcDoesNotExist"));

            Assert.False(string.IsNullOrEmpty(exception.RequestUri));
        }
    }
}
