using IfcToolbox.Core.Geo;
using System.Collections.Generic;

namespace IfcToolbox.Tools.Configurations
{
    public interface IConfigRelocate : IConfigBase
    {
        bool AlignProjectCoordinates { get; set; }
        bool AlignWorldCoordinates { get; set; }
        List<IGeoPlacement> ProjectPlacements { get; set; }
        IGeoPlacement WorldPlacement { get; set; }

        /// <summary>
        /// When set, georeferences the model with this projected CRS and map conversion
        /// (IFC4 and IFC4X3), replacing any existing one.
        /// </summary>
        GeoMapConversion MapConversion { get; set; }
    }
}
