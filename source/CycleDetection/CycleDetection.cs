using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.PrettyPrinting;
using AxiomProfiler.QuantifierModel;

namespace AxiomProfiler.CycleDetection
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
            var path = this.path;
            if (path.First().bindingInfo == null)
            {
                path = path.Skip(1);
            }
            foreach (var instantiation in path)
            {
                if (instantiation.bindingInfo == null)
                {
                    throw new Exception($"Cannot execute findCycle(): bindingInfo missing for {instantiation}");
                }
                var key = instantiation.Quant.BodyTerm.id + "" +
                    instantiation.bindingInfo.fullPattern.id + "";
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
                path.Skip(suffixTree.getStartIdx() + (path.First().bindingInfo == null ? 1 : 0)).Take(suffixTree.getCycleLength() * suffixTree.nRep).ToList();
        }

        public int getRepetiontions()
        {
            if (!processed) findCycle();
            return suffixTree.nRep;
        }
    }

    public class GeneralizationState
    {
        private int idCounter = -2;
        private int genCounter = 1;
        private readonly List<Instantiation>[] loopInstantiations;
        public readonly List<Term> generalizedTerms = new List<Term>();
        private readonly List<Term> genReplacements = new List<Term>();
        private readonly Dictionary<int, Term> replacementDict = new Dictionary<int, Term>();
        private readonly Dictionary<int, BindingInfo> generalizedBindings = new Dictionary<int, BindingInfo>();
        private BindingInfo wrapBindings = null;
        private HashSet<Term> loopProducedAssocBlameTerms = new HashSet<Term>();

        // associated info to generalized blame term 
        // (meaning other generalized blame terms that are not yield terms in the loop)
        public readonly Dictionary<Term, List<Term>> assocGenBlameTerm = new Dictionary<Term, List<Term>>();

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

        public bool IsReplaced(int id)
        {
            return replacementDict.ContainsKey(id);
        }

        public bool IsProducedByLoop(Term term)
        {
            return loopProducedAssocBlameTerms.Contains(term);
        }

        public void generalize()
        {
            for (var it = 0; it < loopInstantiations.Length+1; it++)
            {
                var i = (loopInstantiations.Length + it - 1) % loopInstantiations.Length;
                var j = it % loopInstantiations.Length;
                var generalizedYield = generalizeYieldTermPointWise(loopInstantiations[i], loopInstantiations[j], j <= i, it == loopInstantiations.Length);
                generalizedYield.dependentInstantiationsBlame.Add(loopInstantiations[i].First());
                generalizedTerms.Add(generalizedYield);

                // Other prerequisites:
                var robustIdx = loopInstantiations[i].Count / 2;
                var parent = loopInstantiations[i][j <= i ? Math.Max(robustIdx - 1, 0) : robustIdx];
                var child = loopInstantiations[j][robustIdx];

                var disitinctBlameTerms = child.bindingInfo.getDistinctBlameTerms();
                var idxList = Enumerable.Range(0, disitinctBlameTerms.Count)
                                        .Where(y => {
                                            var yTerm = disitinctBlameTerms[y];
                                            if (!parent.concreteBody.isSubterm(yTerm)) return true;

                                            //Always print terms that are used to expand some subterm of the parent's yield
                                            var matchedIn = child.bindingInfo.equalities.Where(eq => child.bindingInfo.bindings[eq.Key] == yTerm)
                                                    .SelectMany(eq => eq.Value)
                                                    .Select(rhs => child.bindingInfo.matchContext[rhs.id])
                                                    .SelectMany(contexts => contexts.Select(context => context.First()));
                                                return matchedIn.Any(topLevelBlame => parent.concreteBody.isSubterm(topLevelBlame));
                                        })
                                        .ToList();

                foreach (var index in idxList)
                {
                    var terms = loopInstantiations[j].Select(inst => inst.bindingInfo.getDistinctBlameTerms()[index]);
                    var otherGenTerm = generalizeTerms(terms, loopInstantiations[j], false, it == loopInstantiations.Length);
                    otherGenTerm.dependentInstantiationsBlame.Add(loopInstantiations[j].First());

                    //only a finite prefix of terms may be produced outside of the loop
                    var loopProduced = terms.Select(t => loopInstantiations.Any(qInsts => qInsts.Any(inst => inst.concreteBody.isSubterm(t))));
                    loopProduced = loopProduced.SkipWhile(c => !c);
                    if (loopProduced.Any(c => c) && loopProduced.All(c => c))
                    {
                        loopProducedAssocBlameTerms.Add(otherGenTerm);
                    }

                    if (!assocGenBlameTerm.ContainsKey(generalizedYield))
                    {
                        assocGenBlameTerm[generalizedYield] = new List<Term>();
                    }
                    assocGenBlameTerm[generalizedYield].Add(otherGenTerm);
                }

                loopInstantiations[j] = loopInstantiations[j].Select(inst =>
                {
                    var tmp = new Instantiation();
                    inst.CopyTo(tmp);
                    var newBody = GeneralizeBindings(inst.concreteBody, inst.Quant.BodyTerm.Args.Last(), generalizedBindings[inst.Quant.BodyTerm.id]);
                    if (newBody != null)
                    {
                        tmp.concreteBody = newBody;
                    }
                    else
                    {
                        //TODO: try partial generalization?
                        Console.Out.WriteLine($"couldn't generalize bindings for {inst}");
                    }
                    return tmp;
                }).ToList();
            }
            MarkGeneralizations(generalizedTerms.First(), generalizedTerms.Last());
        }

        private void MarkGeneralizations(Term loopStart, Term loopEnd)
        {
            if (genReplacements.Contains(loopStart))
            {
                genReplacements.Add(loopEnd);
            }
            else
            {
                for (int i = 0; i < loopStart.Args.Length; i++)
                {
                    MarkGeneralizations(loopStart.Args[i], loopEnd.Args[i]);
                }
            }
        }

        private Term generalizeYieldTermPointWise(List<Instantiation> parentInsts, List<Instantiation> childInsts, bool blameWrapAround, bool loopWrapAround)
        {
            var yieldTerms = parentInsts
                .Select(inst => inst.concreteBody)
                .Where(t => t != null);

            return generalizeTerms(yieldTerms, childInsts, blameWrapAround, loopWrapAround);
        }

        private static Term GeneralizeBindings(Term concrete, Term quantifier, BindingInfo bindingInfo)
        {
            if (quantifier.id == -1)
            {
                return bindingInfo.bindings[quantifier];
            }
            Term concreteContinue = concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name ? concrete.reverseRewrite : concrete;
            return GeneralizeChildrenBindings(concreteContinue, quantifier, bindingInfo);
        }

        private static Term GeneralizeChildrenBindings(Term concrete, Term quantifier, BindingInfo bindingInfo)
        {
            if (concrete == null) return null;
            Term copy = new Term(concrete);
            if (!quantifier.ContainsFreeVar()) return copy; 
            if (concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name) return null;
            for (int i = 0; i < concrete.Args.Count(); i++)
            {
                var replacement = GeneralizeBindings(concrete.Args[i], quantifier.Args[i], bindingInfo);
                if (replacement == null)
                {
                    return GeneralizeChildrenBindings(concrete.reverseRewrite, quantifier, bindingInfo);
                }
                copy.Args[i] = replacement;
            }
            return copy;
        }

        private static List<Term> AllFreeVarsIn(Term t)
        {
            if (t.id == -1)
            {
                return new List<Term>(new[] { t });
            }
            else
            {
                return t.Args.SelectMany(AllFreeVarsIn).ToList();
            }
        }

        private Term generalizeTerms(IEnumerable<Term> terms, List<Instantiation> highlightInfoInsts, bool blameWrapAround, bool loopWrapAround)
        {
            // stacks for breath first traversal of all terms in parallel
            var todoStacks = terms.Select(t => new Stack<Term>(new[] { t })).ToArray();

            // map to 'vote' on generalization
            // also exposes outliers
            // term name + type + #Args -> #votes
            // last element is id (kept if all terms agree)
            var candidates = new Dictionary<string, Tuple<int, string, int, int>>();
            var concreteHistory = new Stack<Term>();
            var generalizedHistory = new Stack<Term>();

            var idx = blameWrapAround ? Math.Max(highlightInfoInsts.Count / 2 - 1, 0) : highlightInfoInsts.Count / 2;

            BindingInfo localBindingInfo;
            
            if (loopWrapAround)
            {
                if (wrapBindings == null)
                {
                    localBindingInfo = highlightInfoInsts[highlightInfoInsts.Count / 2].bindingInfo.clone();
                    localBindingInfo.matchContext.Clear();
                }
                else {
                    localBindingInfo = wrapBindings;
                }
            }
            else
            {
                if (!generalizedBindings.TryGetValue(highlightInfoInsts[highlightInfoInsts.Count / 2].Quant.BodyTerm.id, out localBindingInfo))
                {
                    localBindingInfo = highlightInfoInsts[highlightInfoInsts.Count / 2].bindingInfo.clone();
                    localBindingInfo.matchContext.Clear();
                }
            }

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

                            // store highlighting info
                            if (loopWrapAround)
                            {
                                wrapBindings = localBindingInfo;
                            }
                            else
                            {
                                generalizedBindings[highlightInfoInsts[idx].Quant.BodyTerm.id] = localBindingInfo;
                            }

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
                foreach (var currentTermAndBindings in todoStacks.Select(stack => stack.Peek()).Zip(highlightInfoInsts.Skip(blameWrapAround ? 1 : 0), Tuple.Create))
                {
                    var currentTerm = currentTermAndBindings.Item1;
                    var boundIn = currentTermAndBindings.Item2;
                    var boundTo = boundIn.bindingInfo.bindings.Keys.FirstOrDefault(k => boundIn.bindingInfo.bindings[k].id == currentTerm.id);
                    if (boundTo != null && localBindingInfo.bindings[boundTo].id < -1)
                    {
                        collectCandidateTerm(localBindingInfo.bindings[boundTo], candidates);
                    }
                    else
                    {
                        collectCandidateTerm(currentTerm, candidates);
                    }
                }

                var currTerm = getGeneralizedTerm(candidates, todoStacks, generalizedHistory);

                // check for blame / binding info
                if (isBlameTerm(highlightInfoInsts, todoStacks, concreteHistory, blameWrapAround, out var bindingKey))
                {
                    var concreteId = todoStacks[idx].Peek().id;
                    if (localBindingInfo.bindings[bindingKey].id != concreteId && localBindingInfo.bindings[bindingKey].id != currTerm.id)
                    {
                        throw new Exception("Trying to match two different terms against the same subpattern");
                    }
                    localBindingInfo.bindings[bindingKey] = currTerm;
                    if (!localBindingInfo.matchContext.ContainsKey(currTerm.id))
                    {
                        localBindingInfo.matchContext[currTerm.id] = new List<List<Term>>();
                    }
                    localBindingInfo.matchContext[currTerm.id].Add(generalizedHistory.Reverse().ToList());
                }
                if (isEqTerm(highlightInfoInsts, todoStacks, concreteHistory, blameWrapAround))
                {
                    var oldTerm = todoStacks[idx].Peek();
                    var key = localBindingInfo.equalities.Keys.First((t) => localBindingInfo.equalities[t].Exists(res => res.id == oldTerm.id));
                    var index = localBindingInfo.equalities[key].FindIndex(el => el.id == oldTerm.id);
                    localBindingInfo.equalities[key][index] = currTerm;
                    if (!localBindingInfo.matchContext.ContainsKey(currTerm.id))
                    {
                        localBindingInfo.matchContext[currTerm.id] = new List<List<Term>>();
                    }
                    localBindingInfo.matchContext[currTerm.id].Add(generalizedHistory.Reverse().ToList());
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

        private bool isBlameTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<Term> concreteHistory, bool flipped, out Term boundTo)
        {
            boundTo = null;
            var childIdx = childInsts.Count / 2;
            var robustIdx = flipped ? Math.Max(childIdx - 1, 0) : childIdx;
            var checkInst = childInsts[childIdx];
            var termKV = checkInst.bindingInfo.bindings.FirstOrDefault(t => t.Value.id == todoStacks[robustIdx].Peek().id);

            if (termKV.Equals(default(KeyValuePair<Term, Term>))) return false;

            boundTo = termKV.Key;
            var term = termKV.Value;

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

        private Term getGeneralizedTerm(Dictionary<string, Tuple<int, string, int, int>> candidates, Stack<Term>[] todoStacks, Stack<Term> generalizedHistory)
        {
            Term currTerm;
            if (candidates.Count == 1)
            {
                // consensus -> decend further
                var value = candidates.Values.First();
                if (value.Item4 == -1)
                {
                    currTerm = new Term(value.Item2 + "_" + (-1 - idCounter), new Term[value.Item3]) { id = idCounter };
                    idCounter--;
                }
                else
                {
                    //agree on id
                    currTerm = new Term(value.Item2, new Term[value.Item3]) { id = value.Item4 };
                }
            }
            else
            {
                // no consensus --> generalize
                // todo: if necessary, detect outlier
                currTerm = getGeneralizedTerm(todoStacks);
            }
            addToGeneralizedTerm(generalizedHistory, currTerm);
            currTerm.Responsible = todoStacks[todoStacks.Count() / 2].Peek().Responsible;
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
            var guardTerm = todoStacks[todoStacks.Length / 2].Peek();
            var newTerm = true;

            if (replacementDict.ContainsKey(guardTerm.id))
            {
                // can only reuse existing term if ALL replaced terms agree on the same generalization.
                var existingGenTerm = replacementDict[guardTerm.id];
                for (var i = 1; i < todoStacks.Length; i++)
                {
                    if (!replacementDict.ContainsKey(todoStacks[i].Peek().id))
                    {
                        // this generalization is incomplete
                        newTerm = false;
                        break;
                    }
                    newTerm = newTerm && existingGenTerm == replacementDict[todoStacks[i].Peek().id];
                }
                if (newTerm)
                {
                    var copy = new Term(existingGenTerm) {id = idCounter};
                    genReplacements.Add(copy);
                    idCounter--;
                    return copy;
                }
            }
            var t = new Term("generalization_" + genCounter, new Term[0]) { id = idCounter };
            genReplacements.Add(t);
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

        private static void collectCandidateTerm(Term currentTerm, Dictionary<string, Tuple<int, string, int, int>> candidates)
        {
            var key = currentTerm.Name + currentTerm.GenericType + currentTerm.Args.Length;
            if (!candidates.ContainsKey(key))
            {
                candidates[key] = new Tuple<int, string, int, int>
                    (0, currentTerm.Name + currentTerm.GenericType, currentTerm.Args.Length, currentTerm.id);
            }
            else
            {
                var oldTuple = candidates[key];
                candidates[key] = new Tuple<int, string, int, int>
                    (oldTuple.Item1 + 1, oldTuple.Item2, oldTuple.Item3, oldTuple.Item4 == currentTerm.id ? oldTuple.Item4 : -1);
            }
        }

        private void HighlightNewTerms(Term newTerm, Term referenceTerm, PrettyPrintFormat format)
        {
            if (newTerm.id < 0 && !referenceTerm.isSubterm(newTerm.id))
            {
                var rule = format.getPrintRule(newTerm);
                rule.font = PrintConstants.ItalicFont;
                format.addTemporaryRule(newTerm.id.ToString(), rule);
                foreach (var subterm in newTerm.Args)
                {
                    HighlightNewTerms(subterm, referenceTerm, format);
                }
            }
        }

        public void tmpHighlightGeneralizedTerm(PrettyPrintFormat format, Term generalizedTerm, bool last)
        {
            foreach (var term in genReplacements)
            {
                var rule = format.getPrintRule(term);
                rule.color = PrintConstants.generalizationColor;
                rule.prefix = term.Name + "(";
                format.addTemporaryRule(term.id.ToString(), rule);
            }

            var bindingInfo = last ? wrapBindings : generalizedBindings[generalizedTerm.dependentInstantiationsBlame.First().Quant.BodyTerm.id];
            foreach (var term in bindingInfo.bindings.Values)
            {
                if (bindingInfo.matchContext.TryGetValue(term.id, out var context))
                {
                    term.highlightTemporarily(format, PrintConstants.blameColor, context);
                }
                else
                {
                    term.highlightTemporarily(format, PrintConstants.blameColor);
                }
            }
            foreach (var term in bindingInfo.bindings.Where(kv1 => kv1.Key.id == -1).Select(kv2 => kv2.Value))
            {
                if (bindingInfo.matchContext.TryGetValue(term.id, out var context))
                {
                    term.highlightTemporarily(format, PrintConstants.bindColor, context);
                }
                else
                {
                    term.highlightTemporarily(format, PrintConstants.bindColor);
                }
            }
            foreach (var term in bindingInfo.equalities.SelectMany(kv1 => kv1.Value))
            {
                term.highlightTemporarily(format, PrintConstants.equalityColor);
            }

            if (last)
            {
                HighlightNewTerms(generalizedTerms.Last(), generalizedTerms.First(), format);
            }
        }

        public BindingInfo GetGeneralizedBindingInfo(Instantiation instantiation)
        {
            return generalizedBindings.Count == 0 ? wrapBindings : generalizedBindings[instantiation.Quant.BodyTerm.id];
        }

        public BindingInfo GetWrapAroundBindingInfo()
        {
            return wrapBindings;
        }
    }
}
