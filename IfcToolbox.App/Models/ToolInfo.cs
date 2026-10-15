using IfcToolbox.App.Services;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace IfcToolbox.App.Models
{
    /// <summary>One tool in the box, as the menu, the Tools page and the tool page header show it.</summary>
    public sealed class ToolInfo
    {
        /// <summary>The title, summary and action label are Tool_{key}_Title, _Summary and _Action in the string resources.</summary>
        public ToolInfo(string key, string glyph, Color color)
        {
            Key = key;
            Glyph = glyph;
            Color = color;
            Title = Loc.Get($"Tool_{key}_Title");
            Summary = Loc.Get($"Tool_{key}_Summary");
            Action = Loc.Get($"Tool_{key}_Action");
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
            new ToolInfo("optimizer", "", Color.FromArgb(255, 0x2E, 0x7D, 0x32)),
            new ToolInfo("relocator", "", Color.FromArgb(255, 0x15, 0x65, 0xC0)),
            new ToolInfo("splitter", "", Color.FromArgb(255, 0xE0, 0x6C, 0x00)),
            // Not in the original app: the counterpart of the Splitter.
            new ToolInfo("combiner", "", Color.FromArgb(255, 0x00, 0x79, 0x6B)),
            new ToolInfo("converter", "", Color.FromArgb(255, 0x6A, 0x1B, 0x9A)),
            new ToolInfo("anonymizer", "", Color.FromArgb(255, 0x45, 0x5A, 0x64)),
            new ToolInfo("validator", "", Color.FromArgb(255, 0x94, 0x24, 0x1E)),
        };

        public static ToolInfo Find(string key) => All.FirstOrDefault(t => t.Key == key);
    }
}
