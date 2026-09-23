using IfcToolbox.Core.Editors;
using System;
using System.Linq;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;

namespace IfcToolbox.Core.Geo
{
    /// <summary>
    /// Reads, writes and applies the IfcMapConversion that georeferences a model
    /// ("level of georeferencing 50": a projected CRS plus an offset, rotation and scale).
    /// <para>
    /// The transformation follows the IFC 4.3 definition of IfcMapConversionScaled:
    /// <c>E = S·Fx·cosθ·x − S·Fy·sinθ·y + Eastings</c>,
    /// <c>N = S·Fx·sinθ·x + S·Fy·cosθ·y + Northings</c>,
    /// <c>H = S·Fz·z + OrthogonalHeight</c>,
    /// with θ = atan2(XAxisOrdinate, XAxisAbscissa). A plain IfcMapConversion is the same
    /// with every factor equal to 1. Note that Scale applies to z as well: it converts units.
    /// </para>
    /// <para>
    /// IFC2X3 has no IfcMapConversion (it used property sets instead), so writing is limited
    /// to IFC4 and IFC4X3, and IfcMapConversionScaled to IFC4X3.
    /// </para>
    /// </summary>
    public static class MapConversionService
    {
        #region Reading

        /// <summary>
        /// The map conversion whose source is the model's 3D context, or null when the model
        /// is not georeferenced this way.
        /// </summary>
        public static GeoMapConversion Read(IModel model)
        {
            var map = FindMapConversion(model);
            if (map == null)
                return null;

            var result = new GeoMapConversion
            {
                EntityLabel = map.EntityLabel,
                Parameters = new MapConversionParameters
                {
                    Eastings = map.Eastings,
                    Northings = map.Northings,
                    OrthogonalHeight = map.OrthogonalHeight,
                    XAxisAbscissa = map.XAxisAbscissa,
                    XAxisOrdinate = map.XAxisOrdinate,
                    Scale = map.Scale,
                },
            };

            if (map is Xbim.Ifc4x3.RepresentationResource.IfcMapConversionScaled scaled)
            {
                result.Parameters.FactorX = scaled.FactorX;
                result.Parameters.FactorY = scaled.FactorY;
                result.Parameters.FactorZ = scaled.FactorZ;
            }

            if (map.TargetCRS is IIfcProjectedCRS crs)
            {
                result.TargetCrs = new ProjectedCrs
                {
                    Name = Text(crs.Name),
                    Description = Text(crs.Description),
                    GeodeticDatum = Text(crs.GeodeticDatum),
                    VerticalDatum = Text(crs.VerticalDatum),
                    MapProjection = Text(crs.MapProjection),
                    MapZone = Text(crs.MapZone),
                };
            }

            return result;
        }

        private static IIfcMapConversion FindMapConversion(IModel model)
        {
            var context = ModelContext(model);
            var conversions = model.Instances.OfType<IIfcMapConversion>().ToList();
            return conversions.FirstOrDefault(m => context != null && m.SourceCRS?.EntityLabel == context.EntityLabel)
                   ?? conversions.FirstOrDefault();
        }

        /// <summary>The 3D model context, which is what a map conversion hangs off.</summary>
        private static IIfcGeometricRepresentationContext ModelContext(IModel model)
        {
            var project = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            return project?.RepresentationContexts
                .OfType<IIfcGeometricRepresentationContext>()
                .Where(c => !(c is IIfcGeometricRepresentationSubContext))
                .OrderByDescending(c => c.ContextType == "Model")
                .ThenByDescending(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
        }

        private static string Text(object value)
        {
            var text = value?.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        #endregion

        #region Transformation

        /// <summary>Local engineering coordinates (model units) to map coordinates (CRS units).</summary>
        public static (double Easting, double Northing, double Height) ToMap(IMapConversionParameters map, double x, double y, double z)
        {
            var (s, fx, fy, fz, cos, sin) = Coefficients(map);
            return (
                s * fx * cos * x - s * fy * sin * y + (map.Eastings ?? 0),
                s * fx * sin * x + s * fy * cos * y + (map.Northings ?? 0),
                s * fz * z + (map.OrthogonalHeight ?? 0));
        }

        /// <summary>The inverse of <see cref="ToMap"/>: map coordinates back to local ones.</summary>
        public static (double X, double Y, double Z) ToLocal(IMapConversionParameters map, double easting, double northing, double height)
        {
            var (s, fx, fy, fz, cos, sin) = Coefficients(map);
            double de = easting - (map.Eastings ?? 0);
            double dn = northing - (map.Northings ?? 0);
            // Undo the rotation, then the scaling.
            double rx = cos * de + sin * dn;
            double ry = -sin * de + cos * dn;
            return (rx / (s * fx), ry / (s * fy), (height - (map.OrthogonalHeight ?? 0)) / (s * fz));
        }

        /// <summary>Rotation of the local x axis on the map, anti-clockwise from east, in degrees.</summary>
        public static double RotationDegrees(IMapConversionParameters map)
        {
            var (_, _, _, _, cos, sin) = Coefficients(map);
            return Math.Atan2(sin, cos) * 180 / Math.PI;
        }

        private static (double Scale, double Fx, double Fy, double Fz, double Cos, double Sin) Coefficients(IMapConversionParameters map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            double abscissa = map.XAxisAbscissa ?? 1;
            double ordinate = map.XAxisOrdinate ?? 0;
            // Only the direction matters, so the vector need not be normalised.
            double theta = (abscissa == 0 && ordinate == 0) ? 0 : Math.Atan2(ordinate, abscissa);

            double scale = map.Scale ?? 1;
            if (scale == 0)
                throw new ArgumentException("A map conversion scale of 0 cannot be applied.", nameof(map));

            return (scale, map.FactorX ?? 1, map.FactorY ?? 1, map.FactorZ ?? 1, Math.Cos(theta), Math.Sin(theta));
        }

        #endregion

        #region Writing

        /// <summary>
        /// Georeferences the model: creates or replaces the IfcProjectedCRS and the
        /// IfcMapConversion on its 3D context. Must run inside a transaction.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// IFC2X3, which has no IfcMapConversion; or per-axis factors on IFC4, which has no
        /// IfcMapConversionScaled.
        /// </exception>
        public static IIfcMapConversion Write(IModel model, GeoMapConversion conversion)
        {
            if (conversion?.Parameters == null)
                throw new ArgumentNullException(nameof(conversion));
            if (string.IsNullOrWhiteSpace(conversion.TargetCrs?.Name))
                throw new ArgumentException("The projected CRS needs a name, normally its EPSG code, e.g. \"EPSG:32760\".", nameof(conversion));

            if (model.SchemaVersion == XbimSchemaVersion.Ifc2X3)
                throw new NotSupportedException(
                    "IFC2X3 has no IfcMapConversion; it georeferenced through property sets. Upgrade the model to IFC4 or IFC4X3 first.");

            var p = conversion.Parameters;
            bool scaled = p.HasFactors;
            if (scaled && !SchemaEntityFactory.Exists(model, "IfcMapConversionScaled"))
                throw new NotSupportedException(
                    $"Per-axis factors need IfcMapConversionScaled, which only exists from IFC4X3; the model is {model.SchemaVersion}.");

            var context = ModelContext(model)
                          ?? throw new InvalidOperationException("The model has no geometric representation context to georeference.");

            // Replace rather than edit: switching between the plain and the scaled class
            // cannot be done in place, and a stale CRS left behind would be ambiguous.
            foreach (var existing in model.Instances.OfType<IIfcMapConversion>()
                         .Where(m => m.SourceCRS?.EntityLabel == context.EntityLabel).ToList())
            {
                var oldCrs = existing.TargetCRS;
                model.Delete(existing);
                if (oldCrs != null && !model.Instances.OfType<IIfcCoordinateOperation>().Any(o => o.TargetCRS?.EntityLabel == oldCrs.EntityLabel))
                    model.Delete(oldCrs);
            }

            var crs = SchemaEntityFactory.New<IIfcProjectedCRS>(model, "IfcProjectedCRS");
            var target = conversion.TargetCrs;
            crs.Name = new IfcLabel(target.Name);
            if (target.Description != null) crs.Description = new IfcText(target.Description);
            if (target.GeodeticDatum != null) crs.GeodeticDatum = new IfcIdentifier(target.GeodeticDatum);
            if (target.VerticalDatum != null) crs.VerticalDatum = new IfcIdentifier(target.VerticalDatum);
            if (target.MapProjection != null) crs.MapProjection = new IfcIdentifier(target.MapProjection);
            if (target.MapZone != null) crs.MapZone = new IfcIdentifier(target.MapZone);

            var map = SchemaEntityFactory.New<IIfcMapConversion>(model,
                scaled ? "IfcMapConversionScaled" : "IfcMapConversion");
            map.SourceCRS = context;
            map.TargetCRS = crs;
            map.Eastings = new IfcLengthMeasure(p.Eastings ?? 0);
            map.Northings = new IfcLengthMeasure(p.Northings ?? 0);
            map.OrthogonalHeight = new IfcLengthMeasure(p.OrthogonalHeight ?? 0);
            if (p.XAxisAbscissa.HasValue) map.XAxisAbscissa = new IfcReal(p.XAxisAbscissa.Value);
            if (p.XAxisOrdinate.HasValue) map.XAxisOrdinate = new IfcReal(p.XAxisOrdinate.Value);
            if (p.Scale.HasValue) map.Scale = new IfcReal(p.Scale.Value);

            if (scaled)
            {
                var mapScaled = (Xbim.Ifc4x3.RepresentationResource.IfcMapConversionScaled)map;
                mapScaled.FactorX = p.FactorX ?? 1;
                mapScaled.FactorY = p.FactorY ?? 1;
                mapScaled.FactorZ = p.FactorZ ?? 1;
            }

            return map;
        }

        #endregion
    }
}
