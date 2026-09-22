using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcToolbox.Core.Ids.Model
{
    /// <summary>
    /// An Information Delivery Specification document.
    /// IDS 1.0 became an official buildingSMART standard on 1 June 2024; this model follows
    /// Schema/ids.xsd version 1.0.0.
    /// </summary>
    public class IdsDocument
    {
        public const string Namespace = "http://standards.buildingsmart.org/IDS";

        public IdsInfo Info { get; set; } = new IdsInfo();
        public IList<IdsSpecification> Specifications { get; set; } = new List<IdsSpecification>();

        public override string ToString() => $"{Info?.Title} ({Specifications.Count} specifications)";
    }

    public class IdsInfo
    {
        public string Title { get; set; }
        public string Copyright { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        /// <summary>The schema constrains this to an email address.</summary>
        public string Author { get; set; }

        public DateTime? Date { get; set; }
        public string Purpose { get; set; }
        public string Milestone { get; set; }
    }

    /// <summary>
    /// IFC schema versions a specification declares itself applicable to.
    /// The names are those ids.xsd allows, which are not the same spelling xbim uses.
    /// </summary>
    public static class IdsIfcVersion
    {
        public const string Ifc2X3 = "IFC2X3";
        public const string Ifc4 = "IFC4";
        public const string Ifc4X3Add2 = "IFC4X3_ADD2";

        /// <summary>Maps an xbim schema version onto the IDS spelling.</summary>
        public static string FromXbim(Xbim.Common.Step21.XbimSchemaVersion schemaVersion)
        {
            switch (schemaVersion)
            {
                case Xbim.Common.Step21.XbimSchemaVersion.Ifc2X3:
                    return Ifc2X3;
                case Xbim.Common.Step21.XbimSchemaVersion.Ifc4:
                case Xbim.Common.Step21.XbimSchemaVersion.Ifc4x1:
                    return Ifc4;
                default:
                    // Ifc4x3 and anything newer report as IFC4X3_ADD2 to IDS.
                    return schemaVersion.ToString().StartsWith("Ifc4x3", StringComparison.OrdinalIgnoreCase)
                        ? Ifc4X3Add2
                        : null;
            }
        }
    }

    /// <summary>
    /// One rule: which entities it applies to, and what those entities must satisfy.
    /// </summary>
    public class IdsSpecification
    {
        public string Name { get; set; }
        public string Identifier { get; set; }
        public string Description { get; set; }
        public string Instructions { get; set; }

        /// <summary>IFC schemas this specification applies to, from ids.xsd's enumeration.</summary>
        public IList<string> IfcVersions { get; set; } = new List<string>();

        public IdsApplicability Applicability { get; set; } = new IdsApplicability();
        public IdsRequirements Requirements { get; set; } = new IdsRequirements();

        public bool AppliesTo(Xbim.Common.Step21.XbimSchemaVersion schemaVersion)
        {
            if (IfcVersions.Count == 0)
                return true;
            var idsVersion = IdsIfcVersion.FromXbim(schemaVersion);
            return idsVersion != null && IfcVersions.Contains(idsVersion, StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Selects the entities a specification is about. All facets must match (logical AND).
    /// </summary>
    public class IdsApplicability
    {
        public IdsEntityFacet Entity { get; set; }
        public IList<IdsPartOfFacet> PartOf { get; set; } = new List<IdsPartOfFacet>();
        public IList<IdsClassificationFacet> Classifications { get; set; } = new List<IdsClassificationFacet>();
        public IList<IdsAttributeFacet> Attributes { get; set; } = new List<IdsAttributeFacet>();
        public IList<IdsPropertyFacet> Properties { get; set; } = new List<IdsPropertyFacet>();
        public IList<IdsMaterialFacet> Materials { get; set; } = new List<IdsMaterialFacet>();

        /// <summary>
        /// From the xs:occurs attribute group. Together with <see cref="MaxOccurs"/> this says
        /// how many entities the specification expects to find:
        /// minOccurs 0 makes the whole specification optional, maxOccurs 0 makes it prohibited.
        /// </summary>
        public int? MinOccurs { get; set; }

        /// <summary>Null means unbounded.</summary>
        public int? MaxOccurs { get; set; }

        /// <summary>No facet at all means the specification applies to every entity.</summary>
        public bool IsEmpty => Entity == null && PartOf.Count == 0 && Classifications.Count == 0
                               && Attributes.Count == 0 && Properties.Count == 0 && Materials.Count == 0;

        public IEnumerable<IIdsFacet> AllFacets()
        {
            if (Entity != null) yield return Entity;
            foreach (var f in PartOf) yield return f;
            foreach (var f in Classifications) yield return f;
            foreach (var f in Attributes) yield return f;
            foreach (var f in Properties) yield return f;
            foreach (var f in Materials) yield return f;
        }
    }

    /// <summary>
    /// What the applicable entities must satisfy. Each facet carries its own cardinality.
    /// </summary>
    public class IdsRequirements
    {
        public string Description { get; set; }

        public IdsEntityFacet Entity { get; set; }
        public IList<IdsPartOfFacet> PartOf { get; set; } = new List<IdsPartOfFacet>();
        public IList<IdsClassificationFacet> Classifications { get; set; } = new List<IdsClassificationFacet>();
        public IList<IdsAttributeFacet> Attributes { get; set; } = new List<IdsAttributeFacet>();
        public IList<IdsPropertyFacet> Properties { get; set; } = new List<IdsPropertyFacet>();
        public IList<IdsMaterialFacet> Materials { get; set; } = new List<IdsMaterialFacet>();

        public bool IsEmpty => !AllFacets().Any();

        public IEnumerable<IIdsFacet> AllFacets()
        {
            if (Entity != null) yield return Entity;
            foreach (var f in PartOf) yield return f;
            foreach (var f in Classifications) yield return f;
            foreach (var f in Attributes) yield return f;
            foreach (var f in Properties) yield return f;
            foreach (var f in Materials) yield return f;
        }
    }
}
