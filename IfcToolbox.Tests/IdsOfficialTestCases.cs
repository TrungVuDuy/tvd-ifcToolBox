using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xunit;
using Xunit.Abstractions;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// Runs the official buildingSMART IDS test cases from
    /// buildingSMART/IDS Documentation/ImplementersDocumentation/TestCases.
    /// <para>
    /// Each case is a pair of files with the same base name: an .ids and the .ifc it is meant
    /// to be run against. The file name states the expected outcome - "pass-" means every
    /// specification must pass, "fail-" means at least one must fail. Cases named "invalid-"
    /// test that an IDS file is rejected by the XSD schema, which is a different concern from
    /// the validation engine, so they are not run here.
    /// </para>
    /// </summary>
    public class IdsOfficialTestCases
    {
        private readonly ITestOutputHelper _output;

        public IdsOfficialTestCases(ITestOutputHelper output)
        {
            _output = output;
        }

        public static string CasesRoot => Path.Combine(Directory.GetCurrentDirectory(), "IdsTestCases");

        public static IEnumerable<object[]> Cases(string facet)
        {
            var folder = Path.Combine(CasesRoot, facet);
            if (!Directory.Exists(folder))
                yield break;

            foreach (var ids in Directory.EnumerateFiles(folder, "*.ids").OrderBy(p => p))
            {
                var name = Path.GetFileNameWithoutExtension(ids);
                if (name.StartsWith("invalid-", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!File.Exists(Path.ChangeExtension(ids, ".ifc")))
                    continue;
                yield return new object[] { facet, Path.GetFileName(ids) };
            }
        }

        public static IEnumerable<object[]> EntityCases() => Cases("entity");
        public static IEnumerable<object[]> AttributeCases() => Cases("attribute");
        public static IEnumerable<object[]> PropertyCases() => Cases("property");
        public static IEnumerable<object[]> ClassificationCases() => Cases("classification");
        public static IEnumerable<object[]> MaterialCases() => Cases("material");
        public static IEnumerable<object[]> PartOfCases() => Cases("partof");
        public static IEnumerable<object[]> RestrictionCases() => Cases("restriction");
        public static IEnumerable<object[]> IdsCases() => Cases("ids");
        public static IEnumerable<object[]> ToleranceCases() => Cases("tolerance");

        [Theory]
        [MemberData(nameof(EntityCases))]
        public void Entity(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(AttributeCases))]
        public void Attribute(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(PropertyCases))]
        public void Property(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(ClassificationCases))]
        public void Classification(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(MaterialCases))]
        public void Material(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(PartOfCases))]
        public void PartOf(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(RestrictionCases))]
        public void Restriction(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(IdsCases))]
        public void Ids(string facet, string fileName) => RunCase(facet, fileName);

        [Theory]
        [MemberData(nameof(ToleranceCases))]
        public void Tolerance(string facet, string fileName) => RunCase(facet, fileName);

        private void RunCase(string facet, string fileName)
        {
            var idsPath = Path.Combine(CasesRoot, facet, fileName);
            var ifcPath = Path.ChangeExtension(idsPath, ".ifc");
            bool shouldPass = fileName.StartsWith("pass-", StringComparison.OrdinalIgnoreCase);

            var document = IdsReader.Load(idsPath);
            using var model = IfcStore.Open(ifcPath);

            var validator = new IdsValidator { ReportPassingEntities = true };
            var result = validator.Validate(document, model, ifcPath);

            // A specification that finds nothing applicable is neither a pass nor a failure of
            // the requirements; the test cases treat it as satisfied.
            bool actualPass = result.Specifications.All(s => s.Status != IdsStatus.Fail);

            if (actualPass != shouldPass)
            {
                _output.WriteLine($"{facet}/{fileName}");
                _output.WriteLine($"  expected {(shouldPass ? "pass" : "fail")}, got {(actualPass ? "pass" : "fail")}");
                foreach (var spec in result.Specifications)
                {
                    _output.WriteLine($"  spec '{spec.Specification.Name}' -> {spec.Status} {spec.Message}");
                    foreach (var entity in spec.Entities)
                    {
                        _output.WriteLine($"    {entity}");
                        foreach (var f in entity.Facets)
                            _output.WriteLine($"      {f}");
                    }
                }
            }

            Assert.Equal(shouldPass, actualPass);
        }
    }
}
