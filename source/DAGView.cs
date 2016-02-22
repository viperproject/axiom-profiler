using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using Z3AxiomProfiler.QuantifierModel;
using Color = Microsoft.Msagl.Drawing.Color;
using MouseButtons = System.Windows.Forms.MouseButtons;
using Size = System.Drawing.Size;

namespace Z3AxiomProfiler
{
    public partial class DAGView : UserControl
    {

        private readonly Z3AxiomProfiler _z3AxiomProfiler;
        private readonly GViewer _viewer;
        private Graph graph;
        private static int newNodeWarningThreshold = 40;

        //Define the colors
        private readonly List<Color> colors = new List<Color> {Color.Purple, Color.Blue,
                Color.Green, Color.LawnGreen, Color.Orange, Color.DarkKhaki, Color.DarkGray, Color.Moccasin,
                Color.DarkSeaGreen, Color.Silver, Color.Salmon, Color.LemonChiffon, Color.Fuchsia,
                Color.ForestGreen, Color.Beige, Color.AliceBlue, Color.MediumTurquoise, Color.Tomato
                };

        private static readonly Color selectionColor = Color.Red;
        private static readonly Color parentColor = Color.DarkOrange;

        private readonly Dictionary<Quantifier, Color> colorMap = new Dictionary<Quantifier, Color>();
        private int currColorIdx;

        public DAGView(Z3AxiomProfiler profiler)
        {
            _z3AxiomProfiler = profiler;
            InitializeComponent();
            //create a viewer object 
            _viewer = new GViewer
            {
                AsyncLayout = true,
                LayoutEditingEnabled = false,
                ToolBarIsVisible = false,
                NavigationVisible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Top = panel1.Bottom,
                Left = Left,
                Size = new Size(Right, Bottom - panel1.Bottom)
            };
            _viewer.MouseMove += _ViewerMouseMove;
            _viewer.MouseUp += _ViewerViewMouseUp;
            _viewer.MouseClick += _ViewerViewClick;
            //associate the viewer with the form 
            Controls.Add(_viewer);
        }

        private void drawGraph()
        {
            if (_z3AxiomProfiler.model == null) return;
            var newNodeInsts = _z3AxiomProfiler.model.instances
                                       .Where(inst => inst.Depth <= maxRenderDepth.Value)
                                       .OrderByDescending(inst => inst.Cost)
                                       .ToList();

            drawGraphWithInstantiations(newNodeInsts);
        }

        public void drawGraphNoFilterQuestion()
        {
            var newNodeInsts = _z3AxiomProfiler.model.instances
                                       .Where(inst => inst.Depth <= maxRenderDepth.Value)
                                       .OrderByDescending(inst => inst.Cost)
                                       .Take(newNodeWarningThreshold)
                                       .ToList();

            drawGraphWithInstantiations(newNodeInsts);
        }

        private void drawGraphWithInstantiations(List<Instantiation> newNodeInsts)
        {
            colorMap.Clear();
            currColorIdx = 0;

            var edgeRoutingSettings = new EdgeRoutingSettings
            {
                EdgeRoutingMode = EdgeRoutingMode.Spline,
                BendPenalty = 50
            };
            var layoutSettings = new SugiyamaLayoutSettings
            {
                AspectRatio = 0.5f,
                LayerSeparation = 20,
                EdgeRoutingSettings = edgeRoutingSettings
            };

            //create a graph object
            graph = new Graph($"Instantiations dependencies [{maxRenderDepth.Value} levels]")
            {
                LayoutAlgorithmSettings = layoutSettings
            };

            if (checkNumNodesWithDialog(ref newNodeInsts)) return;

            foreach (var node in newNodeInsts.Select(connectToVisibleNodes))
            {
                formatNode(node);
            }

            //bind the graph to the viewer 
            _viewer.Graph = graph;
        }

        private void formatNode(Node currNode)
        {
            var inst = (Instantiation)currNode.UserData;
            var nodeColor = getColor(inst.Quant);
            currNode.Attr.LineWidth = 1;
            currNode.Attr.LabelMargin = 5;
            currNode.Attr.Color = Color.Black;
            currNode.Attr.FillColor = nodeColor;

            if (nodeColor.R * 0.299 + nodeColor.G * 0.587 + nodeColor.B * 0.114 <= 186.0)
            {
                currNode.Label.FontColor = Color.White;
            }
            currNode.LabelText = " ";
        }

        private void redrawGraph_Click(object sender, EventArgs e)
        {
            drawGraph();
        }

        private Color getColor(Quantifier quant)
        {
            if (!colorMap.ContainsKey(quant) && currColorIdx >= colors.Count)
            {
                return Color.Black;
            }
            if (colorMap.ContainsKey(quant)) return colorMap[quant];

            colorMap[quant] = colors[currColorIdx];
            currColorIdx++;
            return colorMap[quant];
        }

        private int oldX = -1;
        private int oldY = -1;
        private void _ViewerMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (oldX != -1 && oldY != -1)
            {
                _viewer.Pan(e.X - oldX, e.Y - oldY);
            }
            oldX = e.X;
            oldY = e.Y;
        }

        private void _ViewerViewMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            oldX = -1;
            oldY = -1;
        }

        private Node previouslySelectedNode;
        private readonly List<Node> highlightedNodes = new List<Node>();
        private void _ViewerViewClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var node = _viewer.SelectedObject as Node;
            selectNode(node);
            if (node != null) _z3AxiomProfiler.addInstantiationToHistory((Instantiation)node.UserData);
        }

        public void selectInstantiation(Instantiation inst)
        {
            selectNode(graph.FindNode(inst.uniqueID));
        }

        private void selectNode(Node node)
        {
            if (previouslySelectedNode != null)
            {
                unselectNode();
            }


            if (node != null)
            {
                // format new one
                node.Attr.Color = selectionColor;
                node.Attr.LineWidth = 5;
                node.Label.FontColor = Color.White;
                // plus all parents
                foreach (var sourceNode in node.InEdges.Select(inEdge => inEdge.SourceNode))
                {
                    highlightNode(sourceNode);
                }
                previouslySelectedNode = node;
                _z3AxiomProfiler.SetInfoPanel((Instantiation)node.UserData);
            }
            _viewer.Invalidate();
        }

        private void highlightNode(Node node)
        {
            node.Attr.Color = parentColor;
            node.Attr.LineWidth = 5;
            highlightedNodes.Add(node);
        }

        public void unselectNode()
        {
            if (previouslySelectedNode == null) return;
            // restore old node
            formatNode(previouslySelectedNode);

            // plus all parents
            foreach (var node in highlightedNodes)
            {
                formatNode(node);
            }
            highlightedNodes.Clear();
            previouslySelectedNode = null;
            _viewer.Invalidate();
        }

        private void hideInstantiationButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }
            var nodeToRemove = previouslySelectedNode;
            unselectNode();

            // delete subgraph dependent on only the node being deleted
            Queue<Node> todoRemoveNodes = new Queue<Node>();
            todoRemoveNodes.Enqueue(nodeToRemove);
            while (todoRemoveNodes.Count > 0)
            {
                var currNode = todoRemoveNodes.Dequeue();
                foreach (var edge in currNode.OutEdges.Where(edge => edge.TargetNode.InEdges.Count() == 1))
                {
                    todoRemoveNodes.Enqueue(edge.TargetNode);
                }
                graph.RemoveNode(currNode);
            }

            _viewer.Graph = graph;
            redrawGraph();
        }

        private void showParentsButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }

            Instantiation inst = (Instantiation)previouslySelectedNode.UserData;
            foreach (var parentInst in inst.ResponsibleInstantiations
                .Where(parentInst => graph.FindNode(parentInst.uniqueID) == null))
            {
                connectToVisibleNodes(parentInst);
                formatNode(graph.FindNode(parentInst.uniqueID));
            }

            redrawGraph();
        }

        private void showChildrenButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }
            var currInst = (Instantiation)previouslySelectedNode.UserData;
            var newNodeInsts = currInst.DependantInstantiations
                                       .Where(childInst => graph.FindNode(childInst.uniqueID) == null)
                                       .ToList();
            if (checkNumNodesWithDialog(ref newNodeInsts)) return;

            foreach (var childInst in newNodeInsts)
            {
                connectToVisibleNodes(childInst);
                formatNode(graph.FindNode(childInst.uniqueID));
            }
            redrawGraph();
        }

        private bool checkNumNodesWithDialog(ref List<Instantiation> newNodeInsts)
        {
            if (newNodeInsts.Count > newNodeWarningThreshold)
            {
                var filterDecision = MessageBox.Show(
                    $"This operation would add {newNodeInsts.Count} new nodes to the graph. It is recommended to reduce the number by filtering.\nWould you like to filter the new nodes now?",
                    $"{newNodeInsts.Count} new nodes warning",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);
                switch (filterDecision)
                {
                    case DialogResult.Yes:
                        var filterBox = new InstantiationFilter(newNodeInsts);
                        if (filterBox.ShowDialog() == DialogResult.Cancel)
                        {
                            // just stop
                            return true;
                        }
                        newNodeInsts = filterBox.filtered;
                        break;
                    case DialogResult.Cancel:
                        // just stop
                        return true;
                }
            }
            return false;
        }

        private void redrawGraph()
        {
            _viewer.NeedToCalculateLayout = true;
            _viewer.Graph = graph;
            selectNode(previouslySelectedNode);
        }

        private void showChainButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }

            List<Instantiation> pathInstantiations = new List<Instantiation>();
            var current = (Instantiation)previouslySelectedNode.UserData;
            while (current.DependantInstantiations.Count > 0)
            {
                // follow the longest path
                current = current.DependantInstantiations
                    .Aggregate((i1, i2) => i1.DeepestSubpathDepth > i2.DeepestSubpathDepth ? i1 : i2);

                pathInstantiations.Add(current);
            }
            pathInstantiations = pathInstantiations.Where(inst => graph.FindNode(inst.uniqueID) == null).ToList();
            if (checkNumNodesWithDialog(ref pathInstantiations)) return;

            foreach (var node in pathInstantiations.Select(connectToVisibleNodes))
            {
                formatNode(node);
            }

            redrawGraph();
        }

        private Node connectToVisibleNodes(Instantiation instantiation)
        {
            var instNode = graph.FindNode(instantiation.uniqueID);
            if (instNode == null)
            {
                instNode = graph.AddNode(instantiation.uniqueID);
                instNode.UserData = instantiation;

            }
            var currUniqueId = instantiation.uniqueID;

            // add edges for the instantiation's visible parents
            // if the edge is not already there
            foreach (var parentInst in instantiation.ResponsibleInstantiations
                .Where(inst => graph.FindNode(inst.uniqueID) != null)
                .Where(parentInst => instNode.InEdges.All(edge => edge.Source != parentInst.uniqueID)))
            {
                graph.AddEdge(parentInst.uniqueID, currUniqueId);
            }

            // add in-edges for the instantiation's visible children
            // if the edge is not already there
            foreach (var child in instantiation.DependantInstantiations
                .Where(inst => graph.FindNode(inst.uniqueID) != null)
                .Where(child => instNode.OutEdges.All(edge => edge.Target != child.uniqueID)))
            {
                graph.AddEdge(currUniqueId, child.uniqueID);
            }
            return instNode;
        }

        private void sourceTreeButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }

            var treeInstantiations = new List<Instantiation>();
            var todo = new Queue<Instantiation>();
            todo.Enqueue((Instantiation)previouslySelectedNode.UserData);

            // collect tree
            while (todo.Count > 0)
            {
                var current = todo.Dequeue();

                // add the not visible parents as new nodes
                treeInstantiations.AddRange(current.ResponsibleInstantiations.Where(inst => graph.FindNode(inst.uniqueID) == null));

                // but use all nodes to build the complete tree
                foreach (var inst in current.ResponsibleInstantiations)
                {
                    todo.Enqueue(inst);
                }
            }

            // filtering and displaying
            if (checkNumNodesWithDialog(ref treeInstantiations)) return;
            foreach (var node in treeInstantiations.Select(connectToVisibleNodes))
            {
                formatNode(node);
            }
            redrawGraph();
        }

        private void pathExplanationButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }

            // building path downwards:
            var bestDownPath = buildPathDepthFirst(new InstantiationPath(), previouslySelectedNode, true);
            var bestUpPath = buildPathDepthFirst(new InstantiationPath(), previouslySelectedNode, false);
            bestUpPath.appendWithOverlap(bestDownPath);
            highlightPath(bestUpPath);
            _z3AxiomProfiler.SetInfoPanel(bestUpPath);
        }

        private InstantiationPath buildPathDepthFirst(InstantiationPath basePath, Node node, bool down)
        {
            basePath = new InstantiationPath(basePath);
            if (down)
            {
                basePath.append((Instantiation)node.UserData);
            }
            else
            {
                basePath.prepend((Instantiation)node.UserData);
            }

            var relevantEdges = down ? node.OutEdges : node.InEdges;
            var returnPath = basePath;
            foreach (var edge in relevantEdges)
            {
                var pathCandidate = buildPathDepthFirst(basePath, down ? edge.TargetNode : edge.SourceNode, down);

                if (pathCandidate.Cost() >= returnPath.Cost())
                {
                    returnPath = pathCandidate;
                }
            }
            return returnPath;
        }

        private void highlightPath(InstantiationPath path)
        {
            foreach (var instantiation in path.getInstantiations())
            {
                highlightNode(graph.FindNode(instantiation.uniqueID));
            }
            _viewer.Invalidate();
        }
    }
}
