using IfcToolbox.Core.Editors;
using IfcToolbox.Core.Geo;
using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Core.Utilities;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Helper;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// Every tool run against real IFC 4.3 (IFC4X3_ADD2) models from buildingSMART's Simple
    /// Scene set. These are infrastructure models - roads, railways - whose spatial structure
    /// (nested sites, facilities, facility parts) does not exist in IFC4.
    /// </summary>
    public class Ifc4x3Tests
    {
        /// <summary>Copies a sample into the output folder under a unique name, so tests do not share files.</summary>
        private static string Copy(string source, string tag)
        {
            var target = Path.Combine(LocalFiles.TestOutputFolder,
                Path.GetFileNameWithoutExtension(source) + "_" + tag + ".ifc");
            File.Copy(source, target, overwrite: true);
            return target;
        }

        /// <summary>An output file has to reopen as IFC4X3 - writing it as IFC4 would silently drop every infrastructure entity.</summary>
        private static IfcStore OpenOutput(string path)
        {
            Assert.True(File.Exists(path), $"expected output {path}");
            var model = IfcStore.Open(path);
            Assert.Equal(XbimSchemaVersion.Ifc4x3, model.SchemaVersion);
            return model;
        }

        private static int LabelOf<T>(IfcStore model, string name) where T : IIfcRoot =>
            model.Instances.OfType<T>().First(e => e.Name == name).EntityLabel;

        #region Reading

        [Fact]
        public void SchemaCheckAcceptsIfc4x3()
        {
            var result = SchemaCheck.GetResult(new List<string>
            {
                LocalFiles.Ifc4x3_Road, LocalFiles.Ifc4x3_Rail, LocalFiles.Ifc4x3_Building,
            });

            Assert.True(result.SchemaPass, "rejected: " + string.Join(", ", result.UnsupportedFileNames));
        }

        [Fact]
        public void SpatialTreeIncludesFacilitiesAndNestedSites()
        {
            using var model = IfcStore.Open(LocalFiles.Ifc4x3_Road);
            var tree = HierarchyReader.GetFullHierarchy(model);

            var names = Flatten(tree).Select(n => n.Name).ToList();
            Assert.Contains("road parking - site", names);          // a site inside a site
            Assert.Contains("road parking - road", names);          // IfcRoad
            Assert.Contains("road  - carriageway", names);          // IfcRoadPart inside an IfcRoadPart
        }

        [Fact]
        public void GeoreferencingIsRead()
        {
            var reference = AnalyseProcessor.GetGeoReference(LocalFiles.Ifc4x3_Building);

            var map = reference.MapConvensionCRS;
            Assert.NotNull(map);
            Assert.Equal("EPSG:32760", map.CRS_Name);
            Assert.Equal(729013.3488297004, map.Eastings.Value, 6);
            Assert.Equal(9063992.684697364, map.Northings.Value, 6);
            // The model is in millimetres and the map in metres.
            Assert.Equal(0.001, map.Scale.Value, 9);
            // A 60 degree rotation: (cos 60, sin 60).
            Assert.Equal(0.5, map.XAxisAbscissa.Value, 9);
            Assert.Equal(0.8660254037844387, map.XAxisOrdinate.Value, 9);
        }

        #endregion

        #region Splitting

        [Fact]
        public void SplitByProductKeepsInfrastructureEntities()
        {
            var source = Copy(LocalFiles.Ifc4x3_Road, "byproduct");
            using var model = IfcStore.Open(source);
            var carriageway = LabelOf<IIfcSpatialStructureElement>(model, "road  - carriageway");

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.ByProduct;
            config.SelectedItems = new List<string> { carriageway.ToString() };
            config.Suffix = "Carriageway";
            var output = Assert.Single(SplitterProcessor.Split(model, config, source));

            using var result = OpenOutput(output);
            Assert.NotEmpty(result.Instances.OfType<IIfcElement>());
            // The copy has to bring the spatial chain up to the project with it.
            Assert.Single(result.Instances.OfType<IIfcProject>());
            Assert.Contains(result.Instances.OfType<IIfcSpatialStructureElement>(),
                s => s.ExpressType.ExpressNameUpper == "IFCROAD");
        }

        [Fact]
        public void SplitBySiteFindsNestedSites()
        {
            var source = Copy(LocalFiles.Ifc4x3_Building, "bysite");
            using var model = IfcStore.Open(source);
            // 'house - site' sits inside 'environment - site', not directly under the project.
            var houseSite = LabelOf<IIfcSite>(model, "house - site");

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.BySite;
            config.SelectedItems = new List<string> { houseSite.ToString() };
            var output = Assert.Single(SplitterProcessor.Split(model, config, source));

            using var result = OpenOutput(output);
            Assert.Contains(result.Instances.OfType<IIfcBuilding>(), b => b.Name == "Single-family house");
        }

        [Fact]
        public void SplitByBuildingStoreyOnIfc4x3()
        {
            var source = Copy(LocalFiles.Ifc4x3_Building, "bystorey");
            using var model = IfcStore.Open(source);
            var storey = LabelOf<IIfcBuildingStorey>(model, "00 groundfloor");

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.ByBuildingStorey;
            config.SelectedItems = new List<string> { storey.ToString() };
            var output = Assert.Single(SplitterProcessor.Split(model, config, source));

            using var result = OpenOutput(output);
            Assert.NotEmpty(result.Instances.OfType<IIfcElement>());
        }

        [Fact]
        public void SplitDataOnlyOnIfc4x3()
        {
            var source = Copy(LocalFiles.Ifc4x3_Rail, "dataonly");
            using var model = IfcStore.Open(source);

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.DataOnly;
            var output = Assert.Single(SplitterProcessor.Split(model, config, source));

            using var result = OpenOutput(output);
            Assert.NotEmpty(result.Instances.OfType<IIfcProduct>());
        }

        [Fact]
        public void SplitByFacilityTakesOneRoad()
        {
            var source = Copy(LocalFiles.Ifc4x3_Road, "byfacility");
            using var model = IfcStore.Open(source);
            var road = Facilities.AllFacilities(model).First(f => f.Name == "road parking - road");
            var elementsInRoad = ElementsUnder(road).Select(e => e.GlobalId.ToString()).ToList();
            Assert.NotEmpty(elementsInRoad);

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.ByFacility;
            config.SelectedItems = new List<string> { road.EntityLabel.ToString() };
            var output = Assert.Single(SplitterProcessor.Split(model, config, source));

            using var result = OpenOutput(output);
            var copiedElements = result.Instances.OfType<IIfcElement>().Select(e => e.GlobalId.ToString()).ToList();
            // Every element of that road, and nothing from the other roads.
            Assert.Equal(elementsInRoad.OrderBy(g => g), copiedElements.OrderBy(g => g));
            Assert.Contains(result.Instances.OfType<IIfcSpatialStructureElement>(), s => s.Name == "road parking - road");
            Assert.DoesNotContain(result.Instances.OfType<IIfcSpatialStructureElement>(), s => s.Name == "bridge road connection");
        }

        [Fact]
        public void SplitByFacilityPartTakesOneCarriageway()
        {
            var source = Copy(LocalFiles.Ifc4x3_Road, "bypart");
            using var model = IfcStore.Open(source);
            var carriageway = Facilities.AllFacilityParts(model)
                .First(p => Facilities.UsageOf(p) == "CARRIAGEWAY");
            var expected = ElementsUnder(carriageway).Select(e => e.GlobalId.ToString()).OrderBy(g => g).ToList();
            Assert.NotEmpty(expected);

            var config = ConfigFactory.CreateConfigSplit();
            config.SplitStrategy = SplitStrategy.ByFacilityPart;
            config.SelectedItems = new List<string> { carriageway.EntityLabel.ToString() };
            var output = Assert.Single(SplitterProcessor.Split(model, config, source));

            using var result = OpenOutput(output);
            Assert.Equal(expected, result.Instances.OfType<IIfcElement>().Select(e => e.GlobalId.ToString()).OrderBy(g => g));
        }

        [Fact]
        public void FacilityTreesAreOfferedOnlyForIfc4x3()
        {
            using (var road = IfcStore.Open(LocalFiles.Ifc4x3_Road))
            {
                var trees = TreesReader.GetSpatialTrees(road);
                var facilities = Flatten(Assert.Single(trees.FacilityNodes)).Where(n => !n.IsComposition).Select(n => n.Name).ToList();
                Assert.Contains("road parking - road", facilities);
                Assert.Contains(Flatten(Assert.Single(trees.FacilityPartNodes)), n => n.Name == "road  - carriageway");
            }

            using var house = IfcStore.Open(LocalFiles.Ifc4_SampleHouse);
            var ifc4Trees = TreesReader.GetSpatialTrees(house);
            Assert.Empty(ifc4Trees.FacilityNodes);
            Assert.Empty(ifc4Trees.FacilityPartNodes);
        }

        #endregion

        #region Georeferencing

        [Fact]
        public void MapConversionPlacesTheLocalOriginAtTheOffset()
        {
            using var model = IfcStore.Open(LocalFiles.Ifc4x3_Building);
            var map = MapConversionService.Read(model);

            Assert.Equal("EPSG:32760", map.TargetCrs.Name);
            var (e, n, h) = MapConversionService.ToMap(map.Parameters, 0, 0, 0);
            Assert.Equal(map.Parameters.Eastings.Value, e, 9);
            Assert.Equal(map.Parameters.Northings.Value, n, 9);
            Assert.Equal(map.Parameters.OrthogonalHeight.Value, h, 9);
            Assert.Equal(60, MapConversionService.RotationDegrees(map.Parameters), 9);
        }

        [Fact]
        public void MapConversionAppliesUnitScaleAndRotation()
        {
            using var model = IfcStore.Open(LocalFiles.Ifc4x3_Building);
            var p = MapConversionService.Read(model).Parameters;

            // 1000 mm along local x is 1 m on the map, rotated 60 degrees anti-clockwise.
            var (e, n, h) = MapConversionService.ToMap(p, 1000, 0, 2000);
            Assert.Equal(p.Eastings.Value + 0.5, e, 9);
            Assert.Equal(p.Northings.Value + 0.8660254037844387, n, 9);
            // Scale converts units, so it applies to heights too.
            Assert.Equal(p.OrthogonalHeight.Value + 2, h, 9);
        }

        [Fact]
        public void MapConversionRoundTrips()
        {
            var p = new MapConversionParameters
            {
                Eastings = 500000, Northings = 1200000, OrthogonalHeight = 12,
                XAxisAbscissa = 3, XAxisOrdinate = -4, // not normalised, and a negative angle
                Scale = 0.001, FactorX = 0.9996, FactorY = 0.9996, FactorZ = 1,
            };

            var (e, n, h) = MapConversionService.ToMap(p, 12345.6, -789.1, 250);
            var (x, y, z) = MapConversionService.ToLocal(p, e, n, h);

            Assert.Equal(12345.6, x, 6);
            Assert.Equal(-789.1, y, 6);
            Assert.Equal(250, z, 6);
        }

        [Fact]
        public void GeoreferencingCanBeWrittenToAnIfc4Model()
        {
            var source = Copy(LocalFiles.Ifc4_SampleHouse, "georef4");
            var conversion = new GeoMapConversion
            {
                Parameters = new MapConversionParameters
                {
                    Eastings = 333000, Northings = 5800000, OrthogonalHeight = 35,
                    XAxisAbscissa = 1, XAxisOrdinate = 0, Scale = 0.001,
                },
                TargetCrs = new ProjectedCrs { Name = "EPSG:25832", Description = "ETRS89 / UTM zone 32N" },
            };

            var config = ConfigFactory.CreateConfigRelocate();
            config.MapConversion = conversion;
            var output = RelocatorProcessor.Process(source, config).FilePaths.Single();

            using var result = IfcStore.Open(output);
            var read = MapConversionService.Read(result);
            Assert.NotNull(read);
            Assert.Equal("EPSG:25832", read.TargetCrs.Name);
            Assert.Equal(333000, read.Parameters.Eastings.Value, 6);
            Assert.Equal(0.001, read.Parameters.Scale.Value, 9);
            Assert.Single(result.Instances.OfType<IIfcMapConversion>());
        }

        [Fact]
        public void ScaledConversionIsWrittenOnIfc4x3AndReplacesTheOldOne()
        {
            var source = Copy(LocalFiles.Ifc4x3_Building, "georefscaled");
            var conversion = new GeoMapConversion
            {
                Parameters = new MapConversionParameters
                {
                    Eastings = 729013.35, Northings = 9063992.68, OrthogonalHeight = 1.3,
                    XAxisAbscissa = 0.5, XAxisOrdinate = 0.8660254037844387, Scale = 0.001,
                    FactorX = 0.9996, FactorY = 0.9996, FactorZ = 1,
                },
                TargetCrs = new ProjectedCrs { Name = "EPSG:32760" },
            };

            var config = ConfigFactory.CreateConfigRelocate();
            config.MapConversion = conversion;
            var output = RelocatorProcessor.Process(source, config).FilePaths.Single();

            using var result = OpenOutput(output);
            // The model already had a plain IfcMapConversion; it has to be replaced, not doubled.
            var map = Assert.Single(result.Instances.OfType<IIfcMapConversion>());
            Assert.Equal("IFCMAPCONVERSIONSCALED", map.ExpressType.ExpressNameUpper);
            Assert.Single(result.Instances.OfType<IIfcProjectedCRS>());

            var read = MapConversionService.Read(result).Parameters;
            Assert.Equal(0.9996, read.FactorX.Value, 9);
            Assert.Equal(1.0, read.FactorZ.Value, 9);
        }

        [Fact]
        public void ScaledConversionIsRefusedOnIfc4()
        {
            using var model = IfcStore.Open(LocalFiles.Ifc4_SampleHouse);
            using var txn = model.BeginTransaction("georef");
            var conversion = new GeoMapConversion
            {
                Parameters = new MapConversionParameters { Eastings = 1, Northings = 1, FactorX = 0.9996 },
                TargetCrs = new ProjectedCrs { Name = "EPSG:25832" },
            };

            var exception = Assert.Throws<System.NotSupportedException>(() => MapConversionService.Write(model, conversion));
            Assert.Contains("IfcMapConversionScaled", exception.Message);
        }

        [Fact]
        public void GeoreferencingIsRefusedOnIfc2x3()
        {
            using var model = IfcStore.Open(LocalFiles.Ifc2x3_Duplex_Architecture);
            using var txn = model.BeginTransaction("georef");
            var conversion = new GeoMapConversion
            {
                Parameters = new MapConversionParameters { Eastings = 1, Northings = 1 },
                TargetCrs = new ProjectedCrs { Name = "EPSG:25832" },
            };

            Assert.Throws<System.NotSupportedException>(() => MapConversionService.Write(model, conversion));
        }

        #endregion

        #region Conversion

        [Fact]
        public void BundledIfcConvertRefusesIfc4x3WithAnActionableMessage()
        {
            Assert.Equal("IFC4X3_ADD2", IfcToolbox.Core.Convert.IfcConvert.ReadFileSchema(LocalFiles.Ifc4x3_Road));

            var target = Path.Combine(LocalFiles.TestOutputFolder, "road.glb");
            var exception = Assert.Throws<System.NotSupportedException>(() =>
                IfcToolbox.Core.Convert.IfcConvert.Convert(LocalFiles.Ifc4x3_Road, target,
                    new IfcToolbox.Core.Convert.ConvertOptionsWrap(), IfcToolbox.Core.Convert.ConvertTargetFormat.GLB).ToList());

            Assert.Contains("IFC4X3", exception.Message);
            Assert.Contains(IfcToolbox.Core.Convert.IfcConvert.ExecutablePathVariable, exception.Message);
        }

        #endregion

        #region Editing

        [Fact]
        public void OptimizerKeepsEveryProduct()
        {
            var source = Copy(LocalFiles.Ifc4x3_Road, "optimize");
            int productsBefore;
            long instancesBefore;
            using (var before = IfcStore.Open(source))
            {
                productsBefore = before.Instances.OfType<IIfcProduct>().Count();
                instancesBefore = before.Instances.Count;
            }

            var config = ConfigFactory.CreateConfigOptimize();
            config.OptimizePoint = true;
            config.OptimizePointList = true;
            config.Precision = 4;
            config.MergeOnlyResources = true;
            var processorResult = OptimizerProcessor.Process(source, config);

            using var result = OpenOutput(processorResult.FilePaths.Single());
            Assert.Equal(productsBefore, result.Instances.OfType<IIfcProduct>().Count());
            Assert.True(result.Instances.Count <= instancesBefore,
                $"optimising grew the model from {instancesBefore} to {result.Instances.Count} instances");
        }

        [Fact]
        public void RelocatorMovesTheWorldCoordinateSystem()
        {
            var source = Copy(LocalFiles.Ifc4x3_Rail, "relocate");

            var config = ConfigFactory.CreateConfigRelocate();
            config.AlignWorldCoordinates = true;
            config.WorldPlacement = GeoFactory.CreateGeoPlacement(
                new List<double> { 1000, 2000, 0 }, new List<double> { 1, 0, 0 }, new List<double> { 0, 0, 1 });
            var processorResult = RelocatorProcessor.Process(source, config);

            using var result = OpenOutput(processorResult.FilePaths.Single());
            var context = result.Instances.OfType<IIfcGeometricRepresentationContext>()
                .First(c => !(c is IIfcGeometricRepresentationSubContext) && c.ContextType == "Model");
            var origin = ((IIfcAxis2Placement3D)context.WorldCoordinateSystem).Location;
            Assert.Equal(1000, origin.X, 6);
            Assert.Equal(2000, origin.Y, 6);
        }

        [Fact]
        public void AnonymizerRemovesUserInformation()
        {
            var source = Copy(LocalFiles.Ifc4x3_Road, "anonymize");
            var config = ConfigFactory.CreateConfigAnonymize();
            config.AnonymeUserInfo = true;
            config.RemoveAllUserInfo();

            using var model = IfcStore.Open(source);
            using (var txn = model.BeginTransaction("anonymize"))
            {
                AnonymizerProcessor.Anonymize(model, config);
                txn.Commit();
            }

            Assert.Empty(model.Instances.OfType<IIfcPerson>());
            Assert.Empty(model.Instances.OfType<IIfcOrganization>());
        }

        #endregion

        #region IDS

        [Fact]
        public void IdsChecksInfrastructureEntities()
        {
            // Every carriageway must sit inside a road. Written against IFC4X3_ADD2, which
            // is the only IFC version that has these classes.
            var document = new IdsDocument
            {
                Info = new IdsInfo { Title = "Road structure" },
                Specifications =
                {
                    new IdsSpecification
                    {
                        Name = "Carriageways belong to a road",
                        IfcVersions = { IdsIfcVersion.Ifc4X3Add2 },
                        Applicability = new IdsApplicability
                        {
                            Entity = new IdsEntityFacet
                            {
                                Name = new IdsValue("IFCROADPART"),
                                PredefinedType = new IdsValue("CARRIAGEWAY"),
                            },
                        },
                        Requirements = new IdsRequirements
                        {
                            PartOf =
                            {
                                new IdsPartOfFacet
                                {
                                    Relation = IdsRelations.Aggregates,
                                    Entity = new IdsEntityFacet { Name = new IdsValue("IFCROAD") },
                                },
                            },
                        },
                    },
                },
            };

            using var model = IfcStore.Open(LocalFiles.Ifc4x3_Road);
            var result = new IdsValidator { ReportPassingEntities = true }.Validate(document, model);

            var specification = Assert.Single(result.Specifications);
            Assert.Equal(IdsStatus.Pass, specification.Status);
            // It has to have actually looked at the carriageways, not passed on an empty set.
            var expected = model.Instances.OfType<IIfcSpatialStructureElement>()
                .Count(e => e.ExpressType.ExpressNameUpper == "IFCROADPART"
                            && IdsFacetMatcher.ResolvePredefinedType(e) == "CARRIAGEWAY");
            Assert.True(expected > 0);
            Assert.Equal(expected, specification.ApplicableCount);
            // The IDS declares IFC4X3_ADD2 and the model is IFC4X3, so no version mismatch note.
            Assert.Null(specification.Message);
        }

        [Fact]
        public void IdsReportsTheModelAsIfc4x3()
        {
            Assert.Equal(IdsIfcVersion.Ifc4X3Add2, IdsIfcVersion.FromXbim(XbimSchemaVersion.Ifc4x3));
        }

        #endregion

        /// <summary>Elements contained in a spatial element or anything aggregated below it.</summary>
        private static IEnumerable<IIfcElement> ElementsUnder(IIfcSpatialStructureElement root)
        {
            foreach (var element in root.ContainsElements.SelectMany(r => r.RelatedElements).OfType<IIfcElement>())
                yield return element;
            foreach (var child in root.IsDecomposedBy.SelectMany(r => r.RelatedObjects).OfType<IIfcSpatialStructureElement>())
                foreach (var element in ElementsUnder(child))
                    yield return element;
        }

        private static IEnumerable<HierarchyNode> Flatten(HierarchyNode node)
        {
            if (node == null)
                yield break;
            yield return node;
            foreach (var child in node.Children)
                foreach (var descendant in Flatten(child))
                    yield return descendant;
        }
    }
}
