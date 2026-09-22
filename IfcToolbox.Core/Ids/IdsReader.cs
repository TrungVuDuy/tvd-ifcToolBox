using IfcToolbox.Core.Ids.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace IfcToolbox.Core.Ids
{
    /// <summary>
    /// Reads an IDS 1.0 document. Follows Schema/ids.xsd version 1.0.0 from
    /// github.com/buildingSMART/IDS.
    /// </summary>
    public static class IdsReader
    {
        private static readonly XNamespace Ids = IdsDocument.Namespace;
        private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";

        public static IdsDocument Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("IDS file not found.", filePath);
            return Parse(XDocument.Load(filePath, LoadOptions.SetLineInfo));
        }

        public static IdsDocument Read(Stream stream) => Parse(XDocument.Load(stream, LoadOptions.SetLineInfo));

        public static IdsDocument ReadXml(string xml) => Parse(XDocument.Parse(xml, LoadOptions.SetLineInfo));

        public static IdsDocument Parse(XDocument xml)
        {
            var root = xml.Root;
            if (root == null || root.Name != Ids + "ids")
                throw new IdsFormatException(
                    $"Expected a root element <ids> in namespace {IdsDocument.Namespace}, found <{root?.Name.LocalName}>.");

            var document = new IdsDocument
            {
                Info = ReadInfo(root.Element(Ids + "info")),
            };

            var specifications = root.Element(Ids + "specifications");
            if (specifications != null)
                foreach (var element in specifications.Elements(Ids + "specification"))
                    document.Specifications.Add(ReadSpecification(element));

            return document;
        }

        private static IdsInfo ReadInfo(XElement element)
        {
            var info = new IdsInfo();
            if (element == null)
                return info;

            info.Title = (string)element.Element(Ids + "title");
            info.Copyright = (string)element.Element(Ids + "copyright");
            info.Version = (string)element.Element(Ids + "version");
            info.Description = (string)element.Element(Ids + "description");
            info.Author = (string)element.Element(Ids + "author");
            info.Purpose = (string)element.Element(Ids + "purpose");
            info.Milestone = (string)element.Element(Ids + "milestone");

            var date = (string)element.Element(Ids + "date");
            if (!string.IsNullOrWhiteSpace(date)
                && DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                info.Date = parsed;

            return info;
        }

        private static IdsSpecification ReadSpecification(XElement element)
        {
            var specification = new IdsSpecification
            {
                Name = (string)element.Attribute("name"),
                Identifier = (string)element.Attribute("identifier"),
                Description = (string)element.Attribute("description"),
                Instructions = (string)element.Attribute("instructions"),
            };

            // ifcVersion is an xs:list, i.e. whitespace separated.
            var ifcVersion = (string)element.Attribute("ifcVersion");
            if (!string.IsNullOrWhiteSpace(ifcVersion))
                foreach (var version in ifcVersion.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    specification.IfcVersions.Add(version);

            var applicability = element.Element(Ids + "applicability");
            if (applicability != null)
                specification.Applicability = ReadApplicability(applicability);

            var requirements = element.Element(Ids + "requirements");
            if (requirements != null)
                specification.Requirements = ReadRequirements(requirements);

            return specification;
        }

        private static IdsApplicability ReadApplicability(XElement element)
        {
            var applicability = new IdsApplicability
            {
                MinOccurs = ReadOptionalInt(element.Attribute("minOccurs")),
                MaxOccurs = ReadMaxOccurs(element.Attribute("maxOccurs")),
            };

            var entity = element.Element(Ids + "entity");
            if (entity != null)
                applicability.Entity = ReadEntityFacet(entity);

            foreach (var child in element.Elements(Ids + "partOf"))
                applicability.PartOf.Add(ReadPartOfFacet(child));
            foreach (var child in element.Elements(Ids + "classification"))
                applicability.Classifications.Add(ReadClassificationFacet(child));
            foreach (var child in element.Elements(Ids + "attribute"))
                applicability.Attributes.Add(ReadAttributeFacet(child));
            foreach (var child in element.Elements(Ids + "property"))
                applicability.Properties.Add(ReadPropertyFacet(child));
            foreach (var child in element.Elements(Ids + "material"))
                applicability.Materials.Add(ReadMaterialFacet(child));

            return applicability;
        }

        private static IdsRequirements ReadRequirements(XElement element)
        {
            var requirements = new IdsRequirements
            {
                Description = (string)element.Attribute("description"),
            };

            var entity = element.Element(Ids + "entity");
            if (entity != null)
                requirements.Entity = ReadEntityFacet(entity);

            foreach (var child in element.Elements(Ids + "partOf"))
                requirements.PartOf.Add(ReadPartOfFacet(child));
            foreach (var child in element.Elements(Ids + "classification"))
                requirements.Classifications.Add(ReadClassificationFacet(child));
            foreach (var child in element.Elements(Ids + "attribute"))
                requirements.Attributes.Add(ReadAttributeFacet(child));
            foreach (var child in element.Elements(Ids + "property"))
                requirements.Properties.Add(ReadPropertyFacet(child));
            foreach (var child in element.Elements(Ids + "material"))
                requirements.Materials.Add(ReadMaterialFacet(child));

            return requirements;
        }

        #region Facets

        private static IdsEntityFacet ReadEntityFacet(XElement element)
        {
            // The schema gives the entity facet no cardinality in requirements; it is always
            // required. Reading one anyway would be wrong, so it is not read here.
            return new IdsEntityFacet
            {
                Name = ReadValue(element.Element(Ids + "name")),
                PredefinedType = ReadValue(element.Element(Ids + "predefinedType")),
                Instructions = (string)element.Attribute("instructions"),
                Cardinality = IdsCardinality.Required,
            };
        }

        private static IdsAttributeFacet ReadAttributeFacet(XElement element)
        {
            var facet = new IdsAttributeFacet
            {
                Name = ReadValue(element.Element(Ids + "name")),
                Value = ReadValue(element.Element(Ids + "value")),
            };
            ReadCommon(element, facet);
            return facet;
        }

        private static IdsPropertyFacet ReadPropertyFacet(XElement element)
        {
            var facet = new IdsPropertyFacet
            {
                PropertySet = ReadValue(element.Element(Ids + "propertySet")),
                BaseName = ReadValue(element.Element(Ids + "baseName")),
                Value = ReadValue(element.Element(Ids + "value")),
                DataType = (string)element.Attribute("dataType"),
                Uri = (string)element.Attribute("uri"),
            };
            ReadCommon(element, facet);
            return facet;
        }

        private static IdsClassificationFacet ReadClassificationFacet(XElement element)
        {
            var facet = new IdsClassificationFacet
            {
                System = ReadValue(element.Element(Ids + "system")),
                Value = ReadValue(element.Element(Ids + "value")),
                Uri = (string)element.Attribute("uri"),
            };
            ReadCommon(element, facet);
            return facet;
        }

        private static IdsMaterialFacet ReadMaterialFacet(XElement element)
        {
            var facet = new IdsMaterialFacet
            {
                Value = ReadValue(element.Element(Ids + "value")),
                Uri = (string)element.Attribute("uri"),
            };
            ReadCommon(element, facet);
            return facet;
        }

        private static IdsPartOfFacet ReadPartOfFacet(XElement element)
        {
            var entity = element.Element(Ids + "entity");
            var facet = new IdsPartOfFacet
            {
                Relation = (string)element.Attribute("relation"),
                Entity = entity == null ? null : ReadEntityFacet(entity),
            };
            ReadCommon(element, facet);
            return facet;
        }

        private static void ReadCommon(XElement element, IdsFacet facet)
        {
            facet.Instructions = (string)element.Attribute("instructions");
            facet.Cardinality = ReadCardinality(element.Attribute("cardinality"));
        }

        private static IdsCardinality ReadCardinality(XAttribute attribute)
        {
            // ids.xsd defaults cardinality to "required" when the attribute is absent.
            var text = (string)attribute;
            if (string.IsNullOrWhiteSpace(text))
                return IdsCardinality.Required;

            switch (text.Trim().ToLowerInvariant())
            {
                case "prohibited": return IdsCardinality.Prohibited;
                case "optional": return IdsCardinality.Optional;
                case "required": return IdsCardinality.Required;
                default:
                    throw new IdsFormatException($"Unknown cardinality '{text}'.");
            }
        }

        #endregion

        #region Values

        private static IdsValue ReadValue(XElement element)
        {
            if (element == null)
                return null;

            var simple = element.Element(Ids + "simpleValue");
            if (simple != null)
                return new IdsValue(simple.Value);

            var restriction = element.Element(Xs + "restriction");
            if (restriction != null)
                return new IdsValue { Restriction = ReadRestriction(restriction) };

            // An element with neither child is not valid IDS, but treating it as "no
            // constraint" is friendlier than throwing on an otherwise usable document.
            return null;
        }

        private static IdsRestriction ReadRestriction(XElement element)
        {
            var restriction = new IdsRestriction
            {
                Base = (string)element.Attribute("base"),
            };

            foreach (var child in element.Elements())
            {
                var value = (string)child.Attribute("value");
                switch (child.Name.LocalName)
                {
                    case "enumeration":
                        restriction.Enumeration.Add(value);
                        break;
                    case "pattern":
                        restriction.Patterns.Add(value);
                        break;
                    case "minInclusive":
                        restriction.MinInclusive = value;
                        break;
                    case "maxInclusive":
                        restriction.MaxInclusive = value;
                        break;
                    case "minExclusive":
                        restriction.MinExclusive = value;
                        break;
                    case "maxExclusive":
                        restriction.MaxExclusive = value;
                        break;
                    case "length":
                        restriction.Length = ReadOptionalInt(child.Attribute("value"));
                        break;
                    case "minLength":
                        restriction.MinLength = ReadOptionalInt(child.Attribute("value"));
                        break;
                    case "maxLength":
                        restriction.MaxLength = ReadOptionalInt(child.Attribute("value"));
                        break;
                    case "annotation":
                        break;
                    default:
                        // Unknown facets are ignored rather than fatal: a stricter document
                        // should not stop the rest of it from being checked.
                        break;
                }
            }

            return restriction;
        }

        private static int? ReadOptionalInt(XAttribute attribute)
        {
            var text = (string)attribute;
            if (string.IsNullOrWhiteSpace(text))
                return null;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (int?)null;
        }

        private static int? ReadMaxOccurs(XAttribute attribute)
        {
            var text = (string)attribute;
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (string.Equals(text.Trim(), "unbounded", StringComparison.OrdinalIgnoreCase))
                return null;
            return ReadOptionalInt(attribute);
        }

        #endregion
    }

    /// <summary>Raised when a document cannot be read as IDS.</summary>
    public class IdsFormatException : Exception
    {
        public IdsFormatException(string message) : base(message) { }
        public IdsFormatException(string message, Exception inner) : base(message, inner) { }
    }
}
