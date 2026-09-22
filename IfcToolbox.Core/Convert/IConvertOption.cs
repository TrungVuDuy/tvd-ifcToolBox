using System.Collections.Generic;

namespace IfcToolbox.Core.Convert
{
    public interface IConvertOption
    {
        string Name { get; set; }

        /// <summary>Command line flag as spelled by IfcConvert 0.6.</summary>
        string CItext { get; set; }

        /// <summary>
        /// Flag as spelled by IfcConvert 0.8 and newer, when it differs. Null means unchanged.
        /// </summary>
        string CItextV08 { get; set; }

        bool IsFormatRelated { get; set; }
        bool HasArgs { get; set; }
        IEnumerable<ConvertTargetFormat> RelatedFormats { get; set; }
        bool IsPostOption { get; set; }

        /// <summary>Flag to use for the given IfcConvert build.</summary>
        string GetCItext(IfcConvertVersion version);
    }
}
