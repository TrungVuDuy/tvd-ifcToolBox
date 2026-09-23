using IfcToolbox.Core.Editors;
using IfcToolbox.Core.Geo;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// The optimizer and relocator create geometry entities, and point creation went through a
    /// rewrite to become schema agnostic. Neither tool had tests before, so these pin down that
    /// they still work on IFC2X3 and IFC4.
    /// </summary>
    public class PointEditingTests
    {
        private static string Copy(string source, string tag)
        {
            var target = Path.Combine(LocalFiles.TestOutputFolder,
                Path.GetFileNameWithoutExtension(source) + "_" + tag + ".ifc");
            File.Copy(source, target, overwrite: true);
            return target;
        }

        [Theory]
        [InlineData(LocalFiles.Ifc4_SampleHouse, XbimSchemaVersion.Ifc4)]
        [InlineData(LocalFiles.Ifc2x3_Duplex_Architecture, XbimSchemaVersion.Ifc2X3)]
        public void OptimizerKeepsProductsAndSchema(string file, XbimSchemaVersion schema)
        {
            var source = Copy(file, "opt");
            int productsBefore;
            using (var before = IfcStore.Open(source))
                productsBefore = before.Instances.OfType<IIfcProduct>().Count();

            var config = ConfigFactory.CreateConfigOptimize();
            config.OptimizePoint = true;
            config.OptimizePointList = true;
            config.Precision = 4;
            config.MergeOnlyResources = true;
            var output = OptimizerProcessor.Process(source, config).FilePaths.Single();

            using var result = IfcStore.Open(output);
            Assert.Equal(schema, result.SchemaVersion);
            Assert.Equal(productsBefore, result.Instances.OfType<IIfcProduct>().Count());
            // Rounded to 4 places: no coordinate may carry more.
            Assert.All(result.Instances.OfType<IIfcCartesianPoint>(), p =>
                Assert.All(p.Coordinates, c => Assert.True(PointPrecision.GetDoublePlaces(c) <= 4, $"{(double)c} not rounded")));
        }

        [Theory]
        [InlineData(LocalFiles.Ifc4_SampleHouse, XbimSchemaVersion.Ifc4)]
        [InlineData(LocalFiles.Ifc2x3_Duplex_Architecture, XbimSchemaVersion.Ifc2X3)]
        public void RelocatorMovesTheWorldOrigin(string file, XbimSchemaVersion schema)
        {
            var source = Copy(file, "reloc");
            var config = ConfigFactory.CreateConfigRelocate();
            config.AlignWorldCoordinates = true;
            config.WorldPlacement = GeoFactory.CreateGeoPlacement(
                new List<double> { 10, 20, 30 }, new List<double> { 0, 1, 0 }, new List<double> { 0, 0, 1 });
            var output = RelocatorProcessor.Process(source, config).FilePaths.Single();

            using var result = IfcStore.Open(output);
            Assert.Equal(schema, result.SchemaVersion);
            var context = result.Instances.OfType<IIfcGeometricRepresentationContext>()
                .First(c => !(c is IIfcGeometricRepresentationSubContext) && c.ContextType == "Model");
            var placement = (IIfcAxis2Placement3D)context.WorldCoordinateSystem;
            Assert.Equal(new[] { 10d, 20d, 30d }, new[] { placement.Location.X, placement.Location.Y, placement.Location.Z });
            Assert.Equal(new[] { 0d, 1d, 0d }, placement.RefDirection.DirectionRatios.Select(r => (double)r).ToArray());
        }

        [Theory]
        [InlineData(1.5, 1)]
        [InlineData(1.23456, 5)]
        [InlineData(100, 0)]
        [InlineData(1E-05, 5)]
        [InlineData(1.25E-07, 9)]
        [InlineData(-0.001, 3)]
        public void DecimalPlacesAreCountedIndependentlyOfCulture(double value, int expected)
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                // Vietnamese writes 1,5 - the old implementation looked for '.' and found none.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                Assert.Equal(expected, PointPrecision.GetDoublePlaces(value));

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.Equal(expected, PointPrecision.GetDoublePlaces(value));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]
        public void PointsAreCreatedInTheModelsOwnSchema()
        {
            foreach (var (file, expected) in new[]
            {
                (LocalFiles.Ifc2x3_Duplex_Architecture, "Xbim.Ifc2x3."),
                (LocalFiles.Ifc4_SampleHouse, "Xbim.Ifc4."),
                (LocalFiles.Ifc4x3_Road, "Xbim.Ifc4x3."),
            })
            {
                using var model = IfcStore.Open(file);
                using var txn = model.BeginTransaction("create");
                var point = PointFactory.CreatePoint(model, new List<double> { 1, 2, 3 });
                var direction = PointFactory.CreateDirection(model, new List<double> { 0, 0, 1 });

                Assert.StartsWith(expected, point.GetType().FullName);
                Assert.StartsWith(expected, direction.GetType().FullName);
                Assert.Equal(3, point.Z);
                txn.RollBack();
            }
        }
    }
}
