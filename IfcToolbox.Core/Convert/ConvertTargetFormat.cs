using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcToolbox.Core.Convert
{
    /// <summary>
    /// Output formats IfcConvert can write. The enum name doubles as the file extension.
    /// </summary>
    public enum ConvertTargetFormat
    {
        OBJ,
        DAE,
        STP,
        IGS,
        XML,
        SVG,
        /// <summary>Binary glTF v2.0 - the format web viewers actually consume.</summary>
        GLB,
        /// <summary>IFC-SPF, i.e. IfcConvert writing IFC back out after processing.</summary>
        IFC,
        /// <summary>xeokit JSON. Needs IfcConvert 0.8 or newer.</summary>
        JSON,
        /// <summary>RDF Turtle with Well-Known-Text geometry. Needs IfcConvert 0.8 or newer.</summary>
        TTL,
        /// <summary>Hierarchical Data Format. Needs IfcConvert 0.8 or newer.</summary>
        H5,
    }

    /// <summary>
    /// Which IfcConvert release a format needs.
    /// The executable bundled with IfcToolbox is 0.6, which already writes GLB and IFC;
    /// the remaining formats were added in 0.8 and need a newer binary
    /// (see <see cref="IfcConvert.ExecutablePath"/> for how to supply one).
    /// </summary>
    public static class ConvertFormatInfo
    {
        private static readonly Dictionary<ConvertTargetFormat, IfcConvertVersion> MinimumVersions =
            new Dictionary<ConvertTargetFormat, IfcConvertVersion>
            {
                [ConvertTargetFormat.OBJ] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.DAE] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.STP] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.IGS] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.XML] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.SVG] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.GLB] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.IFC] = new IfcConvertVersion(0, 6),
                [ConvertTargetFormat.JSON] = new IfcConvertVersion(0, 8),
                [ConvertTargetFormat.TTL] = new IfcConvertVersion(0, 8),
                [ConvertTargetFormat.H5] = new IfcConvertVersion(0, 8),
            };

        /// <summary>File extension, including the leading dot.</summary>
        public static string GetExtension(ConvertTargetFormat format) => "." + format.ToString().ToLowerInvariant();

        public static IfcConvertVersion GetMinimumVersion(ConvertTargetFormat format) =>
            MinimumVersions.TryGetValue(format, out var version) ? version : new IfcConvertVersion(0, 6);

        public static bool IsSupportedBy(ConvertTargetFormat format, IfcConvertVersion version) =>
            version == null || version.IsUnknown || version >= GetMinimumVersion(format);

        /// <summary>Formats the given IfcConvert build can produce.</summary>
        public static IEnumerable<ConvertTargetFormat> SupportedFormats(IfcConvertVersion version) =>
            Enum.GetValues(typeof(ConvertTargetFormat)).Cast<ConvertTargetFormat>()
                .Where(f => IsSupportedBy(f, version));
    }
}
