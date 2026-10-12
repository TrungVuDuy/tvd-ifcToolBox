using IfcToolbox.Core.Bsdd.Models;
using IfcToolbox.Core.Ids.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IfcToolbox.Core.Bsdd
{
    /// <summary>
    /// Turns bSDD classes and the properties picked from them into IDS specifications, so
    /// requirements pulled from bSDD are checked by the same IDS engine as a hand written
    /// .ids file, and can be saved and shared as one.
    /// <para>
    /// For a class of the IFC dictionary itself (IfcWall, IfcDoorGATE) the specification applies
    /// to that entity and predefined type. For any other dictionary it applies to whatever
    /// carries a classification reference to the class, which is how bSDD classes are linked
    /// to IFC objects.
    /// </para>
    /// </summary>
    public static class BsddIds
    {
        // IfcWall, IfcWallSOLIDWALL, IfcBuildingElementProxyUSERDEFINED: camel case entity name,
        // then optionally the predefined type in capitals.
        private static readonly Regex IfcClassCode = new(@"^(Ifc(?:[A-Z0-9][a-z0-9]+)+)([A-Z][A-Z0-9_]*)?$");

        // Not narrowed to the schema of the dictionary version: Pset_WallCommon from the IFC 4.3
        // dictionary is just as much a requirement for an IFC4 model, which most models still are.
        private static readonly string[] AllIfcVersions = { IdsIfcVersion.Ifc2X3, IdsIfcVersion.Ifc4, IdsIfcVersion.Ifc4X3Add2 };

        /// <summary>Whether the uri belongs to the IFC dictionary that buildingSMART publishes in bSDD.</summary>
        public static bool IsIfcDictionary(string dictionaryOrClassUri) =>
            dictionaryOrClassUri != null &&
            dictionaryOrClassUri.IndexOf("/uri/buildingsmart/ifc/", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Entity and predefined type named by an IFC dictionary class code, e.g. IfcWallSOLIDWALL
        /// gives (IFCWALL, SOLIDWALL). Null when the code is not an IFC class name.
        /// </summary>
        public static (string Entity, string PredefinedType)? ParseIfcClassCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;
            var match = IfcClassCode.Match(code.Trim());
            if (!match.Success)
                return null;
            return (match.Groups[1].Value.ToUpperInvariant(),
                    match.Groups[2].Success ? match.Groups[2].Value : null);
        }

        /// <summary>
        /// One specification: the class as applicability, each property as a required facet.
        /// </summary>
        /// <param name="dictionaryName">
        /// Classification system name to look for on the model's objects, for classes outside
        /// the IFC dictionary. Defaults to the dictionary part of the class uri.
        /// </param>
        /// <param name="checkAllowedValues">
        /// Also require the value to be one of the property's allowed values in bSDD, when it has any.
        /// Off by default: models often store a value in a different case or language.
        /// </param>
        public static IdsSpecification BuildSpecification(BsddClass bsddClass, IEnumerable<BsddClassProperty> properties,
            string dictionaryName = null, bool checkAllowedValues = false, IEnumerable<string> ifcVersions = null)
        {
            if (bsddClass == null)
                throw new ArgumentNullException(nameof(bsddClass));

            var specification = new IdsSpecification
            {
                Name = string.IsNullOrWhiteSpace(bsddClass.Name) ? bsddClass.Code : bsddClass.Name,
                Identifier = bsddClass.Code,
                Description = bsddClass.Definition,
                IfcVersions = (ifcVersions ?? AllIfcVersions).ToList(),
            };

            var ifcClass = IsIfcDictionary(bsddClass.Uri ?? bsddClass.DictionaryUri) ? ParseIfcClassCode(bsddClass.Code) : null;
            if (ifcClass != null)
            {
                specification.Applicability.Entity = new IdsEntityFacet
                {
                    Name = new IdsValue(ifcClass.Value.Entity),
                    PredefinedType = ifcClass.Value.PredefinedType == null ? null : new IdsValue(ifcClass.Value.PredefinedType),
                };
            }
            else
            {
                specification.Applicability.Classifications.Add(new IdsClassificationFacet
                {
                    System = new IdsValue(dictionaryName ?? DictionaryNameFromUri(bsddClass.DictionaryUri ?? bsddClass.Uri)),
                    Value = new IdsValue(bsddClass.Code ?? bsddClass.ReferenceCode),
                    Uri = bsddClass.Uri,
                });
            }

            foreach (var property in Distinct(properties ?? Enumerable.Empty<BsddClassProperty>()))
            {
                var name = PropertyName(property);
                IdsValue value = checkAllowedValues ? AllowedValues(property) : null;

                // bSDD lists plain IFC attributes (Name, Description, ...) under a pseudo property set.
                if (string.Equals(property.PropertySet, BsddHierarchy.AttributesPropertySet, StringComparison.OrdinalIgnoreCase))
                {
                    specification.Requirements.Attributes.Add(new IdsAttributeFacet { Name = new IdsValue(name), Value = value });
                    continue;
                }

                specification.Requirements.Properties.Add(new IdsPropertyFacet
                {
                    PropertySet = new IdsValue(property.PropertySet),
                    BaseName = new IdsValue(name),
                    Value = value,
                    Uri = property.PropertyUri ?? property.Uri,
                });
            }

            return specification;
        }

        /// <summary>Wraps specifications in a document ready to validate or save.</summary>
        public static IdsDocument BuildDocument(IEnumerable<IdsSpecification> specifications, string title = null)
        {
            return new IdsDocument
            {
                Info = new IdsInfo
                {
                    Title = title ?? "Requirements from the buildingSMART Data Dictionary",
                    Date = DateTime.Today,
                    Description = "Generated by IFC Toolbox from bSDD classes and properties.",
                },
                Specifications = specifications.ToList(),
            };
        }

        /// <summary>
        /// A property can be listed more than once for a class; keep one per property set and name.
        /// Properties without a property set cannot be located in a model and are left out.
        /// </summary>
        private static IEnumerable<BsddClassProperty> Distinct(IEnumerable<BsddClassProperty> properties) =>
            properties
                .Where(p => !string.IsNullOrWhiteSpace(p.PropertySet) && !string.IsNullOrWhiteSpace(PropertyName(p)))
                .GroupBy(p => (p.PropertySet, PropertyName(p)))
                .Select(g => g.First());

        /// <summary>The code is the name stored in IFC files ("FireRating"); Name is the label ("Fire Rating").</summary>
        public static string PropertyName(BsddClassProperty property) =>
            string.IsNullOrWhiteSpace(property.PropertyCode) ? property.Name : property.PropertyCode;

        private static IdsValue AllowedValues(BsddClassProperty property)
        {
            var values = property.AllowedValues?
                .Select(v => v.Code ?? v.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (values == null || values.Count == 0)
                return null;
            return new IdsValue { Restriction = new IdsRestriction { Base = "xs:string", Enumeration = values } };
        }

        /// <summary>
        /// https://identifier.buildingsmart.org/uri/nlsfb/nlsfb2005/2.2/class/21.21 gives "nlsfb2005".
        /// Only a fallback: the dictionary's own name is what models normally carry.
        /// </summary>
        private static string DictionaryNameFromUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return null;
            var parts = uri.Split('/');
            int index = Array.FindIndex(parts, p => p.Equals("uri", StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 2 < parts.Length ? parts[index + 2] : uri;
        }
    }
}
