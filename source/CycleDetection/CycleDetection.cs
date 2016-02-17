using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Z3AxiomProfiler.QuantifierModel;

namespace Z3AxiomProfiler.CycleDetection
{
    public class CycleDetection
    {
        private readonly IEnumerable<Instantiation> path;
        private bool processed;
        private readonly int minRepetitions;
        private const char endChar = char.MaxValue;
        private char currMap = char.MinValue;
        private readonly Dictionary<string, char> mapping = new Dictionary<string, char>();
        private readonly Dictionary<char, List<Instantiation>> reverseMapping = new Dictionary<char, List<Instantiation>>();
        private SuffixTree.SuffixTree suffixTree;

        public CycleDetection(IEnumerable<Instantiation> pathToCheck, int minRep)
        {
            path = pathToCheck;
            minRepetitions = minRep;
        }

        public bool hasCycle()
        {
            if (!processed) findCycle();
            return suffixTree.hasCycle();
        }

        public List<Quantifier> getCycleQuantifiers()
        {
            if (!processed) findCycle();
            var result = new List<Quantifier>();
            if (!hasCycle()) return result;
            result.AddRange(suffixTree.getCycle()
                .Where(c => c != endChar)
                .Select(c => reverseMapping[c].First().Quant));
            return result;
        }

        private void findCycle()
        {
            var chars = new List<char>();

            // map the instantiations
            foreach (var instantiation in path)
            {
                var key = instantiation.Quant.BodyTerm.id + "" +
                    instantiation.bindingInfo.fullPattern.id + "" +
                    instantiation.bindingInfo.numEq;
                if (!mapping.ContainsKey(key))
                {
                    mapping[key] = currMap;
                    currMap++;
                }
                var charValue = mapping[key];
                chars.Add(charValue);
                if (!reverseMapping.ContainsKey(charValue))
                {
                    reverseMapping[charValue] = new List<Instantiation>();
                }
                reverseMapping[charValue].Add(instantiation);
            }
            chars.Add(endChar);

            // search for cycles
            suffixTree = new SuffixTree.SuffixTree(chars.Count, minRepetitions);
            foreach (var c in chars)
            {
                suffixTree.addChar(c);
            }
            processed = true;
            var gen = new GeneralizationState(suffixTree.getCycleLength(), getCycleInstantiations());
            gen.generalize();
        }

        public List<Instantiation> getCycleInstantiations()
        {
            if (!processed) findCycle();
            // return empty list if there is no cycle
            return !hasCycle() ? new List<Instantiation>() :
                path.Skip(suffixTree.getStartIdx()).Take(suffixTree.getCycleLength() * suffixTree.nRep).ToList();
        }

    }

    public class GeneralizationState
    {
        private int idCounter = -2;
        private readonly List<Instantiation>[] loopInstantiations;
        private List<Term> generalizedTerms = new List<Term>();
        private List<Term> blameHighlightTerms = new List<Term>();
        private List<Term> bindHighlightTerms = new List<Term>();
        private Dictionary<int, Term> replacementDict = new Dictionary<int, Term>();

        public GeneralizationState(int cycleLength, IEnumerable<Instantiation> instantiations)
        {
            loopInstantiations = new List<Instantiation>[cycleLength];
            for (var i = 0; i < loopInstantiations.Length; i++)
            {
                loopInstantiations[i] = new List<Instantiation>();
            }

            var index = 0;
            foreach (var instantiation in instantiations)
            {
                loopInstantiations[index].Add(instantiation);
                index = ++index % loopInstantiations.Length;
            }
        }

        public void generalize()
        {
            for (var i = 0; i < loopInstantiations.Length; i++)
            {
                var j = ++i % loopInstantiations.Length;
                generalizeYieldTermPointWise(loopInstantiations[i], loopInstantiations[j]);
            }
        }

        private void generalizeYieldTermPointWise(List<Instantiation> parentInsts, List<Instantiation> childInsts)
        {
            // queues for breath first traversal of all terms in parallel
            var todoStacks = parentInsts
                .Select(inst => inst.dependentTerms.Last())
                .Where(t => t != null)
                .Select(t => new Stack<Term>(new[] { t }))
                .ToArray();

            // map to 'vote' on generalization
            // also exposes outliers
            // term name + type + #Args -> #votes
            var candidates = new Dictionary<string, Tuple<int, string, int>>();

            
            var generalizedStack = new Stack<Term>();
            generalizedStack.Push(new Term("generalization root", new Term[1]));

            var generalizedHistory = new Stack<Term>();

            while (todoStacks[0].Count > 0)
            {
                // find candidates for the next term.
                foreach (var currentTerm in todoStacks.Select(queue => queue.Peek()))
                {
                    collectCandidateTerm(currentTerm, candidates);
                }

                if (candidates.Count == 1)
                {
                    // consensus -> decend further

                    var value = candidates.Values.First();
                    var currTerm = new Term(value.Item2, new Term[value.Item3]) {id = idCounter};
                    idCounter--;
                    var parent = generalizedHistory.Count > 0 ? generalizedHistory.Peek() : null;

                    // connect to parent
                    if (parent != null)
                    {
                        var idx = Array.FindIndex(parent.Args, t => t == null);
                        parent.Args[idx] = currTerm;
                    }

                    if (currTerm.Args.Length > 0)
                    {
                        foreach (var stack in todoStacks)
                        {
                            var curr = stack.Peek();
                            foreach (var subterm in curr.Args)
                            {
                                stack.Push(subterm);
                            }
                        }
                        
                    }
                    generalizedHistory.Push(currTerm);
                    generalizedStack.Push(currTerm);
                }
                else
                {
                    // no consensus --> abstract
                }
            }
        }

        private static void collectCandidateTerm(Term currentTerm, Dictionary<string, Tuple<int, string, int>> candidates)
        {
            var key = currentTerm.Name + currentTerm.GenericType + currentTerm.Args.Length;
            if (!candidates.ContainsKey(key))
            {
                candidates[key] = new Tuple<int, string, int>
                    (0, currentTerm.Name + currentTerm.GenericType, currentTerm.Args.Length);
            }
            else
            {
                var oldTuple = candidates[key];
                candidates[key] = new Tuple<int, string, int>
                    (oldTuple.Item1 + 1, oldTuple.Item2, oldTuple.Item3);
            }
        }
    }
}
