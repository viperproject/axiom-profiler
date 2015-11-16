using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Drawing;
using Z3AxiomProfiler.QuantifierModel;
using Color = Microsoft.Msagl.Drawing.Color;
using MouseButtons = System.Windows.Forms.MouseButtons;

namespace Z3AxiomProfiler
{
    public partial class DAGView : Form
    {

        private readonly Z3AxiomProfiler _z3AxiomProfiler;
        private readonly GViewer _viewer;

        //Define the colors
        private readonly List<Color> colors = new List<Color> {Color.Purple, Color.Blue,
                Color.Green, Color.Red, Color.Orange, Color.Cyan, Color.DarkGray, Color.Yellow,
                Color.YellowGreen, Color.Silver, Color.Salmon, Color.LemonChiffon, Color.Fuchsia,
                Color.ForestGreen, Color.Beige
                };

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
            //associate the viewer with the form 
            Controls.Add(_viewer);
        }

        private void drawGraph()
        {
            Text = $"Instantiations dependencies [{maxRenderDepth.Value} levels]";

            //create a graph object
            Graph graph = new Graph($"Instantiations dependencies [{maxRenderDepth.Value} levels]")
            {
                LayoutAlgorithmSettings = new MdsLayoutSettings();
            };

            foreach (var inst in _z3AxiomProfiler.model.instances.Where(inst => inst.Depth < maxRenderDepth.Value))
            {
                foreach (var dependentInst in inst.DependantInstantiations.Where(i => i.Depth <= maxRenderDepth.Value))
                {
                    graph.AddEdge(inst.FingerPrint, dependentInst.FingerPrint);
                }

            }

            int counter = 0;
            foreach (var currNode in graph.Nodes)
            {
                var inst = _z3AxiomProfiler.model.fingerprints[currNode.Id];
                var nodeColor = getColor(inst.Quant);
                currNode.Attr.FillColor = nodeColor;
                currNode.GeometryNode.Center = new Microsoft.Msagl.Core.Geometry.Point(counter, counter);
                counter++;
                if (nodeColor.R*0.299 + nodeColor.G*0.587 + nodeColor.B*0.114 <= 186)
                {
                    currNode.Label.FontColor = Color.White;
                }
                currNode.LabelText = inst.Quant.PrintName;
            }

            //bind the graph to the viewer 
            _viewer.Graph = graph;
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
    }
}
