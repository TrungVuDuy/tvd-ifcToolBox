using System;
using System.Collections.Generic;

namespace IfcToolbox.Core.Bsdd.Models
{
    // Data contracts for the current bSDD API at api.bsdd.buildingsmart.org.
    //
    // bSDD renamed its data structure in 2023: what used to be a "Domain" is now a
    // "Dictionary" and a "Classification" is now a "Class". The endpoints these types
    // map to therefore have no counterpart in the old generated client.
    //
    // Property names follow the JSON payload; BsddClient deserialises case-insensitively.

    #region Dictionary

    public class BsddDictionary
    {
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string OrganizationCodeOwner { get; set; }
        public string OrganizationNameOwner { get; set; }
        public string DefaultLanguageCode { get; set; }
        public bool IsLatestVersion { get; set; }
        public bool IsVerified { get; set; }
        public bool IsPrivate { get; set; }
        public string License { get; set; }
        public string LicenseUrl { get; set; }
        public string Status { get; set; }
        public string MoreInfoUrl { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public DateTime? LastUpdatedUtc { get; set; }
        public IList<BsddLanguageInfo> AvailableLanguages { get; set; } = new List<BsddLanguageInfo>();

        public override string ToString() => $"{Name} {Version} ({Code})";
    }

    public class BsddLanguageInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }

        public override string ToString() => $"{Name} ({Code})";
    }

    #endregion

    #region Class

    /// <summary>Compact class entry as returned when listing a dictionary's classes.</summary>
    public class BsddClassListItem
    {
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string ClassType { get; set; }
        public string ReferenceCode { get; set; }
        public string ParentClassCode { get; set; }
        public string DescriptionPart { get; set; }

        public override string ToString() => $"{Name} ({Code})";
    }

    /// <summary>Class entry as returned by the search endpoint.</summary>
    public class BsddClassSearchItem
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public string ClassType { get; set; }
        public string ReferenceCode { get; set; }
        public string Description { get; set; }
        public string DictionaryUri { get; set; }
        public string DictionaryName { get; set; }
        public string ParentClassName { get; set; }
        public IList<string> RelatedIfcEntityNames { get; set; } = new List<string>();

        public override string ToString() => $"{Name} ({ReferenceCode})";
    }

    /// <summary>Full class detail.</summary>
    public class BsddClass
    {
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string ClassType { get; set; }
        public string ReferenceCode { get; set; }
        public string Definition { get; set; }
        public string DictionaryUri { get; set; }
        public string Status { get; set; }
        public string Uid { get; set; }

        /// <summary>Immediate parent class, null for a root class.</summary>
        public BsddClassReference ParentClassReference { get; set; }

        /// <summary>
        /// Full inheritance chain, ordered from the root down to this class
        /// (IfcRoot -> IfcObjectDefinition -> ... -> IfcWall).
        /// </summary>
        public IList<BsddClassHierarchyItem> Hierarchy { get; set; } = new List<BsddClassHierarchyItem>();

        public IList<string> CountriesOfUse { get; set; } = new List<string>();
        public IList<string> SubdivisionsOfUse { get; set; } = new List<string>();
        public IList<string> ReplacedObjectCodes { get; set; } = new List<string>();
        public IList<string> ReplacingObjectCodes { get; set; } = new List<string>();
        public DateTime? ActivationDateUtc { get; set; }
        public DateTime? VersionDateUtc { get; set; }

        public override string ToString() => $"{Name} ({Code})";
    }

    /// <summary>Pointer to another class, used for the parent reference.</summary>
    public class BsddClassReference
    {
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }

        public override string ToString() => $"{Name} ({Code})";
    }

    /// <summary>One step of a class's inheritance chain.</summary>
    public class BsddClassHierarchyItem
    {
        public int Level { get; set; }
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }

        public override string ToString() => $"{Level}. {Name} ({Code})";
    }

    /// <summary>
    /// A property as it applies to one class. <see cref="PropertySet"/> is what maps onto an
    /// IFC property set name, which is what the validator checks the model against.
    /// </summary>
    public class BsddClassProperty
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Definition { get; set; }
        public string DataType { get; set; }
        public string PropertyCode { get; set; }
        public string PropertySet { get; set; }
        public string PropertyStatus { get; set; }
        public string PropertyValueKind { get; set; }
        public string PropertyUri { get; set; }
        public string PropertyDictionaryName { get; set; }
        public string PropertyDictionaryUri { get; set; }
        public string PredefinedValue { get; set; }
        public string Example { get; set; }
        public string Dimension { get; set; }
        public IList<string> Units { get; set; }
        public IList<string> QudtCodes { get; set; }
        public IList<BsddAllowedValue> AllowedValues { get; set; }

        public override string ToString() => $"{PropertySet}.{Name} [{DataType}]";
    }

    public class BsddAllowedValue
    {
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Value { get; set; }
        public int? SortNumber { get; set; }

        public override string ToString() => Value;
    }

    #endregion

    #region Response envelopes

    /// <summary>Shared shape of the paged bSDD responses.</summary>
    public interface IBsddPage
    {
        int TotalCount { get; }
        int Offset { get; }
        int Count { get; }
    }

    public class BsddDictionaryResponse : IBsddPage
    {
        public IList<BsddDictionary> Dictionaries { get; set; } = new List<BsddDictionary>();
        public int TotalCount { get; set; }
        public int Offset { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Response of Dictionary/v1/Classes. Note the odd envelope: the paging fields are
    /// prefixed with "classes" here, unlike every other endpoint.
    /// </summary>
    public class BsddDictionaryClassesResponse : IBsddPage
    {
        public string Uri { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public IList<BsddClassListItem> Classes { get; set; } = new List<BsddClassListItem>();
        public int ClassesTotalCount { get; set; }
        public int ClassesOffset { get; set; }
        public int ClassesCount { get; set; }

        public int TotalCount => ClassesTotalCount;
        public int Offset => ClassesOffset;
        public int Count => ClassesCount;
    }

    public class BsddClassSearchResponse : IBsddPage
    {
        public IList<BsddClassSearchItem> Classes { get; set; } = new List<BsddClassSearchItem>();
        public int TotalCount { get; set; }
        public int Offset { get; set; }
        public int Count { get; set; }
    }

    public class BsddClassPropertiesResponse : IBsddPage
    {
        public string ClassUri { get; set; }
        public IList<BsddClassProperty> ClassProperties { get; set; } = new List<BsddClassProperty>();
        public int TotalCount { get; set; }
        public int Offset { get; set; }
        public int Count { get; set; }
    }

    #endregion
}
