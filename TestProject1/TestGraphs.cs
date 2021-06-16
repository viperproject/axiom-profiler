using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Msagl.Drawing;
using AxiomProfiler;

namespace AxiomProfiler
{
    // A visulization and explination for the test graphs can be found here
    // https://docs.google.com/document/d/1SJspfBecgkVT9U8xC-MvQ_NPDTGVfg0yqq7YjJ3ma2s/edit?usp=sharing
    class TestGraphs
    {
        public List<QuantifierModel.Quantifier> Quants; // Quantifiers used in unit tests
        public QuantifierModel.BindingInfo Info; // BindingInfo used to make nodes
        public Graph graph1;

        public TestGraphs()
        {
            InitQuantsAndInfo();

        }

        // initialize 9 quantifiers
        private void InitQuantsAndInfo()
        {
            // initialize Quants
            for (int i = 0; i < 9; i++)
            {
                Quants.Add(new QuantifierModel.Quantifier());
                Quants[i].PrintName = i.ToString();
            }
            // initialize Info, with meaningless arguments
            QuantifierModel.Term[] args = new QuantifierModel.Term[0];
            QuantifierModel.Term Term = new QuantifierModel.Term("a", args);
            Info = new QuantifierModel.BindingInfo(Quants[0], args, args);
        }

        private Node MakeNode(String nodeId, int quant)
        {
            Node node = new Node(nodeId);
            QuantifierModel.Instantiation Inst = new QuantifierModel.Instantiation(Info, "a");
            Inst.Quant = Quants[quant];
            node.UserData = Inst;
            return node;
        }
    }
}
