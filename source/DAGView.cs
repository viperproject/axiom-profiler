using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using GraphX.Controls;
using GraphX.PCL.Common.Enums;
using GraphX.PCL.Logic.Algorithms.OverlapRemoval;
using GraphX.PCL.Logic.Models;
using QuickGraph;
using Z3AxiomProfiler.InstantiationGraph;

namespace Z3AxiomProfiler
{
    public partial class DAGView : Form
    {
        private GraphArea<DataVertex, DataEdge, BidirectionalGraph<DataVertex, DataEdge>> _gArea;
        private ZoomControl _zoomctrl;
        private Z3AxiomProfiler _profiler;

        public DAGView(Z3AxiomProfiler profiler)
        {
            InitializeComponent();
            _profiler = profiler;
            Load += GraphWindow_Load;
        }

        void GraphWindow_Load(object sender, EventArgs e)
        {
            wpfHost.Child = GenerateWpfVisuals();
            _zoomctrl.ZoomToFill();
            but_generate_Click();
        }

        private ZoomControl GenerateWpfVisuals()
        {
            _zoomctrl = new ZoomControl();
            ZoomControl.SetViewFinderVisibility(_zoomctrl, Visibility.Visible);
            
            /* ENABLES WINFORMS HOSTING MODE --- >*/
            var logic = new GXLogicCore<DataVertex, DataEdge, BidirectionalGraph<DataVertex, DataEdge>>
            {
                AsyncAlgorithmCompute = true
            };
            _gArea = new GraphArea<DataVertex, DataEdge, BidirectionalGraph<DataVertex, DataEdge>>
            {
                EnableWinFormsHostingMode = true,
                LogicCore = logic
            };

            logic.Graph = GenerateGraph();
            logic.DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.ISOM;
            logic.DefaultLayoutAlgorithmParams = logic.AlgorithmFactory.CreateLayoutParameters(LayoutAlgorithmTypeEnum.ISOM);

            logic.DefaultOverlapRemovalAlgorithm = OverlapRemovalAlgorithmTypeEnum.FSA;
            logic.DefaultOverlapRemovalAlgorithmParams = logic.AlgorithmFactory.CreateOverlapRemovalParameters(OverlapRemovalAlgorithmTypeEnum.FSA);
            logic.DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.None;
            _zoomctrl.Content = _gArea;
            _gArea.RelayoutFinished += gArea_RelayoutFinished;

            var binDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var myResourceDictionary = new ResourceDictionary { Source = new Uri(Path.Combine(binDir, "graph_template.xaml"), UriKind.RelativeOrAbsolute) };
            _zoomctrl.Resources.MergedDictionaries.Add(myResourceDictionary);
            return _zoomctrl;
        }

        private BidirectionalGraph<DataVertex, DataEdge> GenerateGraph()
        {
            //FOR DETAILED EXPLANATION please see SimpleGraph example project
            var dataGraph = new BidirectionalGraph<DataVertex, DataEdge>();
            for (int i = 1; i < 100; i++)
            {
                var dataVertex = new DataVertex($"MyVertex {i}\nMultiline stuff\n\nHello :-P");
                dataGraph.AddVertex(dataVertex);
            }
            var rand = new Random(0);
            var vlist = dataGraph.Vertices.ToList();
            for (int i = 1; i < 100; i++)
            {
                //Then create two edges optionaly defining Text property to show who are connected
                var dataEdge = new DataEdge(vlist[rand.Next(0,vlist.Count-1)], vlist[rand.Next(0, vlist.Count - 1)]) { Text = "" };
                dataGraph.AddEdge(dataEdge);
            }
            return dataGraph;
        }

        void gArea_RelayoutFinished(object sender, EventArgs e)
        {
            _zoomctrl.ZoomToFill();
        }

        private void but_generate_Click()
        {
            _gArea.GenerateGraph();
            _zoomctrl.ZoomToFill();
        }
    }
}
