namespace IfcToolbox.Core.Ids.Validation
{
    /// <summary>
    /// Outcome of testing one facet against one entity.
    /// <para>
    /// The two flags are what make the three cardinalities work. <see cref="Located"/> says
    /// the thing the facet names exists at all - the attribute, the property in that set, a
    /// classification, a material. <see cref="Matched"/> additionally says its value satisfies
    /// the constraint. An optional facet is satisfied when nothing was located, or when
    /// everything located matched; that is the distinction the IDS manual draws with
    /// "either don't have the property, or if they do, it is of the expected value".
    /// </para>
    /// </summary>
    public struct IdsFacetMatch
    {
        public IdsFacetMatch(bool located, bool matched, string observed)
        {
            Located = located;
            Matched = matched;
            Observed = observed;
        }

        public bool Located { get; }

        public bool Matched { get; }

        /// <summary>What was actually found, for the report. Null when nothing was found.</summary>
        public string Observed { get; }

        /// <summary>Nothing of the kind the facet names is present on the entity.</summary>
        public static readonly IdsFacetMatch NotFound = new IdsFacetMatch(false, false, null);

        public static IdsFacetMatch Found(string observed) => new IdsFacetMatch(true, true, observed);

        /// <summary>Present, but the value does not satisfy the constraint.</summary>
        public static IdsFacetMatch WrongValue(string observed) => new IdsFacetMatch(true, false, observed);
    }
}
