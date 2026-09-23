namespace IfcToolbox.Core.Geo
{
    /// <summary>
    /// The attributes of an IfcMapConversion (and, from IFC 4.3, IfcMapConversionScaled):
    /// how local engineering coordinates map onto a projected CRS - "LoGeoRef 50".
    /// </summary>
    public interface IMapConversionParameters
    {
        double? Eastings { get; set; }
        double? Northings { get; set; }
        double? OrthogonalHeight { get; set; }

        /// <summary>
        /// Unit conversion from the model's length unit to the map's, applied to x, y and z.
        /// 0.001 for a model in millimetres on a map in metres. 1.0 when omitted.
        /// </summary>
        double? Scale { get; set; }

        /// <summary>Together with <see cref="XAxisOrdinate"/>, the direction of the local x axis on the map.</summary>
        double? XAxisAbscissa { get; set; }
        double? XAxisOrdinate { get; set; }

        /// <summary>
        /// IFC 4.3 IfcMapConversionScaled only: extra per-axis factors that scale coordinates
        /// (grid or combined scale factors), as opposed to <see cref="Scale"/>, which converts units.
        /// Null or 1.0 means none.
        /// </summary>
        double? FactorX { get; set; }
        double? FactorY { get; set; }
        double? FactorZ { get; set; }
    }
}
