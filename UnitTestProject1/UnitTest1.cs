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
            List<Quantifier> expected1 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected1));
        }

        // Test if AllDownPatterns recogizes pattern that contain
        // multiple occurences of the same quantifier in the same cycle
        [TestMethod]
        public void TestAllDownPattern5()
        {
            List<List<Quantifier>> result = DAGView.AllDownPatterns(Graphs.graph6.FindNode("A"), 8);
            List<Quantifier> expected1 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Quantifier> expected2 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1], Graphs.Quants[0], Graphs.Quants[2] };
            List<Quantifier> expected3 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1],
                Graphs.Quants[0], Graphs.Quants[2], Graphs.Quants[0], Graphs.Quants[1] };
            List<Quantifier> expected4 = new List<Quantifier>() { Graphs.Quants[0] };
            List<Quantifier> expected5 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[0], Graphs.Quants[1] };
            List<Quantifier> expected6 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[0], Graphs.Quants[1], Graphs.Quants[0] };
            Assert.AreEqual(6, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected1));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected2));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected3));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected4));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected5));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected6));
        }
    }

    [TestClass]
    public class ExtendDownards
    {
        static TestGraphs Graphs = new TestGraphs();

        // trivial case graph with one node a a pattern
        [TestMethod]
        public void TestExtenDownwards1()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendDownwards(Graphs.graph1.FindNode("A"), ref pattern, 10);
            Assert.AreEqual(1, result.Count);
        }

        // small pattern with bound larger then the path
        // on graph 2, with pattern [Quantifier 0]
        [TestMethod]
        public void TestExtendDownwards2()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0] };
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
        public void TestExtendDownwards3()
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
        public void TestExtendDownWards4()
        {
            List<Quantifier> pattern =
                new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[2], Graphs.Quants[3],
                Graphs.Quants[4], Graphs.Quants[5], Graphs.Quants[6], Graphs.Quants[7],
                Graphs.Quants[8], Graphs.Quants[9]};
            List<Node> result =
                DAGView.ExtendDownwards(Graphs.graph2.FindNode("A"), ref pattern, 6);
            List<String> expected = new List<String>() { "A", "C", "D", "E", "F", "G" };
            Assert.AreEqual(expected.Count, result.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], result[i].Id);
            }
        }

        // Pattern of size 2, bound of -1 (unlimited, goes as further as it can)
        // on grpah 3 with the patter [Quantifier 0, Quantifier 1]
        // For every node, if possible, always choose a child that can be extended one more time.
        [TestMethod]
        public void TestExtendDownWards5()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendDownwards(Graphs.graph3.FindNode("A"), ref pattern, -1);
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

    // similar tests with AllDownPattern but with reversed graph
    [TestClass]
    public class AllUpPatternsTest
    {
        static TestGraphs Graphs = new TestGraphs();

        // Travial Case where the node has no dependent nodes
        [TestMethod]
        public void TestAllUpPatterns1()
        {
            List<List<Quantifier>> result = DAGView.AllDownPatterns(Graphs.graph1.FindNode("A"), 8);
            Assert.AreEqual(0, result.Count);
        }

        // Test on bigger graph with minimum bound
        [TestMethod]
        public void TestAllUpPattern2()
        {
            List<List<Quantifier>> result = DAGView.AllUpPatterns(Graphs.graph4.FindNode("A"), 1);
            List<Quantifier> expected = new List<Quantifier>() { Graphs.Quants[0] };
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected));
        }

        // Test on bigger graph with default bound
        [TestMethod]
        public void TestAllUpPattern3()
        {
            List<List<Quantifier>> result = DAGView.AllUpPatterns(Graphs.graph4.FindNode("A"), 8);
            List<Quantifier> expected1 = new List<Quantifier>() { Graphs.Quants[0] };
            List<Quantifier> expected2 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected1));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected2));
        }

        // Test that AllDownPatterns return unique patterns
        [TestMethod]
        public void TestAllUpPattern4()
        {
            List<List<Quantifier>> result = DAGView.AllUpPatterns(Graphs.graph5.FindNode("A"), 8);
            List<Quantifier> expected1 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected1));
        }

        // Test if AllDownPatterns recogizes pattern that contain
        // multiple occurences of the same quantifier in the same cycle
        [TestMethod]
        public void TestAllUpPatterns5()
        {
            List<List<Quantifier>> result = DAGView.AllUpPatterns(Graphs.graph7.FindNode("A"), 8);
            List<Quantifier> expected1 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Quantifier> expected2 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1], Graphs.Quants[0], Graphs.Quants[2] };
            List<Quantifier> expected3 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1],
                Graphs.Quants[0], Graphs.Quants[2], Graphs.Quants[0], Graphs.Quants[1] };
            List<Quantifier> expected4 = new List<Quantifier>() { Graphs.Quants[0] };
            List<Quantifier> expected5 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[0], Graphs.Quants[1] };
            List<Quantifier> expected6 = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[0], Graphs.Quants[1], Graphs.Quants[0] };
            Assert.AreEqual(6, result.Count);
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected1));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected2));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected3));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected4));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected5));
            Assert.IsTrue(DAGView.ContainPattern(ref result, ref expected6));
        }
    }

    // Similar to ExtendDownwards, but up
    [TestClass]
    public class ExtendUpwardsTest
    {
        static TestGraphs Graphs = new TestGraphs();

        // trivial case graph with one node a a pattern
        [TestMethod]
        public void TestExtenUpwards1()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendUpwards(Graphs.graph1.FindNode("A"), ref pattern, 10);
            Assert.AreEqual(1, result.Count);
        }

        // small pattern with bound larger then the path
        // on graph 2, with pattern [Quantifier 0]
        [TestMethod]
        public void TestExtendUpards2()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0] };
            List<Node> result =
                DAGView.ExtendUpwards(Graphs.graph4.FindNode("A"), ref pattern, 3);
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
        public void TestExtendUpwards3()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendUpwards(Graphs.graph4.FindNode("A"), ref pattern, 4);
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
        public void TestExtendUpWards4()
        {
            List<Quantifier> pattern =
                new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[2], Graphs.Quants[3],
                Graphs.Quants[4], Graphs.Quants[5], Graphs.Quants[6], Graphs.Quants[7],
                Graphs.Quants[8], Graphs.Quants[9]};
            List<Node> result =
                DAGView.ExtendUpwards(Graphs.graph4.FindNode("A"), ref pattern, 6);
            List<String> expected = new List<String>() { "A", "C", "D", "E", "F", "G" };
            Assert.AreEqual(expected.Count, result.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], result[i].Id);
            }
        }

        // Pattern of size 2, bound of -1 (unlimited, goes as further as it can)
        // on grpah 3 with the patter [Quantifier 0, Quantifier 1]
        // For every node, if possible, always choose a child that can be extended one more time.
        [TestMethod]
        public void TestExtendUpWards5()
        {
            List<Quantifier> pattern = new List<Quantifier>() { Graphs.Quants[0], Graphs.Quants[1] };
            List<Node> result =
                DAGView.ExtendUpwards(Graphs.graph5.FindNode("A"), ref pattern, -1);
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

    // Tests for the CustomPathComparer
    [TestClass]
    public class CustompathComparerTestClass
    {
        // tuple where
        // item1 is the length of a pth
        // item2 is the nubmer of uncovered children
        // item3 is the length of pattern
        // item 4 is reference to the pattern
        static Tuple<int, int, int, int> t0 = new Tuple<int, int, int, int>(1, 0, 1, 0);
        static Tuple<int, int, int, int> t1 = new Tuple<int, int, int, int>(1, 0, 5, 1);
        static Tuple<int, int, int, int> t2 = new Tuple<int, int, int, int>(10, 15, 3, 2);
        static Tuple<int, int, int, int> t3 = new Tuple<int, int, int, int>(8, 8, 3, 3);
        static Tuple<int, int, int, int> t4 = new Tuple<int, int, int, int>(8, 0, 3, 4);
        static Tuple<int, int, int, int> t5 = new Tuple<int, int, int, int>(6, 8, 3, 5);
        static Tuple<int, int, int, int> t6 = new Tuple<int, int, int, int>(7, 3, 3, 6);
        static Tuple<int, int, int, int> t7 = new Tuple<int, int, int, int>(7, 3, 3, 7);

        public bool HelperFunction(Tuple<int, int, int, int> path1, Tuple<int, int, int, int> path2)
        {
            // check path 2 does not have size lager than path1
            // path1 should have longer path
            if (path2.Item1 > path1.Item1) return false;
            if (path2.Item1 < path1.Item1) return true;

            // If path1 and 2 have the same size,
            // check path2 has more uncovered children than path1
            // path one should have less uncovered children
            if (path2.Item2 < path1.Item2) return false;
            if (path2.Item2 > path1.Item2) return true;

            // if path2 and path1 have the number of uncovered childre
            // check path2 has longer or equal pattern size than path1
            // path1 should have shorter pattern size
            if (path2.Item3 >= path1.Item3) return true;
            return false;
        }

        // travial case
        // list of length 1
        [TestMethod]
        public void TestCustomPathComparer1()
        {
            List<Tuple<int, int, int, int>> testList = new List<Tuple<int, int, int, int>>() { t0 };
            testList.Sort((elem1, elem2) => DAGView.CustomPathComparer(ref elem1, ref elem2));
            for (int i = 1; i < testList.Count; i++)
            {
                Assert.IsTrue(HelperFunction(testList[i - 1], testList[i]));
            }
        }

        // test case where there are only 2 elements, t3 and t5
        // where the only difference in t3 and t5 is the length (and reference)
        [TestMethod]
        public void TestCustomComparer2()
        {
            List<Tuple<int, int, int, int>> testList =
                new List<Tuple<int, int, int, int>>() { t3, t5 };
            testList.Sort((elem1, elem2) => DAGView.CustomPathComparer(ref elem1, ref elem2));
            for (int i = 1; i < testList.Count; i++)
            {
                Assert.IsTrue(HelperFunction(testList[i - 1], testList[i]));
            }
        }

        // testcase where there are only 2 elements, t3, t4
        // where the only difference is the number of children uncovered
        [TestMethod]
        public void TestCustomComparer3()
        {
            List<Tuple<int, int, int, int>> testList =
                new List<Tuple<int, int, int, int>>() { t3, t4 };
            testList.Sort((elem1, elem2) => DAGView.CustomPathComparer(ref elem1, ref elem2));
            for (int i = 1; i < testList.Count; i++)
            {
                Assert.IsTrue(HelperFunction(testList[i - 1], testList[i]));
            }
        }

        // testcase where there are only 2 elements t0, t1
        // where the only difference is the length of pattern
        [TestMethod]
        public void TestCustomComparer4()
        {
            List<Tuple<int, int, int, int>> testList =
                new List<Tuple<int, int, int, int>>() { t0, t1 };
            testList.Sort((elem1, elem2) => DAGView.CustomPathComparer(ref elem1, ref elem2));
            for (int i = 1; i < testList.Count; i++)
            {
                Assert.IsTrue(HelperFunction(testList[i - 1], testList[i]));
            }
        }

        // testcase where there are many elements
        [TestMethod]
        public void TestCustomComparer5()
        {
            List<Tuple<int, int, int, int>> testList =
                new List<Tuple<int, int, int, int>>() { t0, t1, t2, t3, t4, t5, t6, t7 };
            testList.Sort((elem1, elem2) => DAGView.CustomPathComparer(ref elem1, ref elem2));
            for (int i = 1; i < testList.Count; i++)
            {
                Assert.IsTrue(HelperFunction(testList[i - 1], testList[i]));
            }
        }
    }

}