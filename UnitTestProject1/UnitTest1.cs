using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AxiomProfiler;
using AxiomProfiler.QuantifierModel;
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


namespace UnitTestProject1
{
    // This test file is created to test some of pathExplanationButton_Click's helper functions
    // AllDownPatterns
    [TestClass]
    public class DAGViewTest
    {
        static TestGraphs Graphs = new TestGraphs();

        // The following are tests for AllDownPatterns

        // TestAllDownPatterns1
        // Travial Case where the node has no dependent nodes
        [TestMethod]
        public void TestAllDownPatterns1()
        {
            List<List<Quantifier>> result = DAGView.AllDownPatterns(Graphs.graph1.FindNode("A"), 8);
            Assert.AreEqual(0, result.Count);
        }

        // Test on bigger graph with minimum bound
        [TestMethod]
        public void TestAllDownPattern2()
        {
            List<List<Quantifier>> result = DAGView.AllDownPatterns(Graphs.graph2.FindNode("A"), 1);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(Graphs.Quants[0], result[0][0]);
        }

        // Test on bigger graph with default bound
        [TestMethod]
        public void TestAllDownPattern3()
        {
            List<List<Quantifier>> result = DAGView.AllDownPatterns(Graphs.graph2.FindNode("A"), 8);
            List<Quantifier> expected = new List<Quantifier>() { Graphs.Quants[0] };
            Console.WriteLine(result.Count.ToString());
            Assert.AreEqual(2, result.Count);
        }
    }
}
