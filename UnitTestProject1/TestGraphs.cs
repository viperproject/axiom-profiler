using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Msagl.Drawing;
using AxiomProfiler;
using AxiomProfiler.QuantifierModel;

namespace UnitTestProject1
{
    // A visulization and explination for the test graphs can be found here
    // https://docs.google.com/document/d/1SJspfBecgkVT9U8xC-MvQ_NPDTGVfg0yqq7YjJ3ma2s/edit?usp=sharing
    class TestGraphs
    {
        public List<Quantifier> Quants = new List<Quantifier>(); // Quantifiers used in unit tests
        public BindingInfo Info; // BindingInfo used to make nodes
        public Graph graph1;
        public Node node1;
        public TestGraphs()
        {
            InitQuantsAndInfo();
            MakeGraph1();
            node1 = new Node("a");
        }

        // initialize 9 quantifiers
        private void InitQuantsAndInfo()
        {
            // initialize Quants
            for (int i = 0; i < 9; i++)
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

        //Graph one is graph containing only one node
        private void MakeGraph1()
        {
            graph1 = new Graph();
            graph1.AddNode(MakeNode("A", 0));
        }
    }
}
