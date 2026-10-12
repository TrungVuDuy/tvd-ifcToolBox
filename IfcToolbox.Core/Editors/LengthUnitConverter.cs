using IfcToolbox.Core.Geo;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xbim.Common;
using Xbim.Common.Metadata;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Editors
{
    /// <summary>What a unit conversion changed.</summary>
    public class LengthUnitConversion
    {
        public double FromMetres { get; set; }
        public double ToMetres { get; set; }
        /// <summary>Number of length values rescaled.</summary>
        public int ValuesScaled { get; set; }
        /// <summary>Values left alone because they carry their own unit (a property with an explicit unit, for example).</summary>
        public int ValuesWithOwnUnit { get; set; }
    }

    /// <summary>
    /// Changes a model's length unit, e.g. from millimetres to metres, rescaling every length in it.
    /// <para>
    /// Every explicit attribute declared as a length (IfcLengthMeasure, IfcPositiveLengthMeasure,
    /// IfcNonNegativeLengthMeasure), alone, in a list or in a list of lists (point coordinates,
    /// point lists), and every select value that turns out to be a length (a property's nominal
    /// value) is multiplied by old unit / new unit. Values with a unit of their own (a property or
    /// quantity with an explicit unit, IfcMeasureWithUnit) already say what they mean and stay.
    /// Areas, volumes and angles have units of their own in the unit assignment and do not change.
    /// </para>
    /// <para>
    /// The unit assignment gets a new IfcSIUnit for length; the old one is left for any derived
    /// unit that refers to it, so their meaning does not change. A map conversion's scale is
    /// adjusted so the model stays where it was on the map.
    /// </para>
    /// </summary>
    public static class LengthUnitConverter
    {
        private static readonly HashSet<string> LengthTypes = new(StringComparer.Ordinal)
        {
            "IfcLengthMeasure", "IfcPositiveLengthMeasure", "IfcNonNegativeLengthMeasure",
        };

        /// <summary>The units a model can be converted to, by the name used on the command line.</summary>
        public static IReadOnlyDictionary<string, (double Metres, IfcSIPrefix? Prefix)> Units { get; } =
            new Dictionary<string, (double, IfcSIPrefix?)>(StringComparer.OrdinalIgnoreCase)
            {
                ["mm"] = (0.001, IfcSIPrefix.MILLI),
                ["cm"] = (0.01, IfcSIPrefix.CENTI),
                ["m"] = (1, null),
            };

        // Per entity class: the attributes that can hold a length, worked out once.
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Candidates = new();

        /// <summary>
        /// Converts the model to <paramref name="unit"/> ("mm", "cm" or "m"). Must run inside a
        /// transaction. Does nothing when the model is already in that unit.
        /// </summary>
        public static LengthUnitConversion Convert(IModel model, string unit)
        {
            if (!Units.TryGetValue(unit ?? string.Empty, out var target))
                throw new ArgumentException($"Unknown length unit '{unit}'. Use one of: {string.Join(", ", Units.Keys)}.", nameof(unit));

            var result = new LengthUnitConversion
            {
                FromMetres = MapConversionService.LengthUnitInMetres(model),
                ToMetres = target.Metres,
            };
            if (Math.Abs(result.FromMetres - result.ToMetres) < result.ToMetres * 1e-12)
                return result;

            double factor = result.FromMetres / result.ToMetres;
            foreach (var entity in model.Instances.ToList())
                ScaleEntity(entity, factor, result);

            ReplaceLengthUnit(model, target.Prefix);
            AdjustMapConversions(model, factor);
            return result;
        }

        private static void ScaleEntity(IPersistEntity entity, double factor, LengthUnitConversion result)
        {
            // Defines what a unit is (conversion based units); its value is in its own unit.
            if (entity is IIfcMeasureWithUnit)
                return;
            // Eastings, northings and height are lengths in the map's unit, not the model's;
            // only the scale between the two changes (see AdjustMapConversions).
            if (entity is IIfcCoordinateOperation)
                return;

            var properties = Candidates.GetOrAdd(entity.GetType(), _ => FindCandidates(entity.ExpressType));
            if (properties.Length == 0)
                return;

            // A property or quantity with an explicit unit is not in the project's length unit.
            bool ownUnit = HasOwnUnit(entity);

            foreach (var property in properties)
            {
                var value = property.GetValue(entity);
                if (value == null)
                    continue;

                if (IsLength(value))
                {
                    if (ownUnit) { result.ValuesWithOwnUnit++; continue; }
                    property.SetValue(entity, Scale(value, factor));
                    result.ValuesScaled++;
                }
                else if (value is IEnumerable list && value is not string)
                {
                    if (ownUnit) continue;
                    result.ValuesScaled += ScaleList(list, factor);
                }
            }

            // Not a length type, but a distance: the tolerance used to tell points apart.
            if (entity is IIfcGeometricRepresentationContext context && context is not IIfcGeometricRepresentationSubContext && context.Precision.HasValue)
            {
                context.Precision = context.Precision.Value * factor;
                result.ValuesScaled++;
            }
        }

        /// <summary>Rescales the lengths in a list, and in lists inside it. Returns how many.</summary>
        private static int ScaleList(IEnumerable list, double factor)
        {
            int scaled = 0;
            var type = list.GetType();
            var indexer = type.GetProperty("Item", new[] { typeof(int) });
            var items = list.Cast<object>().ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;
                if (IsLength(item) && indexer != null && indexer.CanWrite)
                {
                    indexer.SetValue(list, Scale(item, factor), new object[] { i });
                    scaled++;
                }
                else if (item is IEnumerable nested && item is not string)
                {
                    scaled += ScaleList(nested, factor);
                }
            }
            return scaled;
        }

        /// <summary>
        /// Explicit attributes whose declared type is a length, a nullable length, a select (an
        /// interface, checked at run time) or a list.
        /// </summary>
        private static PropertyInfo[] FindCandidates(ExpressType type) =>
            type.Properties.Values
                .Where(p => !p.IsDerived && !p.IsInverse)
                .Select(p => p.PropertyInfo)
                .Where(p =>
                {
                    var declared = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    bool list = typeof(IEnumerable).IsAssignableFrom(declared) && declared != typeof(string);
                    // Lists are read-only properties whose items are replaced in place.
                    if (list)
                        return true;
                    if (!p.CanWrite)
                        return false;
                    bool select = declared.IsInterface && !typeof(IPersistEntity).IsAssignableFrom(declared);
                    return LengthTypes.Contains(declared.Name) || select;
                })
                .ToArray();

        private static bool IsLength(object value) =>
            value is IExpressValueType && LengthTypes.Contains(value.GetType().Name);

        /// <summary>A new value of the same IFC type (and schema) as <paramref name="value"/>, multiplied.</summary>
        private static object Scale(object value, double factor)
        {
            double number = System.Convert.ToDouble(((IExpressValueType)value).Value, System.Globalization.CultureInfo.InvariantCulture);
            return Activator.CreateInstance(value.GetType(), number * factor);
        }

        private static bool HasOwnUnit(IPersistEntity entity) => entity switch
        {
            IIfcPropertySingleValue single => single.Unit != null,
            IIfcPropertyBoundedValue bounded => bounded.Unit != null,
            IIfcPropertyListValue list => list.Unit != null,
            IIfcPropertyEnumeration enumeration => enumeration.Unit != null,
            IIfcPropertyTableValue table => table.DefiningUnit != null || table.DefinedUnit != null,
            IIfcPhysicalSimpleQuantity quantity => quantity.Unit != null,
            _ => false,
        };

        /// <summary>
        /// Points the unit assignment at a new SI length unit. The old unit stays in the file for
        /// derived units that may use it (a linear velocity, a moment of inertia).
        /// </summary>
        private static void ReplaceLengthUnit(IModel model, IfcSIPrefix? prefix)
        {
            foreach (var assignment in model.Instances.OfType<IIfcUnitAssignment>().ToList())
            {
                var old = assignment.Units.OfType<IIfcNamedUnit>().FirstOrDefault(u => u.UnitType == IfcUnitEnum.LENGTHUNIT);
                if (old != null)
                    assignment.Units.Remove(old);

                var unit = SchemaEntityFactory.New<IIfcSIUnit>(model, "IfcSIUnit");
                unit.UnitType = IfcUnitEnum.LENGTHUNIT;
                unit.Name = IfcSIUnitName.METRE;
                unit.Prefix = prefix;
                assignment.Units.Add(unit);
            }
        }

        /// <summary>
        /// The map conversion turns model units into map units through its scale. The model's
        /// numbers were multiplied by <paramref name="factor"/>, so the scale is divided by it.
        /// </summary>
        private static void AdjustMapConversions(IModel model, double factor)
        {
            foreach (var map in model.Instances.OfType<IIfcMapConversion>())
                map.Scale = (map.Scale ?? 1) / factor;
        }
    }
}
