using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Editors
{
    /// <summary>
    /// Creates geometry resource entities.
    /// Reading is schema agnostic (Xbim.Ifc4.Interfaces covers every schema in Xbim 6),
    /// but creation needs a concrete type, so the public entry points dispatch on
    /// <see cref="IModel.SchemaVersion"/>.
    /// </summary>
    public class PointFactory
    {
        public static IIfcCartesianPoint CreatePoint(IModel model, IIfcCartesianPoint point, bool withPrecision, int precision)
        {
            var values = point.Coordinates.Select(o => (double)o.Value).ToList();
            if (withPrecision)
                values = PointPrecision.Optimize(precision, values);
            return CreatePoint(model, values);
        }

        /// <summary>
        /// Creates an IfcCartesianPoint of the schema the model is using.
        /// </summary>
        public static IIfcCartesianPoint CreatePoint(IModel model, IList<double> locationXYZ)
        {
            if (locationXYZ == null)
                return null;
            if (model.SchemaVersion == XbimSchemaVersion.Ifc2X3)
                return CreatePoint_Ifc2x3(model, locationXYZ);
            return CreatePoint_Ifc4(model, locationXYZ);
        }

        public static Xbim.Ifc4.GeometryResource.IfcCartesianPoint CreatePoint_Ifc4(IModel model, IList<double> locationXYZ)
        {
            if (locationXYZ == null)
                return null;
            Xbim.Ifc4.GeometryResource.IfcCartesianPoint newPoint = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>();
            if (locationXYZ.Count < 3)
                newPoint.SetXY(locationXYZ[0], locationXYZ[1]);
            else
                newPoint.SetXYZ(locationXYZ[0], locationXYZ[1], locationXYZ[2]);
            return newPoint;
        }
        public static Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint CreatePoint_Ifc2x3(IModel model, IList<double> locationXYZ)
        {
            if (locationXYZ == null)
                return null;
            Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint newPoint = model.Instances.New<Xbim.Ifc2x3.GeometryResource.IfcCartesianPoint>();
            if (locationXYZ.Count < 3)
                newPoint.SetXY(locationXYZ[0], locationXYZ[1]);
            else
                newPoint.SetXYZ(locationXYZ[0], locationXYZ[1], locationXYZ[2]);
            return newPoint;
        }

        public static IIfcDirection CreateDirection(IModel model, IIfcDirection direction, bool withPrecision, int precision)
        {
            var values = direction.DirectionRatios.Select(o => (double)o.Value).ToList();
            if (withPrecision)
                values = PointPrecision.Optimize(precision, values);
            return CreateDirection(model, values);
        }

        /// <summary>
        /// Creates an IfcDirection of the schema the model is using.
        /// </summary>
        public static IIfcDirection CreateDirection(IModel model, IList<double> rotations)
        {
            if (rotations == null)
                return null;
            if (model.SchemaVersion == XbimSchemaVersion.Ifc2X3)
                return CreateDirection_Ifc2x3(model, rotations);
            return CreateDirection_Ifc4(model, rotations);
        }

        public static Xbim.Ifc4.GeometryResource.IfcDirection CreateDirection_Ifc4(IModel model, IList<double> rotations)
        {
            if (rotations == null)
                return null;
            Xbim.Ifc4.GeometryResource.IfcDirection direction = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcDirection>();
            if (rotations.Count < 3)
                direction.SetXY(rotations[0], rotations[1]);
            else
                direction.SetXYZ(rotations[0], rotations[1], rotations[2]);
            return direction;
        }
        public static Xbim.Ifc2x3.GeometryResource.IfcDirection CreateDirection_Ifc2x3(IModel model, IList<double> rotations)
        {
            if (rotations == null)
                return null;
            Xbim.Ifc2x3.GeometryResource.IfcDirection direction = model.Instances.New<Xbim.Ifc2x3.GeometryResource.IfcDirection>();
            if (rotations.Count < 3)
                direction.SetXY(rotations[0], rotations[1]);
            else
                direction.SetXYZ(rotations[0], rotations[1], rotations[2]);
            return direction;
        }
    }
}
