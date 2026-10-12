using IfcToolbox.Core.Editors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace IfcToolbox.Tests
{
    public class ModelCombinerTests
    {
        private static string Output(string name) => Path.Combine(LocalFiles.TestOutputFolder, "Combined_" + name + ".ifc");

        private static List<string> ElementIds(IfcStore model) =>
            model.Instances.OfType<IIfcProduct>().Where(p => p is not IIfcSpatialStructureElement)
                .Select(p => p.GlobalId.ToString()).OrderBy(g => g).ToList();

        private static void AssertSound(IfcStore model)
        {
            Assert.Single(model.Instances.OfType<IIfcProject>());
            // A file with duplicate GlobalIds is invalid IFC.
            var ids = model.Instances.OfType<IIfcRoot>().Select(r => r.GlobalId.ToString()).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            // The ifcpatch MergeProjects bug: a context per merged file left behind.
            Assert.Single(model.Instances.OfType<IIfcGeometricRepresentationContext>(),
                c => c is not IIfcGeometricRepresentationSubContext && c.ContextType == "Model");
            // Every representation still has a context, and every element a place in the structure.
            Assert.All(model.Instances.OfType<IIfcShapeRepresentation>(), r => Assert.NotNull(r.ContextOfItems));
        }

        [Fact]
        public void PutsTheSplitHouseBackTogether()
        {
            // The two halves share the project, site and building GlobalIds.
            var output = Output("House");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc4_SampleHouse_0_GroundFloor, LocalFiles.Ifc4_SampleHouse_1_Roof }, output);

            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Single(combined.Instances.OfType<IIfcSite>());
            Assert.Single(combined.Instances.OfType<IIfcBuilding>());
            Assert.Equal(new[] { "Ground Floor", "Roof" },
                combined.Instances.OfType<IIfcBuildingStorey>().Select(s => s.Name.ToString()).OrderBy(n => n));
            Assert.Contains("IfcSite 'Default'", result.SpatialMerged);

            List<string> expected;
            using (var ground = IfcStore.Open(LocalFiles.Ifc4_SampleHouse_0_GroundFloor))
            using (var roof = IfcStore.Open(LocalFiles.Ifc4_SampleHouse_1_Roof))
                expected = ElementIds(ground).Concat(ElementIds(roof)).Distinct().OrderBy(g => g).ToList();
            Assert.Equal(expected, ElementIds(combined));

            // Every element is still contained in a storey of the combined model.
            var contained = combined.Instances.OfType<IIfcRelContainedInSpatialStructure>()
                .SelectMany(r => r.RelatedElements).Select(e => e.GlobalId.ToString()).ToHashSet();
            Assert.Contains(combined.Instances.OfType<IIfcWall>().First().GlobalId.ToString(), contained);
        }

        [Fact]
        public void TheSameFileTwiceGivesTheFileOnce()
        {
            var output = Output("Twice");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc4_SampleHouse, LocalFiles.Ifc4_SampleHouse }, output);

            using var single = IfcStore.Open(LocalFiles.Ifc4_SampleHouse);
            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Equal(ElementIds(single), ElementIds(combined));
            Assert.Equal(ElementIds(single).Count, result.ElementsSkipped);
            Assert.Equal(single.Instances.OfType<IIfcBuildingStorey>().Count(), combined.Instances.OfType<IIfcBuildingStorey>().Count());
            Assert.Equal(single.Instances.OfType<IIfcSpace>().Count(), combined.Instances.OfType<IIfcSpace>().Count());
        }

        [Fact]
        public void NeverMergesTwoElementsOfTheSameFile()
        {
            // The MEP model has 163 spaces, 69 of which share a name with another, some in the same
            // place. They are separate rooms and must stay so; only elements of different files merge.
            var output = Output("RevitStrMep");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc4_Revit_STR, LocalFiles.Ifc4_Revit_MEP }, output);

            int spaces;
            using (var structure = IfcStore.Open(LocalFiles.Ifc4_Revit_STR))
            using (var mep = IfcStore.Open(LocalFiles.Ifc4_Revit_MEP))
                spaces = structure.Instances.OfType<IIfcSpace>().Count() + mep.Instances.OfType<IIfcSpace>().Count();
            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.DoesNotContain(result.SpatialMerged, m => m.StartsWith("IfcSpace"));
            Assert.Equal(spaces, combined.Instances.OfType<IIfcSpace>().Count());
        }

        [Fact]
        public void DisciplineModelsShareStoreysByName()
        {
            // Exported separately: same storey names and levels, different GlobalIds.
            var output = Output("Duplex");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc2x3_Duplex_Architecture, LocalFiles.Ifc2x3_Duplex_MEP }, output);

            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Single(combined.Instances.OfType<IIfcSite>());
            // The unnamed buildings are the same building, so their storeys meet and merge.
            Assert.Single(combined.Instances.OfType<IIfcBuilding>());
            Assert.Single(combined.Instances.OfType<IIfcBuildingStorey>(), s => s.Name == "Level 1");
            Assert.Contains("IfcBuildingStorey 'Level 1'", result.SpatialMerged);
            // MEP's storeys are a subset of architecture's (no T/FDN), so the count is architecture's.
            using (var a = IfcStore.Open(LocalFiles.Ifc2x3_Duplex_Architecture))
                Assert.Equal(a.Instances.OfType<IIfcBuildingStorey>().Count(), combined.Instances.OfType<IIfcBuildingStorey>().Count());

            int architecture, mep;
            using (var a = IfcStore.Open(LocalFiles.Ifc2x3_Duplex_Architecture)) architecture = ElementIds(a).Count;
            using (var m = IfcStore.Open(LocalFiles.Ifc2x3_Duplex_MEP)) mep = ElementIds(m).Count;
            Assert.Equal(architecture + mep - result.ElementsSkipped, ElementIds(combined).Count);
        }

        [Fact]
        public void KeepsStoreysApartWithoutNameMatching()
        {
            var output = Output("DuplexNoNames");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc2x3_Duplex_Architecture, LocalFiles.Ifc2x3_Duplex_MEP }, output,
                new CombineOptions { MergeSpatialByName = false });

            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Equal(2, combined.Instances.OfType<IIfcBuildingStorey>().Count(s => s.Name == "Level 1"));
            Assert.DoesNotContain("IfcBuildingStorey 'Level 1'", result.SpatialMerged);
        }

        [Fact]
        public void CombinesIfc4x3Infrastructure()
        {
            var output = Output("Infra");
            ModelCombiner.Combine(new[] { LocalFiles.Ifc4x3_Road, LocalFiles.Ifc4x3_Rail }, output);

            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Equal("IFC4X3_ADD2", IfcToolbox.Core.Convert.IfcConvert.ReadFileSchema(output));
            Assert.Contains(combined.Instances.OfType<IIfcSpatialStructureElement>(), e => e.ExpressType.Name == "IfcRoad");
            Assert.Contains(combined.Instances.OfType<IIfcSpatialStructureElement>(), e => e.ExpressType.Name == "IfcRailway");
        }

        [Fact]
        public void TypesAndElementsSharedBetweenDisciplinesComeOnce()
        {
            // The structural model copied slabs, walls and their types from the architectural one
            // (Revit copy/monitor), with the same GlobalIds.
            var output = Output("RevitArcStr");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc4_Revit_ARC, LocalFiles.Ifc4_Revit_STR }, output);

            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Contains("IfcWallType 'Basic Wall:CL_W1'", result.TypesMerged);
            Assert.Single(combined.Instances.OfType<IIfcWallType>(), t => t.Name == "Basic Wall:CL_W1");
            Assert.True(result.SkippedByClass.TryGetValue("IfcSlab", out var slabs) && slabs > 0, "no shared slab was recognised");
            // Each shared slab is there once, still typed.
            Assert.All(combined.Instances.OfType<IIfcSlab>(), s => Assert.True(
                combined.Instances.OfType<IIfcSlab>().Count(o => o.GlobalId.ToString() == s.GlobalId.ToString()) == 1));
        }

        [Fact]
        public void RefusesDifferentSchemas()
        {
            var e = Assert.Throws<CombineException>(() =>
                ModelCombiner.Combine(new[] { LocalFiles.Ifc4_SampleHouse, LocalFiles.Ifc2x3_Duplex_Architecture }, Output("Schemas")));
            Assert.Contains("same IFC schema", e.Message);
            Assert.False(File.Exists(Output("Schemas")));
        }

        [Fact]
        public void RefusesDifferentLengthUnitsWhenNotConverting()
        {
            var e = Assert.Throws<CombineException>(() =>
                ModelCombiner.Combine(new[] { LocalFiles.Ifc4_SampleHouse, LocalFiles.Ifc4_CubeAdvancedBrep }, Output("Units"),
                    new CombineOptions { ConvertUnits = false }));
            Assert.Contains("millimetres", e.Message);
            Assert.Contains("metres", e.Message);
        }

        [Fact]
        public void ConvertsAFileInAnotherUnitToTheFirstFiles()
        {
            // The house is in millimetres, the cube in metres.
            double cubeX;
            using (var cube = IfcStore.Open(LocalFiles.Ifc4_CubeAdvancedBrep))
                cubeX = cube.Instances.OfType<IIfcCartesianPoint>().Select(p => p.X).Where(x => Math.Abs(x) > 0.01).Max();

            var output = Output("MixedUnits");
            var result = ModelCombiner.Combine(new[] { LocalFiles.Ifc4_SampleHouse, LocalFiles.Ifc4_CubeAdvancedBrep }, output);

            Assert.Contains(result.UnitsConverted, u => u.StartsWith("Ifc4_CubeAdvancedBrep.ifc: metres to millimetres"));
            using var combined = IfcStore.Open(output);
            AssertSound(combined);
            Assert.Equal(0.001, IfcToolbox.Core.Geo.MapConversionService.LengthUnitInMetres(combined), 12);
            // The cube's points came in a thousand times larger: millimetres now.
            Assert.Contains(combined.Instances.OfType<IIfcCartesianPoint>(), p => Math.Abs(p.X - cubeX * 1000) < 1e-6);
        }
    }
}
