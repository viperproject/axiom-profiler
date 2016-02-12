using System;
using System.Collections.Generic;
using System.Linq;


namespace Z3AxiomProfiler.SuffixTree
{
    // Ported from the java implementation found here:
    // http://stackoverflow.com/questions/9452701/ukkonens-suffix-tree-algorithm-in-plain-english
    public class SuffixTree
    {
        private const int oo = int.MaxValue / 2;
        readonly Node[] nodes;
        readonly char[] text;
        private readonly int root;
        private int position = -1;
        private int currentNode;
        private int needSuffixLink;
        private int remainder;

        private int active_node;
        private int active_length;
        private int active_edge;

        public SuffixTree(int length)
        {
            nodes = new Node[2 * length + 2];
            text = new char[length];
            root = newNode(-1, -1);
            active_node = root;
        }

        private void addSuffixLink(int node)
        {
            if (needSuffixLink > 0)
            {
                nodes[needSuffixLink].link = node;
            }
            needSuffixLink = node;
        }

        char getActiveEdgeText()
        {
            return text[active_edge];
        }

        bool walkDown(int next)
        {
            if (active_length < nodes[next].edgeLength(position)) return false;

            active_edge += nodes[next].edgeLength(position);
            active_length -= nodes[next].edgeLength(position);
            active_node = next;
            return true;
        }

        int newNode(int start, int end)
        {
            nodes[++currentNode] = new Node(start, end);
            return currentNode;
        }

        public void addChar(char c)
        {
            text[++position] = c;
            needSuffixLink = -1;
            remainder++;
            while (remainder > 0)
            {
                if (active_length == 0) active_edge = position;
                if (!nodes[active_node].next.ContainsKey(getActiveEdgeText()))
                {
                    int leaf = newNode(position, oo);
                    nodes[active_node].next[getActiveEdgeText()] = leaf;
                    addSuffixLink(active_node);
                }
                else
                {
                    int next = nodes[active_node].next[getActiveEdgeText()];
                    if (walkDown(next)) continue;
                    if (text[nodes[next].start + active_length] == c)
                    {
                        active_length++;
                        addSuffixLink(active_node);
                        break;
                    }
                    int split = newNode(nodes[next].start, nodes[next].start + active_length);

                    nodes[active_node].next[getActiveEdgeText()] = split;

                    int leaf = newNode(position, oo);
                    nodes[split].next[c] = leaf;
                    nodes[next].start += active_length;
                    nodes[split].next[text[nodes[next].start]] = next;
                    addSuffixLink(split);
                }
                remainder--;

                if (active_node == root && active_length > 0)
                {
                    active_length--;
                    active_edge = position - remainder + 1;
                }
                else
                    active_node = nodes[active_node].link > 0 ? nodes[active_node].link : root; //rule 3
            }
        }

        public void finalize()
        {
            var todo = new Stack<int>();
            todo.Push(root);
            var visited = new HashSet<int>();

            while (todo.Count > 0)
            {
                var current = todo.Peek();

                if (visited.Contains(current))
                {
                    foreach (var childNode in nodes[current].next.Values.Select(idx => nodes[idx]))
                    {
                        nodes[current].leafs.AddRange(childNode.leafs);
                    }
                    todo.Pop();
                }

                visited.Add(current);
                if (nodes[current].next.Count == 0)
                {
                    nodes[current].leafs.Add(nodes[current]);
                    continue;
                }
                foreach (var childIdx in nodes[current].next.Values)
                {
                    todo.Push(childIdx);
                }
            }
        }

        /*
        printing the Suffix Tree in a format understandable by graphviz. The output is written into
        st.dot file. In order to see the suffix tree as a PNG image, run the following command:
        dot -Tpng -O st.dot
        */

        string edgeString(int node)
        {
            var startIdx = nodes[node].start;
            var endIdx = Math.Min(position + 1, nodes[node].end);
            var length = endIdx - startIdx;
            var substring = new char[length];
            Array.Copy(text, startIdx, substring, 0, length);
            return new string(substring);
        }

        public void printTree()
        {
            Console.WriteLine("digraph {");
            Console.WriteLine("\trankdir = LR;");
            Console.WriteLine("\tedge [arrowsize=0.4,fontsize=10]");
            Console.WriteLine("\tnode1 [label=\"\",style=filled,fillcolor=lightgrey,shape=circle,width=.1,height=.1];");
            Console.WriteLine("//------leaves------");
            printLeaves(root);
            Console.WriteLine("//------internal nodes------");
            printInternalNodes(root);
            Console.WriteLine("//------edges------");
            printEdges(root);
            Console.WriteLine("//------suffix links------");
            printSLinks(root);
            Console.WriteLine("}");
        }

        void printLeaves(int x)
        {
            if (nodes[x].next.Count == 0)
            {
                Console.WriteLine("\tnode" + x + " [label=\"\",shape=point]");
            }
            else
            {
                foreach (int child in nodes[x].next.Values)
                {
                    printLeaves(child);
                }
            }
        }

        void printInternalNodes(int x)
        {
            if (x != root && nodes[x].next.Count > 0)
            {
                Console.WriteLine($"\tnode{x} [label=\"{nodes[x].leafs.Count}\",style=filled,fillcolor=lightgrey,shape=circle,width=.07,height=.07]");
            }

            foreach (int child in nodes[x].next.Values)
            {
                printInternalNodes(child);
            }
        }

        void printEdges(int x)
        {
            foreach (int child in nodes[x].next.Values)
            {
                Console.WriteLine("\tnode" + x + " -> node" + child + " [label=\"" + edgeString(child) + "\",weight=3]");
                printEdges(child);
            }
        }

        void printSLinks(int x)
        {
            if (nodes[x].link > 0)
            {
                Console.WriteLine("\tnode" + x + " -> node" + nodes[x].link + " [label=\"\",weight=1,style=dotted]");
            }
            foreach (int child in nodes[x].next.Values)
            {
                printSLinks(child);
            }
        }
    }

    class Node
    {

        /*
           There is no need to create an "Edge" class.
           Information about the edge is stored right in the node.
           [start; end) interval specifies the edge,
           by which the node is connected to its parent node.
        */

        public int start;
        public readonly int end;
        public int link;
        public readonly List<Node> leafs = new List<Node>(); 
        public readonly Dictionary<char, int> next = new Dictionary<char, int>();

        public Node(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        public int edgeLength(int position)
        {
            return Math.Min(end, position + 1) - start;
        }
    }
}
