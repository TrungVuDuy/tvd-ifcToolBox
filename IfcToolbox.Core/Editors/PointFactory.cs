using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;

namespace IfcToolbox.Core.Editors
{
    /// <summary>
    /// Creates geometry resource entities in whatever schema the model uses.
    /// The concrete class comes from the model's own metadata (see
    /// <see cref="SchemaEntityFactory"/>), so IFC2X3, IFC4 and IFC4X3 share one code path.
    /// The previous version assumed "not IFC2X3 means IFC4" and failed on every IFC4X3 model.
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
            var point = SchemaEntityFactory.New<IIfcCartesianPoint>(model, "IfcCartesianPoint");
            point.Coordinates.AddRange(locationXYZ.Take(3).Select(v => new IfcLengthMeasure(v)));
            return point;
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
            var direction = SchemaEntityFactory.New<IIfcDirection>(model, "IfcDirection");
            direction.DirectionRatios.AddRange(rotations.Take(3).Select(v => new IfcReal(v)));
            return direction;
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
