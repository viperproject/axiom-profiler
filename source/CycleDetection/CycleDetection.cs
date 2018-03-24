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

        //It might not always be necessary to compute a generalization after finding a matching loop (e.g. during path selection).
        //If processed is true but gen is null the matching loop detection has run but the generalization has not.
        private GeneralizationState gen = null;

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
            if (gen == null)
            {
                //It might not always be necessary to compute a generalization after finding a matching loop (e.g. during path selection).
                //If processed is true but gen is null the matching loop detection has run but the generalization has not.

                gen = new GeneralizationState(suffixTree.getCycleLength(), getCycleInstantiations());
                gen.generalize();
            }
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
                //in some cases the binding info for the first instantiation in a path cannot be calculated
                //=> we skip to avoid a null-pointer-exception when accessing the matched pattern in the next step
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
        }

        public List<Instantiation> getCycleInstantiations()
        {
            if (!processed) findCycle();
            // return empty list if there is no cycle
            return !hasCycle() ? new List<Instantiation>() :
                path.Skip(suffixTree.getStartIdx() + (path.First().bindingInfo == null ? 1 : 0)).Take(suffixTree.getCycleLength() * suffixTree.nRep).ToList();
        }

        public int GetNumRepetitions()
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

        /* The generalized versions of the yield terms of the quantifier instantiations in the loop. Since a loop explanation starts and ends
         * with generalized terms there is one more generalizedTerm than there are quantifiers:
         * generalizedTerms[0] -loopInstantiations[0].Quant-> generalizedTerms[1] -loopInstantiations[1].Quant-> ... -loopInstantiations[n].Quant-> generalizedTerms[n+1] 
         */
        public readonly List<Term> generalizedTerms = new List<Term>();
        private readonly List<Term> generalizationTerms = new List<Term>(); //All generalization terms that were used (T_1, T_2, ...)
        private readonly Dictionary<int, Term> replacementDict = new Dictionary<int, Term>(); //Map from concrete terms to their generalized counterparts. N.b. this includes terms other than T_1, T_2, ... e.g. if only the term id was generalized away.
        private readonly Dictionary<int, BindingInfo> generalizedBindings = new Dictionary<int, BindingInfo>(); //Map from quantifiers (their body terms) to their generalized binding info
        private BindingInfo wrapBindings = null; //The binding info used to instantiate the first quantifier of the loop from the result of the last quantifier in the loop
        private readonly HashSet<Term> loopProducedAssocBlameTerms = new HashSet<Term>(); //blame terms required in addition to the term produced by the previous instantiation that are also produced by the matching loop
        private Term[] potGeneralizationDependencies = new Term[0]; //Terms that a newly generated generailzation term may depend on. I.e. if the Term T_3(T_1, T_2) is generated the Array conatins T_1 and T_2.
        private Dictionary<Term, Term> genReplacementTermsForNextIteration = new Dictionary<Term, Term>(); //A map from generalization terms in the first term of the loop explanation (e.g. T_1) to their counterparts in the result of a single matching loop iteration (e.g. plus(T_1, x) if the loop piles up plus terms)

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

        /// <summary>
        /// Indicates wheter a concrete term has been replaced by a generalization (such as T_1) in the loop generalization.
        /// </summary>
        /// <param name="id">The id of a concrete term (i.e. a strictly positive integer).</param>
        /// <returns>True if a generalization occurs instead of this term in the loop generalization at least once. False otherwise.</returns>
        public bool IsReplacedByGeneralization(int id)
        {
            if (replacementDict.TryGetValue(id, out var replacement))
            {
                return replacement.generalizationCounter >= 0;
            }
            else
            {
                return false;
            }
        }

        public bool IsProducedByLoop(Term term)
        {
            return loopProducedAssocBlameTerms.Contains(term);
        }

        public void generalize()
        {
            if (loopInstantiations.Length == 0) return;
            for (var it = 0; it < loopInstantiations.Length+1; it++)
            {
                potGeneralizationDependencies = new Term[0];
                var i = (loopInstantiations.Length + it - 1) % loopInstantiations.Length;
                var j = it % loopInstantiations.Length;

                var robustIdx = loopInstantiations[i].Count / 2;
                var parent = loopInstantiations[i][j <= i ? Math.Max(robustIdx - 1, 0) : robustIdx];
                var child = loopInstantiations[j][robustIdx];
                var distinctBlameTerms = child.bindingInfo.getDistinctBlameTerms();

                Term generalizedYield;
                if (it == 0)
                {
                    /* The first term in the matching loop explanation is not produced by a loop instantiation (and may not necessarily
                     * have been produced by a quantifier that occurs in the loop). We therefore need to extract the terms to run the generalization
                     * on differently.
                     * A match may contain several distinct blame terms (that is terms that are not subterms of each other) that are either assembled
                     * into a new term using equality substitutions and/or that match different parts of a multipattern. We first find one of these that
                     * can be produced by the last loop instantiation, so we are later able to explain why the loop can start over, and then run the
                     * gerneralization on these terms. This also has the convenient side-effect of the term only including structure that matches the
                     * trigger, i.e. any unnecessary higher level structure a term might have is automatically omitted.
                     */
                    var loopResultIndex = Enumerable.Range(0, distinctBlameTerms.Count).First(y => parent.concreteBody.isSubterm(distinctBlameTerms[y]));
                    var concreteTerms = loopInstantiations[j].Select(inst => inst.bindingInfo.getDistinctBlameTerms()[loopResultIndex]).Where(t => t != null);
                    generalizedYield = generalizeTerms(concreteTerms, loopInstantiations[j], false, false);
                }
                else
                {
                    // Here we cann simply use the terms produced by the previous instantiation.
                    generalizedYield = generalizeYieldTermPointWise(loopInstantiations[i], loopInstantiations[j], j <= i, it == loopInstantiations.Length);
                }

                //This will later be used to find out which quantifier was used to generate this term.
                generalizedYield.dependentInstantiationsBlame.Add(loopInstantiations[j].First());

                generalizedTerms.Add(generalizedYield);

                potGeneralizationDependencies = generalizationTerms.Where(repl => repl.Args.Count() == 0)
                    .GroupBy(repl => repl.generalizationCounter).Select(group => group.First()).ToArray();

                // Other prerequisites:
                var idxList = Enumerable.Range(0, distinctBlameTerms.Count)
                                        .Where(y => {
                                            var yTerm = distinctBlameTerms[y];
                                            return !parent.concreteBody.isSubterm(yTerm);
                                        })
                                        .ToList();

                foreach (var index in idxList)
                {
                    var instantiations = loopInstantiations[j];
                    var terms = instantiations.Select(inst => inst.bindingInfo.getDistinctBlameTerms()[index]);
                    var otherGenTerm = generalizeTerms(terms, instantiations, false, it == loopInstantiations.Length);
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

            var iterationFinalTerm = generalizedTerms.Last();
            MarkGeneralizations(generalizedTerms.First(), wrapBindings.getDistinctBlameTerms().First(t => iterationFinalTerm.isSubterm(t.id)));
        }

        private void MarkGeneralizations(Term loopStart, Term loopEnd)
        {
            if (generalizationTerms.Contains(loopStart))
            {
                genReplacementTermsForNextIteration[loopStart] = loopEnd;
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

        /// <summary>
        /// Replaces occurences quantified variables in terms produced by quantifier instantiations with their generalizations.
        /// When the generalization algorithm is run on the resulting terms it sees the same terms in these locations such that they
        /// are not generalized away.
        /// </summary>
        /// <param name="concrete">The concrete term in which occurences of quantified variables should be replaced.</param>
        /// <param name="quantifier">The body of the quantifier that produced the concrete term.</param>
        /// <param name="bindingInfo">The generalized binding info indicating what generalized terms each quantified variable is bound to in the loop explanation.</param>
        /// <returns>The concrete term updated to use the specified bindings or null if the algorithm failed. The algorithm fails if the
        /// structue of the concrete term (and the term obtained by reversing z3's rewritings) and the quantifier body do not match.</returns>
        private static Term GeneralizeBindings(Term concrete, Term quantifier, BindingInfo bindingInfo)
        {
            if (quantifier.id == -1)
            {

                // We have reached a quantified variable in the quantifier body => replace the concrete term with the term bound to that quantified variable
                return bindingInfo.bindings[quantifier];
            }

            //If the concrete term doesn't structurally match the qantifier reverse any rewritings made by z3
            var concreteContinue = concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name ? concrete.reverseRewrite : concrete;

            //recurse over the arguments
            return GeneralizeChildrenBindings(concreteContinue, quantifier, bindingInfo);
        }

        private static Term GeneralizeChildrenBindings(Term concrete, Term quantifier, BindingInfo bindingInfo)
        {
            if (concrete == null) return null;
            var copy = new Term(concrete); //do not modify the original term
            if (!quantifier.ContainsFreeVar()) return copy; //nothing left to replace
            if (concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name)
            {
                //If the concrete term doesn't structually match the quantifier body we fail. The caller will backtrack if possible.
                return null;
            }

            //recurse on arguments
            for (int i = 0; i < concrete.Args.Count(); i++)
            {
                var replacement = GeneralizeBindings(concrete.Args[i], quantifier.Args[i], bindingInfo);
                if (replacement == null)
                {
                    //Failed => backtrack
                    if (ReferenceEquals(concrete, concrete.reverseRewrite))
                    {
                        //cannot resolve => continue backtracking
                        return null;
                    }
                    else
                    {
                        /* Try to resolve by reversing the rewriting. We reach this state if the top level structure matches that of the
                         * quantifier body but some deeper structure doesn't and it is not possible to resolve the issue by locally reversing
                         * the rewritng of the deeper term.
                         */
                        return GeneralizeChildrenBindings(concrete.reverseRewrite, quantifier, bindingInfo);
                    }
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

        /// <summary>
        /// Generalizes the given terms by identifying common sturcture in these terms and introducing generalizations (e.g. T_1) where
        /// their structure doesn't match.
        /// </summary>
        /// <param name="terms">The terms to generalize.</param>
        /// <param name="highlightInfoInsts">The quantifier instantiations triggered by the provided terms.</param>
        /// <param name="blameWrapAround">If true, terms[n] triggers highlightInfoInsts[n+1]. If false terms[n] triggers highlightInfoInsts[n].</param>
        /// <param name="loopWrapAround">Indicates that this is the last step in the generalized matching loop explanation and wrapBindings should be used.</param>
        /// <returns>A generalization of all terms in terms.</returns>
        private Term generalizeTerms(IEnumerable<Term> terms, List<Instantiation> highlightInfoInsts, bool blameWrapAround, bool loopWrapAround)
        {
            // stacks for breath first traversal of all terms in parallel
            var todoStacks = terms.Select(t => new Stack<Term>(new[] { t })).ToArray();

            // map to 'vote' on generalization
            // also exposes outliers
            // term name + type + #Args -> #votes
            // last 2 elements are id and generalization counter (kept if all terms agree)
            var candidates = new Dictionary<string, Tuple<int, string, int, int, int>>();
            var concreteHistory = new Stack<Term>();
            var generalizedHistory = new Stack<Term>();

            var idx = blameWrapAround ? Math.Max(highlightInfoInsts.Count / 2 - 1, 0) : highlightInfoInsts.Count / 2;

            BindingInfo localBindingInfo;
            
            if (loopWrapAround)
            {
                //We need to keep the last binding info (that explains how the loop can start over) seperate since it corresponds to the same quantifier as the first one in the loop.
                if (wrapBindings == null)
                {
                    //We start off from the binding info of a concrete term and update it as generalizations are generated.
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
                    //We start off from the binding info of a concrete term and update it as generalizations are generated.
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
                        /* The term was bound to a quantified variable and was already encountered and generalized in this term,
                         * i.e. the same quantified variable occurs multiple times in the quantifer body. If this is the case
                         * for all concrete terms we reuse the exisiting generalization.
                         */
                        collectCandidateTerm(localBindingInfo.bindings[boundTo], candidates);
                    }
                    else
                    {
                        collectCandidateTerm(currentTerm, candidates);
                    }
                }

                var currTerm = getGeneralizedTerm(candidates, todoStacks, generalizedHistory);

                // Update binding info with generalized term if necessary.
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

            //We try to find a binding in the concrete case and assume that it should also be extend to the generalized case
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
            //We try to find an equality that was used in the concrete case and assume that it should also be extended to the generalized case
            var childIdx = childInsts.Count / 2;
            var robustIdx = flipped ? Math.Max(childIdx - 1, 0) : childIdx;
            var checkInst = childInsts[childIdx];
            foreach (var equality in checkInst.bindingInfo.equalities)
            {
                var term = equality.Value.FirstOrDefault(t => t.id == todoStacks[robustIdx].Peek().id);
                if (term != null)
                {
                    var constraints = checkInst.bindingInfo.matchContext[term.id];
                    if (constraintsSat(concreteHistory.Reverse().ToList(), constraints))
                    {
                        return true;
                    }
                }
            }
            return false;
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

        private bool TryGetExistingReplacement(Stack<Term>[] todoStacks, out Term existingGenTerm)
        {
            existingGenTerm = null;

            var guardTerm = todoStacks[0].Peek();
            var newTerm = true;

            if (replacementDict.ContainsKey(guardTerm.id))
            {
                // can only reuse existing term if ALL replaced terms agree on the same generalization.
                existingGenTerm = replacementDict[guardTerm.id];
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
                    existingGenTerm = new Term(existingGenTerm);
                    for (var i = 0; i < existingGenTerm.Args.Length; ++i) existingGenTerm.Args[i] = null;
                }
                return newTerm;
            }
            else
            {
                return false;
            }
        }

        private Term getGeneralizedTerm(Dictionary<string, Tuple<int, string, int, int, int>> candidates, Stack<Term>[] todoStacks, Stack<Term> generalizedHistory)
        {
            Term currTerm;
            if (candidates.Count == 1)
            {
                // consensus -> decend further
                var value = candidates.Values.First();
                if (value.Item4 == -1)
                {
                    currTerm = new Term(value.Item2, new Term[value.Item3]) { id = idCounter };
                    idCounter--;
                }
                else
                {
                    //agree on id
                    currTerm = new Term(value.Item2, new Term[value.Item3], value.Item5) { id = value.Item4 };
                }
            }
            else
            {
                // no consensus --> generalize
                // todo: if necessary, detect outlier
                currTerm = getGeneralizedTerm(todoStacks);
            }

            foreach (var stack in todoStacks)
            {
                replacementDict[stack.Peek().id] = currTerm;
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
            if (TryGetExistingReplacement(todoStacks, out var existingReplacement))
            {
                generalizationTerms.Add(existingReplacement);
                return existingReplacement;
            }
            var t = new Term("T", potGeneralizationDependencies, genCounter) { id = idCounter };
            generalizationTerms.Add(t);
            idCounter--;
            genCounter++;
            
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

        private static void collectCandidateTerm(Term currentTerm, Dictionary<string, Tuple<int, string, int, int, int>> candidates)
        {
            var key = currentTerm.Name + currentTerm.GenericType + currentTerm.Args.Length;
            if (!candidates.ContainsKey(key))
            {
                candidates[key] = new Tuple<int, string, int, int, int>
                    (0, currentTerm.Name + currentTerm.GenericType, currentTerm.Args.Length, currentTerm.id, currentTerm.generalizationCounter);
            }
            else
            {
                var oldTuple = candidates[key];
                candidates[key] = new Tuple<int, string, int, int, int>
                    (oldTuple.Item1 + 1, oldTuple.Item2, oldTuple.Item3, oldTuple.Item4 == currentTerm.id ? oldTuple.Item4 : -1, oldTuple.Item5 == currentTerm.generalizationCounter ? oldTuple.Item5 : -1); //-1 indicates disagreement on id / generalization counter
            }
        }

        /// <summary>
        /// Highlights structure that was added by a single iteration of the matching loop in the last term of the loop explanation by printing it in italic.
        /// </summary>
        private void HighlightNewTerms(Term newTerm, Term referenceTerm, PrettyPrintFormat format)
        {
            if (newTerm.id < 0 && !referenceTerm.isSubterm(newTerm.id))
            {
                var rule = format.getPrintRule(newTerm);
                rule.font = PrintConstants.ItalicFont;
                format.addTemporaryRule(newTerm.id.ToString(), rule);
                for (int i = 0; i < newTerm.Args.Length; ++i)
                {
                    var subterm = newTerm.Args[i];
                    HighlightNewTerms(subterm, referenceTerm, format.nextDepth(newTerm, i));
                }
            }
        }

        /// <summary>
        /// Highlight subterms of a generalized term in accordance with the generalized binding infos.
        /// </summary>
        public void tmpHighlightGeneralizedTerm(PrettyPrintFormat format, Term generalizedTerm, bool last)
        {
            //If only a single generalization term (T_1) exists we print it without the subscript.
            var onlyOne = !generalizationTerms.Where(gen => gen.Args.Count() == 0 && gen.generalizationCounter >= 0).GroupBy(gen => gen.generalizationCounter).Skip(1).Any();

            //Print generalizations and terms that correspond to generalizations when wrapping around the loop in the correct color
            var allGeneralizations = last ? generalizationTerms.Concat(genReplacementTermsForNextIteration.Values) : generalizationTerms;
            foreach (var term in allGeneralizations)
            {
                var rule = format.getPrintRule(term);
                rule.color = PrintConstants.generalizationColor;
                if (term.Args.Count() == 0)
                {
                    rule.prefix = term.Name + (onlyOne || term.generalizationCounter < 0 ? "" : "_" + term.generalizationCounter) + (term.generalizationCounter < 0 && format.showTermId ? $"[g{-term.id}]" : "");
                    rule.suffix = "";
                }
                else
                {
                    rule.prefix = term.Name + (term.generalizationCounter < 0 ? (format.showTermId ? $"[g{-term.id}]" : "") : "_" + (onlyOne ? term.generalizationCounter-1 : term.generalizationCounter)) + "(";
                }
                format.addTemporaryRule(term.id.ToString(), rule);
            }

            //highlight remaining terms using the generalized binding info. Note that these highlightings may override the highlightings for generalizations in some cases.
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

        /// <summary>
        /// Prints a section indicating what the generalization terms (T_1, T_2, ...) when starting the next loop iteration correspond to in terms
        /// of the result of the previous loop iteration.
        /// </summary>
        public void PrintGeneralizationsForNextIteration(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.switchToDefaultFormat();
            foreach (var binding in genReplacementTermsForNextIteration.GroupBy(kv => kv.Key.generalizationCounter).Select(group => group.First()))
            {
                content.Append("Where ");
                binding.Key.PrettyPrint(content, format);
                content.switchToDefaultFormat();
                content.Append(" was used in this iteration\n");
                binding.Value.PrettyPrint(content, format);
                content.switchToDefaultFormat();
                content.Append(" will be used in the next iteration.\n\n");
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
