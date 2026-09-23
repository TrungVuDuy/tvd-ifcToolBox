using System;
using Xbim.Common;

namespace IfcToolbox.Core.Editors
{
    /// <summary>
    /// Creates an entity of the model's own schema by its EXPRESS name.
    /// <para>
    /// xbim refuses to put an entity of one schema into a model of another ("This factory only
    /// creates types from its assembly"), so creating a point with the IFC4 class in an
    /// IFC4X3 model fails. Resolving the class from the model's metadata picks the right one
    /// for IFC2X3, IFC4 and IFC4X3 alike, and because every schema's entities implement the
    /// Xbim.Ifc4 interfaces, the caller can fill it in through those without branching.
    /// </para>
    /// </summary>
    public static class SchemaEntityFactory
    {
        public static T New<T>(IModel model, string expressName) where T : class
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var expressType = model.Metadata.ExpressType(expressName.ToUpperInvariant());
            if (expressType == null)
                throw new NotSupportedException(
                    $"{expressName} does not exist in the {model.SchemaVersion} schema.");

            var entity = model.Instances.New(expressType.Type);
            return entity as T ?? throw new InvalidOperationException(
                $"{expressType.Type.FullName} does not implement {typeof(T).Name}.");
        }

        /// <summary>True when the model's schema has a class of that name.</summary>
        public static bool Exists(IModel model, string expressName) =>
            model?.Metadata.ExpressType(expressName.ToUpperInvariant()) != null;
    }
}
