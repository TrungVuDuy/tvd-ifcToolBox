using System.Collections.Generic;

namespace IfcToolbox.Core.Ids.Model
{
    /// <summary>
    /// Whether a requirement facet has to match, must not match, or is merely informative.
    /// The entity facet carries no cardinality in requirements: ids.xsd states it is always
    /// treated as required.
    /// </summary>
    public enum IdsCardinality
    {
        Required,
        Optional,
        Prohibited,
    }

    /// <summary>
    /// One condition of a specification. The same six facet types are used both to select
    /// which entities a specification applies to and to state what they must satisfy.
    /// </summary>
    public interface IIdsFacet
    {
        /// <summary>Only meaningful inside requirements; applicability facets always AND together.</summary>
        IdsCardinality Cardinality { get; set; }

        /// <summary>Free text the IDS author leaves for whoever authors the IFC.</summary>
        string Instructions { get; set; }

        /// <summary>Human readable description, used in reports.</summary>
        string Describe();
    }

    public abstract class IdsFacet : IIdsFacet
    {
        public IdsCardinality Cardinality { get; set; } = IdsCardinality.Required;
        public string Instructions { get; set; }
        public abstract string Describe();
        public override string ToString() => Describe();
    }

    /// <summary>IFC entity type, optionally narrowed by predefined type.</summary>
    public class IdsEntityFacet : IdsFacet
    {
        /// <summary>Entity name as written in IDS: upper case, e.g. IFCWALL.</summary>
        public IdsValue Name { get; set; }

        public IdsValue PredefinedType { get; set; }

        public override string Describe() =>
            PredefinedType == null ? $"entity {Name}" : $"entity {Name} of predefined type {PredefinedType}";
    }

    /// <summary>A direct IFC attribute of the entity, such as Name or Description.</summary>
    public class IdsAttributeFacet : IdsFacet
    {
        public IdsValue Name { get; set; }

        /// <summary>Optional constraint on the attribute's value.</summary>
        public IdsValue Value { get; set; }

        public override string Describe() =>
            Value == null ? $"attribute {Name}" : $"attribute {Name} = {Value}";
    }

    /// <summary>A property in an IfcPropertySet or a quantity in an IfcElementQuantity.</summary>
    public class IdsPropertyFacet : IdsFacet
    {
        public IdsValue PropertySet { get; set; }

        /// <summary>
        /// Property name as stored in the IFC file. Called baseName in IDS to make clear it is
        /// not the localised label a tool might display.
        /// </summary>
        public IdsValue BaseName { get; set; }

        public IdsValue Value { get; set; }

        /// <summary>Name of an IFC defined type, upper case, e.g. IFCLABEL.</summary>
        public string DataType { get; set; }

        public string Uri { get; set; }

        public override string Describe() =>
            Value == null
                ? $"property {PropertySet}.{BaseName}"
                : $"property {PropertySet}.{BaseName} = {Value}";
    }

    /// <summary>A classification reference associated with the entity.</summary>
    public class IdsClassificationFacet : IdsFacet
    {
        /// <summary>Classification system name, e.g. Uniclass 2015.</summary>
        public IdsValue System { get; set; }

        /// <summary>Optional constraint on the reference identification.</summary>
        public IdsValue Value { get; set; }

        public string Uri { get; set; }

        public override string Describe() =>
            Value == null ? $"classification from {System}" : $"classification {Value} from {System}";
    }

    /// <summary>A material associated with the entity.</summary>
    public class IdsMaterialFacet : IdsFacet
    {
        public IdsValue Value { get; set; }

        public string Uri { get; set; }

        public override string Describe() =>
            Value == null ? "any material" : $"material {Value}";
    }

    /// <summary>
    /// The entity takes part in a relationship with another entity, e.g. it is aggregated
    /// into an IfcBuildingStorey.
    /// </summary>
    public class IdsPartOfFacet : IdsFacet
    {
        /// <summary>
        /// One of the relations ids.xsd allows. Null means any of them.
        /// Note IFCRELVOIDSELEMENT and IFCRELFILLSELEMENT are a single enumeration entry in
        /// the schema, separated by a space.
        /// </summary>
        public string Relation { get; set; }

        /// <summary>The entity on the other side of the relationship.</summary>
        public IdsEntityFacet Entity { get; set; }

        public override string Describe() =>
            Relation == null ? $"part of {Entity?.Name}" : $"part of {Entity?.Name} via {Relation}";
    }

    /// <summary>Relation names ids.xsd permits on a partOf facet.</summary>
    public static class IdsRelations
    {
        public const string Aggregates = "IFCRELAGGREGATES";
        public const string AssignsToGroup = "IFCRELASSIGNSTOGROUP";
        public const string ContainedInSpatialStructure = "IFCRELCONTAINEDINSPATIALSTRUCTURE";
        public const string Nests = "IFCRELNESTS";
        public const string VoidsOrFills = "IFCRELVOIDSELEMENT IFCRELFILLSELEMENT";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Aggregates, AssignsToGroup, ContainedInSpatialStructure, Nests, VoidsOrFills,
        };
    }
}
