using IfcToolbox.Core.Editors;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// Files the toolbox writes are shared, so they must not say who made them or where on whose
    /// machine. The original wrote the Windows user name as an IfcPerson into every split and
    /// optimized file, and xbim filled the header with the full local path.
    /// </summary>
    public class OutputPrivacyTests
    {
        private static readonly string Folder = Path.Combine(LocalFiles.TestOutputFolder, "Privacy");

        private static string Source(string name)
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, name + ".ifc");
            File.Copy(LocalFiles.Ifc4_SampleHouse, path, overwrite: true);
            return path;
        }

        public static IEnumerable<object[]> Outputs()
        {
            yield return new object[] { "optimize" };
            yield return new object[] { "split" };
            yield return new object[] { "relocate" };
            yield return new object[] { "anonymize" };
            yield return new object[] { "combine" };
            yield return new object[] { "units" };
        }

        private static string Produce(string tool)
        {
            var source = Source(tool);
            switch (tool)
            {
                case "optimize":
                    return OptimizerProcessor.Process(source, ConfigFactory.CreateConfigOptimize()).FilePaths.Single();
                case "split":
                    var split = ConfigFactory.CreateConfigSplit();
                    split.SplitStrategy = SplitStrategy.DataOnly;
                    return SplitterProcessor.Process(source, split).FilePaths.Single();
                case "relocate":
                    // A change to the model, so that xbim records an editor.
                    var relocate = (ConfigRelocate)ConfigFactory.CreateConfigRelocate();
                    relocate.MapConversion = new IfcToolbox.Core.Geo.GeoMapConversion
                    {
                        TargetCrs = new IfcToolbox.Core.Geo.ProjectedCrs { Name = "EPSG:25832" },
                        Parameters = new IfcToolbox.Core.Geo.MapConversionParameters { Eastings = 1, Northings = 2, OrthogonalHeight = 0, XAxisAbscissa = 1, XAxisOrdinate = 0, Scale = 0.001 },
                    };
                    return RelocatorProcessor.Process(source, relocate).FilePaths.Single();
                case "anonymize":
                    var anonymize = ConfigFactory.CreateConfigAnonymize();
                    anonymize.AnonymeUserInfo = true;
                    anonymize.RemoveAllUserInfo();
                    return AnonymizerProcessor.Process(source, anonymize).FilePaths.Single(p => p.EndsWith(".ifc"));
                case "combine":
                    var output = Path.Combine(Folder, "combined.ifc");
                    ModelCombiner.Combine(new[] { LocalFiles.Ifc4_SampleHouse_0_GroundFloor, LocalFiles.Ifc4_SampleHouse_1_Roof }, output);
                    return output;
                case "units":
                    var converted = Path.Combine(Folder, "units_m.ifc");
                    using (var model = IfcToolbox.Core.Utilities.ToolboxFiles.Open(source))
                    {
                        using (var txn = model.BeginTransaction("Units"))
                        {
                            LengthUnitConverter.Convert(model, "m");
                            txn.Commit();
                        }
                        IfcToolbox.Core.Utilities.ToolboxFiles.Save(model, converted);
                    }
                    return converted;
                default:
                    throw new ArgumentException(tool);
            }
        }

        [Theory]
        [MemberData(nameof(Outputs))]
        public void OutputSaysNothingAboutTheMachineOrItsUser(string tool)
        {
            var output = Produce(tool);
            var text = File.ReadAllText(output);

            // STEP escapes a backslash as two.
            var folder = Path.GetDirectoryName(Path.GetFullPath(output)).Replace("\\", "\\\\");
            Assert.DoesNotContain(folder, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"IFCPERSON($,'{Environment.UserName}'", text, StringComparison.OrdinalIgnoreCase);

            var header = text.Split('\n').First(l => l.StartsWith("FILE_NAME", StringComparison.Ordinal));
            Assert.Contains($"'{Path.GetFileName(output)}'", header);
            Assert.Contains("IFC Toolbox", header);
            Assert.DoesNotContain("BIM Mars", text);
        }
    }
}
