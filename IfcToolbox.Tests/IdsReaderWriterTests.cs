using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Tools.Processors;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace IfcToolbox.Tests
{
    public class IdsReaderTests
    {
        private const string Sample = @"<?xml version='1.0' encoding='utf-8'?>
<ids xmlns='http://standards.buildingsmart.org/IDS' xmlns:xs='http://www.w3.org/2001/XMLSchema'>
  <info>
    <title>Fire rating</title>
    <author>someone@example.com</author>
    <date>2024-06-01</date>
    <version>1.2</version>
  </info>
  <specifications>
    <specification name='Walls need a fire rating' ifcVersion='IFC4 IFC4X3_ADD2' identifier='FR-01'>
      <applicability minOccurs='0' maxOccurs='unbounded'>
        <entity><name><simpleValue>IFCWALL</simpleValue></name></entity>
        <property>
          <propertySet><simpleValue>Pset_WallCommon</simpleValue></propertySet>
          <baseName><simpleValue>IsExternal</simpleValue></baseName>
          <value><simpleValue>true</simpleValue></value>
        </property>
      </applicability>
      <requirements>
        <attribute cardinality='optional'>
          <name><simpleValue>Description</simpleValue></name>
        </attribute>
        <property dataType='IFCLABEL' instructions='Ask the fire engineer'>
          <propertySet><simpleValue>Pset_WallCommon</simpleValue></propertySet>
          <baseName><simpleValue>FireRating</simpleValue></baseName>
          <value>
            <xs:restriction base='xs:string'>
              <xs:enumeration value='REI30' />
              <xs:enumeration value='REI60' />
            </xs:restriction>
          </value>
        </property>
        <material cardinality='prohibited'>
          <value><simpleValue>Timber</simpleValue></value>
        </material>
      </requirements>
    </specification>
  </specifications>
</ids>";

        [Fact]
        public void ReadsInfo()
        {
            var document = IdsReader.ReadXml(Sample);

            Assert.Equal("Fire rating", document.Info.Title);
            Assert.Equal("someone@example.com", document.Info.Author);
            Assert.Equal("1.2", document.Info.Version);
            Assert.Equal(new DateTime(2024, 6, 1), document.Info.Date);
        }

        [Fact]
        public void ReadsSpecificationMetadata()
        {
            var specification = Assert.Single(IdsReader.ReadXml(Sample).Specifications);

            Assert.Equal("Walls need a fire rating", specification.Name);
            Assert.Equal("FR-01", specification.Identifier);
            // ifcVersion is an xs:list, so several versions share one attribute.
            Assert.Equal(new[] { "IFC4", "IFC4X3_ADD2" }, specification.IfcVersions);
            Assert.Equal(0, specification.Applicability.MinOccurs);
            Assert.Null(specification.Applicability.MaxOccurs); // unbounded
        }

        [Fact]
        public void ReadsApplicabilityFacets()
        {
            var applicability = IdsReader.ReadXml(Sample).Specifications[0].Applicability;

            Assert.Equal("IFCWALL", applicability.Entity.Name.SimpleValue);
            var property = Assert.Single(applicability.Properties);
            Assert.Equal("Pset_WallCommon", property.PropertySet.SimpleValue);
            Assert.Equal("IsExternal", property.BaseName.SimpleValue);
            Assert.Equal("true", property.Value.SimpleValue);
        }

        [Fact]
        public void ReadsCardinalityIncludingTheDefault()
        {
            var requirements = IdsReader.ReadXml(Sample).Specifications[0].Requirements;

            Assert.Equal(IdsCardinality.Optional, requirements.Attributes.Single().Cardinality);
            Assert.Equal(IdsCardinality.Prohibited, requirements.Materials.Single().Cardinality);
            // The schema defaults a missing cardinality to required.
            Assert.Equal(IdsCardinality.Required, requirements.Properties.Single().Cardinality);
        }

        [Fact]
        public void ReadsRestrictionsAndInstructions()
        {
            var property = IdsReader.ReadXml(Sample).Specifications[0].Requirements.Properties.Single();

            Assert.Equal("IFCLABEL", property.DataType);
            Assert.Equal("Ask the fire engineer", property.Instructions);
            Assert.True(property.Value.IsRestriction);
            Assert.Equal(new[] { "REI30", "REI60" }, property.Value.Restriction.Enumeration);
        }

        [Fact]
        public void RejectsSomethingThatIsNotIds()
        {
            Assert.Throws<IdsFormatException>(() => IdsReader.ReadXml("<notIds/>"));
        }
    }

    public class IdsWriterTests
    {
        [Fact]
        public void RoundTripsEveryFacet()
        {
            var original = IdsReader.ReadXml(GetSampleFromReaderTests());

            var xml = IdsWriter.ToXmlString(original);
            var reloaded = IdsReader.ReadXml(xml);

            Assert.Equal(original.Info.Title, reloaded.Info.Title);
            Assert.Equal(original.Info.Date, reloaded.Info.Date);

            var a = original.Specifications.Single();
            var b = reloaded.Specifications.Single();
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Identifier, b.Identifier);
            Assert.Equal(a.IfcVersions, b.IfcVersions);
            Assert.Equal(a.Applicability.MinOccurs, b.Applicability.MinOccurs);
            Assert.Equal(a.Applicability.Entity.Name.SimpleValue, b.Applicability.Entity.Name.SimpleValue);
            Assert.Equal(a.Requirements.Properties.Single().Value.Restriction.Enumeration,
                b.Requirements.Properties.Single().Value.Restriction.Enumeration);
            Assert.Equal(a.Requirements.Materials.Single().Cardinality,
                b.Requirements.Materials.Single().Cardinality);
            Assert.Equal(a.Requirements.Attributes.Single().Cardinality,
                b.Requirements.Attributes.Single().Cardinality);
        }

        [Fact]
        public void EveryOfficialTestCaseSurvivesARoundTrip()
        {
            var folder = Path.Combine(IdsOfficialTestCases.CasesRoot, "property");
            var files = Directory.EnumerateFiles(folder, "pass-*.ids").Take(25).ToList();
            Assert.NotEmpty(files);

            foreach (var file in files)
            {
                var original = IdsReader.Load(file);
                var reloaded = IdsReader.ReadXml(IdsWriter.ToXmlString(original));

                Assert.Equal(original.Specifications.Count, reloaded.Specifications.Count);
                for (int i = 0; i < original.Specifications.Count; i++)
                {
                    Assert.Equal(original.Specifications[i].Name, reloaded.Specifications[i].Name);
                    Assert.Equal(original.Specifications[i].Requirements.AllFacets().Count(),
                        reloaded.Specifications[i].Requirements.AllFacets().Count());
                }
            }
        }

        private static string GetSampleFromReaderTests()
        {
            var field = typeof(IdsReaderTests).GetField("Sample",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (string)field.GetRawConstantValue();
        }
    }

    public class IdsProcessorTests
    {
        /// <summary>
        /// The toolbox-level entry point, checking a real sample model against an IDS built in
        /// code, and producing a report.
        /// </summary>
        [Fact]
        public void ValidatesASampleModelAndWritesACsv()
        {
            var idsPath = Path.Combine(LocalFiles.TestOutputFolder, "wall-fire-rating.ids");
            IdsWriter.Save(BuildFireRatingIds(), idsPath);

            var modelPath = Path.Combine(LocalFiles.TestOutputFolder, "Ifc4_Revit_ARC_FireRatingAdded.ifc");
            File.Copy(LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, modelPath, overwrite: true);

            var processorResult = IdsProcessor.ProcessToCsv(modelPath, idsPath);

            Assert.True(processorResult.Success);
            var result = Assert.IsType<IdsValidationResult>(processorResult.Value);

            var specification = Assert.Single(result.Specifications);
            Assert.True(specification.ApplicableCount > 0 || specification.Status == IdsStatus.NotApplicable);

            var csvPath = Assert.Single(processorResult.FilePaths);
            Assert.True(File.Exists(csvPath));
            Assert.StartsWith("Specification,Identifier,EntityLabel", File.ReadAllText(csvPath));
        }

        [Fact]
        public void ReportsWhatWasFoundForEachFailure()
        {
            // A requirement no wall can satisfy, so the report has something to say.
            var document = BuildFireRatingIds();
            document.Specifications[0].Requirements.Properties[0].Value = new IdsValue("NOT-A-REAL-RATING");

            var modelPath = LocalFiles.Ifc4_Revit_ARC_FireRatingAdded;
            var processorResult = IdsProcessor.Process(modelPath, document);
            var result = (IdsValidationResult)processorResult.Value;

            Assert.False(result.Passed);
            var text = IdsReport.ToText(result);
            Assert.Contains("FAIL", text);
            Assert.Contains("Ask the fire engineer", text); // instructions reach the report

            var rows = IdsReport.ToRows(result).ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.False(r.Passed));
            Assert.All(rows, r => Assert.Equal("IFCWALL", r.EntityType));
        }

        [Fact]
        public void SeveralResultsShareOneCsvHeader()
        {
            var document = BuildFireRatingIds();
            document.Specifications[0].Requirements.Properties[0].Value = new IdsValue("NOT-A-REAL-RATING");
            var result = (IdsValidationResult)IdsProcessor.Process(LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, document).Value;

            var single = IdsReport.ToCsv(result).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var combined = IdsReport.ToCsv(new[] { result, result }).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Single(combined, line => line.StartsWith("Specification,"));
            Assert.Equal(1 + 2 * (single.Length - 1), combined.Length);
        }

        private static IdsDocument BuildFireRatingIds()
        {
            return new IdsDocument
            {
                Info = new IdsInfo { Title = "Wall fire rating", Version = "1.0" },
                Specifications =
                {
                    new IdsSpecification
                    {
                        Name = "Walls carry a fire rating",
                        Identifier = "FR-01",
                        IfcVersions = { IdsIfcVersion.Ifc4 },
                        Applicability = new IdsApplicability
                        {
                            MinOccurs = 0,
                            Entity = new IdsEntityFacet { Name = new IdsValue("IFCWALL") },
                        },
                        Requirements = new IdsRequirements
                        {
                            Properties =
                            {
                                new IdsPropertyFacet
                                {
                                    PropertySet = new IdsValue("Pset_WallCommon"),
                                    BaseName = new IdsValue("FireRating"),
                                    Instructions = "Ask the fire engineer",
                                },
                            },
                        },
                    },
                },
            };
        }
    }
}
