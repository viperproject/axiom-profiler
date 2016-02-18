﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Z3AxiomProfiler.PrettyPrinting;
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
        private GeneralizationState gen;

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

        public GeneralizationState getGeneralization()
        {
            if (!processed) findCycle();
            return gen;
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
            gen = new GeneralizationState(suffixTree.getCycleLength(), getCycleInstantiations());
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
        private int genCounter = 1;
        private readonly List<Instantiation>[] loopInstantiations;
        public readonly List<Term> generalizedTerms = new List<Term>();
        private readonly List<Term> blameHighlightTerms = new List<Term>();
        private readonly List<Term> bindHighlightTerms = new List<Term>();
        private readonly List<Term> eqHighlightTerms = new List<Term>();
        private readonly Dictionary<int, Term> replacementDict = new Dictionary<int, Term>();

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
                var j = (i + 1) % loopInstantiations.Length;
                generalizedTerms.Add(generalizeYieldTermPointWise(loopInstantiations[i], loopInstantiations[j], j < i));
            }
        }

        private Term generalizeYieldTermPointWise(List<Instantiation> parentInsts, List<Instantiation> childInsts, bool flipped)
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
            var concreteHistory = new Stack<Term>();
            var generalizedHistory = new Stack<Term>();
            var idx = flipped ? Math.Max(childInsts.Count / 2 - 1, 0) : childInsts.Count / 2;

            while (true)
            {
                // check for backtrack condition
                if (generalizedHistory.Count > 0)
                {
                    var mostRecent = generalizedHistory.Peek();
                    if (mostRecent.Args.Length == 0 || mostRecent.Args[0] != null)
                    {
                        // this term is full --> backtrack

                        if (generalizedHistory.Count == 1)
                        {
                            // were about to pop the generalized root --> finished
                            return mostRecent;
                        }

                        // all subterms connected -> pop parent
                        generalizedHistory.Pop();
                        concreteHistory.Pop();
                        foreach (var stack in todoStacks)
                        {
                            stack.Pop();
                        }
                        continue;
                    }
                }

                // find candidates for the next term.
                foreach (var currentTerm in todoStacks.Select(stack => stack.Peek()))
                {
                    collectCandidateTerm(currentTerm, candidates);
                }

                var currTerm = getGeneralizedTerm(candidates, todoStacks, generalizedHistory);

                // check for blame / binding info
                if (isBlameTerm(childInsts, todoStacks, concreteHistory, flipped))
                {
                    blameHighlightTerms.Add(currTerm);
                }
                else if (isBindTerm(childInsts, todoStacks, concreteHistory, flipped))
                {
                    bindHighlightTerms.Add(currTerm);
                }
                else if (isEqTerm(childInsts, todoStacks, concreteHistory, flipped))
                {
                    eqHighlightTerms.Add(currTerm);
                }

                // always push the generalized term, because it is one term 'behind' the others
                generalizedHistory.Push(currTerm);
                concreteHistory.Push(todoStacks[idx].Peek());
                // push children if applicable
                if (currTerm.Args.Length > 0)
                {
                    pushSubterms(todoStacks);
                }

                // reset candidates for next round
                candidates.Clear();
            }
        }

        private bool isBlameTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<Term> concreteHistory, bool flipped)
        {
            var childIdx = childInsts.Count / 2;
            var robustIdx = flipped ? Math.Max(childIdx - 1, 0) : childIdx;
            var checkInst = childInsts[childIdx];
            var term = checkInst.Responsible.FirstOrDefault(t => t.id == todoStacks[robustIdx].Peek().id);

            if (term == null) return false;

            var constraints = checkInst.bindingInfo.matchContext[term.id];
            return constraintsSat(concreteHistory.Reverse().ToList(), constraints);
        }

        private bool isBindTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<Term> concreteHistory, bool flipped)
        {
            var childIdx = childInsts.Count / 2;
            var robustIdx = flipped ? Math.Max(childIdx - 1, 0) : childIdx;
            var checkInst = childInsts[childIdx];
            var term = checkInst.Bindings.FirstOrDefault(t => t.id == todoStacks[robustIdx].Peek().id);

            if (term == null) return false;

            var constraints = checkInst.bindingInfo.matchContext[term.id];
            return constraintsSat(concreteHistory.Reverse().ToList(), constraints);
        }

        private bool isEqTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<Term> concreteHistory, bool flipped)
        {
            var childIdx = childInsts.Count / 2;
            var robustIdx = flipped ? Math.Max(childIdx - 1, 0) : childIdx;
            var checkInst = childInsts[childIdx];
            Term term = null;
            foreach (var equality in checkInst.bindingInfo.equalities)
            {
                term = equality.Value.FirstOrDefault(t => t.id == todoStacks[robustIdx].Peek().id);
                if (term != null) break;
            }

            if (term == null) return false;

            var constraints = checkInst.bindingInfo.matchContext[term.id];
            return constraintsSat(concreteHistory.Reverse().ToList(), constraints);
        }

        private bool constraintsSat(List<Term> gerneralizeHistory, List<List<Term>> constraints)
        {
            if (constraints.Count == 0) return true;
            return (from constraint in constraints
                    where constraint.Count <= gerneralizeHistory.Count
                    let slice = gerneralizeHistory.GetRange(gerneralizeHistory.Count - constraint.Count, constraint.Count)
                    select slice.Zip(constraint, (term1, term2) => term1.id == term2.id))
                                .Any(intermediate => intermediate.All(val => val));
        }

        private Term getGeneralizedTerm(Dictionary<string, Tuple<int, string, int>> candidates, Stack<Term>[] todoStacks, Stack<Term> generalizedHistory)
        {
            Term currTerm;
            if (candidates.Count == 1)
            {
                // consensus -> decend further
                var value = candidates.Values.First();
                currTerm = new Term(value.Item2, new Term[value.Item3]) { id = idCounter };
                idCounter--;
            }
            else
            {
                // no consensus --> generalize
                // todo: if necessary, detect outlier
                currTerm = getGeneralizedTerm(todoStacks);
            }
            addToGeneralizedTerm(generalizedHistory, currTerm);
            return currTerm;
        }

        private static void addToGeneralizedTerm(Stack<Term> generalizedHistory, Term currTerm)
        {
            var genParent = generalizedHistory.Count > 0 ? generalizedHistory.Peek() : null;
            // connect to parent
            if (genParent != null)
            {
                var idx = Array.FindLastIndex(genParent.Args, t => t == null);
                genParent.Args[idx] = currTerm;
            }
        }

        private Term getGeneralizedTerm(Stack<Term>[] todoStacks)
        {
            var guardTerm = todoStacks[0].Peek();
            var newTerm = true;

            if (replacementDict.ContainsKey(guardTerm.id))
            {
                // can only reuse existing term if ALL replaced terms agree on the same generalization.
                var existingGenTerm = replacementDict[guardTerm.id];
                for (var i = 1; i < todoStacks.Length; i++)
                {
                    newTerm = newTerm && existingGenTerm == replacementDict[todoStacks[i].Peek().id];
                }
                if (newTerm) return existingGenTerm;
            }
            var t = new Term("generalized_replacement_" + genCounter, new Term[0]) { id = idCounter };
            idCounter--;
            genCounter++;

            foreach (var stack in todoStacks)
            {
                replacementDict[stack.Peek().id] = t;
            }
            return t;
        }

        private static void pushSubterms(IEnumerable<Stack<Term>> todoStacks)
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

        public void tempHighlightBlameBindTerms(PrettyPrintFormat format)
        {
            foreach (var term in blameHighlightTerms)
            {
                term.highlightTemporarily(format, Color.Coral);
            }
            foreach (var term in bindHighlightTerms)
            {
                term.highlightTemporarily(format, Color.DeepSkyBlue);
            }
            foreach (var term in eqHighlightTerms)
            {
                term.highlightTemporarily(format, Color.Goldenrod);
            }
        }
    }
}
