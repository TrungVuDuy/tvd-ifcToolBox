using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace IfcToolbox.App.Models
{
    /// <summary>One tool in the box, as the menu, the Tools page and the tool page header show it.</summary>
    public sealed class ToolInfo
    {
        public ToolInfo(string key, string title, string glyph, Color color, string summary, string action)
        {
            Key = key;
            Title = title;
            Glyph = glyph;
            Color = color;
            Summary = summary;
            Action = action;
        }

        /// <summary>Navigation tag and the --page name, e.g. "validator".</summary>
        public string Key { get; }
        public string Title { get; }
        /// <summary>Segoe Fluent Icons glyph.</summary>
        public string Glyph { get; }
        public Color Color { get; }
        public string Summary { get; }
        /// <summary>Label of the button that runs the tool, e.g. "Validate IFC".</summary>
        public string Action { get; }
    }

    public static class ToolCatalog
    {
        // Same order as the original app's menu.
        public static IReadOnlyList<ToolInfo> All { get; } = new[]
        {
            new ToolInfo("optimizer", "IFC Optimizer", "", Color.FromArgb(255, 0x2E, 0x7D, 0x32),
                "Reduce file size by merging duplicate instances and rounding away floating point noise.",
                "Optimize IFC"),
            new ToolInfo("relocator", "IFC Relocator", "", Color.FromArgb(255, 0x15, 0x65, 0xC0),
                "Move the world and project coordinate systems, align two models, georeference to a projected CRS.",
                "Relocate IFC"),
            new ToolInfo("splitter", "IFC Splitter", "", Color.FromArgb(255, 0xE0, 0x6C, 0x00),
                "Split a model by type, site, building, level, IFC 4.3 facility or facility part, or by picked objects.",
                "Split IFC"),
            new ToolInfo("converter", "IFC Converter", "", Color.FromArgb(255, 0x6A, 0x1B, 0x9A),
                "Convert to glTF, OBJ, Collada, STEP, IGES, XML, SVG and more with IfcOpenShell.",
                "Convert IFC"),
            new ToolInfo("anonymizer", "IFC Anonymizer", "", Color.FromArgb(255, 0x45, 0x5A, 0x64),
                "Remove people, organisations and chosen product information before sharing a model.",
                "Anonymize IFC"),
            new ToolInfo("validator", "IFC Validator", "", Color.FromArgb(255, 0x94, 0x24, 0x1E),
                "Check a model against an IDS 1.0 file or requirements taken from the buildingSMART Data Dictionary.",
                "Validate IFC"),
        };

        public static ToolInfo Find(string key) => All.FirstOrDefault(t => t.Key == key);
    }
}
