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
    /// Writes an IDS 1.0 document. Element order follows Schema/ids.xsd, which uses
    /// xs:sequence throughout, so the order is part of being valid rather than a style choice.
    /// </summary>
    public static class IdsWriter
    {
        private static readonly XNamespace Ids = IdsDocument.Namespace;
        private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static void Save(IdsDocument document, string filePath)
        {
            ToXml(document).Save(filePath);
        }

        public static string ToXmlString(IdsDocument document) => ToXml(document).ToString();

        public static XDocument ToXml(IdsDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var root = new XElement(Ids + "ids",
                new XAttribute(XNamespace.Xmlns + "xs", Xs),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(Xsi + "schemaLocation",
                    $"{IdsDocument.Namespace} http://standards.buildingsmart.org/IDS/1.0/ids.xsd"),
                WriteInfo(document.Info),
                new XElement(Ids + "specifications",
                    document.Specifications.Select(WriteSpecification)));

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        }

        private static XElement WriteInfo(IdsInfo info)
        {
            info = info ?? new IdsInfo();
            var element = new XElement(Ids + "info",
                new XElement(Ids + "title", info.Title ?? string.Empty));

            AddIfPresent(element, "copyright", info.Copyright);
            AddIfPresent(element, "version", info.Version);
            AddIfPresent(element, "description", info.Description);
            AddIfPresent(element, "author", info.Author);
            if (info.Date.HasValue)
                element.Add(new XElement(Ids + "date", info.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            AddIfPresent(element, "purpose", info.Purpose);
            AddIfPresent(element, "milestone", info.Milestone);
            return element;
        }

        private static void AddIfPresent(XElement parent, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                parent.Add(new XElement(Ids + name, value));
        }

        private static XElement WriteSpecification(IdsSpecification specification)
        {
            var element = new XElement(Ids + "specification",
                new XAttribute("name", specification.Name ?? string.Empty),
                new XAttribute("ifcVersion", string.Join(" ", specification.IfcVersions)));

            if (!string.IsNullOrEmpty(specification.Identifier))
                element.Add(new XAttribute("identifier", specification.Identifier));
            if (!string.IsNullOrEmpty(specification.Description))
                element.Add(new XAttribute("description", specification.Description));
            if (!string.IsNullOrEmpty(specification.Instructions))
                element.Add(new XAttribute("instructions", specification.Instructions));

            element.Add(WriteApplicability(specification.Applicability));
            if (specification.Requirements != null && !specification.Requirements.IsEmpty)
                element.Add(WriteRequirements(specification.Requirements));

            return element;
        }

        private static XElement WriteApplicability(IdsApplicability applicability)
        {
            applicability = applicability ?? new IdsApplicability();
            var element = new XElement(Ids + "applicability");

            if (applicability.MinOccurs.HasValue)
                element.Add(new XAttribute("minOccurs", applicability.MinOccurs.Value));
            element.Add(new XAttribute("maxOccurs",
                applicability.MaxOccurs.HasValue ? applicability.MaxOccurs.Value.ToString(CultureInfo.InvariantCulture) : "unbounded"));

            AddFacets(element, applicability.Entity, applicability.PartOf, applicability.Classifications,
                applicability.Attributes, applicability.Properties, applicability.Materials, withCardinality: false);
            return element;
        }

        private static XElement WriteRequirements(IdsRequirements requirements)
        {
            var element = new XElement(Ids + "requirements");
            if (!string.IsNullOrEmpty(requirements.Description))
                element.Add(new XAttribute("description", requirements.Description));

            AddFacets(element, requirements.Entity, requirements.PartOf, requirements.Classifications,
                requirements.Attributes, requirements.Properties, requirements.Materials, withCardinality: true);
            return element;
        }

        private static void AddFacets(XElement parent, IdsEntityFacet entity,
            IEnumerable<IdsPartOfFacet> partOf, IEnumerable<IdsClassificationFacet> classifications,
            IEnumerable<IdsAttributeFacet> attributes, IEnumerable<IdsPropertyFacet> properties,
            IEnumerable<IdsMaterialFacet> materials, bool withCardinality)
        {
            // The schema fixes this order.
            if (entity != null)
                parent.Add(WriteEntity(entity));
            foreach (var facet in partOf)
                parent.Add(WritePartOf(facet, withCardinality));
            foreach (var facet in classifications)
                parent.Add(WriteClassification(facet, withCardinality));
            foreach (var facet in attributes)
                parent.Add(WriteAttribute(facet, withCardinality));
            foreach (var facet in properties)
                parent.Add(WriteProperty(facet, withCardinality));
            foreach (var facet in materials)
                parent.Add(WriteMaterial(facet, withCardinality));
        }

        private static XElement WriteEntity(IdsEntityFacet facet)
        {
            // The entity facet never carries a cardinality; it is always required.
            var element = new XElement(Ids + "entity", WriteValue("name", facet.Name));
            if (facet.PredefinedType != null)
                element.Add(WriteValue("predefinedType", facet.PredefinedType));
            AddInstructions(element, facet);
            return element;
        }

        private static XElement WriteAttribute(IdsAttributeFacet facet, bool withCardinality)
        {
            var element = new XElement(Ids + "attribute", WriteValue("name", facet.Name));
            if (facet.Value != null)
                element.Add(WriteValue("value", facet.Value));
            AddCommon(element, facet, withCardinality);
            return element;
        }

        private static XElement WriteProperty(IdsPropertyFacet facet, bool withCardinality)
        {
            var element = new XElement(Ids + "property",
                WriteValue("propertySet", facet.PropertySet),
                WriteValue("baseName", facet.BaseName));
            if (facet.Value != null)
                element.Add(WriteValue("value", facet.Value));
            if (!string.IsNullOrEmpty(facet.DataType))
                element.Add(new XAttribute("dataType", facet.DataType));
            if (!string.IsNullOrEmpty(facet.Uri))
                element.Add(new XAttribute("uri", facet.Uri));
            AddCommon(element, facet, withCardinality);
            return element;
        }

        private static XElement WriteClassification(IdsClassificationFacet facet, bool withCardinality)
        {
            var element = new XElement(Ids + "classification");
            if (facet.Value != null)
                element.Add(WriteValue("value", facet.Value));
            if (facet.System != null)
                element.Add(WriteValue("system", facet.System));
            if (!string.IsNullOrEmpty(facet.Uri))
                element.Add(new XAttribute("uri", facet.Uri));
            AddCommon(element, facet, withCardinality);
            return element;
        }

        private static XElement WriteMaterial(IdsMaterialFacet facet, bool withCardinality)
        {
            var element = new XElement(Ids + "material");
            if (facet.Value != null)
                element.Add(WriteValue("value", facet.Value));
            if (!string.IsNullOrEmpty(facet.Uri))
                element.Add(new XAttribute("uri", facet.Uri));
            AddCommon(element, facet, withCardinality);
            return element;
        }

        private static XElement WritePartOf(IdsPartOfFacet facet, bool withCardinality)
        {
            var element = new XElement(Ids + "partOf");
            if (facet.Entity != null)
                element.Add(WriteEntity(facet.Entity));
            if (!string.IsNullOrEmpty(facet.Relation))
                element.Add(new XAttribute("relation", facet.Relation));
            AddCommon(element, facet, withCardinality);
            return element;
        }

        private static void AddCommon(XElement element, IIdsFacet facet, bool withCardinality)
        {
            // "required" is the schema default, so writing it would only add noise.
            if (withCardinality && facet.Cardinality != IdsCardinality.Required)
                element.Add(new XAttribute("cardinality", facet.Cardinality.ToString().ToLowerInvariant()));
            AddInstructions(element, facet);
        }

        private static void AddInstructions(XElement element, IIdsFacet facet)
        {
            if (!string.IsNullOrEmpty(facet.Instructions))
                element.Add(new XAttribute("instructions", facet.Instructions));
        }

        private static XElement WriteValue(string name, IdsValue value)
        {
            if (value == null)
                return null;
            if (value.Restriction != null)
                return new XElement(Ids + name, WriteRestriction(value.Restriction));
            return new XElement(Ids + name, new XElement(Ids + "simpleValue", value.SimpleValue ?? string.Empty));
        }

        private static XElement WriteRestriction(IdsRestriction restriction)
        {
            var element = new XElement(Xs + "restriction",
                new XAttribute("base", restriction.Base ?? "xs:string"));

            foreach (var value in restriction.Enumeration)
                element.Add(new XElement(Xs + "enumeration", new XAttribute("value", value ?? string.Empty)));
            foreach (var pattern in restriction.Patterns)
                element.Add(new XElement(Xs + "pattern", new XAttribute("value", pattern ?? string.Empty)));

            AddFacet(element, "minInclusive", restriction.MinInclusive);
            AddFacet(element, "maxInclusive", restriction.MaxInclusive);
            AddFacet(element, "minExclusive", restriction.MinExclusive);
            AddFacet(element, "maxExclusive", restriction.MaxExclusive);
            AddFacet(element, "length", restriction.Length?.ToString(CultureInfo.InvariantCulture));
            AddFacet(element, "minLength", restriction.MinLength?.ToString(CultureInfo.InvariantCulture));
            AddFacet(element, "maxLength", restriction.MaxLength?.ToString(CultureInfo.InvariantCulture));

            return element;
        }

        private static void AddFacet(XElement parent, string name, string value)
        {
            if (value != null)
                parent.Add(new XElement(Xs + name, new XAttribute("value", value)));
        }
    }
}
