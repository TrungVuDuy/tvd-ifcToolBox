using IfcToolbox.Core.Hierarchy;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcToolbox.App.Controls
{
    /// <summary>
    /// Shows <see cref="HierarchyNode"/> trees in a multi-select <see cref="TreeView"/> and turns
    /// the ticks back into what they select.
    /// <para>
    /// Children are created when a node is first opened, so a model with tens of thousands of
    /// products shows at once. A ticked node stands for its whole subtree, opened or not.
    /// </para>
    /// </summary>
    public sealed class HierarchySelection
    {
        private readonly TreeView _tree;
        private readonly Dictionary<TreeViewNode, (HierarchyNode Data, int Depth)> _nodes = new();
        private Func<HierarchyNode, string> _valueOf = _ => null;
        private Func<HierarchyNode, string> _label = n => n.Name;
        private Func<HierarchyNode, int, bool> _expand = (_, _) => false;

        public HierarchySelection(TreeView tree)
        {
            _tree = tree;
            _tree.Expanding += OnExpanding;
            _tree.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SelectionChanged;

        /// <param name="valueOf">What a node selects (an entity label, an object type), or null when ticking it alone selects nothing.</param>
        /// <param name="label">Text of a node.</param>
        /// <param name="expand">Whether a node at a depth starts open.</param>
        public void Show(IEnumerable<HierarchyNode> roots, Func<HierarchyNode, string> valueOf,
            Func<HierarchyNode, string> label, Func<HierarchyNode, int, bool> expand)
        {
            _valueOf = valueOf;
            _label = label;
            _expand = expand;
            _tree.RootNodes.Clear();
            _nodes.Clear();
            foreach (var root in roots.Where(r => r != null))
                _tree.RootNodes.Add(Create(root, 0));
        }

        public void Clear()
        {
            _tree.RootNodes.Clear();
            _nodes.Clear();
        }

        /// <summary>Everything the ticked nodes select, opened or not, without duplicates.</summary>
        public List<string> SelectedValues()
        {
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var ticked = _tree.SelectedNodes.ToHashSet();
            foreach (var node in _tree.SelectedNodes)
            {
                if (!_nodes.TryGetValue(node, out var entry))
                    continue;
                // The tree lists a ticked node's children as ticked too; the ancestor covers them.
                if (HasTickedAncestor(node, ticked))
                    continue;
                foreach (var value in Values(entry.Data))
                    if (seen.Add(value))
                        values.Add(value);
            }
            return values;
        }

        private static bool HasTickedAncestor(TreeViewNode node, HashSet<TreeViewNode> ticked)
        {
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
                if (ticked.Contains(parent))
                    return true;
            return false;
        }

        /// <summary>
        /// The first selectable nodes going down: a ticked road segment stands for itself, its
        /// nested parts included in its file, rather than adding a file per nested part.
        /// </summary>
        private IEnumerable<string> Values(HierarchyNode node)
        {
            var value = _valueOf(node);
            if (value != null)
            {
                yield return value;
                yield break;
            }
            foreach (var child in node.Children)
                foreach (var nested in Values(child))
                    yield return nested;
        }

        private TreeViewNode Create(HierarchyNode data, int depth)
        {
            var node = new TreeViewNode { Content = _label(data) };
            _nodes[node] = (data, depth);
            if (data.Children.Count > 0)
            {
                if (_expand(data, depth))
                {
                    Realize(node);
                    node.IsExpanded = true;
                }
                else
                {
                    node.HasUnrealizedChildren = true;
                }
            }
            return node;
        }

        private void Realize(TreeViewNode node)
        {
            var (data, depth) = _nodes[node];
            bool ticked = _tree.SelectedNodes.Contains(node);
            foreach (var child in data.Children)
            {
                var childNode = Create(child, depth + 1);
                node.Children.Add(childNode);
                // A ticked node stays ticked all the way down when it is opened.
                if (ticked)
                    _tree.SelectedNodes.Add(childNode);
            }
            node.HasUnrealizedChildren = false;
        }

        private void OnExpanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            if (args.Node.HasUnrealizedChildren && _nodes.ContainsKey(args.Node))
                Realize(args.Node);
        }
    }
}
