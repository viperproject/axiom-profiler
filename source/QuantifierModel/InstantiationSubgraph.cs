using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.CycleDetection;
using AxiomProfiler.PrettyPrinting;
using System;
using AxiomProfiler.Utilities;
using Microsoft.Msagl.Drawing;
namespace AxiomProfiler.QuantifierModel
{
    class InstantiationSubgraph
    {
        private readonly List<List<Instantiation>> subgraphInstantiations;
        private int cycleSize;
        public InstantiationSubgraph(ref List<List<Node>> subgraph, int size)
        {
            cycleSize = size;
            subgraphInstantiations = new List<List<Instantiation>>();
            foreach (List<Node> cycle in subgraph)
            {
                if (cycle.Count != cycleSize) break;
                List<Instantiation> cycleInstantiations = new List<Instantiation>();
                foreach (Node node in cycle)
                {
                    cycleInstantiations.Add((Instantiation)(node.UserData));
                }
                subgraphInstantiations.Add(cycleInstantiations);
            }
        }
    }
}
