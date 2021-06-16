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

        [TestMethod]
        public void TestAllDownPatterns1()
        {
            List<List<Quantifier>> result = DAGView.AllDownPattern(Graphs.graph1.FindNode("a"), 8);
            Assert.AreEqual(0, result.Count);
            //Assert.AreEqual("a", Graphs.node1.Id);
        }
    }
}
