using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Routing;
using Z3AxiomProfiler.QuantifierModel;
using Color = Microsoft.Msagl.Drawing.Color;
using MouseButtons = System.Windows.Forms.MouseButtons;
using Size = System.Drawing.Size;

namespace Z3AxiomProfiler
{
    public partial class DAGView : Form
    {

        private readonly Z3AxiomProfiler _z3AxiomProfiler;
        private readonly GViewer _viewer;
        private Graph graph;

        //Define the colors
        private readonly List<Color> colors = new List<Color> {Color.Purple, Color.Blue,
                Color.Green, Color.LawnGreen, Color.Orange, Color.Cyan, Color.DarkGray, Color.Moccasin,
                Color.YellowGreen, Color.Silver, Color.Salmon, Color.LemonChiffon, Color.Fuchsia,
                Color.ForestGreen, Color.Beige
                };

        private static readonly Color selectionColor = Color.Red;
        private static readonly Color parentColor = Color.Yellow;

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
                EdgeInsertButtonVisible = false,
                LayoutEditingEnabled = false,
                //LayoutAlgorithmSettingsButtonVisible = false,
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
            Text = $"Instantiations dependencies [{maxRenderDepth.Value} levels]";

            var edgeRoutingSettings = new EdgeRoutingSettings
            {
                EdgeRoutingMode = EdgeRoutingMode.Spline,
                BendPenalty = 50
            };
            var layoutSettings = new SugiyamaLayoutSettings
            {
                AspectRatio = 4,
                LayerSeparation = 10,
                EdgeRoutingSettings = edgeRoutingSettings
            };
            //create a graph object
            graph = new Graph($"Instantiations dependencies [{maxRenderDepth.Value} levels]")
            {
                LayoutAlgorithmSettings = layoutSettings
            };

            foreach (var inst in _z3AxiomProfiler.model.instances.Where(inst => inst.Depth < maxRenderDepth.Value))
            {
                foreach (var dependentInst in inst.DependantInstantiations.Where(i => i.Depth <= maxRenderDepth.Value))
                {
                    graph.AddEdge(inst.FingerPrint, dependentInst.FingerPrint);
                }

            }

            foreach (var currNode in graph.Nodes)
            {
                formatNode(currNode);
            }

            //bind the graph to the viewer 
            _viewer.Graph = graph;
        }

        private void formatNode(Node currNode)
        {
            var inst = _z3AxiomProfiler.model.fingerprints[currNode.Id];
            currNode.UserData = inst;
            var nodeColor = getColor(inst.Quant);
            currNode.Attr.FillColor = nodeColor;
            if (nodeColor.R*0.299 + nodeColor.G*0.587 + nodeColor.B*0.114 <= 186)
            {
                currNode.Label.FontColor = Color.White;
            }
            currNode.LabelText = inst.Quant.PrintName + '\n' + inst.FingerPrint;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
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

        private void DAGView_Load(object sender, EventArgs e)
        {
            drawGraph();
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
        private void _ViewerViewClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var node = _viewer.SelectedObject as Node;
            selectNode(node);
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
                node.Attr.FillColor = selectionColor;
                node.Label.FontColor = Color.White;
                // plus all parents
                foreach (var inEdge in node.InEdges)
                {
                    inEdge.SourceNode.Attr.FillColor = parentColor;
                    inEdge.SourceNode.Label.FontColor = Color.Black;
                }
                previouslySelectedNode = node;
                _z3AxiomProfiler.SetToolTip((Instantiation) node.UserData);
            }
            _viewer.Invalidate();
        }

        private void unselectNode()
        {
            // restore old node
            formatNode(previouslySelectedNode);

            // plus all parents
            foreach (var inEdge in previouslySelectedNode.InEdges)
            {
                formatNode(inEdge.SourceNode);
            }
            previouslySelectedNode = null;
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
            
            _viewer.NeedToCalculateLayout = true;
            _viewer.Graph = graph;
            _viewer.Invalidate();
        }

        private void showParentsButton_Click(object sender, EventArgs e)
        {
            if (previouslySelectedNode == null)
            {
                return;
            }

            bool added = false;
            Instantiation inst = (Instantiation) previouslySelectedNode.UserData;

            foreach (var parentInst in inst.ResponsibleInstantiations
                .Where(parentInst => graph.FindNode(parentInst.FingerPrint) == null))
            {
                // add eges to all visible children of this parent
                foreach (var childInst in parentInst.DependantInstantiations
                    .Where(childInst => graph.FindNode(childInst.FingerPrint) != null))
                {
                    graph.AddEdge(parentInst.FingerPrint, childInst.FingerPrint);
                    added = true;
                }

                // add in-edges for the parent's visible parents
                foreach (var resInst in parentInst.ResponsibleInstantiations
                    .Where(i => graph.FindNode(i.FingerPrint) != null))
                {
                    graph.AddEdge(resInst.FingerPrint, parentInst.FingerPrint);
                }

                if (added)
                {
                    formatNode(graph.FindNode(parentInst.FingerPrint));
                }
            }

            _viewer.NeedToCalculateLayout = true;
            _viewer.Graph = graph;
            selectNode(previouslySelectedNode);
        }
    }
}
