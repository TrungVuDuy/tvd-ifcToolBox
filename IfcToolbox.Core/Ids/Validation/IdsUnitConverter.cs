using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Ids.Validation
{
    /// <summary>
    /// Converts measured values from a model's own units into the SI base units IDS compares
    /// against. "Numerical measure values are represented in IDS files using SI units. When
    /// IFC models are verified, their values need to be converted to the default unit before
    /// comparison" - IDS user manual, units.
    /// <para>
    /// A model drawn in millimetres reports a length of 1000; the same length expressed the
    /// way IDS expects is 1. Without this step every numeric property check on a millimetre
    /// model would fail.
    /// </para>
    /// <para>
    /// Covers the SI units and conversion based units of the seven base quantities and their
    /// square and cubic forms. Derived units (IfcDerivedUnit) are not converted; values using
    /// them are compared as written.
    /// </para>
    /// </summary>
    public class IdsUnitConverter
    {
        private readonly Dictionary<string, double> _factorsByUnitType =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> MeasureToUnitType =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IFCLENGTHMEASURE"] = "LENGTHUNIT",
                ["IFCPOSITIVELENGTHMEASURE"] = "LENGTHUNIT",
                ["IFCNONNEGATIVELENGTHMEASURE"] = "LENGTHUNIT",
                ["IFCAREAMEASURE"] = "AREAUNIT",
                ["IFCVOLUMEMEASURE"] = "VOLUMEUNIT",
                ["IFCMASSMEASURE"] = "MASSUNIT",
                ["IFCTIMEMEASURE"] = "TIMEUNIT",
                ["IFCPLANEANGLEMEASURE"] = "PLANEANGLEUNIT",
                ["IFCPOSITIVEPLANEANGLEMEASURE"] = "PLANEANGLEUNIT",
                ["IFCSOLIDANGLEMEASURE"] = "SOLIDANGLEUNIT",
                ["IFCTHERMODYNAMICTEMPERATUREMEASURE"] = "THERMODYNAMICTEMPERATUREUNIT",
                ["IFCELECTRICCURRENTMEASURE"] = "ELECTRICCURRENTUNIT",
                ["IFCLUMINOUSINTENSITYMEASURE"] = "LUMINOUSINTENSITYUNIT",
                ["IFCAMOUNTOFSUBSTANCEMEASURE"] = "AMOUNTOFSUBSTANCEUNIT",
            };

        /// <summary>How many times the SI prefix multiplies the base unit.</summary>
        private static readonly Dictionary<string, double> PrefixFactors =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["EXA"] = 1e18, ["PETA"] = 1e15, ["TERA"] = 1e12, ["GIGA"] = 1e9,
                ["MEGA"] = 1e6, ["KILO"] = 1e3, ["HECTO"] = 1e2, ["DECA"] = 1e1,
                ["DECI"] = 1e-1, ["CENTI"] = 1e-2, ["MILLI"] = 1e-3, ["MICRO"] = 1e-6,
                ["NANO"] = 1e-9, ["PICO"] = 1e-12, ["FEMTO"] = 1e-15, ["ATTO"] = 1e-18,
            };

        public IdsUnitConverter(IModel model)
        {
            var assignment = model?.Instances.OfType<IIfcUnitAssignment>().FirstOrDefault();
            if (assignment == null)
                return;

            foreach (var unit in assignment.Units)
            {
                var unitType = UnitTypeOf(unit);
                if (unitType == null)
                    continue;
                var factor = FactorOf(unit);
                if (factor.HasValue)
                    _factorsByUnitType[unitType] = factor.Value;
            }
        }

        /// <summary>
        /// Value in SI base units, or the value unchanged when its unit is not known.
        /// </summary>
        public double Convert(double value, string measureName, IIfcUnit explicitUnit = null)
        {
            if (explicitUnit != null)
            {
                var explicitFactor = FactorOf(explicitUnit);
                if (explicitFactor.HasValue)
                    return value * explicitFactor.Value;
            }

            if (measureName == null || !MeasureToUnitType.TryGetValue(measureName, out var unitType))
                return value;
            return _factorsByUnitType.TryGetValue(unitType, out var factor) ? value * factor : value;
        }

        /// <summary>True when the measure is one this converter knows how to normalise.</summary>
        public static bool IsConvertible(string measureName) =>
            measureName != null && MeasureToUnitType.ContainsKey(measureName);

        private static string UnitTypeOf(IIfcUnit unit)
        {
            switch (unit)
            {
                case IIfcNamedUnit named:
                    return named.UnitType.ToString();
                default:
                    return null;
            }
        }

        /// <summary>Multiplier that takes a value in this unit to the SI base unit.</summary>
        private static double? FactorOf(IIfcUnit unit, int depth = 0)
        {
            if (depth > 4)
                return null;

            switch (unit)
            {
                case IIfcSIUnit si:
                    if (si.Prefix == null)
                        return 1d;
                    if (!PrefixFactors.TryGetValue(si.Prefix.ToString(), out var prefix))
                        return 1d;
                    // A prefix on a square or cubic unit applies to each dimension.
                    return Math.Pow(prefix, DimensionOf(si.Name.ToString()));

                case IIfcConversionBasedUnit conversion:
                    var measureWithUnit = conversion.ConversionFactor;
                    if (measureWithUnit?.ValueComponent == null)
                        return null;
                    if (!double.TryParse(IdsFacetMatcher.ToText(measureWithUnit.ValueComponent),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var component))
                        return null;
                    var baseFactor = FactorOf(measureWithUnit.UnitComponent, depth + 1) ?? 1d;
                    return component * baseFactor;

                default:
                    return null;
            }
        }

        /// <summary>Exponent the SI prefix is raised to, from the unit name.</summary>
        private static int DimensionOf(string siUnitName)
        {
            if (siUnitName == null)
                return 1;
            if (siUnitName.StartsWith("SQUARE_", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (siUnitName.StartsWith("CUBIC_", StringComparison.OrdinalIgnoreCase))
                return 3;
            return 1;
        }
    }
}
