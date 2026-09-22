using IfcToolbox.Core.Geo;
using IfcToolbox.Core.Utilities;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Editors
{
    /// <summary>
    /// Relocates the world coordinate system of the model context and the placement of each site.
    /// Since Xbim 6 exposes one interface layer for all schemas, a single implementation covers
    /// IFC2x3, IFC4 and IFC4x3 - the previous _Ifc4 / _Ifc2x3 pair has been merged.
    /// </summary>
    public class PointAlignment
    {
        public static void AlignCoordinates(IModel model, bool alignWorldCoordinates, bool alignProjectCoordinates,
            IGeoPlacement worldPlacement, IEnumerable<IGeoPlacement> projectPlacements, bool logDetail)
        {
            var ifcProject = model.Instances.OfType<IIfcProject>().FirstOrDefault();
            if (ifcProject == null)
                return;

            if (alignWorldCoordinates)
            {
                var projectContext = GetPresentationContext(ifcProject);
                if (projectContext != null)
                    AlignPlacement(projectContext.WorldCoordinateSystem, worldPlacement, model, logDetail);
            }
            if (alignProjectCoordinates)
            {
                var targetLabels = projectPlacements.Select(p => p.RefEntityLable).ToList();
                foreach (var ifcSite in model.Instances.OfType<IIfcSite>())
                {
                    if (!targetLabels.Contains(ifcSite.EntityLabel))
                        continue;
                    if (!(ifcSite.ObjectPlacement is IIfcLocalPlacement localPlacement))
                        continue;
                    var projectPlacement = projectPlacements.FirstOrDefault(p => p.RefEntityLable == ifcSite.EntityLabel);
                    AlignPlacement(localPlacement.RelativePlacement, projectPlacement, model, logDetail);
                }
            }
        }

        private static void AlignPlacement(IIfcAxis2Placement axis2Placement, IGeoPlacement refPlacement, IModel model, bool logDetail)
        {
            if (axis2Placement == null || refPlacement == null)
                return;

            var placement = GeoFactory.CreateGeoPlacement(axis2Placement);
            if (placement.Equals(refPlacement))
                return;

            if (placement.LocationXYZ != refPlacement.LocationXYZ)
            {
                var point = PointFactory.CreatePoint(model, refPlacement.LocationXYZ);
                if (axis2Placement is IIfcAxis2Placement3D placement3D)
                    placement3D.Location = point;
                else if (axis2Placement is IIfcAxis2Placement2D placement2D)
                    placement2D.Location = point;
            }
            if (placement.RotationX != refPlacement.RotationX)
            {
                var refDirection = PointFactory.CreateDirection(model, refPlacement.RotationX);
                if (axis2Placement is IIfcAxis2Placement3D placement3D)
                    placement3D.RefDirection = refDirection;
                else if (axis2Placement is IIfcAxis2Placement2D placement2D)
                    placement2D.RefDirection = refDirection;
            }
            if (placement.RotationZ != refPlacement.RotationZ)
            {
                var axis = PointFactory.CreateDirection(model, refPlacement.RotationZ);
                if (axis2Placement is IIfcAxis2Placement3D placement3D)
                    placement3D.Axis = axis;
            }
            if (logDetail)
            {
                Marslogger.Mark($"#{axis2Placement.EntityLabel} location updated.");
                Log.Information("IfcAxis2Placement: {@obj}", GeoFactory.CreateGeoPlacement(axis2Placement));
            }
        }

        private static IIfcGeometricRepresentationContext GetPresentationContext(IIfcProject project)
        {
            //includes also inherited SubContexts (not necessary for this application)
            var allCtx = project.RepresentationContexts.OfType<IIfcGeometricRepresentationContext>();
            //avoid subs (unneccessary overhead)
            var noSubCtx = allCtx.Where(ctx => !(ctx is IIfcGeometricRepresentationSubContext)).ToList();
            //get only the context for model
            return noSubCtx.Where(a => a.ContextType == "Model").SingleOrDefault();
        }
    }
}
