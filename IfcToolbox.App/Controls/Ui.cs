using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

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
    }
}
