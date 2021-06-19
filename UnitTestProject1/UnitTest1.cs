using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using AxiomProfiler;
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
            List<Quantifier> expected1 = new List<Quantifier>() { Graphs.Quants[0] };
            List<Quantifier> expected2 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected1));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected2));
        }

        // Test that AllDownPatterns return unique patterns
        [TestMethod]
        public void TestAllDownPattern4()
        {
            List<List<Quantifier>> result = DAGView.AllDownPatterns(Graphs.graph3.FindNode("A"), 8);
            List<Quantifier> expected = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected));
        }
    }
}
