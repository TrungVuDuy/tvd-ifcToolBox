namespace IfcToolbox.Core.Geo
{
    public class MapConversionParameters : IMapConversionParameters
    {
        public double? Eastings { get; set; }
        public double? Northings { get; set; }
        public double? OrthogonalHeight { get; set; }
        public double? Scale { get; set; }
        public double? XAxisAbscissa { get; set; }
        public double? XAxisOrdinate { get; set; }
        public double? FactorX { get; set; }
        public double? FactorY { get; set; }
        public double? FactorZ { get; set; }

        /// <summary>True when per-axis factors other than 1 are set, i.e. an IfcMapConversionScaled is needed.</summary>
        public bool HasFactors =>
            IsFactor(FactorX) || IsFactor(FactorY) || IsFactor(FactorZ);

        private static bool IsFactor(double? value) => value.HasValue && value.Value != 1.0;
    }

    /// <summary>The target map: an IfcProjectedCRS, normally named by its EPSG code.</summary>
    public class ProjectedCrs
    {
        /// <summary>e.g. "EPSG:32760". Required by IFC.</summary>
        public string Name { get; set; }
        public string Description { get; set; }
        public string GeodeticDatum { get; set; }
        public string VerticalDatum { get; set; }
        public string MapProjection { get; set; }
        public string MapZone { get; set; }
    }

    /// <summary>A map conversion together with the CRS it targets.</summary>
    public class GeoMapConversion
    {
        public MapConversionParameters Parameters { get; set; } = new MapConversionParameters();
        public ProjectedCrs TargetCrs { get; set; } = new ProjectedCrs();

        /// <summary>Label of the IfcMapConversion read from a model; 0 for one not taken from a model.</summary>
        public int EntityLabel { get; set; }
    }
}
