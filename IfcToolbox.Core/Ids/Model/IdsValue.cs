using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace IfcToolbox.Core.Ids.Model
{
    /// <summary>
    /// An IDS value constraint: either a literal (<c>simpleValue</c>) or an XSD
    /// <c>xs:restriction</c>. See ids.xsd, complexType "idsValue".
    /// </summary>
    public class IdsValue
    {
        public IdsValue() { }

        public IdsValue(string simpleValue)
        {
            SimpleValue = simpleValue;
        }

        /// <summary>Literal to match, when the constraint is a simple value.</summary>
        public string SimpleValue { get; set; }

        /// <summary>Set instead of <see cref="SimpleValue"/> when the constraint is a restriction.</summary>
        public IdsRestriction Restriction { get; set; }

        public bool IsRestriction => Restriction != null;

        /// <summary>
        /// True when the candidate satisfies this constraint.
        /// A null candidate never matches: IDS treats "no value" as a failure, which is what
        /// separates a required facet from an optional one.
        /// <para>
        /// Comparison is case sensitive, but numbers are compared as numbers so that an
        /// IfcReal of 42.0 satisfies a simpleValue of "42".
        /// </para>
        /// </summary>
        public bool Matches(string candidate)
        {
            if (candidate == null)
                return false;
            if (Restriction != null)
                return Restriction.Matches(candidate);
            if (string.Equals(SimpleValue, candidate, StringComparison.Ordinal))
                return true;
            return NumericEquals(SimpleValue, candidate);
        }

        /// <summary>
        /// Tolerance IDS prescribes for floating point equality.
        /// </summary>
        public const double Epsilon = 1e-6;

        /// <summary>
        /// Numeric equality as IDS defines it:
        /// <c>x == v</c> when <c>(v - abs(v)*e - e) &lt; x &lt; (v + abs(v)*e + e)</c>, with
        /// <c>e = 1e-6</c>. The relative part copes with railway lengths, the fixed part with
        /// areas close to zero. Both bounds are strict.
        /// <para>
        /// This applies to equality only. Ranges (minInclusive and friends) are compared
        /// exactly, which is how an IDS author asks for a tolerance of their own.
        /// </para>
        /// </summary>
        /// <param name="specified">The value the IDS asks for, v in the formula.</param>
        /// <param name="candidate">The value found in the model, x in the formula.</param>
        internal static bool NumericEquals(string specified, string candidate)
        {
            if (!TryParseNumber(specified, out double v) || !TryParseNumber(candidate, out double x))
                return false;
            if (v.Equals(x))
                return true;

            // The formula is written with strict inequalities, but a value sitting exactly on
            // a bound has to pass: the bound is itself computed in floating point, so the last
            // bits of the comparison are not meaningful. The IDS documentation says as much
            // when it notes that values near the limit of double precision may compare either
            // way. Hence the symmetric form plus a few bits of slack.
            double margin = Math.Abs(v) * Epsilon + Epsilon;
            return Math.Abs(x - v) <= margin * (1 + 1e-9);
        }

        internal static bool TryParseNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        public override string ToString() => Restriction?.ToString() ?? SimpleValue;
    }

    /// <summary>
    /// The subset of XSD facets IDS allows inside <c>xs:restriction</c>.
    /// </summary>
    public class IdsRestriction
    {
        /// <summary>xs:restriction/@base, e.g. "xs:string" or "xs:double".</summary>
        public string Base { get; set; }

        public IList<string> Enumeration { get; set; } = new List<string>();
        public IList<string> Patterns { get; set; } = new List<string>();

        public string MinInclusive { get; set; }
        public string MaxInclusive { get; set; }
        public string MinExclusive { get; set; }
        public string MaxExclusive { get; set; }

        public int? Length { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }

        public bool HasBounds => MinInclusive != null || MaxInclusive != null
                                 || MinExclusive != null || MaxExclusive != null;

        public bool Matches(string candidate)
        {
            if (candidate == null)
                return false;

            if (Enumeration.Count > 0
                && !Enumeration.Contains(candidate, StringComparer.Ordinal)
                && !Enumeration.Any(e => IdsValue.NumericEquals(e, candidate)))
                return false;

            // Several patterns act as alternatives, the way XSD unions them.
            if (Patterns.Count > 0 && !Patterns.Any(p => MatchesPattern(candidate, p)))
                return false;

            if (Length.HasValue && candidate.Length != Length.Value)
                return false;
            if (MinLength.HasValue && candidate.Length < MinLength.Value)
                return false;
            if (MaxLength.HasValue && candidate.Length > MaxLength.Value)
                return false;

            if (HasBounds && !MatchesBounds(candidate))
                return false;

            return true;
        }

        /// <summary>
        /// XSD patterns are implicitly anchored to the whole value, unlike .NET regular
        /// expressions, so the anchors are added here.
        /// </summary>
        private static bool MatchesPattern(string candidate, string pattern)
        {
            try
            {
                return Regex.IsMatch(candidate, "^(?:" + pattern + ")$");
            }
            catch (ArgumentException)
            {
                // An unparseable pattern cannot match anything; failing the check is safer
                // than throwing in the middle of validating a whole model.
                return false;
            }
        }

        private bool MatchesBounds(string candidate)
        {
            if (!TryParseNumber(candidate, out double value))
                return false;

            if (MinInclusive != null && (!TryParseNumber(MinInclusive, out var min) || value < min))
                return false;
            if (MaxInclusive != null && (!TryParseNumber(MaxInclusive, out var max) || value > max))
                return false;
            if (MinExclusive != null && (!TryParseNumber(MinExclusive, out var minEx) || value <= minEx))
                return false;
            if (MaxExclusive != null && (!TryParseNumber(MaxExclusive, out var maxEx) || value >= maxEx))
                return false;

            return true;
        }

        private static bool TryParseNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        public override string ToString()
        {
            var parts = new List<string>();
            if (Enumeration.Count > 0) parts.Add("one of [" + string.Join(", ", Enumeration) + "]");
            foreach (var pattern in Patterns) parts.Add("matching /" + pattern + "/");
            if (Length.HasValue) parts.Add($"length {Length}");
            if (MinLength.HasValue) parts.Add($"length >= {MinLength}");
            if (MaxLength.HasValue) parts.Add($"length <= {MaxLength}");
            if (MinInclusive != null) parts.Add($">= {MinInclusive}");
            if (MaxInclusive != null) parts.Add($"<= {MaxInclusive}");
            if (MinExclusive != null) parts.Add($"> {MinExclusive}");
            if (MaxExclusive != null) parts.Add($"< {MaxExclusive}");
            return parts.Count == 0 ? "any value" : string.Join(" and ", parts);
        }
    }
}
