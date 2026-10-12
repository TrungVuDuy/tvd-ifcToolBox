using IfcToolbox.Core.Convert;
using IfcToolbox.Core.Editors;
using IfcToolbox.Core.Geo;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace IfcToolbox.Tests
{
    public class LengthUnitConverterTests
    {
        private static IfcStore OpenCopy(string file, string tag)
        {
            var copy = Path.Combine(LocalFiles.TestOutputFolder, "Units_" + tag + Path.GetExtension(file));
            File.Copy(file, copy, overwrite: true);
            return IfcStore.Open(copy);
        }

        private static LengthUnitConversion Convert(IfcStore model, string unit)
        {
            using var txn = model.BeginTransaction("Convert units");
            var result = LengthUnitConverter.Convert(model, unit);
            txn.Commit();
            return result;
        }

        [Fact]
        public void MillimetresToMetresRescalesLengthsOnly()
        {
            using var model = OpenCopy(LocalFiles.Ifc4_SampleHouse, "mm_to_m");
            var point = model.Instances.OfType<IIfcCartesianPoint>().First(p => Math.Abs(p.X) > 100);
            double x = point.X;
            var solid = model.Instances.OfType<IIfcExtrudedAreaSolid>().First();
            double depth = solid.Depth;
            var roof = model.Instances.OfType<IIfcBuildingStorey>().Single(s => s.Name == "Roof");
            double elevation = roof.Elevation.Value;
            var area = model.Instances.OfType<IIfcQuantityArea>().FirstOrDefault();
            double? areaValue = area?.AreaValue;

            var result = Convert(model, "m");

            Assert.Equal(0.001, result.FromMetres, 12);
            Assert.True(result.ValuesScaled > 1000);
            Assert.Equal(x / 1000, point.X, 9);
            Assert.Equal(depth / 1000, (double)solid.Depth, 9);
            Assert.Equal(elevation / 1000, (double)roof.Elevation.Value, 9);
            // Areas have a unit of their own and do not change.
            if (area != null)
                Assert.Equal(areaValue.Value, (double)area.AreaValue, 9);
            Assert.Equal(1, MapConversionService.LengthUnitInMetres(model), 12);
        }

        [Fact]
        public void ConvertingThereAndBackGivesTheOriginalNumbers()
        {
            using var model = OpenCopy(LocalFiles.Ifc4_SampleHouse, "roundtrip");
            var before = model.Instances.OfType<IIfcCartesianPoint>().Take(500).Select(p => p.Coordinates.Select(c => (double)c).ToArray()).ToList();

            Convert(model, "m");
            Convert(model, "mm");

            var after = model.Instances.OfType<IIfcCartesianPoint>().Take(500).Select(p => p.Coordinates.Select(c => (double)c).ToArray()).ToList();
            for (int i = 0; i < before.Count; i++)
                for (int j = 0; j < before[i].Length; j++)
                    Assert.Equal(before[i][j], after[i][j], 6);
        }

        /// <summary>
        /// IfcConvert writes geometry in metres whatever the model's unit, so the original
        /// millimetre model and its metre conversion must come out the same. This catches any
        /// length the converter missed, wherever it hides in the geometry.
        /// </summary>
        [Theory]
        [InlineData(LocalFiles.Ifc4_SampleHouse, "m")]
        [InlineData(LocalFiles.Ifc2x3_Duplex_Architecture, "mm")]
        public void GeometryIsUnchangedAccordingToIfcConvert(string file, string unit)
        {
            var folder = Path.Combine(LocalFiles.TestOutputFolder, "UnitsGeometry");
            Directory.CreateDirectory(folder);
            var original = Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + ".ifc");
            File.Copy(file, original, overwrite: true);
            var converted = Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + "_" + unit + ".ifc");
            using (var model = IfcStore.Open(original))
            {
                Convert(model, unit);
                model.SaveAs(converted);
            }

            var a = Bounds(ToObj(original));
            var b = Bounds(ToObj(converted));
            for (int i = 0; i < 6; i++)
                Assert.Equal(a[i], b[i], 3);
        }

        private static string ToObj(string ifc)
        {
            var obj = Path.ChangeExtension(ifc, ".obj");
            IfcConvert.Convert(ifc, obj, new ConvertOptionsWrap { ExcludeEntities = IfcConvert.GetExcludeEntitiesDefault(ConvertTargetFormat.OBJ) },
                ConvertTargetFormat.OBJ, Path.ChangeExtension(ifc, ".log.txt"));
            return obj;
        }

        /// <summary>min x, y, z, max x, y, z of the OBJ's vertices.</summary>
        private static double[] Bounds(string obj)
        {
            var vertices = File.ReadLines(obj).Where(l => l.StartsWith("v ", StringComparison.Ordinal))
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(3).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray())
                .ToList();
            Assert.NotEmpty(vertices);
            return new[]
            {
                vertices.Min(v => v[0]), vertices.Min(v => v[1]), vertices.Min(v => v[2]),
                vertices.Max(v => v[0]), vertices.Max(v => v[1]), vertices.Max(v => v[2]),
            };
        }

        [Fact]
        public void KeepsTheModelInPlaceOnTheMap()
        {
            using var model = OpenCopy(LocalFiles.Ifc4_SampleHouse, "georef");
            using (var txn = model.BeginTransaction("Georeference"))
            {
                MapConversionService.Write(model, new GeoMapConversion
                {
                    TargetCrs = new ProjectedCrs { Name = "EPSG:25832" },
                    Parameters = new MapConversionParameters { Eastings = 333000, Northings = 5800000, OrthogonalHeight = 0, XAxisAbscissa = 1, XAxisOrdinate = 0, Scale = 0.001 },
                });
                txn.Commit();
            }
            var before = MapConversionService.ToMap(MapConversionService.Read(model).Parameters, 12000, 3000, 2500);

            Convert(model, "m");

            var map = MapConversionService.Read(model).Parameters;
            Assert.Equal(1, map.Scale.Value, 9);
            var after = MapConversionService.ToMap(map, 12, 3, 2.5);
            Assert.Equal(before.Easting, after.Easting, 6);
            Assert.Equal(before.Northing, after.Northing, 6);
            Assert.Equal(before.Height, after.Height, 6);
        }

        [Fact]
        public void ValuesWithTheirOwnUnitStay()
        {
            using var model = OpenCopy(LocalFiles.Ifc4_SampleHouse, "ownunit");
            IIfcPropertySingleValue own;
            using (var txn = model.BeginTransaction("Property in its own unit"))
            {
                // 5 metres, said so explicitly.
                var metre = SchemaEntityFactory.New<IIfcSIUnit>(model, "IfcSIUnit");
                metre.UnitType = IfcUnitEnum.LENGTHUNIT;
                metre.Name = IfcSIUnitName.METRE;
                own = SchemaEntityFactory.New<IIfcPropertySingleValue>(model, "IfcPropertySingleValue");
                own.Name = "Reach";
                own.NominalValue = new Xbim.Ifc4.MeasureResource.IfcLengthMeasure(5);
                own.Unit = metre;
                txn.Commit();
            }

            var result = Convert(model, "m");

            Assert.Equal(5, (double)(Xbim.Ifc4.MeasureResource.IfcLengthMeasure)own.NominalValue, 9);
            Assert.True(result.ValuesWithOwnUnit > 0);
        }

        [Theory]
        [InlineData(LocalFiles.Ifc2x3_Duplex_Architecture, "mm", 1000)]
        [InlineData(LocalFiles.Ifc4x3_Road, "m", 0.001)]
        public void WorksOnEverySchema(string file, string unit, double factor)
        {
            using var model = OpenCopy(file, "schema_" + unit);
            var point = model.Instances.OfType<IIfcCartesianPoint>().First(p => Math.Abs(p.X) > 1);
            double x = point.X;

            Convert(model, unit);

            Assert.Equal(x * factor, point.X, 6);
            Assert.Equal(LengthUnitConverter.Units[unit].Metres, MapConversionService.LengthUnitInMetres(model), 12);
        }

        [Fact]
        public void TheSavedFileDeclaresTheNewUnit()
        {
            var output = Path.Combine(LocalFiles.TestOutputFolder, "Units_saved_m.ifc");
            using (var model = OpenCopy(LocalFiles.Ifc4_SampleHouse, "save"))
            {
                Convert(model, "m");
                model.SaveAs(output);
            }
            using var saved = IfcStore.Open(output);
            Assert.Equal(1, MapConversionService.LengthUnitInMetres(saved), 12);
        }
    }
}
