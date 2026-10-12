using IfcToolbox.Core.Convert;
using IfcToolbox.Core.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace IfcToolbox.Tests
{
    public class IfcConvertTests
    {
        /// <summary>
        /// Runs a conversion and checks that something was actually written.
        /// The previous tests only called Convert and passed as long as it did not throw,
        /// which it never did - a conversion that produced no file looked identical to one
        /// that worked.
        /// </summary>
        private static string Convert(ConvertTargetFormat format, string suffix,
            ConvertOptionsWrap options, string sourceFile = null)
        {
            var filePath = sourceFile ?? LocalFiles.Ifc4_SampleHouse;
            var outputFolder = LocalFiles.TestOutputFolder;
            string outputFileName = ConsoleFile.GetOutputFileName(filePath, outputFolder, suffix,
                ConvertFormatInfo.GetExtension(format));
            string logTextFileName = ConsoleFile.GetOutputFileName(outputFileName, outputFolder, "_Log", ".txt");

            IEnumerable<string> produced = IfcConvert.Convert(filePath, outputFileName, options, format, logTextFileName);

            Assert.Contains(outputFileName, produced);
            Assert.True(File.Exists(outputFileName), $"{format} output was not written");
            Assert.True(new FileInfo(outputFileName).Length > 0, $"{format} output is empty");
            return outputFileName;
        }

        [Fact]
        public static void ConvertToObjNoOption()
        {
            var output = Convert(ConvertTargetFormat.OBJ, "_OBJ", new ConvertOptionsWrap());
            // OBJ always comes with a material library next to it.
            Assert.True(File.Exists(Path.ChangeExtension(output, ".mtl")));
        }

        [Fact]
        public static void ConvertToObjWithOptions()
        {
            Convert(ConvertTargetFormat.OBJ, "_OBJwithOpt", new ConvertOptionsWrap
            {
                YUp = true,
                UseElementNames = true,
            });
        }

        [Fact]
        public static void ConvertToDae()
        {
            Convert(ConvertTargetFormat.DAE, "_DAE", new ConvertOptionsWrap
            {
                EnableLayersetSlicing = true,
                UseElementTypes = true,
            });
        }

        [Fact]
        public static void ConvertToStp()
        {
            Convert(ConvertTargetFormat.STP, "_STP", new ConvertOptionsWrap { WeldVertices = true });
        }

        [Fact]
        public static void ConvertToIgs()
        {
            Convert(ConvertTargetFormat.IGS, "_IGS", new ConvertOptionsWrap
            {
                UseWorldCoords = true,
                ConvertBackUnits = true,
            });
        }

        [Fact]
        public static void ConvertToXml()
        {
            Convert(ConvertTargetFormat.XML, "_XML", new ConvertOptionsWrap
            {
                DisableBooleanResults = true,
                LayersetFirst = true,
            });
        }

        [Fact]
        public static void ConvertToSvg()
        {
            Convert(ConvertTargetFormat.SVG, "_SVG", new ConvertOptionsWrap
            {
                AutoSection = true,
                AutoElevation = true,
            });
        }

        [Fact]
        public static void ConvertToGlb()
        {
            var output = Convert(ConvertTargetFormat.GLB, "_GLB", new ConvertOptionsWrap());

            // A .glb starts with the ASCII magic "glTF" followed by the version as uint32.
            using var stream = File.OpenRead(output);
            using var reader = new BinaryReader(stream);
            Assert.Equal(new byte[] { 0x67, 0x6C, 0x54, 0x46 }, reader.ReadBytes(4)); // "glTF"
            Assert.Equal(2u, reader.ReadUInt32());
        }

        [Fact]
        public static void ConvertToGlbWithWorldCoords()
        {
            Convert(ConvertTargetFormat.GLB, "_GLBworld", new ConvertOptionsWrap
            {
                UseWorldCoords = true,
                CenterModel = true,
            });
        }

        [Fact]
        public static void ConvertToIfc()
        {
            var output = Convert(ConvertTargetFormat.IFC, "_IFC", new ConvertOptionsWrap
            {
                UseWorldCoords = true,
            });
            Assert.StartsWith("ISO-10303-21", File.ReadAllText(output).TrimStart());
        }
    }

    public class IfcConvertVersionTests
    {
        [Theory]
        [InlineData("IfcOpenShell IfcConvert 0.6.0b0 (OCC 7.3.0)", 0, 6)]
        [InlineData("IfcOpenShell IfcConvert 0.8.5 (OCC 7.8.1)", 0, 8)]
        [InlineData("IfcOpenShell IfcConvert v0.7.0", 0, 7)]
        public static void ParsesVersionOutput(string output, int major, int minor)
        {
            var version = IfcConvertVersion.Parse(output);
            Assert.False(version.IsUnknown);
            Assert.Equal(major, version.Major);
            Assert.Equal(minor, version.Minor);
        }

        [Fact]
        public static void UnparseableOutputIsUnknown()
        {
            Assert.True(IfcConvertVersion.Parse("").IsUnknown);
            Assert.True(IfcConvertVersion.Parse("no numbers here").IsUnknown);
        }

        [Fact]
        public static void DetectsTheBundledExecutable()
        {
            IfcConvert.EnsureExecutable();
            var version = IfcConvert.GetVersion();

            Assert.False(version.IsUnknown);
            // The executable shipped in the repository is IfcOpenShell 0.6.
            Assert.Equal(0, version.Major);
            Assert.Equal(6, version.Minor);
            Assert.False(version.IsAtLeast08);
        }

        [Fact]
        public static void FormatsAreGatedOnTheDetectedVersion()
        {
            var v06 = new IfcConvertVersion(0, 6);
            var v08 = new IfcConvertVersion(0, 8);

            // GLB and IFC already work with the bundled 0.6 build.
            Assert.True(ConvertFormatInfo.IsSupportedBy(ConvertTargetFormat.GLB, v06));
            Assert.True(ConvertFormatInfo.IsSupportedBy(ConvertTargetFormat.IFC, v06));

            // These serializers only exist from 0.8 onwards.
            Assert.False(ConvertFormatInfo.IsSupportedBy(ConvertTargetFormat.JSON, v06));
            Assert.False(ConvertFormatInfo.IsSupportedBy(ConvertTargetFormat.TTL, v06));
            Assert.False(ConvertFormatInfo.IsSupportedBy(ConvertTargetFormat.H5, v06));
            Assert.True(ConvertFormatInfo.IsSupportedBy(ConvertTargetFormat.JSON, v08));

            Assert.DoesNotContain(ConvertTargetFormat.JSON, ConvertFormatInfo.SupportedFormats(v06));
            Assert.Contains(ConvertTargetFormat.GLB, ConvertFormatInfo.SupportedFormats(v06));
        }

        [Fact]
        public static void RequestingAnUnsupportedFormatFailsLoudly()
        {
            // The bundled build is 0.6, so asking for a 0.8 only serializer has to say so
            // rather than silently writing nothing.
            var exception = Assert.Throws<System.NotSupportedException>(() =>
                IfcConvert.Convert(LocalFiles.Ifc4_SampleHouse,
                    Path.Combine(LocalFiles.TestOutputFolder, "unsupported.json"),
                    new ConvertOptionsWrap(), ConvertTargetFormat.JSON).ToList());

            Assert.Contains(IfcConvert.ExecutablePathVariable, exception.Message);
        }
    }

    public class ConvertOptionServiceTests
    {
        [Fact]
        public static void RenamedOptionsFollowTheDetectedVersion()
        {
            var options = new ConvertOptionsWrap
            {
                OrientShells = true,
                DisableBooleanResults = true,
                UseElementNumericIds = true,
            };

            var on06 = ConvertOptionService.GeneratePrompt(options, ConvertTargetFormat.OBJ,
                new IfcConvertVersion(0, 6), null, false);
            var on08 = ConvertOptionService.GeneratePrompt(options, ConvertTargetFormat.OBJ,
                new IfcConvertVersion(0, 8), null, false);

            Assert.Contains("--orient-shells", on06);
            Assert.Contains("--disable-boolean-results", on06);
            Assert.Contains("--use-element-numeric-ids", on06);

            // IfcOpenShell renamed these in 0.8; passing the old spelling makes IfcConvert
            // reject the entire command line.
            Assert.Contains("--reorient-shells", on08);
            Assert.Contains("--disable-boolean-result ", on08 + " ");
            Assert.DoesNotContain("--disable-boolean-results", on08);
            Assert.Contains("--use-element-step-ids", on08);
            Assert.DoesNotContain("--use-element-numeric-ids", on08);
        }

        [Fact]
        public static void UnknownVersionKeepsTheBundledSpelling()
        {
            var options = new ConvertOptionsWrap { OrientShells = true };

            var prompt = ConvertOptionService.GeneratePrompt(options, ConvertTargetFormat.OBJ,
                IfcConvertVersion.Unknown, null, false);

            Assert.Contains("--orient-shells", prompt);
            Assert.DoesNotContain("--reorient-shells", prompt);
        }

        [Fact]
        public static void FormatSpecificOptionsAreFilteredOut()
        {
            var options = new ConvertOptionsWrap { YUp = true, AutoSection = true };

            var forObj = ConvertOptionService.GeneratePrompt(options, ConvertTargetFormat.OBJ,
                IfcConvertVersion.Unknown, null, false);
            var forGlb = ConvertOptionService.GeneratePrompt(options, ConvertTargetFormat.GLB,
                IfcConvertVersion.Unknown, null, false);

            Assert.Contains("--y-up", forObj);          // OBJ only
            Assert.DoesNotContain("--auto-section", forObj); // SVG only
            Assert.DoesNotContain("--y-up", forGlb);
        }

        [Fact]
        public static void ProcessorLeavesTheUsersTextFileAlone()
        {
            // The processor used to log to model.txt and delete it first.
            var folder = Path.Combine(LocalFiles.TestOutputFolder, "ProcessorLog");
            Directory.CreateDirectory(folder);
            var model = Path.Combine(folder, "House.ifc");
            File.Copy(LocalFiles.Ifc4_SampleHouse, model, overwrite: true);
            var notes = Path.ChangeExtension(model, ".txt");
            File.WriteAllText(notes, "my notes");

            var config = IfcToolbox.Tools.Configurations.ConfigFactory.CreateConfigConvert(new ConvertOptionsWrap(), ConvertTargetFormat.GLB);
            var result = IfcToolbox.Tools.Processors.ConverterProcessor.Process(model, config);

            Assert.True(result.Success);
            Assert.Contains(Path.ChangeExtension(model, ".glb"), result.FilePaths);
            Assert.Equal("my notes", File.ReadAllText(notes));
            Assert.DoesNotContain(notes, result.FilePaths);
        }
    }
}
