using IfcToolbox.Core.Bsdd.Models;
using IfcToolbox.Core.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IfcToolbox.Core.Bsdd
{
    /// <summary>
    /// Turns bSDD classes into the <see cref="HierarchyNode"/> trees the validator consumes,
    /// so a requirement set can be pulled from bSDD instead of being written out by hand.
    /// <para>
    /// Shape produced: class -> property set -> property, which is exactly what
    /// PropertyExistence flattens into "Pset.Property" columns.
    /// </para>
    /// </summary>
    public static class BsddHierarchy
    {
        /// <summary>
        /// bSDD reports plain IFC attributes (Name, Description, Tag, ...) under this
        /// pseudo property set. They are attributes on the entity, not entries in an
        /// IfcPropertySet, so the validator can never find them and they are left out by default.
        /// </summary>
        public const string AttributesPropertySet = "Attributes";

        /// <summary>
        /// Builds one class node from a class and its properties.
        /// </summary>
        /// <param name="includeAttributes">
        /// Include the "Attributes" pseudo property set. Off by default, see
        /// <see cref="AttributesPropertySet"/>.
        /// </param>
        /// <param name="nameSelector">
        /// How to name the class node. The validator matches this against the IFC entity name,
        /// so the default uses the class code (IfcWall), not the human readable name (Wall).
        /// </param>
        public static HierarchyNode BuildClassNode(BsddClass bsddClass, IEnumerable<BsddClassProperty> properties,
            bool includeAttributes = false, Func<BsddClass, string> nameSelector = null)
        {
            if (bsddClass == null)
                throw new ArgumentNullException(nameof(bsddClass));

            nameSelector ??= c => c.Code ?? c.ReferenceCode ?? c.Name;
            var classNode = new HierarchyNode(nameSelector(bsddClass), bsddClass.Uri, bsddClass.Definition,
                isComposition: true);

            AddPropertySets(classNode, properties, includeAttributes);
            return classNode;
        }

        /// <summary>
        /// Fetches a class and its properties from bSDD and returns it as a node tree.
        /// </summary>
        public static async Task<HierarchyNode> BuildClassNodeAsync(BsddClient client, string classUri,
            bool includeAttributes = false, CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var bsddClass = await client.GetClassAsync(classUri, cancellationToken).ConfigureAwait(false);
            var properties = await client.GetAllClassPropertiesAsync(classUri, cancellationToken).ConfigureAwait(false);
            return BuildClassNode(bsddClass, properties, includeAttributes);
        }

        /// <summary>
        /// Builds a node per class uri, ready to hand to the validator.
        /// Classes are requested one after another rather than in parallel to stay friendly
        /// to the bSDD rate limits.
        /// </summary>
        public static async Task<IList<HierarchyNode>> BuildClassNodesAsync(BsddClient client,
            IEnumerable<string> classUris, bool includeAttributes = false,
            CancellationToken cancellationToken = default)
        {
            var nodes = new List<HierarchyNode>();
            foreach (var classUri in classUris)
            {
                cancellationToken.ThrowIfCancellationRequested();
                nodes.Add(await BuildClassNodeAsync(client, classUri, includeAttributes, cancellationToken).ConfigureAwait(false));
            }
            return nodes;
        }

        private static void AddPropertySets(HierarchyNode classNode, IEnumerable<BsddClassProperty> properties,
            bool includeAttributes)
        {
            if (properties == null)
                return;

            var grouped = properties
                .Where(p => !string.IsNullOrWhiteSpace(p.PropertySet))
                .Where(p => includeAttributes || !string.Equals(p.PropertySet, AttributesPropertySet, StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => p.PropertySet, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var psetNode = new HierarchyNode(group.Key, null, null, isComposition: true);
                psetNode.AddParentId(classNode.Id);

                // A property can be listed more than once for a class; the validator only
                // cares whether the name is present, so keep the first occurrence.
                foreach (var property in group.GroupBy(PropertyName, StringComparer.Ordinal).Select(g => g.First())
                                              .OrderBy(PropertyName, StringComparer.Ordinal))
                {
                    var propertyNode = new HierarchyNode(PropertyName(property), property.Uri, property.DataType)
                    {
                        Value = property
                    };
                    propertyNode.AddParentId(property.PropertyUri);
                    psetNode.Children.Add(propertyNode);
                }

                classNode.Children.Add(psetNode);
            }
        }

        /// <summary>
        /// The name to match against a model.
        /// <para>
        /// bSDD carries two names per property: <c>Name</c> is the human readable label
        /// ("Fire Rating") while <c>PropertyCode</c> is the identifier that actually appears
        /// in an IFC file ("FireRating"). The validator compares against IfcProperty.Name,
        /// so the code is the one to use - taking Name here would make every check report
        /// "not found".
        /// </para>
        /// </summary>
        private static string PropertyName(BsddClassProperty property)
        {
            return string.IsNullOrWhiteSpace(property.PropertyCode) ? property.Name : property.PropertyCode;
        }
    }
}
