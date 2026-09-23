using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Hierarchy
{
    /// <summary>
    /// The IFC 4.3 spatial vocabulary for built assets.
    /// <para>
    /// IFC 4.3 generalised the building-only structure into facilities: IfcRoad, IfcRailway,
    /// IfcBridge, IfcMarineFacility - and IfcBuilding, which became a subtype of IfcFacility -
    /// each broken down into facility parts (IfcRoadPart, IfcRailwayPart, IfcBridgePart, ...),
    /// which can themselves nest. None of these exist before IFC 4.3, so on IFC2X3 and IFC4
    /// models every query here simply returns nothing.
    /// </para>
    /// <para>
    /// The IFC4 interfaces xbim offers for every schema have no facility concept, so these are
    /// recognised by their IFC 4.3 classes.
    /// </para>
    /// </summary>
    public static class Facilities
    {
        public static bool IsFacility(IPersistEntity entity) =>
            entity is Xbim.Ifc4x3.ProductExtension.IfcFacility;

        public static bool IsFacilityPart(IPersistEntity entity) =>
            entity is Xbim.Ifc4x3.ProductExtension.IfcFacilityPart;

        /// <summary>Every facility in the model: roads, railways, bridges, buildings, marine facilities.</summary>
        public static IEnumerable<IIfcSpatialStructureElement> AllFacilities(IModel model) =>
            model.Instances.OfType<Xbim.Ifc4x3.ProductExtension.IfcFacility>().Cast<IIfcSpatialStructureElement>();

        /// <summary>Every facility part, at whatever depth it is nested.</summary>
        public static IEnumerable<IIfcSpatialStructureElement> AllFacilityParts(IModel model) =>
            model.Instances.OfType<Xbim.Ifc4x3.ProductExtension.IfcFacilityPart>().Cast<IIfcSpatialStructureElement>();

        /// <summary>
        /// The part's type as its PredefinedType says, e.g. CARRIAGEWAY or TRACK, which is how
        /// facility parts are told apart (the class alone says little: IfcRoadPart covers
        /// everything from a whole road segment to a single shoulder).
        /// </summary>
        public static string UsageOf(IPersistEntity facilityPart)
        {
            var property = facilityPart.ExpressType.Properties.Values
                .FirstOrDefault(p => p.PropertyInfo.Name == "PredefinedType");
            return property?.PropertyInfo.GetValue(facilityPart)?.ToString();
        }
    }
}
