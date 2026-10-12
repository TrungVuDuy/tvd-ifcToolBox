using IfcToolbox.Core.Geo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace IfcToolbox.Core.Editors
{
    public class CombineOptions
    {
        /// <summary>
        /// Also merge sites, buildings and storeys that have the same class and name, not only
        /// those with the same GlobalId. Discipline models exported separately usually share
        /// storey names but not GlobalIds.
        /// </summary>
        public bool MergeSpatialByName { get; set; } = true;

        /// <summary>
        /// An element whose GlobalId came in from an earlier file is the same object and is
        /// copied once. Off: it is copied again and given a new GlobalId.
        /// </summary>
        public bool SkipDuplicateElements { get; set; } = true;
    }

    /// <summary>What happened while combining, for a report.</summary>
    public class CombineResult
    {
        public string OutputPath { get; set; }
        public int ElementsCopied { get; set; }
        public int ElementsSkipped { get; set; }
        /// <summary>The skipped elements by class, e.g. IfcWall: 3, so the user can judge whether they really are the same objects.</summary>
        public IDictionary<string, int> SkippedByClass { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
        /// <summary>"IfcBuildingStorey 'Level 1'", one per element that was folded into an earlier one.</summary>
        public IList<string> SpatialMerged { get; } = new List<string>();
        /// <summary>Same GlobalId or name as an earlier element, kept apart because it is placed elsewhere.</summary>
        public IList<string> SpatialKeptApart { get; } = new List<string>();
        /// <summary>Type objects found in several files, now one: "IfcWallType 'Basic Wall:Generic'".</summary>
        public IList<string> TypesMerged { get; } = new List<string>();
        public int GlobalIdsRenewed { get; set; }
        /// <summary>The renewed GlobalIds by class, e.g. IfcPropertySet: 4.</summary>
        public IDictionary<string, int> RenewedByClass { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public IList<string> Warnings { get; } = new List<string>();
    }

    /// <summary>Raised when the files cannot go into one model, e.g. different schemas or units.</summary>
    public class CombineException : Exception
    {
        public CombineException(string message) : base(message) { }
    }

    /// <summary>
    /// Combines several IFC files into one model with a single IfcProject.
    /// <para>
    /// Every file's products are copied in. The projects after the first are folded into the
    /// first: their sites hang off it and their geometry moves onto its representation contexts,
    /// so no duplicate project or context is left behind. Sites, buildings, storeys and IFC 4.3
    /// facilities that are the same element (same GlobalId, or optionally the same class and
    /// name) are merged as well, but only when they sit in exactly the same place, so that no
    /// geometry moves. Anything still sharing a GlobalId at the end gets a new one.
    /// </para>
    /// <para>
    /// The files must share the schema and the length unit: mixing them would need a schema
    /// conversion or rescaling every coordinate, which this does not attempt.
    /// </para>
    /// </summary>
    public static class ModelCombiner
    {
        public static CombineResult Combine(IReadOnlyList<string> sourcePaths, string outputPath,
            CombineOptions options = null, CancellationToken cancellationToken = default)
        {
            if (sourcePaths == null || sourcePaths.Count < 2)
                throw new ArgumentException("Combining needs at least two files.", nameof(sourcePaths));
            options ??= new CombineOptions();
            var result = new CombineResult { OutputPath = outputPath };

            XbimSchemaVersion schema = default;
            double unit = 0;
            IfcStore target = null;
            try
            {
                var copiedElementIds = new HashSet<string>(StringComparer.Ordinal);
                // Which file each spatial element came from: elements of one file are never merged together.
                var sourceOf = new Dictionary<int, int>();
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = sourcePaths[i];
                    using var source = IfcStore.Open(path);

                    double sourceUnit = MapConversionService.LengthUnitInMetres(source);
                    if (i == 0)
                    {
                        schema = source.SchemaVersion;
                        unit = sourceUnit;
                        target = IfcStore.Create(EditorCredentials(), schema, XbimStoreType.InMemoryModel);
                        target.Header.FileDescription = source.Header.FileDescription;
                    }
                    else if (source.SchemaVersion != schema)
                    {
                        throw new CombineException($"{Path.GetFileName(path)} is {source.SchemaVersion}, {Path.GetFileName(sourcePaths[0])} is {schema}. " +
                                                   "Only files of the same IFC schema can be combined.");
                    }
                    else if (Math.Abs(sourceUnit - unit) > unit * 1e-9)
                    {
                        throw new CombineException($"{Path.GetFileName(path)} measures lengths in {UnitName(sourceUnit)}, {Path.GetFileName(sourcePaths[0])} in {UnitName(unit)}. " +
                                                   "Only files with the same length unit can be combined.");
                    }

                    var products = source.Instances.OfType<IIfcProduct>().ToList();
                    var toCopy = new List<IIfcProduct>();
                    foreach (var product in products)
                    {
                        // Spatial elements always come in; duplicates of them are merged afterwards.
                        if (product is IIfcSpatialStructureElement)
                        {
                            toCopy.Add(product);
                            continue;
                        }
                        if (options.SkipDuplicateElements && !copiedElementIds.Add(product.GlobalId.ToString()))
                        {
                            result.ElementsSkipped++;
                            var name = product.ExpressType.Name;
                            result.SkippedByClass[name] = result.SkippedByClass.TryGetValue(name, out var n) ? n + 1 : 1;
                            continue;
                        }
                        toCopy.Add(product);
                        result.ElementsCopied++;
                    }

                    using (source.BeginEntityCaching())
                    using (source.BeginInverseCaching())
                    using (var txn = target.BeginTransaction($"Copy {Path.GetFileName(path)}"))
                    {
                        target.InsertCopy(toCopy, true, false, new XbimInstanceHandleMap(source, target));
                        txn.Commit();
                    }
                    foreach (var element in target.Instances.OfType<IIfcSpatialStructureElement>())
                        sourceOf.TryAdd(element.EntityLabel, i);
                }

                cancellationToken.ThrowIfCancellationRequested();
                using (var txn = target.BeginTransaction("Reconcile"))
                {
                    var project = MergeProjects(target, result);
                    if (project != null)
                        MergeSpatialChildren(target, project, options, sourceOf, result);
                    MergeTypes(target, result);
                    result.GlobalIdsRenewed = RenewDuplicateGlobalIds(target, result.RenewedByClass);
                    txn.Commit();
                }

                target.Header.FileName.Name = Path.GetFileName(outputPath);
                target.SaveAs(outputPath);
                return result;
            }
            finally
            {
                target?.Dispose();
            }
        }

        #region Projects and contexts

        /// <summary>Keeps the first project and folds the others into it. Returns the one left.</summary>
        private static IIfcProject MergeProjects(IModel model, CombineResult result)
        {
            var projects = model.Instances.OfType<IIfcProject>().ToList();
            if (projects.Count == 0)
                return null;
            var keeper = projects[0];

            foreach (var other in projects.Skip(1))
            {
                foreach (var rel in other.IsDecomposedBy.ToList())
                {
                    var same = keeper.IsDecomposedBy.FirstOrDefault(r => SameId(r, rel));
                    if (same == null)
                        rel.RelatingObject = keeper;
                    else
                        MoveAndDelete(model, rel.RelatedObjects, same.RelatedObjects, rel);
                }
                // The kept project has its own properties.
                foreach (var rel in other.IsDefinedBy.ToList())
                {
                    rel.RelatedObjects.Remove(other);
                    if (rel.RelatedObjects.Count == 0)
                        DeletePropertyRelation(model, rel);
                }
                foreach (var rel in other.Declares.ToList())
                    rel.RelatingContext = keeper;

                var units = other.UnitsInContext;
                model.Delete(other);
                if (units != null && !ReferenceEquals(units, keeper.UnitsInContext))
                    model.Delete(units);
            }

            // A file whose elements were all skipped as duplicates brings its spatial elements
            // without the relation to its project: the copy only climbs the structure from
            // contained elements. Hang such orphans off the kept project so they get merged.
            var orphans = model.Instances.OfType<IIfcSpatialStructureElement>().Where(e => !e.Decomposes.Any()).ToList();
            if (orphans.Count > 0)
            {
                var rel = keeper.IsDecomposedBy.FirstOrDefault();
                if (rel == null)
                {
                    rel = SchemaEntityFactory.New<IIfcRelAggregates>(model, "IfcRelAggregates");
                    rel.GlobalId = new Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId(Xbim.IO.Step21.StepGuidHelper.ConvertToBase64(Guid.NewGuid()));
                    rel.RelatingObject = keeper;
                }
                foreach (var orphan in orphans)
                    rel.RelatedObjects.Add(orphan);
            }

            // Every main context the kept project does not own came from another file. Some are
            // reached only through the geometry, not through their project, so look at all of them.
            foreach (var context in model.Instances.OfType<IIfcGeometricRepresentationContext>()
                         .Where(c => c is not IIfcGeometricRepresentationSubContext && !keeper.RepresentationContexts.Contains(c)).ToList())
                MergeContext(model, keeper, context, result);
            return keeper;
        }

        /// <summary>
        /// Moves the geometry of one file's context onto the matching context of the kept
        /// project (same type and dimension; sub contexts by identifier and target view).
        /// A context without a match is handed over to the kept project instead.
        /// </summary>
        private static void MergeContext(IModel model, IIfcProject keeper, IIfcGeometricRepresentationContext context, CombineResult result)
        {
            var match = keeper.RepresentationContexts.OfType<IIfcGeometricRepresentationContext>()
                .Where(c => c is not IIfcGeometricRepresentationSubContext)
                .FirstOrDefault(c => c.ContextType == context.ContextType && c.CoordinateSpaceDimension == context.CoordinateSpaceDimension);
            if (match == null)
            {
                keeper.RepresentationContexts.Add(context);
                return;
            }

            if (!SamePlacement(match.WorldCoordinateSystem, context.WorldCoordinateSystem))
                result.Warnings.Add($"The {context.ContextType ?? "model"} context of a later file has a different world coordinate system; " +
                                    "its geometry now uses the first file's, so it may appear moved. Align the files with the Relocator first.");
            if (!SameDirection(match.TrueNorth, context.TrueNorth))
                result.Warnings.Add("The files point true north differently; the first file's true north is kept.");

            var representations = model.Instances.OfType<IIfcRepresentation>().ToList();
            foreach (var sub in context.HasSubContexts.ToList())
            {
                var subMatch = match.HasSubContexts.FirstOrDefault(s => s.ContextIdentifier == sub.ContextIdentifier && s.TargetView == sub.TargetView);
                if (subMatch == null)
                {
                    sub.ParentContext = match;
                    continue;
                }
                foreach (var representation in representations.Where(r => ReferenceEquals(r.ContextOfItems, sub)))
                    representation.ContextOfItems = subMatch;
                model.Delete(sub);
            }
            foreach (var representation in representations.Where(r => ReferenceEquals(r.ContextOfItems, context)))
                representation.ContextOfItems = match;
            model.Delete(context);
        }

        #endregion

        #region Spatial structure

        /// <summary>
        /// Merges the spatial elements directly under <paramref name="parent"/>, then goes down
        /// into what is left, so that children are compared once their parents are one.
        /// </summary>
        private static void MergeSpatialChildren(IModel model, IIfcObjectDefinition parent, CombineOptions options,
            IReadOnlyDictionary<int, int> sourceOf, CombineResult result)
        {
            var kept = new List<IIfcSpatialStructureElement>();
            // The files each kept element already stands for.
            var sources = new Dictionary<IIfcSpatialStructureElement, HashSet<int>>();
            int SourceOf(IIfcSpatialStructureElement e) => sourceOf.TryGetValue(e.EntityLabel, out var s) ? s : -1;
            var children = parent.IsDecomposedBy.SelectMany(r => r.RelatedObjects).OfType<IIfcSpatialStructureElement>().ToList();
            foreach (var child in children)
            {
                // Only an element from another file can be the same one: two rooms of one file that
                // share a name are two rooms.
                int source = SourceOf(child);
                var same = kept.FirstOrDefault(k => !sources[k].Contains(source) && IsSameElement(k, child, options));
                if (same == null)
                {
                    kept.Add(child);
                    sources[child] = new HashSet<int> { source };
                    continue;
                }
                if (!SamePlacement(same.ObjectPlacement, child.ObjectPlacement))
                {
                    result.SpatialKeptApart.Add(Describe(child));
                    kept.Add(child);
                    sources[child] = new HashSet<int> { source };
                    continue;
                }
                Absorb(model, same, child);
                sources[same].Add(source);
                result.SpatialMerged.Add(Describe(child));
            }

            foreach (var element in kept)
                MergeSpatialChildren(model, element, options, sourceOf, result);
        }

        private static bool IsSameElement(IIfcSpatialStructureElement a, IIfcSpatialStructureElement b, CombineOptions options)
        {
            if (a.ExpressType != b.ExpressType)
                return false;
            if (a.GlobalId.ToString() == b.GlobalId.ToString())
                return true;
            // Unnamed counts as a name: discipline exports often leave the building unnamed, and the
            // placement check still has to pass.
            return options.MergeSpatialByName && (a.Name?.ToString() ?? string.Empty) == (b.Name?.ToString() ?? string.Empty);
        }

        /// <summary>
        /// Folds <paramref name="duplicate"/> into <paramref name="keeper"/>: what it aggregates,
        /// contains and references moves over, and placements relative to it become relative to
        /// the keeper, which is placed identically.
        /// </summary>
        private static void Absorb(IModel model, IIfcSpatialStructureElement keeper, IIfcSpatialStructureElement duplicate)
        {
            // A relation with the GlobalId of one the keeper already has is the same relation,
            // copied from each file: its objects join the keeper's, and it goes.
            foreach (var rel in duplicate.IsDecomposedBy.ToList())
            {
                var same = keeper.IsDecomposedBy.FirstOrDefault(r => SameId(r, rel));
                if (same == null)
                    rel.RelatingObject = keeper;
                else
                    MoveAndDelete(model, rel.RelatedObjects, same.RelatedObjects, rel);
            }
            foreach (var rel in duplicate.ContainsElements.ToList())
            {
                var same = keeper.ContainsElements.FirstOrDefault(r => SameId(r, rel));
                if (same == null)
                    rel.RelatingStructure = keeper;
                else
                    MoveAndDelete(model, rel.RelatedElements, same.RelatedElements, rel);
            }
            foreach (var rel in duplicate.ReferencesElements.ToList())
                rel.RelatingStructure = keeper;

            var placement = duplicate.ObjectPlacement;
            if (placement != null && keeper.ObjectPlacement != null)
            {
                foreach (var local in model.Instances.OfType<IIfcLocalPlacement>().Where(p => ReferenceEquals(p.PlacementRelTo, placement)).ToList())
                    local.PlacementRelTo = keeper.ObjectPlacement;
            }

            // The keeper already carries the element's own properties.
            foreach (var rel in duplicate.IsDefinedBy.ToList())
            {
                rel.RelatedObjects.Remove(duplicate);
                if (rel.RelatedObjects.Count == 0)
                    DeletePropertyRelation(model, rel);
            }
            foreach (var rel in duplicate.Decomposes.ToList())
            {
                rel.RelatedObjects.Remove(duplicate);
                if (rel.RelatedObjects.Count == 0)
                    model.Delete(rel);
            }

            model.Delete(duplicate);
            if (placement != null)
                model.Delete(placement);
        }

        private static bool SameId(IIfcRoot a, IIfcRoot b) =>
            !ReferenceEquals(a, b) && a.GlobalId.ToString() == b.GlobalId.ToString();

        private static void MoveAndDelete<T>(IModel model, IItemSet<T> from, IItemSet<T> to, IPersistEntity relation)
        {
            foreach (var item in from.ToList())
                if (!to.Contains(item))
                    to.Add(item);
            model.Delete(relation);
        }

        /// <summary>A property relation that relates nothing any more, with its property set unless another relation uses it.</summary>
        private static void DeletePropertyRelation(IModel model, IIfcRelDefinesByProperties rel)
        {
            var definition = rel.RelatingPropertyDefinition;
            model.Delete(rel);
            if (definition is IIfcPropertySet set
                && !model.Instances.OfType<IIfcRelDefinesByProperties>().Any(r => ReferenceEquals(r.RelatingPropertyDefinition, set))
                && !model.Instances.OfType<IIfcTypeObject>().Any(t => t.HasPropertySets.Contains(set)))
                DeletePropertySet(model, set);
        }

        private static void DeletePropertySet(IModel model, IIfcPropertySet set)
        {
            foreach (var property in set.HasProperties.ToList())
                model.Delete(property);
            model.Delete(set);
        }

        /// <summary>
        /// Type objects with the same GlobalId came from several files and are one type: the
        /// objects typed by the copies are typed by the first, and the copies go. Returns the names.
        /// </summary>
        private static void MergeTypes(IModel model, CombineResult result)
        {
            foreach (var group in model.Instances.OfType<IIfcTypeObject>().GroupBy(t => t.GlobalId.ToString()).Where(g => g.Count() > 1).ToList())
            {
                var keeper = group.First();
                foreach (var duplicate in group.Skip(1))
                {
                    if (duplicate.ExpressType != keeper.ExpressType)
                        continue;
                    foreach (var rel in duplicate.Types.ToList())
                        rel.RelatingType = keeper;
                    var sets = duplicate.HasPropertySets.OfType<IIfcPropertySet>().ToList();
                    model.Delete(duplicate);
                    foreach (var set in sets.Where(s => !model.Instances.OfType<IIfcTypeObject>().Any(t => t.HasPropertySets.Contains(s))))
                        DeletePropertySet(model, set);
                    result.TypesMerged.Add($"{keeper.ExpressType.Name} '{keeper.Name}'");
                }
            }
        }

        private static string Describe(IIfcSpatialStructureElement element) =>
            $"{element.ExpressType.Name} '{element.Name}'";

        #endregion

        #region Comparisons

        private static bool SamePlacement(IIfcObjectPlacement a, IIfcObjectPlacement b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            if (a is not IIfcLocalPlacement la || b is not IIfcLocalPlacement lb)
                return false;
            // Parents are merged first, so equal relative placements mean the same place.
            return SamePlacement(la.RelativePlacement, lb.RelativePlacement);
        }

        private static bool SamePlacement(IIfcAxis2Placement a, IIfcAxis2Placement b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            return GeoFactory.CreateGeoPlacement(a).Equals(GeoFactory.CreateGeoPlacement(b));
        }

        private static bool SameDirection(IIfcDirection a, IIfcDirection b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            var x = a.DirectionRatios.ToList();
            var y = b.DirectionRatios.ToList();
            return x.Count == y.Count && x.Zip(y, (p, q) => Math.Abs((double)p - (double)q) < 1e-9).All(same => same);
        }

        #endregion

        /// <summary>Gives every IfcRoot after the first with a given GlobalId a new one. Returns how many.</summary>
        private static int RenewDuplicateGlobalIds(IModel model, IDictionary<string, int> byClass)
        {
            int renewed = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in model.Instances.OfType<IIfcRoot>().ToList())
            {
                if (seen.Add(root.GlobalId.ToString()))
                    continue;
                string id;
                do id = Xbim.IO.Step21.StepGuidHelper.ConvertToBase64(Guid.NewGuid());
                while (!seen.Add(id));
                root.GlobalId = new Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId(id);
                var name = root.ExpressType.Name;
                byClass[name] = byClass.TryGetValue(name, out var n) ? n + 1 : 1;
                renewed++;
            }
            return renewed;
        }

        private static string UnitName(double metres) => metres switch
        {
            _ when Math.Abs(metres - 0.001) < 1e-12 => "millimetres",
            _ when Math.Abs(metres - 0.01) < 1e-12 => "centimetres",
            _ when Math.Abs(metres - 1) < 1e-12 => "metres",
            _ when Math.Abs(metres - 0.3048) < 1e-12 => "feet",
            _ when Math.Abs(metres - 0.0254) < 1e-12 => "inches",
            _ => $"units of {metres} m",
        };

        private static XbimEditorCredentials EditorCredentials() => new()
        {
            ApplicationDevelopersName = "IfcToolbox contributors",
            ApplicationFullName = "IFC Toolbox",
            ApplicationIdentifier = "Ifc.Toolbox",
            ApplicationVersion = typeof(ModelCombiner).Assembly.GetName().Version?.ToString(3) ?? "2.0",
            // No user name: the combined file is shared, and the sources keep their own authors.
            EditorsFamilyName = "IFC Toolbox",
        };
    }
}
