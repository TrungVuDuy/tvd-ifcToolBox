using IfcToolbox.Cli;
using IfcToolbox.Core.Geo;
using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// The ifctoolbox command line, run in process. Exit codes are part of the contract:
    /// pipelines branch on them.
    /// </summary>
    public class CliTests
    {
        private static (int Exit, string Out, string Err) Run(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            int exit = CliApp.Run(args, output, error);
            return (exit, output.ToString(), error.ToString());
        }

        private static string Copy(string source, string tag)
        {
            var target = Path.Combine(LocalFiles.TestOutputFolder, Path.GetFileNameWithoutExtension(source) + "_cli_" + tag + ".ifc");
            File.Copy(source, target, overwrite: true);
            return target;
        }

        #region Usage

        [Fact]
        public void UnknownCommandIsAUsageError()
        {
            var (exit, _, err) = Run("frobnicate");
            Assert.Equal(ExitCodes.UsageError, exit);
            Assert.Contains("error", err);
        }

        [Fact]
        public void MissingModelIsAUsageError()
        {
            var (exit, _, _) = Run("info", Path.Combine(LocalFiles.TestOutputFolder, "does-not-exist.ifc"));
            Assert.Equal(ExitCodes.UsageError, exit);
        }

        [Fact]
        public void ProgressMessagesStayOffStandardOutput()
        {
            // Processors report their run time through Marslogger; without --verbose that must
            // not end up in stdout, or --json output could not be parsed.
            var (exit, output, _) = Run("info", LocalFiles.Ifc4_SampleHouse, "--json");
            Assert.Equal(ExitCodes.Success, exit);
            Assert.DoesNotContain("Process completed", output);
            JsonDocument.Parse(output);
        }

        #endregion

        #region info

        [Fact]
        public void InfoDescribesAnIfc4x3Model()
        {
            var (exit, output, _) = Run("info", LocalFiles.Ifc4x3_Road, "--json");
            Assert.Equal(ExitCodes.Success, exit);

            var json = JsonDocument.Parse(output).RootElement;
            Assert.Equal("IFC4X3_ADD2", json.GetProperty("schema").GetString());
            Assert.Contains(json.GetProperty("spatial").EnumerateArray(),
                s => s.GetProperty("class").GetString() == "IfcRoadPart" && s.GetProperty("usage").GetString() == "CARRIAGEWAY");
            Assert.Equal("EPSG:32760", json.GetProperty("georeferencing").GetProperty("crs").GetString());
        }

        #endregion

        #region validate

        private static string WriteIds(string name, string requiredValue)
        {
            var document = new IdsDocument
            {
                Info = new IdsInfo { Title = name },
                Specifications =
                {
                    new IdsSpecification
                    {
                        Name = "Walls carry a fire rating",
                        IfcVersions = { IdsIfcVersion.Ifc4 },
                        Applicability = new IdsApplicability { Entity = new IdsEntityFacet { Name = new IdsValue("IFCWALL") } },
                        Requirements = new IdsRequirements
                        {
                            Properties =
                            {
                                new IdsPropertyFacet
                                {
                                    PropertySet = new IdsValue("Pset_WallCommon"),
                                    BaseName = new IdsValue("FireRating"),
                                    Value = requiredValue == null ? null : new IdsValue(requiredValue),
                                },
                            },
                        },
                    },
                },
            };
            var path = Path.Combine(LocalFiles.TestOutputFolder, name + ".ids");
            IdsWriter.Save(document, path);
            return path;
        }

        [Fact]
        public void ValidatePassesWithExitCodeZero()
        {
            // Every wall Revit exports carries a Name. (Not every wall in this sample has a
            // FireRating - the foundation walls do not - so that would not be a passing case.)
            var document = new IdsDocument
            {
                Info = new IdsInfo { Title = "cli-pass" },
                Specifications =
                {
                    new IdsSpecification
                    {
                        Name = "Walls are named",
                        IfcVersions = { IdsIfcVersion.Ifc4 },
                        Applicability = new IdsApplicability { Entity = new IdsEntityFacet { Name = new IdsValue("IFCWALL") } },
                        Requirements = new IdsRequirements
                        {
                            Attributes = { new IdsAttributeFacet { Name = new IdsValue("Name") } },
                        },
                    },
                },
            };
            var ids = Path.Combine(LocalFiles.TestOutputFolder, "cli-pass.ids");
            IdsWriter.Save(document, ids);

            var (exit, output, _) = Run("validate", LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, "--ids", ids);

            Assert.Equal(ExitCodes.Success, exit);
            Assert.Contains("PASSED", output);
        }

        [Fact]
        public void ValidateFailsWithExitCodeOneAndWritesAReport()
        {
            var ids = WriteIds("cli-fail", "NOT-A-REAL-RATING");
            var report = Path.Combine(LocalFiles.TestOutputFolder, "cli-fail-report.csv");
            if (File.Exists(report)) File.Delete(report);

            var (exit, output, _) = Run("validate", LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, "--ids", ids, "--report", report);

            Assert.Equal(ExitCodes.ValidationFailed, exit);
            Assert.Contains("FAILED", output);
            var lines = File.ReadAllLines(report);
            Assert.StartsWith("Specification,", lines[0]);
            Assert.True(lines.Length > 1, "the report lists no failures");
        }

        [Fact]
        public void ValidateJsonCanBeParsed()
        {
            var ids = WriteIds("cli-json", "NOT-A-REAL-RATING");
            var (exit, output, _) = Run("validate", LocalFiles.Ifc4_Revit_ARC_FireRatingAdded, "--ids", ids, "--json");

            Assert.Equal(ExitCodes.ValidationFailed, exit);
            var json = JsonDocument.Parse(output).RootElement;
            Assert.False(json.GetProperty("passed").GetBoolean());
            var failures = json.GetProperty("documents")[0].GetProperty("specifications")[0].GetProperty("failures");
            Assert.True(failures.GetArrayLength() > 0);
            Assert.Equal("IFCWALL", failures[0].GetProperty("entity").GetString());
        }

        [Fact]
        public void ValidateRunsTheOfficialTestCases()
        {
            // One pass and one fail case from the buildingSMART suite, end to end.
            var folder = Path.Combine(IdsOfficialTestCases.CasesRoot, "attribute");
            var pass = Directory.GetFiles(folder, "pass-*.ids").First();
            var fail = Directory.GetFiles(folder, "fail-*.ids").First();

            Assert.Equal(ExitCodes.Success, Run("validate", Path.ChangeExtension(pass, ".ifc"), "--ids", pass).Exit);
            Assert.Equal(ExitCodes.ValidationFailed, Run("validate", Path.ChangeExtension(fail, ".ifc"), "--ids", fail).Exit);
        }

        #endregion

        #region split

        [Fact]
        public void SplitListsFacilityParts()
        {
            var (exit, output, _) = Run("split", LocalFiles.Ifc4x3_Road, "--by", "part", "--list");
            Assert.Equal(ExitCodes.Success, exit);
            Assert.Contains("[CARRIAGEWAY]", output);
        }

        [Fact]
        public void SplitByFacilityName()
        {
            var source = Copy(LocalFiles.Ifc4x3_Road, "facility");
            var (exit, output, err) = Run("split", source, "--by", "facility", "--item", "road parking - road");

            Assert.True(exit == ExitCodes.Success, err);
            var file = output.Trim().Split('\n').Last().Trim();
            using var model = IfcStore.Open(file);
            Assert.Equal(XbimSchemaVersion.Ifc4x3, model.SchemaVersion);
            Assert.Contains(model.Instances.OfType<IIfcSpatialStructureElement>(), s => s.Name == "road parking - road");
        }

        [Fact]
        public void SplitWithAnUnknownNameIsAUsageError()
        {
            var (exit, _, err) = Run("split", LocalFiles.Ifc4x3_Road, "--by", "facility", "--item", "no such road");
            Assert.Equal(ExitCodes.UsageError, exit);
            Assert.Contains("--list", err);
        }

        [Fact]
        public void SplitWithoutItemsIsAUsageError()
        {
            Assert.Equal(ExitCodes.UsageError, Run("split", LocalFiles.Ifc4x3_Road, "--by", "facility").Exit);
        }

        #endregion

        #region combine

        [Fact]
        public void CombineWritesOneModelAndReportsWhatWasMerged()
        {
            var target = Path.Combine(LocalFiles.TestOutputFolder, "cli_combined.ifc");
            var (exit, output, err) = Run("combine", LocalFiles.Ifc4_SampleHouse_0_GroundFloor, LocalFiles.Ifc4_SampleHouse_1_Roof, "-o", target, "--json");

            Assert.True(exit == ExitCodes.Success, err);
            using var json = JsonDocument.Parse(output);
            Assert.Equal(target, json.RootElement.GetProperty("output").GetString());
            Assert.Contains(json.RootElement.GetProperty("spatialMerged").EnumerateArray(), m => m.GetString() == "IfcSite 'Default'");

            using var model = IfcStore.Open(target);
            Assert.Single(model.Instances.OfType<IIfcProject>());
            Assert.Equal(2, model.Instances.OfType<IIfcBuildingStorey>().Count());
        }

        [Fact]
        public void CombiningModelsThatDoNotFitIsAUsageError()
        {
            // Millimetres and metres.
            var (exit, _, err) = Run("combine", LocalFiles.Ifc4_SampleHouse, LocalFiles.Ifc4_CubeAdvancedBrep,
                "-o", Path.Combine(LocalFiles.TestOutputFolder, "cli_units.ifc"));
            Assert.Equal(ExitCodes.UsageError, exit);
            Assert.Contains("length unit", err);

            Assert.Equal(ExitCodes.UsageError, Run("combine", LocalFiles.Ifc4_SampleHouse).Exit);
        }

        #endregion

        #region optimize / anonymize

        [Fact]
        public void OptimizeWritesWhereAsked()
        {
            var source = Copy(LocalFiles.Ifc4_SampleHouse, "optimize");
            var target = Path.Combine(LocalFiles.TestOutputFolder, "cli-optimized", "house.ifc");
            if (File.Exists(target)) File.Delete(target);

            var (exit, output, err) = Run("optimize", source, "--precision", "4", "--output", target);

            Assert.True(exit == ExitCodes.Success, err);
            Assert.True(File.Exists(target));
            Assert.Contains("bytes", output);
        }

        [Fact]
        public void OptimizeRejectsANonsensePrecision()
        {
            Assert.Equal(ExitCodes.UsageError, Run("optimize", LocalFiles.Ifc4_SampleHouse, "--precision", "99").Exit);
        }

        [Fact]
        public void AnonymizeRemovesPeople()
        {
            var source = Copy(LocalFiles.Ifc4_SampleHouse, "anon");
            var (exit, output, err) = Run("anonymize", source);

            Assert.True(exit == ExitCodes.Success, err);
            using var model = IfcStore.Open(output.Trim());
            Assert.Empty(model.Instances.OfType<IIfcPerson>());
        }

        #endregion

        #region georef

        [Fact]
        public void GeorefShowsTheConversion()
        {
            // The plain form, with no options: this crashed in the published exe, because an
            // array option that is not given comes back as an empty array, not null.
            var (exit, output, err) = Run("georef", LocalFiles.Ifc4x3_Building);
            Assert.True(exit == ExitCodes.Success, err);
            Assert.Contains("EPSG:32760", output);
            Assert.Contains("rotation  60", output);

            var (jsonExit, json, _) = Run("georef", LocalFiles.Ifc4x3_Building, "--json");
            Assert.Equal(ExitCodes.Success, jsonExit);
            var root = JsonDocument.Parse(json).RootElement;
            Assert.True(root.GetProperty("georeferenced").GetBoolean());
            Assert.False(root.TryGetProperty("point", out _));
        }

        [Fact]
        public void GeorefOnAModelWithoutConversion()
        {
            var (exit, output, _) = Run("georef", LocalFiles.Ifc4_SampleHouse);
            Assert.Equal(ExitCodes.Success, exit);
            Assert.Contains("not georeferenced", output);

            // Asking for a point with nothing to apply is an error, not a silent success.
            Assert.Equal(ExitCodes.Error, Run("georef", LocalFiles.Ifc4_SampleHouse, "--to-map", "0", "0", "0").Exit);
        }

        [Fact]
        public void GeorefTransformsAPoint()
        {
            // 1000 mm along x on a 60 degree rotated, millimetre model is (0.5, 0.866) m on the map.
            var (exit, output, _) = Run("georef", LocalFiles.Ifc4x3_Building, "--to-map", "1000", "0", "0");
            Assert.Equal(ExitCodes.Success, exit);

            var values = output.Trim().Split(' ').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(729013.3488297004 + 0.5, values[0], 5);
            Assert.Equal(9063992.684697364 + 0.8660254, values[1], 5);
        }

        [Fact]
        public void GeorefSetsAConversionAndInfersTheScale()
        {
            var source = Copy(LocalFiles.Ifc4_SampleHouse, "georef");
            var (exit, output, err) = Run("georef", source, "--crs", "EPSG:25832",
                "--eastings", "333000", "--northings", "5800000", "--rotation", "90");

            Assert.True(exit == ExitCodes.Success, err);
            var file = output.Split('\n')[0].Trim();
            using var model = IfcStore.Open(file);
            var map = MapConversionService.Read(model);
            Assert.Equal("EPSG:25832", map.TargetCrs.Name);
            Assert.Equal(90, MapConversionService.RotationDegrees(map.Parameters), 6);
            // The sample house is in millimetres, so the scale comes out as 0.001.
            Assert.Equal(0.001, map.Parameters.Scale.Value, 9);
            Assert.Contains("assuming the CRS is in metres", output);
        }

        [Fact]
        public void GeorefWithoutACoordinateIsAUsageError()
        {
            Assert.Equal(ExitCodes.UsageError, Run("georef", LocalFiles.Ifc4_SampleHouse, "--crs", "EPSG:25832").Exit);
        }

        #endregion

        #region convert

        [Fact]
        public void ConvertToGlb()
        {
            var source = Copy(LocalFiles.Ifc4_SampleHouse, "convert");
            var target = Path.ChangeExtension(source, ".glb");
            var (exit, output, err) = Run("convert", source, "--format", "glb");

            Assert.True(exit == ExitCodes.Success, err);
            Assert.Contains(target, output);
            Assert.True(new FileInfo(target).Length > 0);
        }

        [Fact]
        public void ConvertOfIfc4x3WithTheBundledConverterIsAnError()
        {
            var (exit, _, err) = Run("convert", LocalFiles.Ifc4x3_Road, "--format", "glb");
            Assert.Equal(ExitCodes.Error, exit);
            Assert.Contains("IFCTOOLBOX_IFCCONVERT", err);
        }

        [Fact]
        public void ConvertToAnUnknownFormatIsAUsageError()
        {
            Assert.Equal(ExitCodes.UsageError, Run("convert", LocalFiles.Ifc4_SampleHouse, "--format", "dwg").Exit);
        }

        #endregion
    }
}
