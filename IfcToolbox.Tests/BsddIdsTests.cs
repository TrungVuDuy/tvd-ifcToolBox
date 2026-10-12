using IfcToolbox.Core.Bsdd;
using IfcToolbox.Core.Bsdd.Models;
using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>bSDD classes and properties turned into IDS specifications.</summary>
    public class BsddIdsTests
    {
        private const string Ifc43 = "https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3";

        [Theory]
        [InlineData("IfcWall", "IFCWALL", null)]
        [InlineData("IfcWallSOLIDWALL", "IFCWALL", "SOLIDWALL")]
        [InlineData("IfcBuildingElementProxyUSERDEFINED", "IFCBUILDINGELEMENTPROXY", "USERDEFINED")]
        [InlineData("IfcFlowTerminal", "IFCFLOWTERMINAL", null)]
        public void ParsesIfcClassCodes(string code, string entity, string predefinedType)
        {
            var parsed = BsddIds.ParseIfcClassCode(code);
            Assert.NotNull(parsed);
            Assert.Equal(entity, parsed.Value.Entity);
            Assert.Equal(predefinedType, parsed.Value.PredefinedType);
        }

        [Theory]
        [InlineData("21.21")]
        [InlineData("Wall")]
        [InlineData("")]
        public void RejectsCodesThatAreNotIfcClasses(string code)
        {
            Assert.Null(BsddIds.ParseIfcClassCode(code));
        }

        [Fact]
        public void IfcDictionaryClassAppliesToTheEntity()
        {
            var specification = BsddIds.BuildSpecification(IfcWallSolidWall(), WallProperties());

            Assert.Equal("IFCWALL", specification.Applicability.Entity.Name.SimpleValue);
            Assert.Equal("SOLIDWALL", specification.Applicability.Entity.PredefinedType.SimpleValue);
            Assert.Empty(specification.Applicability.Classifications);
            // Not narrowed to IFC 4.3 although the class comes from the 4.3 dictionary.
            Assert.Contains(IdsIfcVersion.Ifc4, specification.IfcVersions);
        }

        [Fact]
        public void OtherDictionariesApplyThroughTheClassification()
        {
            var bsddClass = new BsddClass
            {
                Uri = "https://identifier.buildingsmart.org/uri/nlsfb/nlsfb2005/2.2/class/21.21",
                DictionaryUri = "https://identifier.buildingsmart.org/uri/nlsfb/nlsfb2005/2.2",
                Code = "21.21",
                Name = "buitenwanden; massieve constructies",
            };

            var specification = BsddIds.BuildSpecification(bsddClass, WallProperties(), dictionaryName: "NL-SfB 2005");

            Assert.Null(specification.Applicability.Entity);
            var classification = Assert.Single(specification.Applicability.Classifications);
            Assert.Equal("NL-SfB 2005", classification.System.SimpleValue);
            Assert.Equal("21.21", classification.Value.SimpleValue);
            Assert.Equal(bsddClass.Uri, classification.Uri);
        }

        [Fact]
        public void PropertiesBecomeRequirementsAndAttributesBecomeAttributeFacets()
        {
            var specification = BsddIds.BuildSpecification(IfcWallSolidWall(), WallProperties());

            // Duplicate FireRating and the property without a set are dropped.
            Assert.Equal(new[] { "FireRating", "IsExternal" },
                specification.Requirements.Properties.Select(p => p.BaseName.SimpleValue).OrderBy(n => n));
            Assert.All(specification.Requirements.Properties, p => Assert.Equal("Pset_WallCommon", p.PropertySet.SimpleValue));
            Assert.All(specification.Requirements.Properties, p => Assert.Null(p.Value));

            var attribute = Assert.Single(specification.Requirements.Attributes);
            Assert.Equal("Description", attribute.Name.SimpleValue);
        }

        [Fact]
        public void AllowedValuesAreCheckedOnlyWhenAsked()
        {
            var properties = WallProperties();
            properties.Add(new BsddClassProperty
            {
                PropertySet = "Pset_WallCommon",
                PropertyCode = "Status",
                AllowedValues = new List<BsddAllowedValue> { new() { Code = "NEW", Value = "New" }, new() { Code = "EXISTING", Value = "Existing" } },
            });

            var lenient = BsddIds.BuildSpecification(IfcWallSolidWall(), properties);
            Assert.Null(lenient.Requirements.Properties.Single(p => p.BaseName.SimpleValue == "Status").Value);

            var strict = BsddIds.BuildSpecification(IfcWallSolidWall(), properties, checkAllowedValues: true);
            var value = strict.Requirements.Properties.Single(p => p.BaseName.SimpleValue == "Status").Value;
            Assert.Equal(new[] { "NEW", "EXISTING" }, value.Restriction.Enumeration);
        }

        [Fact]
        public void GeneratedDocumentSurvivesWritingAndReading()
        {
            var document = BsddIds.BuildDocument(new[] { BsddIds.BuildSpecification(IfcWallSolidWall(), WallProperties()) });

            var read = IdsReader.Read(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(IdsWriter.ToXmlString(document))));

            var specification = Assert.Single(read.Specifications);
            Assert.Equal("IFCWALL", specification.Applicability.Entity.Name.SimpleValue);
            Assert.Equal(2, specification.Requirements.Properties.Count);
        }

        [Fact]
        public void ChecksASampleModel()
        {
            // The sample's walls carry Pset_WallCommon.FireRating but are not SOLIDWALL, so use plain IfcWall.
            var wall = new BsddClass { Uri = Ifc43 + "/class/IfcWall", DictionaryUri = Ifc43, Code = "IfcWall", Name = "Wall" };
            var fireRating = new[] { new BsddClassProperty { PropertySet = "Pset_WallCommon", PropertyCode = "FireRating", Name = "Fire Rating" } };
            var document = BsddIds.BuildDocument(new[] { BsddIds.BuildSpecification(wall, fireRating) });

            var result = (IdsValidationResult)IdsProcessor.Process(LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, document).Value;

            // 47 walls, 5 of them without FireRating. Passing walls are not listed in the result,
            // yet the counts have to include them.
            var specification = Assert.Single(result.Specifications);
            Assert.Equal(IdsStatus.Fail, specification.Status);
            Assert.Equal(47, specification.ApplicableCount);
            Assert.Equal(5, specification.FailedCount);
            Assert.Equal(42, specification.PassedCount);
            Assert.Equal(5, specification.Entities.Count);
        }

        [Trait("Category", "Network")]
        [SkippableFact]
        public async Task BuildsFromLiveBsdd()
        {
            Skip.If(System.Environment.GetEnvironmentVariable("IFCTOOLBOX_SKIP_NETWORK_TESTS") == "1");

            using var client = new BsddClient();
            var bsddClass = await client.GetClassAsync(Ifc43 + "/class/IfcWall");
            var properties = await client.GetAllClassPropertiesAsync(bsddClass.Uri);

            var specification = BsddIds.BuildSpecification(bsddClass, properties);

            Assert.Equal("IFCWALL", specification.Applicability.Entity.Name.SimpleValue);
            Assert.Contains(specification.Requirements.Properties,
                p => p.PropertySet.SimpleValue == "Pset_WallCommon" && p.BaseName.SimpleValue == "FireRating");
        }

        private static BsddClass IfcWallSolidWall() => new()
        {
            Uri = Ifc43 + "/class/IfcWallSOLIDWALL",
            DictionaryUri = Ifc43,
            Code = "IfcWallSOLIDWALL",
            Name = "Solid Wall",
            Definition = "A solid wall.",
        };

        private static List<BsddClassProperty> WallProperties() => new()
        {
            new() { PropertySet = "Pset_WallCommon", PropertyCode = "FireRating", Name = "Fire Rating" },
            new() { PropertySet = "Pset_WallCommon", PropertyCode = "FireRating", Name = "Fire Rating" },
            new() { PropertySet = "Pset_WallCommon", PropertyCode = "IsExternal", Name = "Is External" },
            new() { PropertySet = "Attributes", PropertyCode = "Description", Name = "Description" },
            new() { PropertySet = null, PropertyCode = "Orphan", Name = "Orphan" },
        };
    }
}
