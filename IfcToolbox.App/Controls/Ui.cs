using IfcToolbox.App.Services;
using IfcToolbox.Core.Hierarchy;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Text.RegularExpressions;

namespace IfcToolbox.App.Controls
{
    public static class Ui
    {
        /// <summary>
        /// Scrolls an element to the top of the view once layout has caught up. Asking straight
        /// after making it visible can run before it has a position, and then does nothing.
        /// </summary>
        public static void ScrollIntoView(FrameworkElement element, double alignment = 0)
        {
            element.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                element.UpdateLayout();
                element.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = alignment, AnimationDesired = true });
            });
        }

        /// <summary>
        /// "Ground Floor  ·  IfcBuildingStorey", "IfcWall  ·  3 entities": a tree node with its
        /// description. The count comes from IfcToolbox.Core in English and is shown in the
        /// interface language.
        /// </summary>
        public static string NodeLabel(HierarchyNode node)
        {
            if (string.IsNullOrEmpty(node.Description))
                return node.Name;
            var count = Regex.Match(node.Description, @"^(\d+) entit(y|ies)$");
            var description = count.Success ? Loc.Count("Tree_Entities", int.Parse(count.Groups[1].Value)) : node.Description;
            return $"{node.Name}  ·  {description}";
        }
    }
}
