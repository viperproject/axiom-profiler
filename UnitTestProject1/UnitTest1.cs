using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using AxiomProfiler;
using AxiomProfiler.QuantifierModel;
using Microsoft.Msagl.Drawing;

namespace UnitTestProject1
{
    // This test file is created to test some of pathExplanationButton_Click's helper functions
    // AllDownPatterns
    [TestClass]
    public class AllDownPatternsTest
    {
        static TestGraphs Graphs = new TestGraphs();

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
            List<Quantifier> expected = new List<Quantifier>() { Graphs.Quants[0] };
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected));
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

    [TestClass]
    public class ExtendDownardsWithPattern
    {
        static TestGraphs Graphs = new TestGraphs();

        // trivial case graph with one node a a pattern
        [TestMethod]
        public void TestExtenDownwardsWithPattern1()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result = 
                DAGView.ExtendDownwards(Graphs.graph1.FindNode("A"), ref pattern, 10);
            Assert.AreEqual(1, result.Count);
        }

        // small pattern with bound larger then the path
        // on graph 2, with pattern [Quantifier 0]
        [TestMethod]
        public void TestExtendDownwardsWithPattern2()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0]};
            List<Node> result = 
                DAGView.ExtendDownwards(Graphs.graph2.FindNode("A"), ref pattern, 3);
            List<String> expected = new List<String>() { "A", "M" };
            Assert.AreEqual(expected.Count, result.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], result[i].Id);
            }
        }

        // Pattern of size 2 and bound of size 4 (the entire path)
        // on graph2, with the pattern [Quantifier 0, Quantifier 1]
        [TestMethod]
        public void TestExtendDownwardsWithPattern3()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendDownwards(Graphs.graph2.FindNode("A"), ref pattern, 4);
            List<String> expected = new List<String>() { "A", "B", "L", "N" };
            Assert.AreEqual(expected.Count, result.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], result[i].Id);
            }
        }

        // Pattern of size 9, but bound to 6 (won't complete a cycle)
        // on graph2, with the pattern
        // [Quantifier 0, Quantifier 2, Quantifier3, ... , Quantifier 9]
        [TestMethod]
        public void TestExtendDownWardsWithPattern3()
        {
            List<Quantifier> pattern =
                new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[2], Graphs.Quants[3],
                Graphs.Quants[4], Graphs.Quants[5], Graphs.Quants[6], Graphs.Quants[7],
                Graphs.Quants[8], Graphs.Quants[9]};
            List<Node> result =
                DAGView.ExtendDownwards(Graphs.graph2.FindNode("A"), ref pattern, 6);
            List<String> expected = new List<String>() { "A", "C", "D", "E", "F", "G" };
            Assert.AreEqual(expected.Count, result.Count);
            for (int i= 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], result[i].Id);
            }
        }

        // Pattern of size 2, bound of 10
        // on grpah 3 with the patter [Quantifier 0, Quantifier 1]
        // For ever node, if possible, always choose a child that can be extended one more time.
        [TestMethod]
        public void TestExtendDownWardsWithPattern4()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendDownwards(Graphs.graph2.FindNode("A"), ref pattern, 10);
            List<string> expected =
                new List<string>() { "A", "C", "E", "G" };
            Assert.AreEqual(5, result.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(expected[i], result[i].Id);
            }
            Assert.IsTrue((result[4].Id == "H") | (result[4].Id == "I"));
        }
    }
}
