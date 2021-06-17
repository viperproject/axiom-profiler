using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using AxiomProfiler.QuantifierModel;
using Color = Microsoft.Msagl.Drawing.Color;
using MouseButtons = System.Windows.Forms.MouseButtons;
using Size = System.Drawing.Size;
using AxiomProfiler.Utilities;

namespace AxiomProfiler
{
    public partial class DAGView : UserControl
    {

        private readonly AxiomProfiler _z3AxiomProfiler;
        private readonly GViewer _viewer;
        private Graph graph;
        private static readonly int newNodeWarningThreshold = 200;
        private static readonly int numberOfInitialNodes = 40;

        //Define the colors
        private readonly List<Color> colors = new List<Color> {Color.Purple, Color.Blue,
                Color.Green, Color.LawnGreen, Color.Orange, Color.DarkKhaki, Color.DarkGray, Color.Moccasin,
                Color.DarkSeaGreen, Color.Silver, Color.Salmon, Color.LemonChiffon, Color.Fuchsia,
                Color.ForestGreen, Color.Beige, Color.AliceBlue, Color.MediumTurquoise, Color.Tomato,
                Color.Black
                };

        private static readonly Color selectionColor = Color.Red;
        private static readonly Color parentColor = Color.DarkOrange;

        private readonly Dictionary<Quantifier, Color> colorMap = new Dictionary<Quantifier, Color>();

        public DAGView(AxiomProfiler profiler)
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
                                       .OrderByDescending(inst => inst.DeepestSubpathDepth)
                                       .ToList();

            drawGraphWithInstantiations(newNodeInsts, true);
        }

        public void drawGraphNoFilterQuestion()
        {
            var newNodeInsts = _z3AxiomProfiler.model.instances
                                       .Where(inst => inst.Depth <= maxRenderDepth.Value)
                                       .OrderByDescending(inst => inst.DeepestSubpathDepth)
                                       .Take(numberOfInitialNodes)
                                       .ToList();

            drawGraphWithInstantiations(newNodeInsts);
        }

        public void Clear()
        {
            drawGraphWithInstantiations(new List<Instantiation>());
        }

        private void drawGraphWithInstantiations(List<Instantiation> newNodeInsts, bool forceDialog = false)
        {
            var usedQuants = newNodeInsts.Select(inst => inst.Quant).Distinct().ToList();
            var removedQuants = colorMap.Keys.Where(quant => !usedQuants.Contains(quant)).ToList();
            foreach (var removedQuant in removedQuants) colorMap.Remove(removedQuant);

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

            if (checkNumNodesWithDialog(ref newNodeInsts, forceDialog)) return;

            // Sorting helps ensure that the most common quantifiers end up with different colors
            var prioritySortedNewNodeInsts = newNodeInsts.GroupBy(inst => inst.Quant)
                .OrderByDescending(group => group.Count())
                .SelectMany(group => group);
            foreach (var node in prioritySortedNewNodeInsts.Select(connectToVisibleNodes))
            {
                formatNode(node);
            }

            //bind the graph to the viewer 
            _viewer.Graph = graph;
        }

        private void formatNode(Node currNode)
        {
            var inst = (Instantiation)currNode.UserData;
            if (inst.Quant.Namespace == "")
            {
                var nodeColor = getColor(inst.Quant);
                currNode.Attr.LineWidth = 1;
                currNode.Attr.LabelMargin = 5;
                currNode.Attr.Color = Color.Black;
                currNode.Attr.FillColor = nodeColor;

                if (nodeColor.R * 0.299 + nodeColor.G * 0.587 + nodeColor.B * 0.114 <= 186.0)
                {
                    currNode.Label.FontColor = Color.White;
                }
            }
            else
            {
                currNode.Attr.Shape = Shape.Diamond;
                currNode.Attr.LineWidth = 1;
                currNode.Attr.LabelMargin = 0;
                currNode.Attr.Color = Color.DimGray;
                currNode.Attr.FillColor = Color.Transparent;
            }
            currNode.LabelText = " ";
        }

        private void redrawGraph_Click(object sender, EventArgs e)
        {
            drawGraph();
        }

        private Color getColor(Quantifier quant)
        {
            //Hard coded colors for generating screenshots for the paper
            /*switch (quant.Qid) {
                case "sortedness":
                    return Color.Green;
                case "next_def":
                    return Color.Yellow;
                case "injectivity":
                    return Color.Purple;
                default:*/
                if (!colorMap.TryGetValue(quant, out var color))
                {
                    color = colors.OrderBy(c => colorMap.Values.Count(used => used == c)).First();
                    colorMap[quant] = color;
                }

                return color;
            //}
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
            if (previouslySelectedNode != null || highlightedNodes.Count != 0)
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
            if (previouslySelectedNode == null && highlightedNodes.Count == 0) return;
            // restore old node
            if (previouslySelectedNode != null) formatNode(previouslySelectedNode);

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

        private bool checkNumNodesWithDialog(ref List<Instantiation> newNodeInsts, bool forceDialog = false)
        {
            if (forceDialog || newNodeInsts.Count > newNodeWarningThreshold)
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

                //make sure that all instantiations causing this path are visible and can be explored by the user
                foreach (var responsibleInst in current.ResponsibleInstantiations)
                {
                    if (!pathInstantiations.Contains(responsibleInst))
                    {
                        pathInstantiations.Add(responsibleInst);
                    }
                }
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

        // This functions and it's helper functions are properlly explain in
        //https://docs.google.com/document/d/1SJspfBecgkVT9U8xC-MvQ_NPDTGVfg0yqq7YjJ3ma2s/edit?usp=sharing
        // helper functions were originally private, but changing to public was needed to unit testing
        private void pathExplanationButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }

            Task.Run(() =>
            {
                // During debugging we want VS to catch exceptions so we can inspect the program state at the point where the exception was thrown.
                // For release builds we catch the execption here and display a message so the user knows that that they shouldn't wait for a path to be found.
#if !DEBUG
                try
                {
#endif
                    _z3AxiomProfiler.DisplayMessage("Finding best path for generalization...");

                    // building path downwards:
                    var bestDownPath = BestDownPath(previouslySelectedNode);
                    InstantiationPath bestUpPath;
                    if (bestDownPath.TryGetLoop(out var loop))
                    {
                        bestUpPath = ExtendPathUpwardsWithLoop(loop, previouslySelectedNode, bestDownPath);
                        if (bestUpPath == null)
                        {
                            bestUpPath = BestUpPath(previouslySelectedNode, bestDownPath);
                        }
                    }
                    else
                    {
                        bestUpPath = BestUpPath(previouslySelectedNode, bestDownPath);
                    }

                    List<Instantiation> toRemove;
                    if (bestUpPath.TryGetCyclePath(out var cyclePath))
                    {
                        highlightPath(cyclePath);
                        _z3AxiomProfiler.UpdateSync(cyclePath);
                        toRemove = cyclePath.GetInstnationsUnusedInGeneralization();
                    }
                    else
                    {
                        highlightPath(bestUpPath);
                        _z3AxiomProfiler.UpdateSync(bestUpPath);
                        toRemove = bestUpPath.GetInstnationsUnusedInGeneralization();
                    }

                    // Stop highlighting nodes that weren't used in the generalization.
                    foreach (var inst in toRemove)
                    {
                        var node = graph.FindNode(inst.uniqueID);
                        if (node != null)
                        {
                            formatNode(node);
                        }
                    }
                    _viewer.Invalidate();
#if !DEBUG
                }
                catch (Exception exception)
                {
                    _z3AxiomProfiler.DisplayMessage($"An exception was thrown. Please report this bug to viper@inf.ethz.ch.\nDescription of the exception: {exception.Message}");
		    Console.WriteLine(exception);
                }
#endif
            });
        }

        // These factors were determined experimentally
        private static readonly double outlierThreshold = 0.3;
        private static readonly double incomingEdgePenalizationFactor = 0.5;

        // Return a score where
        // - having longer length recieves a higher score
        // - having less covered children recieves a higher score
        // - having shorter pattern recieved a higher score
        private static double InstantiationPathScoreFunction(InstantiationPath instantiationPath, bool eliminatePrefix, bool eliminatePostfix)
        {
            // TODO
            return 0.0;
        }

        // For performance reasons we cannot score all possible paths. Instead we score segments of length 8 and
        // build a path from the best segments.
        private static readonly int pathSegmentSize = 8;


        // Described in https://docs.google.com/document/d/1SJspfBecgkVT9U8xC-MvQ_NPDTGVfg0yqq7YjJ3ma2s/edit?usp=sharing
        // Get all down patterns,
        // extent them to max(3, lcm(length of patterns)),
        // score them and sort by score
        // extend to top 5 path fully
        // score and sort the fully extended path
        // return the best one
        public InstantiationPath BestDownPath(Node node)
        {
            // TODO
            return null;
        }

        // Similar to BestDownPath but up
        public InstantiationPath BestUpPath(Node node, InstantiationPath downPath)
        {
            // TODO
            return null;
        }

        // Return all down patterns found with the bound
        public static List<List<Quantifier>> AllDownPatterns(Node node, int bound)
        {
            // TODO
            return null;
        }

        public static List<List<Quantifier>> AllUpPatterns(Node node, int bound)
        {
            // TODO
            return null;
        }

        public static InstantiationPath ExtendPathUpwardsWithLoop(IEnumerable<Tuple<Quantifier, Term>> loop, Node node, InstantiationPath downPath)
        {
            var nodeInst = (Instantiation)node.UserData;
            if (!loop.Any(inst => inst.Item1 == nodeInst.Quant && inst.Item2 == nodeInst.bindingInfo.fullPattern)) return null;
            loop = loop.Reverse().RepeatIndefinietly();
            loop = loop.SkipWhile(inst => inst.Item1 != nodeInst.Quant || inst.Item2 != nodeInst.bindingInfo.fullPattern);
            var res = ExtendPathUpwardsWithInstantiations(new InstantiationPath(), loop, node);
            res.appendWithOverlap(downPath);
            return res;
        }

        public static InstantiationPath ExtendPathDownwardsWithLoop(IEnumerable<Tuple<Quantifier, Term>> loop, Node node, InstantiationPath downPath)
        {
            //TODO
            return null;
        }

        private static InstantiationPath ExtendPathUpwardsWithInstantiations(InstantiationPath path, IEnumerable<Tuple<Quantifier, Term>> instantiations, Node node)
        {
            if (!instantiations.Any()) return path;
            var instantiation = instantiations.First();
            var nodeInst = (Instantiation)node.UserData;
            if (instantiation.Item1 != nodeInst.Quant || instantiation.Item2 != nodeInst.bindingInfo.fullPattern) return path;
            var extendedPath = new InstantiationPath(path);
            extendedPath.prepend(nodeInst);
            var bestPath = extendedPath;
            var remainingInstantiations = instantiations.Skip(1);
            foreach (var predecessor in node.InEdges)
            {
                var candidatePath = ExtendPathUpwardsWithInstantiations(extendedPath, remainingInstantiations, predecessor.SourceNode);
                if (candidatePath.Length() > bestPath.Length())
                {
                    bestPath = candidatePath;
                }
            }
            return bestPath;
        }

        private void highlightPath(InstantiationPath path)
        {
            if (previouslySelectedNode != null || highlightedNodes.Count != 0)
            {
                unselectNode();
            }

            foreach (var instantiation in path.getInstantiations())
            {
                highlightNode(graph.FindNode(instantiation.uniqueID));
            }
            _viewer.Invalidate();
        }
    }
}
