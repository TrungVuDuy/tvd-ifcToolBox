namespace IfcToolbox.Tools.Configurations
{
    public enum SplitStrategy
    {
        ByProduct,
        ByObjectType,
        DataOnly,
        BySite,
        ByBuilding,
        ByBuildingStorey,
        /// <summary>IFC 4.3: road, railway, bridge, marine facility or building.</summary>
        ByFacility,
        /// <summary>IFC 4.3: a facility part such as a carriageway or a track.</summary>
        ByFacilityPart,
    }
}
