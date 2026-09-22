using IfcToolbox.Core.Ids.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Ids.Validation
{
    /// <summary>
    /// Tests IDS facets against entities in an xbim model.
    /// Reading goes through Xbim.Ifc4.Interfaces, which covers every schema since xbim 6.
    /// <para>
    /// Where a facet's locator can match more than one thing - a baseName restriction hitting
    /// several properties, for instance - every match has to satisfy the value constraint.
    /// The official test cases call this "all matching properties must satisfy requirements".
    /// </para>
    /// </summary>
    public class IdsFacetMatcher
    {
        private readonly Dictionary<IModel, IdsUnitConverter> _converters = new Dictionary<IModel, IdsUnitConverter>();

        /// <summary>Unit converter for the model an entity belongs to, built once per model.</summary>
        private IdsUnitConverter ConverterFor(IPersistEntity entity)
        {
            var model = entity.Model;
            if (model == null)
                return null;
            if (!_converters.TryGetValue(model, out var converter))
            {
                converter = new IdsUnitConverter(model);
                _converters[model] = converter;
            }
            return converter;
        }

        /// <summary>
        /// One thing the facet's locator found. A property can hold several values at once
        /// (enumerated, list, bounded, table); IDS is satisfied when any one of them matches,
        /// so the alternatives travel together.
        /// </summary>
        private sealed class Located
        {
            public Located(params string[] values)
            {
                Values = values?.Where(v => !string.IsNullOrEmpty(v)).ToList() ?? new List<string>();
            }

            public Located(IEnumerable<string> values)
            {
                Values = values?.Where(v => !string.IsNullOrEmpty(v)).ToList() ?? new List<string>();
            }

            public IList<string> Values { get; }

            public bool HasValue => Values.Count > 0;

            public bool Satisfies(IdsValue constraint) => Values.Any(constraint.Matches);

            public override string ToString() => Values.Count == 0 ? "<null>" : string.Join("/", Values);
        }

        public IdsFacetMatch Match(IIdsFacet facet, IPersistEntity entity)
        {
            switch (facet)
            {
                case IdsEntityFacet f: return MatchEntity(f, entity);
                case IdsAttributeFacet f: return MatchAttribute(f, entity);
                case IdsPropertyFacet f: return MatchProperty(f, entity);
                case IdsClassificationFacet f: return MatchClassification(f, entity);
                case IdsMaterialFacet f: return MatchMaterial(f, entity);
                case IdsPartOfFacet f: return MatchPartOf(f, entity);
                default:
                    throw new NotSupportedException($"Unsupported IDS facet {facet?.GetType().Name}.");
            }
        }

        /// <summary>
        /// Folds the located candidates into one result: located when any were found,
        /// matched only when they all satisfy the constraint.
        /// </summary>
        private static IdsFacetMatch All(IList<Located> located, IdsValue constraint)
        {
            if (located.Count == 0)
                return IdsFacetMatch.NotFound;

            var observed = string.Join(", ", located.Select(l => l.ToString()).Distinct());

            // With no value constraint, merely existing is enough, but a null value is not
            // an existing value as far as IDS is concerned.
            bool ok = constraint == null
                ? located.All(l => l.HasValue)
                : located.All(l => l.Satisfies(constraint));

            return ok ? IdsFacetMatch.Found(observed) : IdsFacetMatch.WrongValue(observed);
        }

        #region Entity

        public IdsFacetMatch MatchEntity(IdsEntityFacet facet, IPersistEntity entity)
        {
            // "The IFC Class must match exactly" - subtypes are not matched.
            var className = entity.ExpressType.ExpressNameUpper;
            if (facet.Name != null && !facet.Name.Matches(className) && !MatchesVia2x3Mapping(facet.Name, entity))
                return new IdsFacetMatch(false, false, className);

            if (facet.PredefinedType == null)
                return IdsFacetMatch.Found(className);

            var candidates = PredefinedTypeCandidates(entity).ToList();
            if (candidates.Count == 0)
                return new IdsFacetMatch(false, false, className);

            var observed = $"{className}/{string.Join("|", candidates)}";
            return candidates.Any(facet.PredefinedType.Matches)
                ? IdsFacetMatch.Found(observed)
                : IdsFacetMatch.WrongValue(observed);
        }

        /// <summary>
        /// Values an IDS predefinedType may legitimately match.
        /// When the IFC says USERDEFINED, both the literal USERDEFINED and the user supplied
        /// name are acceptable, which is what the examples in the entity facet documentation
        /// show (IDS USERDEFINED against IFC USERDEFINED + ElementType FOO passes, and so
        /// does IDS FOO against the same entity).
        /// </summary>
        public static IEnumerable<string> PredefinedTypeCandidates(IPersistEntity entity)
        {
            if (entity is IIfcObject obj)
            {
                var typeObject = obj.IsTypedBy?.FirstOrDefault()?.RelatingType;
                if (typeObject != null)
                {
                    var fromType = Candidates(typeObject, TypeUserDefinedAttributes).ToList();
                    if (fromType.Count > 0)
                        return fromType;
                }
                return Candidates(entity, OccurrenceUserDefinedAttributes);
            }
            return Candidates(entity, TypeUserDefinedAttributes);
        }

        private static IEnumerable<string> Candidates(IPersistEntity entity, string[] userDefinedFallbackProperties)
        {
            var predefinedType = GetAttributeString(entity, "PredefinedType");

            // NOTDEFINED states that the entity does not categorise itself, so it is treated
            // as absent and the search continues on the occurrence.
            if (predefinedType == null || string.Equals(predefinedType, "NOTDEFINED", StringComparison.OrdinalIgnoreCase))
            {
                var fallback = FirstAttribute(entity, userDefinedFallbackProperties);
                if (fallback != null)
                    yield return fallback;
                yield break;
            }

            if (string.Equals(predefinedType, "USERDEFINED", StringComparison.OrdinalIgnoreCase))
            {
                var userDefined = FirstAttribute(entity, userDefinedFallbackProperties);
                if (userDefined != null)
                    yield return userDefined;
                yield return predefinedType;
                yield break;
            }

            yield return predefinedType;
        }

        /// <summary>
        /// IFC2X3 expresses a number of elements as a generic occurrence plus a specific type
        /// object: an air terminal is an IfcFlowTerminal typed by an IfcAirTerminalType. IDS
        /// always uses the IFC4 style name (IFCAIRTERMINAL), so the pair has to be resolved
        /// through the official mapping table.
        /// </summary>
        private static bool MatchesVia2x3Mapping(IdsValue name, IPersistEntity entity)
        {
            if (entity.Model?.SchemaVersion != Xbim.Common.Step21.XbimSchemaVersion.Ifc2X3)
                return false;

            var className = entity.ExpressType.ExpressNameUpper;

            foreach (var idsName in Ifc2x3EntityMapping.MappedNames)
            {
                if (!name.Matches(idsName))
                    continue;
                if (!Ifc2x3EntityMapping.TryGet(idsName, out var mapping))
                    continue;

                // The entity being checked is the occurrence, not the type object: the table
                // describes a pair, and it is the occurrence that stands for the IFC4 class.
                if (className == mapping.OccurrenceEntity && entity is IIfcObject obj)
                    if (obj.IsTypedBy.Select(r => r.RelatingType)
                          .Any(t => t != null && t.ExpressType.ExpressNameUpper == mapping.TypeEntity))
                        return true;
            }

            return false;
        }

        /// <summary>
        /// Single best predefined type for reporting; see <see cref="PredefinedTypeCandidates"/>
        /// for what a facet actually matches against.
        /// </summary>
        public static string ResolvePredefinedType(IPersistEntity entity) =>
            PredefinedTypeCandidates(entity).FirstOrDefault();

        /// <summary>
        /// Where a type object keeps its user defined category. Most type classes call it
        /// ElementType, but process and resource types use their own names.
        /// </summary>
        private static readonly string[] TypeUserDefinedAttributes = { "ElementType", "ProcessType", "ResourceType", "ObjectType" };

        private static readonly string[] OccurrenceUserDefinedAttributes = { "ObjectType" };

        private static string FirstAttribute(IPersistEntity entity, string[] attributeNames)
        {
            foreach (var attributeName in attributeNames)
            {
                var value = GetAttributeString(entity, attributeName);
                if (value != null)
                    return value;
            }
            return null;
        }

        #endregion

        #region Attribute

        public IdsFacetMatch MatchAttribute(IdsAttributeFacet facet, IPersistEntity entity)
        {
            if (facet.Name == null)
                return IdsFacetMatch.NotFound;

            // The name can be a restriction, so every direct attribute is a candidate.
            // Unlike properties, "name restrictions will match any result": one matching
            // attribute is enough, the others need not also satisfy the value.
            var located = new List<string>();
            foreach (var property in DirectAttributes(entity))
            {
                if (!facet.Name.Matches(property.Name))
                    continue;
                var value = AttributeValue(property.GetValue(entity));
                // An unset attribute is not there at all, so it cannot make an optional facet
                // fail. An attribute set to an empty string is there, and fails the check.
                if (value != null)
                    located.Add(value);
            }

            if (located.Count == 0)
                return IdsFacetMatch.NotFound;

            var observed = string.Join(", ", located.Distinct());
            bool ok = facet.Value == null
                ? located.Any(v => v.Length > 0)
                : located.Any(facet.Value.Matches);

            return ok ? IdsFacetMatch.Found(observed) : IdsFacetMatch.WrongValue(observed);
        }

        /// <summary>
        /// Text of an attribute value, or null when the attribute holds nothing at all.
        /// An empty aggregate and a logical UNKNOWN count as nothing, as does an unset
        /// attribute; an empty string does not - it is a value, just not a usable one.
        /// </summary>
        private static string AttributeValue(object value)
        {
            if (value == null)
                return null;
            if (value is IEnumerable sequence && !(value is string) && !(value is IExpressValueType))
                return sequence.Cast<object>().Any() ? ToText(value) ?? string.Empty : null;
            if (value is IExpressValueType express && express.Value == null)
                return null;

            return ToText(value) ?? string.Empty;
        }

        private static IEnumerable<System.Reflection.PropertyInfo> DirectAttributes(IPersistEntity entity)
        {
            return entity.ExpressType.Properties.Values
                .Where(p => p.EntityAttribute != null && p.EntityAttribute.Order > 0)
                .Select(p => p.PropertyInfo);
        }

        private static string GetAttributeString(IPersistEntity entity, string attributeName)
        {
            var property = DirectAttributes(entity).FirstOrDefault(p => p.Name == attributeName);
            if (property == null)
                return null;
            var text = ToText(property.GetValue(entity));
            return string.IsNullOrEmpty(text) ? null : text;
        }

        #endregion

        #region Property

        public IdsFacetMatch MatchProperty(IdsPropertyFacet facet, IPersistEntity entity)
        {
            if (facet.PropertySet == null || facet.BaseName == null)
                return IdsFacetMatch.NotFound;

            var converter = ConverterFor(entity);
            var located = new List<Located>();
            bool aMatchingSetLacksTheProperty = false;

            foreach (var set in AllPropertySetsOf(entity))
            {
                if (set.Name == null || !facet.PropertySet.Matches(set.Name))
                    continue;

                bool foundInThisSet = false;
                foreach (var entry in set.Entries)
                {
                    if (entry.Name == null || !facet.BaseName.Matches(entry.Name))
                        continue;
                    foundInThisSet = true;

                    // The declared data type is checked per value: a table property legitimately
                    // holds values of different measures side by side.
                    var accepted = entry.Values
                        .Where(v => MeasureAccepted(facet.DataType, v.Measure))
                        .Select(v => Normalize(v, entry.Unit, converter));

                    located.Add(new Located(accepted));
                }

                if (!foundInThisSet)
                    aMatchingSetLacksTheProperty = true;
            }

            var result = All(located, facet.Value);

            // "All matching property sets must satisfy requirements": once the property has
            // been found somewhere, a matching set that lacks it is a failure. On its own it
            // is not enough to say the property was located at all.
            if (result.Located && aMatchingSetLacksTheProperty)
                return IdsFacetMatch.WrongValue(result.Observed);

            return result;
        }

        private static bool MeasureAccepted(string declaredDataType, string measure)
        {
            if (string.IsNullOrEmpty(declaredDataType) || measure == null)
                return true;
            return string.Equals(declaredDataType, measure, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Rewrites a measured value into the SI base units IDS compares against.
        /// </summary>
        private static string Normalize(PropertyValue value, IIfcUnit unit, IdsUnitConverter converter)
        {
            if (value.Text == null || converter == null || !IdsUnitConverter.IsConvertible(value.Measure))
                return value.Text;
            if (!IdsValue.TryParseNumber(value.Text, out double number))
                return value.Text;
            return FormatNumber(converter.Convert(number, value.Measure, unit));
        }

        internal static string FormatNumber(double value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// Property sets of the occurrence plus those inherited from its defining type,
        /// including quantity sets: IDS treats Qto_* entries as properties too.
        /// The occurrence comes first so that its values override the type's.
        /// </summary>
        private static IEnumerable<IIfcPropertySetDefinition> PropertySetsOf(IPersistEntity entity)
        {
            if (entity is IIfcObject obj)
            {
                var ownSets = obj.IsDefinedBy
                    .SelectMany(r => r.RelatingPropertyDefinition.PropertySetDefinitions)
                    .ToList();
                foreach (var set in ownSets)
                    yield return set;

                var ownNames = new HashSet<string>(ownSets.Select(s => ToText(s.Name)).Where(n => n != null));
                foreach (var type in obj.IsTypedBy.Select(r => r.RelatingType).Where(t => t != null))
                    foreach (var set in type.HasPropertySets)
                        // A property set defined on the occurrence overrides the type's set
                        // of the same name.
                        if (!ownNames.Contains(ToText(set.Name)))
                            yield return set;
            }
            else if (entity is IIfcTypeObject typeObject)
            {
                foreach (var set in typeObject.HasPropertySets)
                    yield return set;
            }
            else if (entity is IIfcContext context)
            {
                // IfcProject and IfcProjectLibrary are IfcContext, not IfcObject, but they
                // carry property sets the same way.
                foreach (var relation in context.IsDefinedBy)
                    foreach (var set in relation.RelatingPropertyDefinition.PropertySetDefinitions)
                        yield return set;
            }
        }

        /// <summary>One value of a property, with the IFC measure it was written as.</summary>
        private struct PropertyValue
        {
            public PropertyValue(string text, string measure)
            {
                Text = text;
                Measure = measure;
            }

            public string Text;

            /// <summary>Name of the IFC defined type, upper case, or null when not applicable.</summary>
            public string Measure;
        }

        private struct PropertyEntry
        {
            public string Name;

            /// <summary>Alternative values; any one satisfying the constraint is enough.</summary>
            public IEnumerable<PropertyValue> Values;

            /// <summary>Unit stated on the property itself, overriding the project's.</summary>
            public IIfcUnit Unit;
        }

        /// <summary>A named group of properties, whatever IFC construct carries it.</summary>
        private sealed class PropertySetView
        {
            public PropertySetView(string name, IEnumerable<PropertyEntry> entries)
            {
                Name = name;
                Entries = entries;
            }

            public string Name { get; }
            public IEnumerable<PropertyEntry> Entries { get; }
        }

        /// <summary>
        /// Every property set that applies, including the material property sets that
        /// IfcMaterial carries (IfcMaterialProperties in IFC4, IfcExtendedMaterialProperties
        /// in IFC2X3). Those are IfcExtendedProperties, not IfcPropertySetDefinition, so they
        /// cannot travel as the same type.
        /// </summary>
        private static IEnumerable<PropertySetView> AllPropertySetsOf(IPersistEntity entity)
        {
            foreach (var set in PropertySetsOf(entity))
                yield return new PropertySetView(ToText(set.Name), EntriesOf(set));

            if (entity is IIfcMaterialDefinition materialDefinition)
                foreach (var set in materialDefinition.HasProperties)
                    yield return new PropertySetView(MaterialPropertySetName(set), MaterialProperties(set));
        }

        /// <summary>
        /// Name of a material property set, read from the entity's own attributes.
        /// <para>
        /// The interface property cannot be used: on an IFC2X3 IfcExtendedMaterialProperties
        /// xbim 6 throws MissingMethodException while mapping the value onto the IFC4
        /// IfcLabel. Reading the attribute directly sidesteps that.
        /// </para>
        /// </summary>
        private static string MaterialPropertySetName(IIfcMaterialProperties set)
        {
            return GetAttributeString(set, "Name");
        }

        private static IEnumerable<PropertyEntry> MaterialProperties(IIfcMaterialProperties set)
        {
            // IFC2X3 keeps them in ExtendedProperties, and xbim's IFC4 interface mapping for
            // that class throws, so the attribute is read directly.
            var properties = AggregateAttribute(set, "ExtendedProperties")
                             ?? AggregateAttribute(set, "Properties");

            if (properties == null)
            {
                try
                {
                    properties = set.Properties;
                }
                catch (MissingMethodException)
                {
                    properties = Enumerable.Empty<IIfcProperty>();
                }
            }

            return PropertyEntriesOf(properties);
        }

        /// <summary>Reads an aggregate attribute by name, or null when the class has none.</summary>
        private static IEnumerable<IIfcProperty> AggregateAttribute(IPersistEntity entity, string attributeName)
        {
            var property = DirectAttributes(entity).FirstOrDefault(p => p.Name == attributeName);
            if (property == null)
                return null;
            return property.GetValue(entity) is IEnumerable aggregate
                ? aggregate.Cast<object>().OfType<IIfcProperty>().ToList()
                : null;
        }

        private static IEnumerable<PropertyEntry> EntriesOf(IIfcPropertySetDefinition set)
        {
            if (set is IIfcPropertySet propertySet)
            {
                foreach (var entry in PropertyEntriesOf(propertySet.HasProperties))
                    yield return entry;
            }
            else if (set is IIfcElementQuantity quantitySet)
            {
                foreach (var quantity in quantitySet.Quantities)
                {
                    var (value, measure) = QuantityValue(quantity);
                    yield return new PropertyEntry
                    {
                        Name = ToText(quantity.Name),
                        Values = new[] { new PropertyValue(value, measure) },
                    };
                }
            }
            else
            {
                // Predefined property sets such as IfcDoorPanelProperties keep their data in
                // direct attributes rather than in IfcProperty instances. IDS supports them,
                // while discouraging their use.
                foreach (var attribute in DirectAttributes(set))
                {
                    if (attribute.Name == "GlobalId" || attribute.Name == "OwnerHistory"
                        || attribute.Name == "Name" || attribute.Name == "Description")
                        continue;
                    var raw = attribute.GetValue(set);
                    var value = AttributeValue(raw);
                    if (value == null)
                        continue;
                    yield return new PropertyEntry
                    {
                        Name = attribute.Name,
                        Values = new[] { new PropertyValue(value, MeasureName(raw)) },
                    };
                }
            }
        }

        private static IEnumerable<PropertyEntry> PropertyEntriesOf(IEnumerable<IIfcProperty> properties)
        {
            foreach (var property in properties)
            {
                var (values, unit) = PropertyValues(property);
                yield return new PropertyEntry { Name = ToText(property.Name), Values = values, Unit = unit };
            }
        }

        private static (IEnumerable<PropertyValue> Values, IIfcUnit Unit) PropertyValues(IIfcProperty property)
        {
            switch (property)
            {
                case IIfcPropertySingleValue single:
                    return (ValuesOf(new object[] { single.NominalValue }), single.Unit);
                case IIfcPropertyEnumeratedValue enumerated:
                    return (ValuesOf(enumerated.EnumerationValues), null);
                case IIfcPropertyListValue list:
                    return (ValuesOf(list.ListValues), list.Unit);
                case IIfcPropertyBoundedValue bounded:
                    return (ValuesOf(new object[] { bounded.LowerBoundValue, bounded.UpperBoundValue, bounded.SetPointValue }),
                        bounded.Unit);
                case IIfcPropertyTableValue table:
                    // Both columns count: a value may appear as a defining or a defined value,
                    // and the two columns can use different measures.
                    return (ValuesOf(table.DefiningValues).Concat(ValuesOf(table.DefinedValues)),
                        table.DefinedUnit ?? table.DefiningUnit);
                default:
                    return (Enumerable.Empty<PropertyValue>(), null);
            }
        }

        private static IEnumerable<PropertyValue> ValuesOf(IEnumerable values) =>
            values == null
                ? Enumerable.Empty<PropertyValue>()
                : values.Cast<object>().Select(v => new PropertyValue(ToText(v), MeasureName(v))).ToList();

        private static (string Value, string Measure) QuantityValue(IIfcPhysicalQuantity quantity)
        {
            switch (quantity)
            {
                case IIfcQuantityLength q: return (ToText(q.LengthValue), "IFCLENGTHMEASURE");
                case IIfcQuantityArea q: return (ToText(q.AreaValue), "IFCAREAMEASURE");
                case IIfcQuantityVolume q: return (ToText(q.VolumeValue), "IFCVOLUMEMEASURE");
                case IIfcQuantityCount q: return (ToText(q.CountValue), "IFCCOUNTMEASURE");
                case IIfcQuantityWeight q: return (ToText(q.WeightValue), "IFCMASSMEASURE");
                case IIfcQuantityTime q: return (ToText(q.TimeValue), "IFCTIMEMEASURE");
                default: return (null, null);
            }
        }

        /// <summary>Upper case name of the IFC defined type wrapping a value, e.g. IFCLABEL.</summary>
        private static string MeasureName(object value) =>
            value is IExpressValueType express ? express.GetType().Name.ToUpperInvariant() : null;

        #endregion

        #region Classification

        /// <summary>
        /// An entity carrying any classification at all counts as located; both the system
        /// and the value are part of the match. That is what separates
        /// "an optional classification value passes if null", where the entity has none, from
        /// "an optional classification value fails if no match", where it has one that does
        /// not fit.
        /// </summary>
        public IdsFacetMatch MatchClassification(IdsClassificationFacet facet, IPersistEntity entity)
        {
            var classifications = ClassificationsOf(entity).ToList();
            if (classifications.Count == 0)
                return IdsFacetMatch.NotFound;

            var seen = new List<string>();

            foreach (var relating in classifications)
            {
                if (relating is IIfcClassificationReference reference)
                {
                    var system = SystemNameOf(reference);
                    // A subreference satisfies a value naming any of its ancestors, so the
                    // whole chain of identifications counts.
                    var identifications = IdentificationChain(reference).Where(i => i != null).ToList();
                    seen.Add($"{system ?? "?"}:{string.Join("/", identifications)}");

                    if (facet.System != null && !(system != null && facet.System.Matches(system)))
                        continue;
                    if (facet.Value == null
                        ? identifications.Count > 0
                        : identifications.Any(facet.Value.Matches))
                        return IdsFacetMatch.Found(seen[seen.Count - 1]);
                }
                else if (relating is IIfcClassification classification)
                {
                    var name = ToText(classification.Name);
                    seen.Add(name ?? "?");

                    if (facet.System != null && !(name != null && facet.System.Matches(name)))
                        continue;
                    // A bare classification has no reference identification to test, so it can
                    // only satisfy a facet that does not constrain the value.
                    if (facet.Value == null && !string.IsNullOrEmpty(name))
                        return IdsFacetMatch.Found(name);
                }
            }

            return IdsFacetMatch.WrongValue(string.Join(", ", seen.Distinct()));
        }

        /// <summary>
        /// Classifications associated with the entity, merged with those of its defining type
        /// for any classification system the occurrence does not itself cover, plus external
        /// references for non-rooted resources such as IfcMaterial.
        /// </summary>
        private static IEnumerable<IIfcClassificationSelect> ClassificationsOf(IPersistEntity entity)
        {
            var own = new List<IIfcClassificationSelect>();

            if (entity is IIfcObjectDefinition definition)
                own.AddRange(definition.HasAssociations.OfType<IIfcRelAssociatesClassification>()
                    .Select(r => r.RelatingClassification).Where(c => c != null));

            // Non-rooted resources cannot take part in IfcRelAssociatesClassification; they
            // are linked through IfcExternalReferenceRelationship instead.
            if (entity is IIfcMaterialDefinition materialDefinition)
                own.AddRange(materialDefinition.HasExternalReferences
                    .Select(r => r.RelatingReference).OfType<IIfcClassificationSelect>());

            if (!(entity is IIfcObject obj))
                return own;

            var coveredSystems = new HashSet<string>(own.Select(SystemOf).Where(s => s != null));
            var inherited = obj.IsTypedBy.Select(r => r.RelatingType).Where(t => t != null)
                .SelectMany(t => t.HasAssociations.OfType<IIfcRelAssociatesClassification>())
                .Select(r => r.RelatingClassification).Where(c => c != null)
                // The occurrence overrides the type per system, not wholesale.
                .Where(c => !coveredSystems.Contains(SystemOf(c) ?? string.Empty));

            return own.Concat(inherited);
        }

        private static string SystemOf(IIfcClassificationSelect classification)
        {
            switch (classification)
            {
                case IIfcClassificationReference reference: return SystemNameOf(reference);
                case IIfcClassification c: return ToText(c.Name);
                default: return null;
            }
        }

        /// <summary>The reference's identification plus those of its parent references.</summary>
        private static IEnumerable<string> IdentificationChain(IIfcClassificationReference reference)
        {
            object current = reference;
            for (int depth = 0; depth < 16 && current is IIfcClassificationReference node; depth++)
            {
                yield return ToText(node.Identification) ?? ToText(node.Name);
                current = node.ReferencedSource;
            }
        }

        /// <summary>
        /// Walks up ReferencedSource until it reaches the IfcClassification that names the
        /// system; references can be nested several levels deep.
        /// </summary>
        private static string SystemNameOf(IIfcClassificationReference reference)
        {
            object source = reference.ReferencedSource;
            for (int depth = 0; depth < 16 && source != null; depth++)
            {
                if (source is IIfcClassification classification)
                    return ToText(classification.Name);
                if (source is IIfcClassificationReference parent)
                {
                    source = parent.ReferencedSource;
                    continue;
                }
                break;
            }
            return null;
        }

        #endregion

        #region Material

        public IdsFacetMatch MatchMaterial(IdsMaterialFacet facet, IPersistEntity entity)
        {
            var names = new List<string>();
            foreach (var material in MaterialsOf(entity))
                CollectMaterialNames(material, names);

            if (names.Count == 0)
                return IdsFacetMatch.NotFound;

            if (facet.Value == null)
                return IdsFacetMatch.Found(string.Join(", ", names));

            // Any one of the names or categories in a set satisfying the value is enough.
            var hit = names.FirstOrDefault(n => facet.Value.Matches(n));
            return hit != null
                ? IdsFacetMatch.Found(hit)
                : IdsFacetMatch.WrongValue(string.Join(", ", names));
        }

        /// <summary>Materials of the entity, or those inherited from its defining type.</summary>
        private static IEnumerable<IIfcMaterialSelect> MaterialsOf(IPersistEntity entity)
        {
            if (entity is IIfcObjectDefinition definition)
            {
                var own = definition.HasAssociations.OfType<IIfcRelAssociatesMaterial>()
                    .Select(r => r.RelatingMaterial).Where(m => m != null).ToList();
                if (own.Count > 0)
                    return own;
            }

            if (entity is IIfcObject obj)
                return obj.IsTypedBy.Select(r => r.RelatingType).Where(t => t != null)
                    .SelectMany(t => t.HasAssociations.OfType<IIfcRelAssociatesMaterial>())
                    .Select(r => r.RelatingMaterial).Where(m => m != null);

            return Enumerable.Empty<IIfcMaterialSelect>();
        }

        /// <summary>
        /// Collects every name and category in a material structure. IDS lets a value match
        /// a category as well as a name, at any level of a layer, profile or constituent set.
        /// </summary>
        private static void CollectMaterialNames(IIfcMaterialSelect material, ICollection<string> names, int depth = 0)
        {
            if (material == null || depth > 8)
                return;

            switch (material)
            {
                case IIfcMaterial m:
                    AddName(ToText(m.Name), names);
                    AddName(ToText(m.Category), names);
                    break;
                case IIfcMaterialList list:
                    foreach (var item in list.Materials)
                        CollectMaterialNames(item, names, depth + 1);
                    break;
                case IIfcMaterialLayerSetUsage usage:
                    CollectMaterialNames(usage.ForLayerSet, names, depth + 1);
                    break;
                case IIfcMaterialLayerSet layerSet:
                    AddName(ToText(layerSet.LayerSetName), names);
                    foreach (var layer in layerSet.MaterialLayers)
                        CollectMaterialNames(layer, names, depth + 1);
                    break;
                case IIfcMaterialLayer layer:
                    AddName(ToText(layer.Name), names);
                    AddName(ToText(layer.Category), names);
                    CollectMaterialNames(layer.Material, names, depth + 1);
                    break;
                case IIfcMaterialProfileSetUsage profileUsage:
                    CollectMaterialNames(profileUsage.ForProfileSet, names, depth + 1);
                    break;
                case IIfcMaterialProfileSet profileSet:
                    AddName(ToText(profileSet.Name), names);
                    foreach (var profile in profileSet.MaterialProfiles)
                        CollectMaterialNames(profile, names, depth + 1);
                    break;
                case IIfcMaterialProfile profile:
                    AddName(ToText(profile.Name), names);
                    AddName(ToText(profile.Category), names);
                    CollectMaterialNames(profile.Material, names, depth + 1);
                    break;
                case IIfcMaterialConstituentSet constituentSet:
                    AddName(ToText(constituentSet.Name), names);
                    foreach (var constituent in constituentSet.MaterialConstituents)
                        CollectMaterialNames(constituent, names, depth + 1);
                    break;
                case IIfcMaterialConstituent constituent:
                    AddName(ToText(constituent.Name), names);
                    AddName(ToText(constituent.Category), names);
                    CollectMaterialNames(constituent.Material, names, depth + 1);
                    break;
            }
        }

        private static void AddName(string name, ICollection<string> names)
        {
            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                names.Add(name);
        }

        #endregion

        #region PartOf

        public IdsFacetMatch MatchPartOf(IdsPartOfFacet facet, IPersistEntity entity)
        {
            if (facet.Entity == null)
                return IdsFacetMatch.NotFound;

            string lastSeen = null;
            bool anyRelation = false;

            foreach (var related in RelatedEntities(facet.Relation, entity))
            {
                anyRelation = true;
                var match = MatchEntity(facet.Entity, related);
                lastSeen = match.Observed ?? lastSeen;
                if (match.Matched)
                    return IdsFacetMatch.Found(match.Observed);
            }

            return anyRelation ? IdsFacetMatch.WrongValue(lastSeen) : IdsFacetMatch.NotFound;
        }

        /// <summary>
        /// The entities on the other side of the relationship, per the relations ids.xsd
        /// allows. A null relation means any of them.
        /// <para>
        /// Relationships are followed transitively: the test cases require that a part
        /// aggregated several levels deep still counts as part of its ancestor, and likewise
        /// for nesting and spatial containment.
        /// </para>
        /// </summary>
        private static IEnumerable<IPersistEntity> RelatedEntities(string relation, IPersistEntity entity)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<IPersistEntity>();
            queue.Enqueue(entity);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var parent in DirectlyRelated(relation, current))
                {
                    if (parent == null || !seen.Add(parent.EntityLabel))
                        continue;
                    yield return parent;
                    queue.Enqueue(parent);
                }
            }
        }

        private static IEnumerable<IPersistEntity> DirectlyRelated(string relation, IPersistEntity entity)
        {
            bool any = string.IsNullOrWhiteSpace(relation);

            if (entity is IIfcObjectDefinition definition)
            {
                if (any || relation == IdsRelations.Aggregates)
                    foreach (var rel in definition.Decomposes.OfType<IIfcRelAggregates>())
                        yield return rel.RelatingObject;

                if (any || relation == IdsRelations.Nests)
                    foreach (var rel in definition.Nests)
                        yield return rel.RelatingObject;

                if (any || relation == IdsRelations.AssignsToGroup)
                    foreach (var rel in definition.HasAssignments.OfType<IIfcRelAssignsToGroup>())
                        yield return rel.RelatingGroup;
            }

            if ((any || relation == IdsRelations.ContainedInSpatialStructure) && entity is IIfcElement contained)
                foreach (var rel in contained.ContainedInStructure)
                    yield return rel.RelatingStructure;

            if (any || relation == IdsRelations.VoidsOrFills)
            {
                if (entity is IIfcFeatureElementSubtraction opening && opening.VoidsElements != null)
                    yield return opening.VoidsElements.RelatingBuildingElement;
                if (entity is IIfcElement filling)
                    foreach (var rel in filling.FillsVoids)
                        yield return rel.RelatingOpeningElement;
            }
        }

        #endregion

        /// <summary>
        /// Renders an IFC value as the text IDS compares against.
        /// Booleans become lowercase "true"/"false" because IDS mandates that spelling, and
        /// doubles use the invariant culture so a comma decimal separator never leaks in.
        /// A logical UNKNOWN has no text, which makes it fail every constraint.
        /// </summary>
        public static string ToText(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    return text;
                case bool flag:
                    return flag ? "true" : "false";
                case IExpressValueType express:
                    return ToText(express.Value);
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString("R", CultureInfo.InvariantCulture);
                case decimal m:
                    return m.ToString(CultureInfo.InvariantCulture);
                case IEnumerable sequence:
                    var texts = sequence.Cast<object>().Select(ToText).Where(t => !string.IsNullOrEmpty(t)).ToList();
                    return texts.Count == 0 ? null : string.Join(", ", texts);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}
