using System.Collections.Generic;

namespace IfcToolbox.Core.Convert
{
    public class ConvertOption : IConvertOption
    {
        public string Name { get; set; }
        public string CItext { get; set; }
        public string CItextV08 { get; set; }
        public bool HasArgs { get; set; }
        public bool IsFormatRelated { get; set; }
        public IEnumerable<ConvertTargetFormat> RelatedFormats { get; set; }
        public bool IsPostOption { get; set; }

        public string GetCItext(IfcConvertVersion version)
        {
            if (version != null && version.IsAtLeast08 && !string.IsNullOrEmpty(CItextV08))
                return CItextV08;
            return CItext;
        }
    }

    public static class ConvertOptionExtension
    {
        public static IConvertOption RegistAsPostOption(this IConvertOption convertOption)
        {
            convertOption.IsPostOption = true;
            return convertOption;
        }

        /// <summary>
        /// Records the spelling IfcOpenShell switched to in 0.8, so the same option keeps
        /// working whichever executable is in use.
        /// </summary>
        public static IConvertOption RenamedIn08(this IConvertOption convertOption, string ciTextV08)
        {
            convertOption.CItextV08 = ciTextV08;
            return convertOption;
        }
    }
}
