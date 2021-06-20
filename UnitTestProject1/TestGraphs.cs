using System;
using System.Collections.Generic;
using Microsoft.Msagl.Drawing;
using AxiomProfiler.QuantifierModel;

namespace UnitTestProject1
{
    // A visulization and explination for the test graphs can be found here
    // https://docs.google.com/document/d/1SJspfBecgkVT9U8xC-MvQ_NPDTGVfg0yqq7YjJ3ma2s/edit?usp=sharing
    // Some graphs 
    class TestGraphs
    {
        public List<Quantifier> Quants = new List<Quantifier>(); // Quantifiers used in unit tests
        public BindingInfo Info; // BindingInfo used to make nodes
        public Graph graph1, graph2, graph3, graph4, graph5;

        // Constructor
        // Mkae the graphs needsed for testing
        public TestGraphs()
        {
            InitQuantsAndInfo();
            MakeGraph1();
            MakeGraph2();
            MakeGraph3();
            MakeGraph4();
            MakeGraph5();
        }

        // initialize 9 quantifiers
        private void InitQuantsAndInfo()
        {
            // initialize Quants
            for (int i = 0; i < 10; i++)
            {
                Quants.Add(new Quantifier());
                Quants[i].PrintName = i.ToString();
            }
            // initialize Info, with meaningless arguments
            Term[] args = new Term[0];
            Term Term = new Term("a", args);
            Info = new BindingInfo(Quants[0], args, args);
        }

        // function that make of node with Id = nodeId,
        // and UserDate = a instantiation with quantifier quant
        private Node MakeNode(String nodeId, int quant)
        {
            Node node = new Node(nodeId);
            Instantiation Inst = new Instantiation(Info, "a");
            Inst.Quant = Quants[quant];
            node.UserData = Inst;
            return node;
        }

        // Graph1 is graph containing only one node
        private void MakeGraph1()
        {
            graph1 = new Graph();
            graph1.AddNode(MakeNode("A", 0));
        }

        // Graph2
        private void MakeGraph2()
        {
            graph2 = new Graph();
            graph2.AddNode(MakeNode("A", 0));
            graph2.AddNode(MakeNode("B", 1));
            graph2.AddNode(MakeNode("C", 2));
            graph2.AddNode(MakeNode("D", 3));
            graph2.AddNode(MakeNode("E", 4));
            graph2.AddNode(MakeNode("F", 5));
            graph2.AddNode(MakeNode("G", 6));
            graph2.AddNode(MakeNode("H", 7));
            graph2.AddNode(MakeNode("I", 8));
            graph2.AddNode(MakeNode("J", 9));
            graph2.AddNode(MakeNode("K", 0));
            graph2.AddNode(MakeNode("L", 0));
            graph2.AddNode(MakeNode("M", 0));
            graph2.AddNode(MakeNode("N", 1));
            char prev = 'A';
            for (char c = 'C'; c <= 'K'; c++)
            {
                graph2.AddEdge(prev.ToString(), c.ToString());
                prev = c;
            }
            graph2.AddEdge("A", "B");
            graph2.AddEdge("B", "L");
            graph2.AddEdge("L", "N");
            graph2.AddEdge("A", "M");
        }

        private void MakeGraph3()
        {
            graph3 = new Graph();
            graph3.AddNode(MakeNode("A", 0));
            graph3.AddNode(MakeNode("B", 1));
            graph3.AddNode(MakeNode("C", 1));
            graph3.AddNode(MakeNode("D", 0));
            graph3.AddNode(MakeNode("E", 0));
            graph3.AddNode(MakeNode("F", 1));
            graph3.AddNode(MakeNode("G", 1));
            graph3.AddNode(MakeNode("H", 0));
            graph3.AddNode(MakeNode("I", 0));
            graph3.AddEdge("A", "B");
            graph3.AddEdge("A", "C");
            graph3.AddEdge("C", "D");
            graph3.AddEdge("C", "E");
            graph3.AddEdge("E", "F");
            graph3.AddEdge("E", "G");
            graph3.AddEdge("G", "H");
            graph3.AddEdge("G", "I");
        }

        private void MakeGraph4()
        {
            graph4 = new Graph();
            graph4.AddNode(MakeNode("A", 0));
            graph4.AddNode(MakeNode("B", 1));
            graph4.AddNode(MakeNode("C", 2));
            graph4.AddNode(MakeNode("D", 3));
            graph4.AddNode(MakeNode("E", 4));
            graph4.AddNode(MakeNode("F", 5));
            graph4.AddNode(MakeNode("G", 6));
            graph4.AddNode(MakeNode("H", 7));
            graph4.AddNode(MakeNode("I", 8));
            graph4.AddNode(MakeNode("J", 9));
            graph4.AddNode(MakeNode("K", 0));
            graph4.AddNode(MakeNode("L", 0));
            graph4.AddNode(MakeNode("M", 0));
            graph4.AddNode(MakeNode("N", 1));
            char prev = 'K';
            for (char c = 'J'; c >= 'C'; c--)
            {
                graph4.AddEdge(prev.ToString(), c.ToString());
                prev = c;
            }
            graph4.AddEdge("C", "A");
            graph4.AddEdge("B", "A");
            graph4.AddEdge("L", "B");
            graph4.AddEdge("N", "L");
            graph4.AddEdge("M", "A");
        }

        private void MakeGraph5()
        {
            graph5 = new Graph();
            graph5.AddNode(MakeNode("A", 0));
            graph5.AddNode(MakeNode("B", 1));
            graph5.AddNode(MakeNode("C", 1));
            graph5.AddNode(MakeNode("D", 0));
            graph5.AddNode(MakeNode("E", 0));
            graph5.AddNode(MakeNode("F", 1));
            graph5.AddNode(MakeNode("G", 1));
            graph5.AddNode(MakeNode("H", 0));
            graph5.AddNode(MakeNode("I", 0));
            graph5.AddEdge("B", "A");
            graph5.AddEdge("C", "A");
            graph5.AddEdge("D", "C");
            graph5.AddEdge("E", "C");
            graph5.AddEdge("F", "E");
            graph5.AddEdge("G", "E");
            graph5.AddEdge("H", "G");
            graph5.AddEdge("I", "G");
        }
    }
}
