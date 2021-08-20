using System;
using System.Collections.Generic;
using System.Linq;
using AxiomProfiler.PrettyPrinting;
using AxiomProfiler.QuantifierModel;
using AxiomProfiler.Utilities;

namespace AxiomProfiler.CycleDetection
{
    using ConstraintElementType = Tuple<Term, int>;
    using ConstraintType = List<Tuple<Term, int>>;

    public class CycleDetection
    {
        private readonly List<Instantiation> path; // Path on which to search for a cycle.
        private bool processed; // Has cycle detection been run, yet?
        private readonly int minRepetitions; // Threshold for identifying a repeating pattern as a cycle.
        private const char endChar = char.MaxValue; // Marks end of the string.
        private char currMap = char.MinValue; // Next unused character when constructing a string from path.
        private readonly Dictionary<string, char> mapping = new Dictionary<string, char>(); // Instantiations -> characters in the string given to the suffix tree.
        private readonly Dictionary<char, List<Instantiation>> reverseMapping = new Dictionary<char, List<Instantiation>>();
        private SuffixTree.SuffixTree suffixTree;

        //It might not always be necessary to compute a generalization after finding a matching loop (e.g. during path selection).
        //If processed is true but gen is null the matching loop detection has run but the generalization has not.
        private GeneralizationState gen = null;

        public CycleDetection(IEnumerable<Instantiation> pathToCheck, int minRep)
        {
            path = pathToCheck.ToList();
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
            if (gen == null && hasCycle())
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
                .Select(c => reverseMapping[c].First().Quant)); // The cycle detection works on chars. We need to map these back to the corresponding instantiations and obtain the quantifier from there.
            return result;
        }

        private void findCycle()
        {
            var chars = new List<char>();

            // Map the instantiations. At this point we ignore edge types (which part of the pattern was matched by the preceding
            // instantiation).
            foreach (var instantiation in path)
            {
                var key = instantiation.Quant.Qid + "_" +
                    (instantiation.bindingInfo.fullPattern?.id ?? -1);
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

            // search for cycles and validate them using edge types until a valid cycle is found or no more cycles exist
            bool retry;
            do
            {
                retry = false;

                suffixTree = new SuffixTree.SuffixTree(chars.Count, minRepetitions);
                foreach (var c in chars)
                {
                    suffixTree.addChar(c);
                }

                if (suffixTree.hasCycle())
                {
                    var nRep = suffixTree.nRep;
                    var cycleLength = suffixTree.getCycleLength();
                    var startIdx = suffixTree.getStartIdx();

                    var cycleInstantiations = path.Skip(startIdx).Take(cycleLength * nRep);

                    // We ignore the first instantiation: since its incoming edge (if any) is not part of the cycle,
                    // there is no reason why it should follow any kind of pattern.
                    var cycleFingerprints = cycleInstantiations.Zip(cycleInstantiations.Skip(1), (prev, next) => !next.bindingInfo.IsPatternMatch() ?
                        Enumerable.Repeat(Tuple.Create<Quantifier, Term, Term>(next.Quant, null, null), 1) :
                        next.bindingInfo.bindings.Where(kv => prev.dependentTerms.Any(t => t.id == kv.Value.Item2.id))
                        .Select(kv => Tuple.Create(next.Quant, next.bindingInfo.fullPattern, kv.Key))).ToArray();

                    // We will count how often each fingerprint (edge type) occurs for each quantifier in the cycle
                    var perStepFingerprints = new Dictionary<Tuple<Quantifier, Term, Term>, int>[suffixTree.getCycleLength()];
                    for (var i = 0; i < perStepFingerprints.Length; i++)
                    {
                        perStepFingerprints[i] = new Dictionary<Tuple<Quantifier, Term, Term>, int>();
                    }

                    var index = 1 % perStepFingerprints.Length;
                    foreach (var instantiationFingerprints in cycleFingerprints)
                    {
                        foreach (var fingerprint in instantiationFingerprints) {
                            perStepFingerprints[index][fingerprint] = perStepFingerprints[index].TryGetValue(fingerprint, out var prevSum) ? prevSum + 1 : 1;
                        }
                        index = (index + 1) % perStepFingerprints.Length;
                    }

                    for (var i = 0; i < perStepFingerprints.Length; ++i)
                    {
                        var orderedStats = perStepFingerprints[i].OrderByDescending(kv => kv.Value);

                        // Check whether all instantiations for a single step have the same incomming edge type
                        if (orderedStats.First().Value != nRep - (i == 0 ? 1 : 0))
                        {

                            // If not we retry with a modified string that includes the edge information relvant for this case.
                            retry = true;

                            // We use the original character for the most common edge type...
                            var keepFingerprint = orderedStats.First().Key;
                            var toUpdate = new List<int>();
                            for (var loopIdx = (cycleLength + i - 1) % cycleLength; loopIdx < cycleFingerprints.Length; loopIdx += cycleLength)
                            {
                                if (cycleFingerprints[loopIdx].Contains(keepFingerprint))
                                {
                                    foreach (var fingerprint in cycleFingerprints[loopIdx])
                                    {
                                        --perStepFingerprints[i][fingerprint];
                                    }
                                }
                                else
                                {
                                    toUpdate.Add(loopIdx);
                                }
                            }
                            // ... and assign new ones to the remaining instantiations.
                            do
                            {
                                // We can use this greedy algorithm to introduce the lowest number of additional characters.
                                var nextFingerprint = orderedStats.First().Key;
                                var nextKey = nextFingerprint.Item1.BodyTerm.id + "_" + nextFingerprint.Item2.id + nextFingerprint.Item3.id;
                                if (!mapping.TryGetValue(nextKey, out var curChar))
                                {
                                    mapping[nextKey] = currMap;
                                    curChar = currMap;
                                    ++currMap;
                                }

                                var it = 0;
                                while (it < toUpdate.Count)
                                {
                                    var loopIdx = toUpdate[it];
                                    if (cycleFingerprints[loopIdx].Contains(nextFingerprint))
                                    {
                                        //Update instantiations that have the correct incomming edge type
                                        var instantiation = path[startIdx + loopIdx];
                                        reverseMapping[chars[startIdx + loopIdx]].Remove(instantiation);
                                        
                                        chars[startIdx + loopIdx] = curChar;
                                        
                                        if (!reverseMapping.TryGetValue(curChar, out var reverseList))
                                        {
                                            reverseList = new List<Instantiation>();
                                            reverseMapping[curChar] = reverseList;
                                        }
                                        reverseList.Add(instantiation);

                                        // Update the edge type counts.
                                        foreach (var fingerprint in cycleFingerprints[loopIdx])
                                        {
                                            --perStepFingerprints[i][fingerprint];
                                        }
                                        toUpdate.RemoveAt(it);
                                    }
                                    else
                                    {
                                        ++it;
                                    }
                                }
                            } while (orderedStats.First().Value > 0);
                        }
                    }
                }
            } while (retry);

            processed = true;
        }

        public List<Instantiation> getCycleInstantiations()
        {
            if (!processed) findCycle();
            // return empty list if there is no cycle
            return !hasCycle() ? new List<Instantiation>() :
                path.Skip(suffixTree.getStartIdx()).Take(suffixTree.getCycleLength() * suffixTree.nRep).ToList();
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
        private readonly List<Instantiation>[] loopInstantiationsWorkSpace;

        /* The generalized versions of the yield terms of the quantifier instantiations in the loop. Since a loop explanation starts and ends
         * with generalized terms there is one more generalizedTerm than there are quantifiers:
         * generalizedTerms[0] -loopInstantiations[0].Quant-> generalizedTerms[1] -loopInstantiations[1].Quant-> ... -loopInstantiations[n].Quant-> generalizedTerms[n+1] 
         */
        public readonly List<Tuple<Term, BindingInfo, List<Term>>> generalizedTerms = new List<Tuple<Term, BindingInfo, List<Term>>>(); // Tuples contain the generalized term produced by the predecessor in the loop, the generalized binding info for matching that term, additional terms that are needed to match the pattern.
        private readonly List<Term> generalizationTerms = new List<Term>(); //All generalization terms that were used (T_1, T_2, ...)
        private readonly Dictionary<Tuple<int, int>, List<Term>> replacementDict = new Dictionary<Tuple<int, int>, List<Term>>(); //Map from concrete terms to their generalized counterparts. N.b. this includes terms other than T_1, T_2, ... e.g. if only the term id was generalized away.
        private readonly HashSet<Term> loopProducedAssocBlameTerms = new HashSet<Term>(Term.semanticTermComparer); //blame terms required in addition to the term produced by the previous instantiation that are also produced by the matching loop
        private Dictionary<Term, Term> genReplacementTermsForNextIteration = new Dictionary<Term, Term>(Term.semanticTermComparer); //A map from generalization terms in the first term of the loop explanation (e.g. T_1) to their counterparts in the result of a single matching loop iteration (e.g. plus(T_1, x) if the loop piles up plus terms)

        public bool TrueLoop { get; private set; } = true;
        public bool NeedsIds { get; private set; } = false;
        public readonly List<Instantiation> UnusedInstantitations = new List<Instantiation>();

        private static readonly EqualityExplanation[] emptyEqualityExplanations = new EqualityExplanation[0];

        public bool isPath;

        public GeneralizationState(int cycleLength, IEnumerable<Instantiation> instantiations)
        {
            isPath = true;
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

            var maxI = 0;
            for (var i = cycleLength - 1; i >= 0; --i)
            {
                //TODO: find a longest subsequence
                if (loopInstantiations[i][0].concreteBody.Name != loopInstantiations[i][1].concreteBody.Name)
                {
                    maxI = i + 1;
                    break;
                }
            }
            foreach (var list in loopInstantiations)
            {
                list.Clear();
            }
            index = 0;
            foreach (var instantiation in instantiations.Skip(maxI))
            {
                loopInstantiations[index].Add(instantiation);
                index = ++index % loopInstantiations.Length;
            }
            UnusedInstantitations.AddRange(instantiations.Take(maxI));

            // We will modify some instantiations later on so we create a copy.
            loopInstantiationsWorkSpace = loopInstantiations.Select(list => list.Select(inst => inst.CopyForBindingInfoModification()).ToList()).ToArray();
        }

        /// <summary>
        /// Indicates wheter a concrete term has been replaced by a generalization (such as T_1) in the loop generalization.
        /// </summary>
        /// <param name="id">The id of a concrete term (i.e. a strictly positive integer).</param>
        /// <returns>True if a generalization occurs instead of this term in the loop generalization at least once. False otherwise.</returns>
        public bool IsReplacedByGeneralization(int id)
        {
            return replacementDict.Any(kv => kv.Key.Item2 == id && kv.Value.Any(gen => gen.generalizationCounter >= 0));
        }

        public bool IsProducedByLoop(Term term)
        {
            return loopProducedAssocBlameTerms.Contains(term);
        }

        /// <summary>
        /// Generalizes the given loop.
        /// </summary>
        public void generalize()
        {
            if (loopInstantiations.Length == 0) return;

            // We will later use these to compare the terms we start from with the ones we end up with and deduce
            // how generalizations change after one loop iteration.
            var interestingBoundTos = new List<Term>();
            var interestingExplicitBoundTermIdxs = new List<int>();
            var theoryConstraintExplanationGeneralizer = new TheoryConstraintExplanationGeneralizer(this);
            
            for (var it = 0; it < loopInstantiationsWorkSpace.Length+1; it++)
            {
                var i = (loopInstantiationsWorkSpace.Length + it - 1) % loopInstantiationsWorkSpace.Length;
                var j = it % loopInstantiationsWorkSpace.Length;

                var robustIdx = loopInstantiationsWorkSpace[i].Count / 2;
                var parent = loopInstantiationsWorkSpace[i][j <= i ? Math.Max(robustIdx - 1, 0) : robustIdx];
                var child = loopInstantiationsWorkSpace[j][robustIdx];

                var nextBindingInfo = loopInstantiationsWorkSpace[j].First().bindingInfo.Clone(); // Will be filled with generalized info.
                nextBindingInfo.bindings.Clear();
                nextBindingInfo.equalities.Clear();
                foreach (var idx in Enumerable.Range(0, nextBindingInfo.explicitlyBlamedTerms.Length))
                {
                    nextBindingInfo.explicitlyBlamedTerms[idx] = null;
                }

                Term parentConcreteTerm;
                Term generalizedYield;
                var isWrapInstantiation = it == loopInstantiations.Length;
                if (isWrapInstantiation)
                {
                    // Check whether we have reached the end of a repeating pattern that is not a mathing loop.
                    // This happens if we are reducing the structure of a term, e.g. forall x {f(x)}. x on f(f(...f(x))).
                    foreach (var kv in loopInstantiationsWorkSpace[i].Last().bindingInfo.bindings)
                    {
                        if (kv.Key.id != -1 && (kv.Key.Name != kv.Value.Item2.Name ||
                            kv.Key.GenericType != kv.Value.Item2.GenericType ||
                            kv.Key.Args.Length != kv.Value.Item2.Args.Length))
                        {
                            TrueLoop = false;
                            break;
                        }
                    }
                }

                if (it == 0 && isPath)
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

                    IEnumerable<Term> concreteTerms;
                    if (loopInstantiationsWorkSpace[j].All(inst => inst.bindingInfo.IsPatternMatch()))
                    {
                        var boundToCandidates = loopInstantiationsWorkSpace[i].Zip(loopInstantiationsWorkSpace[j].Skip(j <= i ? 1 : 0), (p, c) =>
                            c.bindingInfo.bindings.Where(kv => p.dependentTerms.Contains(kv.Value.Item2, Term.semanticTermComparer)).Select(kv => kv.Key));
                        var boundTo = boundToCandidates.Skip(1).Aggregate(new HashSet<Term>(boundToCandidates.First()), (set, iterationResult) =>
                        {
                            return set;
                        }).FirstOrDefault();

                        if (boundTo == null) throw new Exception("Couldn't generalize!");
                        interestingBoundTos.Add(boundTo);

                        parentConcreteTerm = child.bindingInfo.bindings[boundTo].Item2;
                        concreteTerms = loopInstantiationsWorkSpace[j].Select(inst => inst.bindingInfo.bindings[boundTo].Item2);
                    }
                    else
                    {
                        // This shouldn't happen since the cycle detection treats MBQI and triggered instantiations as separate quantifiers
                        if (loopInstantiationsWorkSpace[j].Any(inst => inst.bindingInfo.IsPatternMatch())) throw new Exception("Couldn't generalize!");

                        var indexCandidates = loopInstantiationsWorkSpace[i].Zip(loopInstantiationsWorkSpace[j].Skip(j <= i ? 1 : 0), (p, c) =>
                            Enumerable.Range(0, c.bindingInfo.explicitlyBlamedTerms.Length).Where(idx => p.dependentTerms.Contains(c.bindingInfo.explicitlyBlamedTerms[idx], Term.semanticTermComparer)));
                        int blameIdx;
                        try
                        {
                            blameIdx = indexCandidates.Skip(1).Aggregate(new HashSet<int>(indexCandidates.First()), (set, iterationResult) =>
                            {
                                set.IntersectWith(iterationResult);
                                return set;
                            }).First();
                        }
                        catch (InvalidOperationException)
                        {
                            throw new Exception("Couldn't generalize!");
                        }
                        interestingExplicitBoundTermIdxs.Add(blameIdx);

                        parentConcreteTerm = child.bindingInfo.explicitlyBlamedTerms[blameIdx];
                        concreteTerms = loopInstantiationsWorkSpace[j].Select(inst => inst.bindingInfo.explicitlyBlamedTerms[blameIdx]);
                    }

                    generalizedYield = generalizeTerms(concreteTerms, loopInstantiationsWorkSpace[j], nextBindingInfo, false, false);
                }
                else
                {
                    // Here we can simply use the terms produced by the previous instantiation.
                    parentConcreteTerm = parent.concreteBody;
                    generalizedYield = generalizeYieldTermPointWise(loopInstantiationsWorkSpace[i], loopInstantiationsWorkSpace[j], nextBindingInfo, j <= i, isWrapInstantiation);
                }
                
                //This will later be used to find out which quantifier was used to generate this term.
                generalizedYield.Responsible = loopInstantiationsWorkSpace[i].First();
                generalizedYield.dependentInstantiationsBlame.Add(loopInstantiationsWorkSpace[j].First());
                

                // Other prerequisites:
                var instantiations = loopInstantiationsWorkSpace[j].Skip(isWrapInstantiation ? 1 : 0).ToList();
                var perIterationBoundTos = loopInstantiationsWorkSpace[j].Select(inst => inst.bindingInfo.getDistinctBlameTerms()
                    .SelectMany(y => inst.bindingInfo.bindings.Where(kv => kv.Value.Item2.id == y.id).Select(kv => kv.Key)));
                var boundTos = perIterationBoundTos.Skip(1).Aggregate(new HashSet<Term>(perIterationBoundTos.First()), (set, bts) =>
                {
                    set.UnionWith(bts);
                    return set;
                });

                var assocTerms = new List<Term>();
                foreach (var boundTo in boundTos)
                {
                    var terms = instantiations.Select(inst => inst.bindingInfo.bindings[boundTo].Item2);
                    var otherGenTerm = GeneralizeAssocTerm(j, nextBindingInfo, isWrapInstantiation, instantiations, terms);

                    if (!generalizedYield.isSubterm(otherGenTerm.id))
                    {
                        assocTerms.Add(otherGenTerm);
                        if (it == 0)
                        {
                            interestingBoundTos.Add(boundTo);
                        }
                    }
                }

                var perIterationBlameIdxs = loopInstantiationsWorkSpace[j].Select(inst => inst.bindingInfo.getDistinctBlameTerms()
                    .SelectMany(y => Enumerable.Range(0, inst.bindingInfo.explicitlyBlamedTerms.Length).Where(idx => inst.bindingInfo.explicitlyBlamedTerms[idx].id == y.id)));
                var assocIdxs = perIterationBlameIdxs.Skip(1).Aggregate(new HashSet<int>(perIterationBlameIdxs.First()), (set, idxs) =>
                {
                    set.UnionWith(idxs);
                    return set;
                });

                foreach (var assocIdx in assocIdxs)
                {
                    var terms = instantiations.Select(inst => inst.bindingInfo.explicitlyBlamedTerms[assocIdx]);
                    var otherGenTerm = GeneralizeAssocTerm(j, nextBindingInfo, isWrapInstantiation, instantiations, terms);

                    if (!generalizedYield.isSubterm(otherGenTerm.id))
                    {
                        assocTerms.Add(otherGenTerm);
                        if (it == 0)
                        {
                            interestingExplicitBoundTermIdxs.Add(assocIdx);
                        }
                    }
                }

                generalizedTerms.Add(Tuple.Create(generalizedYield, nextBindingInfo, assocTerms));

                if (!isWrapInstantiation && nextBindingInfo.IsPatternMatch())
                {
                    // We now try to modify the concrete instantiations for the next step so they look as though they had been
                    // instantiated from the generalized term. Where they match we will keep the generalizations from the previous
                    // step.
                    var afterNextIdx = (j + 1) % loopInstantiationsWorkSpace.Length;
                    var isWrap = afterNextIdx <= j;
                    for (int iterator = 0; iterator < loopInstantiationsWorkSpace[j].Count; ++iterator)
                    {
                        var inst = loopInstantiationsWorkSpace[j][iterator];
                        BindingInfo afterNextBindingInfo;
                        if (isWrap)
                        {
                            var offsetIterator = iterator + 1;
                            if (offsetIterator >= loopInstantiationsWorkSpace[afterNextIdx].Count)
                            {
                                afterNextBindingInfo = null;
                            }
                            else
                            {
                                afterNextBindingInfo = loopInstantiationsWorkSpace[afterNextIdx][offsetIterator].bindingInfo;
                            }
                        }
                        else
                        {
                            afterNextBindingInfo = loopInstantiationsWorkSpace[afterNextIdx][iterator].bindingInfo;
                        }

                        var concreteBody = inst.concreteBody;
                        var concreteBindingInfo = inst.bindingInfo;
                        var quantBody = inst.Quant.BodyTerm.Args.Last();
                        Term newBody;
                        if (quantBody.Name == "or" && concreteBody.Args.Length != quantBody.Args.Length)
                        {
                            var mainClause = quantBody.Args.Last();
                            if (concreteBody.Name == "or")
                            {
                                newBody = new Term(concreteBody);
                                newBody.Args[newBody.Args.Length - 1] = GeneralizeAtBindings(concreteBody.Args.Last(), mainClause, concreteBindingInfo, nextBindingInfo, out _);
                            }
                            else
                            {
                                newBody = GeneralizeAtBindings(concreteBody, mainClause, concreteBindingInfo, nextBindingInfo, out _);
                            }
                        }
                        else
                        {
                            newBody = GeneralizeAtBindings(concreteBody, quantBody, concreteBindingInfo, nextBindingInfo, out _);
                        }
                        if (newBody != null)
                        {
                            if (afterNextBindingInfo != null) UpdateBindingsForReplacementTerm(concreteBody, newBody, afterNextBindingInfo, new ConstraintType(), new ConstraintType());
                            inst.concreteBody = newBody;
                        }
                        else
                        {
                            //TODO: try partial generalization?
                            Console.Out.WriteLine($"couldn't generalize bindings for {inst}");
                        }
                        loopInstantiationsWorkSpace[j][iterator] = inst;
                    }
                }
            }

            // Generalize equality explanations. We first generalize all terms, so we can reference them from recursive explanations.
            for (var it = 0; it < loopInstantiationsWorkSpace.Length; it++)
            {
                GeneralizeEqualityExplanations(it);
            }
            // Equalities for the wrap instantiation are irrelevant since they are never displayed and any information relvant for the generalization substitution
            // is already contained in the first instantiation.
            // If this becomes interesting in the future a separate version of the replacementDict should be used both in GeneralizeEqualityExplanations() and getGeneralizedTerm().
            generalizedTerms.Last().Item2.EqualityExplanations = new EqualityExplanation[0];

            DoGeneralizationSubstitution();
            SimplifyAfterGeneralizationSubstitution();

            // We match the first term in the generalized loop iteration against the last in order to figure out how the generalizations
            // change after each iteration.
            var wrapBindings = generalizedTerms.Last().Item2;
            var genBindings = generalizedTerms.First().Item2;
            foreach (var boundTo in interestingBoundTos)
            {

                // These might overwrite each other. That is ok.
                MarkGeneralizations(genBindings.bindings[boundTo].Item2, wrapBindings.bindings[boundTo].Item2, wrapBindings, Enumerable.Empty<Term>());
            }
            foreach (var idx in interestingExplicitBoundTermIdxs)
            {

                // These might hoverwrite each other. That is ok.
                MarkGeneralizations(genBindings.explicitlyBlamedTerms[idx], wrapBindings.explicitlyBlamedTerms[idx], wrapBindings, Enumerable.Empty<Term>());
            }

            if (!TrueLoop)
            {
                var firstBindingInfo = generalizedTerms[0].Item2;
                var lastBindingInfo = generalizedTerms.Last().Item2;
                foreach (var key in firstBindingInfo.bindings.Keys.Where(k => k.id == -1))
                {
                    if (!lastBindingInfo.bindings.ContainsKey(key))
                    {
                        lastBindingInfo.bindings[key] = default(Tuple<List<ConstraintType>, Term>);
                    }
                }
            }

            // Finally we update the generalization numbering to be contiguous again, which might not be the case anymore after some
            // were eliminated.
            var genTerms = generalizedTerms.SelectMany(t => t.Item1.GetAllGeneralizationSubterms()
                .Concat(t.Item2.EqualityExplanations.SelectMany(ee => {
                    var gens = new List<Term>();
                    EqualityExplanationTermVisitor.singleton.visit(ee, term => gens.AddRange(term.GetAllGeneralizationSubterms()));
                    return gens;
                }))
                .Concat(t.Item2.bindings.Values.Where(v => v != default(Tuple<List<ConstraintType>, Term>)).SelectMany(b => b.Item2.GetAllGeneralizationSubterms()))
                .Concat(t.Item2.equalities.Values.SelectMany(e => e.SelectMany(se => se.Item2.GetAllGeneralizationSubterms())))
                .Concat(t.Item3.SelectMany(term => term.GetAllGeneralizationSubterms())))
            .Distinct().ToList();

            var remainingGens = new HashSet<int>(genTerms.Select(gen => gen.generalizationCounter));
            var lookup = genTerms.SelectMany(gen => gen.GetAllGeneralizationSubtermsAndDependencies()).ToLookup(gen => gen.generalizationCounter);

            generalizationTerms.Clear();
            var genCounter = 1;
            foreach (var counter in remainingGens.OrderBy(x => x))
            {
                foreach (var term in lookup[counter])
                {
                    term.generalizationCounter = genCounter;
                    term.Args = term.Args.Where(gen => remainingGens.Contains(gen.generalizationCounter)).ToArray();
                    generalizationTerms.Add(term);
                }
                ++genCounter;
            }
        }

        private Term GeneralizeAssocTerm(int j, BindingInfo nextBindingInfo, bool isWrapInstantiation, List<Instantiation> instantiations, IEnumerable<Term> terms)
        {
            var otherGenTerm = generalizeTerms(terms, instantiations, nextBindingInfo, false, isWrapInstantiation);
            otherGenTerm.Responsible = terms.Last().Responsible;
            otherGenTerm.dependentInstantiationsBlame.Add(loopInstantiationsWorkSpace[j].First());

            //only a finite prefix of terms may be produced outside of the loop
            var loopProduced = terms.Select(t => loopInstantiationsWorkSpace.Any(qInsts => qInsts.Any(inst => inst.concreteBody.isSubterm(t))));
            loopProduced = loopProduced.SkipWhile(c => !c);
            if (loopProduced.Any() && loopProduced.All(c => c))
            {
                loopProducedAssocBlameTerms.Add(otherGenTerm);
            }

            return otherGenTerm;
        }

        /// <summary>
        /// Compares two terms and creates a map indicating how the generalizations changed between them.
        /// </summary>
        /// <remarks>
        /// The last term in the generalized loop iteration is the first term of the "next" iteration in terms of the current one.
        /// One can, therefore, easily compare the first and last terms of a generalized loop iteration to find out how the generalizations
        /// change within one iteration.
        /// </remarks>
        private void MarkGeneralizations(Term loopStart, Term loopEnd, BindingInfo wrapBindings, IEnumerable<Term> loopEndHistory)
        {
            if (loopStart.generalizationCounter >= 0 || loopEnd.generalizationCounter >= 0)
            {
                // We found a generalization.
                genReplacementTermsForNextIteration[loopStart] = loopEnd;
                if (loopStart.generalizationCounter < 0 && loopEnd.generalizationCounter >= 0)
                {
                    TrueLoop = false;
                }
            }
            else if ((loopStart.Args.Length == 0 || loopEnd.Args.Length == 0) && loopStart.id != loopEnd.id)
            {
                genReplacementTermsForNextIteration[loopStart] = loopEnd;
                if (loopStart.Args.Length == 0 && loopEnd.Args.Length == 0)
                {
                    NeedsIds = true;
                }
            }
            else if (loopStart.Name == loopEnd.Name && loopStart.Args.Length == loopEnd.Args.Length)
            {
                // Parallel descent
                for (int i = 0; i < loopStart.Args.Length; i++)
                {
                    MarkGeneralizations(loopStart.Args[i], loopEnd.Args[i], wrapBindings, loopEndHistory.Concat(Enumerable.Repeat(loopEnd, 1)));
                }
            }
        }

        /// <summary>
        /// Generalizes the equality explanations of the specified (generalized) instantiation.
        /// </summary>
        /// <param name="instIndex"> The index of the instantiation in the generalized loop iteration. </param>
        private void GeneralizeEqualityExplanations(int instIndex)
        {
            // We may elimnate some instantitations (usually base cases of recursive equalities). We use this array to decide which
            // instantiations to remove from the path shown to the user.
            var usedInstnatiations = loopInstantiationsWorkSpace[instIndex].Select(x => true).ToArray();

            var insts = loopInstantiationsWorkSpace[instIndex].ToList();
            BindingInfo generalizedBindingInfo = generalizedTerms[instIndex].Item2;

            // We already know which equalities will be needed by the generalized iteration, just not why they hold.
            var neededEqs = generalizedBindingInfo.equalities.SelectMany(kv =>
            {
                var rhs = generalizedBindingInfo.bindings[kv.Key].Item2;
                return kv.Value.Select(t => Tuple.Create(t.Item2, rhs));
            }).Where(t => !Term.semanticTermComparer.Equals(t.Item1, t.Item2))
                .Distinct(new LambdaEqualityComparer<Tuple<Term, Term>>((t1, t2) => Term.semanticTermComparer.Equals(t1.Item1, t2.Item1) &&
                Term.semanticTermComparer.Equals(t1.Item2, t2.Item2)));

            // We go through the concrete equality explanations and match them against the equalities we need.
            var equalityExplanations = neededEqs.Select(t =>
            {
                var source = t.Item1;
                var target = t.Item2;
                var eeCollector = new List<Tuple<EqualityExplanation, int>>();
                for (var i = 0; i < insts.Count; ++i)
                {
                    var inst = insts[i];
                    foreach (var ee in inst.bindingInfo.EqualityExplanations.Distinct())
                    {
                        // All possiblities for generalizing the source.
                        if (!replacementDict.TryGetValue(Tuple.Create(i, ee.source.id), out var generalizedSource)) continue;
                        // All possiblites for generalizing the target.
                        if (!replacementDict.TryGetValue(Tuple.Create(i, ee.target.id), out var generalizedTarget)) continue;

                        // Check if the explanation could be a concrete version of the needed equality.
                        if  (generalizedSource.Any(g => Term.semanticTermComparer.Equals(g, source)) && generalizedTarget.Any(g => Term.semanticTermComparer.Equals(g, target)))
                        {
                            eeCollector.Add(Tuple.Create(ee, i));
                        }
                    }
                }
                return eeCollector;
            }).ToList();

            equalityExplanations = equalityExplanations.Where(l => l.Count != 0).ToList();

            var recursionPointFinder = new RecursionPointFinder();
            generalizedBindingInfo.EqualityExplanations = equalityExplanations.Select(list => {
                // We pick an index in the middle to make sure it follows the general pattern.
                var safeIndex = list.Count / 2;

                // Get explanations that may be referenced (i.e. exist before) by list[safeIndex]. These have to occur in previous concrete
                // iterations or in the same iteration before this quantifier.
                var candidates = new List<List<EqualityExplanation[]>>();
                var referenceGeneration = list[safeIndex].Item2;
                for (var generation = 0; generation <= referenceGeneration; ++generation)
                {
                    var generationSlice = loopInstantiationsWorkSpace.Select(quantInstantiations => quantInstantiations[generation].bindingInfo.EqualityExplanations);
                    if (generation == referenceGeneration)
                    {
                        generationSlice = generationSlice.Take(instIndex);
                    }
                    candidates.Add(generationSlice.ToList());
                }
                candidates.Reverse();

                // Find recursion points.
                recursionPointFinder.visit(list[safeIndex].Item1, Tuple.Create(new List<int>(), candidates));

                // Convert found recursion points so they reference equality explanations independent of their position in the EqualityExplanations array.
                var recursionPoints = new Dictionary<List<int>, Tuple<int, int, List<Term>, List<Term>>>();
                foreach (var key in recursionPointFinder.recursionPoints.Keys)
                {
                    var foundPoint = recursionPointFinder.recursionPoints[key];
                    var ee = candidates[foundPoint.Item1][foundPoint.Item2][foundPoint.Item3];
                    var genSource = replacementDict[Tuple.Create(referenceGeneration - foundPoint.Item1, ee.source.id)];
                    var genTarget = replacementDict[Tuple.Create(referenceGeneration - foundPoint.Item1, ee.target.id)];
                    recursionPoints[key] = Tuple.Create(foundPoint.Item1, foundPoint.Item2, genSource, genTarget);
                }
                recursionPointFinder.recursionPoints.Clear();

                // Check for which explanations the recursion points can be used. There may be some explanations at the beginning that
                // don't follow the general pattern, yet (e.g. if they have no predecessors).
                var validationFilter = ValidateRecursionPoints(recursionPoints, list);
                IEnumerable<Tuple<EqualityExplanation, int>> valid;
                if (validationFilter.Count(b => b) > 1)
                {
                    var validExplanations = list.Zip(validationFilter, Tuple.Create).Where(filter => filter.Item2).Select(filter => filter.Item1);

                    // We insert RecursiveReferenceEqualityExplanation. If the explanations truly follow the same pattern they should
                    // match structurally after this step.
                    var explanationsWithoutRecursion = GeneralizeAtRecursionPoints(recursionPoints, validExplanations.Select(pair => pair.Item1));

                    valid = explanationsWithoutRecursion.Zip(validExplanations.Select(pair => pair.Item2), Tuple.Create);
                }
                else
                {

                    // If the recursion points are only valid for a single explanation we discard them.
                    valid = list;
                }

                // remember which instnatiations we didn't use
                var usedLookup = new HashSet<int>(valid.Select(t => t.Item2));
                for (var i = 0; i < usedInstnatiations.Length; ++i)
                {
                    if (!usedLookup.Contains(i))
                    {
                        usedInstnatiations[i] = false;
                    }
                }

                // Now we generalize. If the explanations already match structurally this simply replaces all terms with their generalizations.
                var result = valid.Skip(1).Aggregate(valid.First().Item1, (gen, conc) => EqualityExplanationGeneralizer(gen, conc, false));

                // We set all other generalizations that we could have chosen equal to the ones we actually chose.
                // This will allow us to eliminate generalizations that were only needed by the first few (concrete) iterations that
                // have now been eliminated from the generalized terms during generalization substitution.
                var generalizedSourcePerIteration = list.Select(tuple => replacementDict[Tuple.Create(tuple.Item2, tuple.Item1.source.id)].Where(t => t.generalizationCounter >= 0));
                var generalizedSource = generalizedSourcePerIteration.Skip(1).Aggregate(new HashSet<Term>(generalizedSourcePerIteration.First()), (s, ts) =>
                {
                    s.IntersectWith(ts);
                    return s;
                });
                var generalizedTargetPerIteration = list.Select(tuple => replacementDict[Tuple.Create(tuple.Item2, tuple.Item1.target.id)].Where(t => t.generalizationCounter >= 0));
                var generalizedTarget = generalizedTargetPerIteration.Skip(1).Aggregate(new HashSet<Term>(generalizedTargetPerIteration.First()), (s, ts) =>
                {
                    s.IntersectWith(ts);
                    return s;
                });
                foreach (var source in generalizedSource)
                {
                    if (!eqsFromEliminatingIterations.TryGetValue(source, out var existingEqs))
                    {
                        existingEqs = new List<Term>();
                        eqsFromEliminatingIterations[source] = existingEqs;
                    }
                    existingEqs.Add(result.source);
                }
                foreach (var target in generalizedTarget)
                {
                    if (!eqsFromEliminatingIterations.TryGetValue(target, out var existingEqs))
                    {
                        existingEqs = new List<Term>();
                        eqsFromEliminatingIterations[target] = existingEqs;
                    }
                    existingEqs.Add(result.target);
                }

                NonRecursiveEqualityExplanationGeneralizer.singleton.Reset();
                return result;
            }).ToArray();

            // Update RecursiveReferenceEqualityExplanations from references to concrete explanations to references to their
            // generalized versions.
            foreach (var pair in generalizedBindingInfo.EqualityExplanations.Zip(equalityExplanations, Tuple.Create))
            {
                var generalized = pair.Item1;
                var concretes = pair.Item2;
                var backPointers = concretes.SelectMany(ee => ee.Item1.ReferenceBackPointers).ToList();

                foreach (var recursiveReference in backPointers)
                {
                    recursiveReference.UpdateReference(generalized);
                }
            }

            // Add eliminated instantiations to the list so they can be removed from the graph shown to the user later on.
            for (var i = 0; i < usedInstnatiations.Length; ++i)
            {
                if (!usedInstnatiations[i])
                {
                    UnusedInstantitations.Add(insts[i]);
                }
            }
        }

        private class RecursionPointFinder: EqualityExplanationVisitor<object, Tuple<List<int>, List<List<EqualityExplanation[]>>>>
        {
            public Dictionary<List<int>, Tuple<int, int, int>> recursionPoints = new Dictionary<List<int>, Tuple<int, int, int>>();

            private static Tuple<int, int, int> FindRecursionPoint(EqualityExplanation explanation, List<List<EqualityExplanation[]>> candidates)
            {
                if (explanation.source.id == explanation.target.id) return null;

                // We try to find some previous equality explanation (at top level) that can be referenced to replace the passed explanation.
                for (var generation = 0; generation < candidates.Count; ++generation)
                {
                    var generationCandidates = candidates[generation];
                    for (var quantifier = 0; quantifier < generationCandidates.Count; ++quantifier)
                    {
                        var quantifierCandidates = generationCandidates[quantifier];
                        for (var equality = 0; equality < quantifierCandidates.Length; ++equality)
                        {
                            var equalityCandidate = quantifierCandidates[equality];

                            /* Since the candidates were produced by instantiations that (dirctly or indirectly) caused the current instantiation
                             * their scope and therefore all their equality explanations must still be valid. We can, therefore, omit some of the
                             * checks usually performed by SemanticTermComparer.Equals().
                             */
                            if (equalityCandidate.source.id == explanation.source.id && equalityCandidate.target.id == explanation.target.id)
                            {
                                return new Tuple<int, int, int>(generation, quantifier, equality);
                            }
                        }
                    }
                }
                return null;
            }

            private static Tuple<int, int, int> FindRecursionPointInTransitiveExplanation(TransitiveEqualityExplanation explanation, int strictlyStartingAtIndex, List<List<EqualityExplanation[]>> candidates, out int length)
            {
                length = 0;
                var relevantEqualities = explanation.equalities.Skip(strictlyStartingAtIndex).Select((ee, index) => Tuple.Create(ee, index + 1));
                if (!relevantEqualities.Any()) return null;
                var startId = relevantEqualities.First().Item1.source.id;
                var longestGen = 0;
                var longestQuant = 0;
                var longestEq = 0;

                // We try to find an explanation that explains a number of steps in the transitive explanation.
                for (var generation = 0; generation < candidates.Count; ++generation)
                {
                    var generationCandidates = candidates[generation];
                    for (var quantifier = 0; quantifier < generationCandidates.Count; ++quantifier)
                    {
                        var quantifierCandidates = generationCandidates[quantifier];
                        for (var equality = 0; equality < quantifierCandidates.Length; ++equality)
                        {
                            var equalityCandidate = quantifierCandidates[equality];
                            if (equalityCandidate.source.id == startId)
                            {
                                var candidateLength = relevantEqualities.FirstOrDefault(explanationLengthPair => explanationLengthPair.Item1.target.id == equalityCandidate.target.id)?.Item2;
                                if (candidateLength.HasValue && candidateLength.Value > length && candidateLength.Value < explanation.equalities.Length)
                                {
                                    length = candidateLength.Value;
                                    longestGen = generation;
                                    longestQuant = quantifier;
                                    longestEq = equality;
                                }
                            }
                        }
                    }
                }
                if (length == 0)
                {
                    return null;
                }
                else
                {
                    return Tuple.Create(longestGen, longestQuant, longestEq);
                }
            }

            public override object Direct(DirectEqualityExplanation target, Tuple<List<int>, List<List<EqualityExplanation[]>>> arg)
            {
                // Already a leaf. If the equality term changes it is explained by some path explanation.
                return null;
            }

            public override object Transitive(TransitiveEqualityExplanation target, Tuple<List<int>, List<List<EqualityExplanation[]>>> arg)
            {
                var path = arg.Item1;
                var candidates = arg.Item2;

                // Try replacing entire explanation first...
                var recursionPoint = path.Count == 0 ? null : FindRecursionPoint(target, candidates);
                if (recursionPoint == null)
                {
                    var stepSize = 1;
                    for (var i = 0; i < target.equalities.Count(); i += stepSize)
                    {

                        // ... then try replacing the steps
                        var internalRecursionPoint = FindRecursionPointInTransitiveExplanation(target, i, candidates, out stepSize);
                        if (internalRecursionPoint == null)
                        {
                            stepSize = 1;
                            var newPath = new List<int>(path) { i };
                            visit(target.equalities[i], Tuple.Create(newPath, candidates));
                        }
                        else
                        {
                            var pathForInternalRecursionPoint = new List<int>(path) { i, -stepSize };
                            recursionPoints.Add(pathForInternalRecursionPoint, internalRecursionPoint);
                        }
                    }
                }
                else
                {
                    recursionPoints.Add(path, recursionPoint);
                }
                return null;
            }

            public override object Congruence(CongruenceExplanation target, Tuple<List<int>, List<List<EqualityExplanation[]>>> arg)
            {
                var path = arg.Item1;
                var candidates = arg.Item2;
                var recursionPoint = path.Count == 0 ? null : FindRecursionPoint(target, candidates);
                if (recursionPoint == null)
                {
                    for (var i = 0; i < target.sourceArgumentEqualities.Count(); ++i)
                    {
                        var newPath = new List<int>(path) { i };
                        visit(target.sourceArgumentEqualities[i], Tuple.Create(newPath, candidates));
                    }
                }
                else
                {
                    recursionPoints.Add(path, recursionPoint);
                }
                return null;
            }

            public override object Theory(TheoryEqualityExplanation target, Tuple<List<int>, List<List<EqualityExplanation[]>>> arg)
            {
                // Already a leaf.
                return null;
            }

            public override object RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<List<int>, List<List<EqualityExplanation[]>>> arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private class RecursionPointVerifier: EqualityExplanationVisitor<bool, Tuple<IEnumerable<int>, EqualityExplanation>>
        {
            public static readonly RecursionPointVerifier singleton = new RecursionPointVerifier();

            // We follow the path and check wether the explanation at that point matches the specified equality explanation.

            public override bool Direct(DirectEqualityExplanation target, Tuple<IEnumerable<int>, EqualityExplanation> arg)
            {
                var path = arg.Item1;
                var explanation = arg.Item2;
                return !path.Any() && target.source.id == explanation.source.id && target.target.id == explanation.target.id;
            }
            
            public override bool Transitive(TransitiveEqualityExplanation target, Tuple<IEnumerable<int>, EqualityExplanation> arg)
            {
                var path = arg.Item1;
                var explanation = arg.Item2;
                var indicator = path.Take(3).Count();
                if (indicator == 0)
                {
                    return target.source.id == explanation.source.id && target.target.id == explanation.target.id;
                }
                else if (indicator == 2 && path.ElementAt(1) < 0)
                {
                    var startIndex = path.ElementAt(0);
                    var endIndex = startIndex - path.ElementAt(1) - 1;
                    if (endIndex < target.equalities.Length)
                    {
                        return target.equalities[startIndex].source.id == explanation.source.id && target.equalities[endIndex].target.id == explanation.target.id;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    var nextArg = Tuple.Create(path.Skip(1), explanation);
                    var index = path.First();
                    if (index < target.equalities.Length)
                    {
                        return visit(target.equalities[index], nextArg);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            public override bool Congruence(CongruenceExplanation target, Tuple<IEnumerable<int>, EqualityExplanation> arg)
            {
                var path = arg.Item1;
                var explanation = arg.Item2;
                if (path.Any())
                {
                    var nextArg = Tuple.Create(path.Skip(1), explanation);
                    var index = path.First();
                    if (index < target.sourceArgumentEqualities.Length)
                    {
                        return visit(target.sourceArgumentEqualities[index], nextArg);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return target.source.id == explanation.source.id && target.target.id == explanation.target.id;
                }
            }

            public override bool Theory(TheoryEqualityExplanation target, Tuple<IEnumerable<int>, EqualityExplanation> arg)
            {
                var path = arg.Item1;
                var explanation = arg.Item2;
                return !path.Any() && target.source.id == explanation.source.id && target.target.id == explanation.target.id;
            }

            public override bool RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<IEnumerable<int>, EqualityExplanation> arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private bool[] ValidateRecursionPoints(Dictionary<List<int>, Tuple<int, int, List<Term>, List<Term>>> recursionPoints, List<Tuple<EqualityExplanation, int>> equalityExplanations)
        {
            var recursionPointVerifier = RecursionPointVerifier.singleton;
            var returnValue = Enumerable.Repeat(true, equalityExplanations.Count).ToArray();

            foreach (var recursionPoint in recursionPoints)
            {
                var path = recursionPoint.Key;
                var recursionOffset = recursionPoint.Value;
                var generationOffset = recursionOffset.Item1;
                var quantifer = recursionOffset.Item2;
                var equalitySource = recursionOffset.Item3;
                var equalityTarget = recursionOffset.Item4;

                // Explanations that do not have the necessary number of predecessors cannot be valid...
                int i = 0;
                for (; i < equalityExplanations.Count; ++i)
                {
                    if (equalityExplanations[i].Item2 >= generationOffset) break;
                    returnValue[i] = false;
                }

                // ... use the visitor to validate the remaining explanations
                for (; i < equalityExplanations.Count; ++i)
                {
                    // Find the explanation that should be referenced if the recursion point is valid.
                    var explanation = equalityExplanations[i].Item1;
                    var explanationGeneratiton = equalityExplanations[i].Item2;
                    var targetGeneration = explanationGeneratiton - generationOffset;
                    var recursionTarget = loopInstantiationsWorkSpace[quantifer][targetGeneration].bindingInfo.EqualityExplanations.SingleOrDefault(ee =>
                    {
                        var generalizedSource = replacementDict[Tuple.Create(targetGeneration, ee.source.id)];
                        var generalizedTarget = replacementDict[Tuple.Create(targetGeneration, ee.target.id)];
                        return generalizedSource.Any(s => equalitySource.Contains(s)) && generalizedTarget.Any(t => equalityTarget.Contains(t));
                    });

                    var arg = Tuple.Create<IEnumerable<int>, EqualityExplanation>(path, recursionTarget);
                    if (recursionTarget == null || !recursionPointVerifier.visit(explanation, arg))
                    {
                        returnValue[i] = false;
                    }
                }
            }
            return returnValue;
        }

        private class RecursionPointGeneralizer: EqualityExplanationVisitor<EqualityExplanation, Tuple<IEnumerable<int>, Tuple<EqualityExplanation, int>>>
        {

            // Follow the path and replace the explanation at its end with a RecursiveReferenceEqualityExplanation.

            public static readonly RecursionPointGeneralizer singleton = new RecursionPointGeneralizer();

            public override EqualityExplanation Direct(DirectEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<EqualityExplanation, int>> arg)
            {
                var recursionPoint = arg.Item2;
                return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<EqualityExplanation, int>> arg)
            {
                var path = arg.Item1;

                // We're only interested in whether the path contains 0, 1, or more than 1 more elements. We can check this efficiently.
                var indicator = path.Take(3).Count();
                if (indicator == 0)
                {
                    // The entire explanation should be replaced.
                    var recursionPoint = arg.Item2;
                    return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
                }
                else if (indicator == 2 && path.ElementAt(1) < 0)
                {
                    // A subsequence of equalities whithin this explanation should be replaced.
                    var startIndex = path.ElementAt(0);
                    var length = -path.ElementAt(1);
                    var endIndex = startIndex + length - 1;
                    var recursionPoint = arg.Item2;

                    var newLength = target.equalities.Length - length + 1;
                    var newEqualities = new EqualityExplanation[newLength];
                    Array.Copy(target.equalities, newEqualities, startIndex);
                    newEqualities[startIndex] = new RecursiveReferenceEqualityExplanation(target.equalities[startIndex].source, target.equalities[endIndex].target, recursionPoint.Item1, recursionPoint.Item2);
                    Array.Copy(target.equalities, endIndex + 1, newEqualities, startIndex + 1, newLength - startIndex - 1);

                    return new TransitiveEqualityExplanation(target.source, target.target, newEqualities);
                }
                else
                {
                    // Continue following the path.
                    var index = path.First();
                    var nextPath = path.Skip(1);
                    var nextArg = Tuple.Create(nextPath, arg.Item2);

                    var newEqualities = new EqualityExplanation[target.equalities.Length];
                    Array.Copy(target.equalities, newEqualities, target.equalities.Length);
                    newEqualities[index] = visit(target.equalities[index], nextArg);

                    return new TransitiveEqualityExplanation(target.source, target.target, newEqualities);
                }
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, Tuple<IEnumerable<int>, Tuple<EqualityExplanation, int>> arg)
            {
                var path = arg.Item1;
                if (path.Any())
                {
                    var index = path.First();
                    var nextPath = path.Skip(1);
                    var nextArg = Tuple.Create(nextPath, arg.Item2);

                    var newEqualities = new EqualityExplanation[target.sourceArgumentEqualities.Length];
                    Array.Copy(target.sourceArgumentEqualities, newEqualities, target.sourceArgumentEqualities.Length);
                    newEqualities[index] = visit(target.sourceArgumentEqualities[index], nextArg);

                    return new CongruenceExplanation(target.source, target.target, newEqualities);
                }
                else
                {
                    var recursionPoint = arg.Item2;
                    return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
                }
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<EqualityExplanation, int>> arg)
            {
                var recursionPoint = arg.Item2;
                return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<EqualityExplanation, int>> arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private IEnumerable<EqualityExplanation> GeneralizeAtRecursionPoints(Dictionary<List<int>, Tuple<int, int, List<Term>, List<Term>>> recursionPoints, IEnumerable<EqualityExplanation> equalityExplanations)
        {
            var recursionPointGeneralizer = RecursionPointGeneralizer.singleton;
            var currentGeneralizations = equalityExplanations.ToArray();

            /* Generalizing transitive equality explanations may change the number of child explanations. In particular multiple explanations may be combined into
             * a single recursive reference. Since this changes the indices of all explanations that come afterward we need to make sure that we already generalized
             * all of those explanations so we don't use invalid indices later on. Ordering the paths in reverse alphabetical order achieves just that.
             */
            var alphabeticalComparer = Comparer<List<int>>.Create((x, y) => {
                foreach (var pair in x.Zip(y, Tuple.Create))
                {
                    var result = Comparer<int>.Default.Compare(pair.Item1, pair.Item2);
                    if (result != 0) return result;
                }
                return Comparer<int>.Default.Compare(x.Count, y.Count);
            });

            var safeIndex = loopInstantiationsWorkSpace[0].Count / 2;
            foreach (var recursionPoint in recursionPoints.OrderByDescending(kv => kv.Key, alphabeticalComparer))
            {
                var recursionTargetIterationOffset = recursionPoint.Value.Item1;
                var recursionTargetQuantifier = recursionPoint.Value.Item2;
                var equalitySource = recursionPoint.Value.Item3;
                var equalityTarget = recursionPoint.Value.Item4;
                
                var referencedEquality = loopInstantiationsWorkSpace[recursionTargetQuantifier][safeIndex].bindingInfo.EqualityExplanations.First(ee =>
                {
                    var generalizedSource = replacementDict[Tuple.Create(safeIndex, ee.source.id)];
                    var generalizedTarget = replacementDict[Tuple.Create(safeIndex, ee.target.id)];
                    return generalizedSource.Any(s => equalitySource.Contains(s)) && generalizedTarget.Any(t => equalityTarget.Contains(t));
                });

                var recursionInfo = Tuple.Create(referencedEquality, recursionTargetIterationOffset);
                for (int i = 0; i < currentGeneralizations.Length; ++i)
                {
                    currentGeneralizations[i] = recursionPointGeneralizer.visit(currentGeneralizations[i], Tuple.Create<IEnumerable<int>, Tuple<EqualityExplanation, int>>(recursionPoint.Key, recursionInfo));
                }
            }
            return currentGeneralizations;
        }

        private class NonRecursiveEqualityExplanationGeneralizer: EqualityExplanationVisitor<EqualityExplanation, Tuple<GeneralizationState, EqualityExplanation, int, bool>>
        {
            public static readonly NonRecursiveEqualityExplanationGeneralizer singleton = new NonRecursiveEqualityExplanationGeneralizer();
            private readonly HashSet<Term> locallyProducedGeneralizations = new HashSet<Term>();

            public void Reset()
            {
                locallyProducedGeneralizations.Clear();
            }

            private static Term CopyTermAndSetIterationOffset(Term t, int offset)
            {
                if (t.id >= 0) return t;
                var newArgs = t.Args.Select(a => CopyTermAndSetIterationOffset(a, offset)).ToArray();
                return new Term(t, newArgs) { iterationOffset = offset };
            }

            private Term GetGeneralizedTerm(Term gen, Term other, GeneralizationState genState, int iteration, bool wrap)
            {
                // We produced the generalization ourselves => can simply generalize the concrete term to the same generalization
                if (locallyProducedGeneralizations.Contains(gen) && (gen.generalizationCounter >= 0 || (gen.Name == other.Name && gen.Args.Length == other.Args.Length)))
                {
                    var key = Tuple.Create(iteration, other.id);
                    if (!genState.replacementDict.TryGetValue(key, out var generalizations))
                    {
                        generalizations = new List<Term>();
                        genState.replacementDict[key] = generalizations;
                    }
                    generalizations.Add(gen);
                    return gen;
                }

                // Find generalization that can be used for both terms.
                var t1 = gen;
                var t2 = other;
                if (t1.id != t2.id)
                {
                    int offset;
                    var wrapOffset = wrap ? 1 : 0;
                    for (offset = wrapOffset; offset <= iteration && t1.id != t2.id; ++offset)
                    {
                        var possibleGeneralizations = Enumerable.Repeat(t2, 1);
                        if (genState.replacementDict.TryGetValue(Tuple.Create(iteration - offset, t2.id), out var generalizations))
                        {
                            possibleGeneralizations = possibleGeneralizations.Concat(generalizations);
                        }
                        foreach (var generalization in possibleGeneralizations.OrderBy(g => g.generalizationCounter))
                        {
                            if (t1.id >= 0)
                            {
                                if (genState.replacementDict.Any(kv => kv.Key.Item2 == t1.id && kv.Key.Item1 < iteration - offset && kv.Value.Any(g => g.id == generalization.id)))
                                {
                                    t1 = generalization;
                                    t2 = generalization;
                                    break;
                                }
                            }
                            else
                            {
                                // within a single iteration replacementDict is injective => we get the correct concrete terms
                                var alternativePreviousGeneralizations = genState.replacementDict.Where(kv => kv.Key.Item1 < iteration - offset && kv.Value.Any(t => t.id == t1.id)).Select(kv => kv.Value);
                                if (alternativePreviousGeneralizations.All(alts => alts.Any(t => t.id == generalization.id)))
                                {
                                    t1 = generalization;
                                    t2 = generalization;
                                    break;
                                }
                            }
                        }
                    }

                    // If we still haven't found a generalization we need to create one ourselves.
                    if (t1.id != t2.id || (offset - wrapOffset != t1.iterationOffset + 1 && t1.iterationOffset != 0))
                    {
                        Term newGen;
                        if (t1.generalizationCounter < 0 && t2.generalizationCounter < 0 && t1.Name == t2.Name && t1.Args.Length == t2.Args.Length)
                        {

                            // Can keep name.
                            var generalizedArgs = t1.Args.Zip(t2.Args, (a1, a2) => GetGeneralizedTerm(a1, a2, genState, iteration, wrap)).ToArray();
                            newGen = new Term(t1.Name, generalizedArgs)
                            {
                                id = genState.idCounter,
                                Responsible = t1.Responsible?.Quant == t2.Responsible?.Quant ? t1.Responsible : null
                            };
                            --genState.idCounter;
                        }
                        else
                        {

                            // Create T term.
                            newGen = new Term("T", emptyTerms, genState.genCounter)
                            {
                                id = genState.idCounter,
                                Responsible = t1.Responsible != null && t2.Responsible != null && t1.Responsible.Quant == t2.Responsible.Quant ? t1.Responsible : null
                            };
                            ++genState.genCounter;
                            --genState.idCounter;
                            genState.generalizationTerms.Add(newGen);
                        }

                        locallyProducedGeneralizations.Add(newGen);

                        // Add to generlization state data structures.
                        var key = Tuple.Create(iteration, other.id);
                        if (!genState.replacementDict.TryGetValue(key, out var gens))
                        {
                            gens = new List<Term>();
                            genState.replacementDict[key] = gens;
                        }
                        gens.Add(newGen);
                        if (gen.id < 0)
                        {
                            var existingEntries = genState.replacementDict.Where(kv => kv.Key.Item1 > iteration && kv.Value.Any(t => t.id == gen.id)).ToList();
                            foreach (var entry in existingEntries)
                            {
                                genState.replacementDict[entry.Key].Add(newGen);
                            }
                        }
                        else
                        {
                            for (var i = iteration; i < genState.loopInstantiationsWorkSpace[0].Count(); ++i)
                            {
                                key = Tuple.Create(i, other.id);
                                if (!genState.replacementDict.TryGetValue(key, out gens))
                                {
                                    gens = new List<Term>();
                                    genState.replacementDict[key] = gens;
                                }
                                gens.Add(newGen);
                            }
                        }

                        return newGen;
                    }
                    else if (offset - wrapOffset > 1 && t1.iterationOffset == 0)
                    {
                        t1 = CopyTermAndSetIterationOffset(t1, offset - wrapOffset - 1);
                    }
                }
                return t1;
            }

            private EqualityExplanation DefaultGeneralization(EqualityExplanation target, EqualityExplanation other, GeneralizationState genState, int iteration, bool wrap)
            {
                var sourceTerm = GetGeneralizedTerm(target.source, other.source, genState, iteration, wrap);
                var targetTerm = GetGeneralizedTerm(target.target, other.target, genState, iteration, wrap);
                return new TransitiveEqualityExplanation(sourceTerm, targetTerm, emptyEqualityExplanations);
            }

            private EqualityExplanation GeneralizeDirect(Term newSource, Term newTarget, DirectEqualityExplanation generalized, DirectEqualityExplanation other, GeneralizationState genState, int iteration, bool wrap)
            {
                var steps = new List<EqualityExplanation>();

                var eqTerm = GetGeneralizedTerm(generalized.equality, other.equality, genState, iteration, wrap);
                var sourceTerm = GetGeneralizedTerm(newSource, other.source, genState, iteration, wrap);

                // If the equality has a different iteration offset than the source term, we need to add a step that converts between
                // generalizations of the different iterations (even though the concrete terms are the same).
                if (sourceTerm.iterationOffset != eqTerm.iterationOffset)
                {
                    var originalSourceTerm = GetGeneralizedTerm(generalized.source, other.source, genState, iteration - eqTerm.iterationOffset, wrap);
                    originalSourceTerm = CopyTermAndSetIterationOffset(originalSourceTerm, originalSourceTerm.iterationOffset + eqTerm.iterationOffset);
                    steps.Add(new TransitiveEqualityExplanation(sourceTerm, originalSourceTerm, emptyEqualityExplanations));
                    sourceTerm = originalSourceTerm;
                }

                // Generalize target in terms of the iteration of the equality term
                var targetTerm = GetGeneralizedTerm(generalized.target, other.target, genState, iteration - eqTerm.iterationOffset, wrap);
                targetTerm = CopyTermAndSetIterationOffset(targetTerm, targetTerm.iterationOffset + eqTerm.iterationOffset);
                
                steps.Add(new DirectEqualityExplanation(sourceTerm, targetTerm, eqTerm));

                // Generalize target in terms of the current iteration
                var newTargetTerm = GetGeneralizedTerm(newTarget, other.target, genState, iteration, wrap);

                // Add step if the two are different
                if (targetTerm.id != newTargetTerm.id)
                {
                    steps.Add(new TransitiveEqualityExplanation(targetTerm, newTargetTerm, emptyEqualityExplanations));
                }

                if (steps.Count == 1)
                {
                    return steps.First();
                }
                else
                {
                    return new TransitiveEqualityExplanation(steps.First().source, steps.Last().target, steps.ToArray());
                }
            }

            public override EqualityExplanation Direct(DirectEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation, int, bool> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                var iteration = arg.Item3;
                var wrap = arg.Item4;
                if  (other.GetType() != typeof(DirectEqualityExplanation))
                {
                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var otherDirect = (DirectEqualityExplanation) other;
                return GeneralizeDirect(target.source, target.target, target, otherDirect, genState, iteration, wrap);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation, int, bool> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                var iteration = arg.Item3;
                var wrap = arg.Item4;
                if (other.GetType() != typeof(TransitiveEqualityExplanation))
                {

                    // We may have introduced additonal steps to convert between iteration offsets (see GeneralizeDirect() / GeneralizeRecursive())
                    if (target.equalities.Length > 1 && target.equalities.Length <= 3)
                    {
                        if (other.GetType() == typeof(DirectEqualityExplanation))
                        {
                            var otherDirect = (DirectEqualityExplanation) other;
                            var generalizedIndex = Array.FindIndex(target.equalities, ee => ee.GetType() == typeof(DirectEqualityExplanation));
                            if (generalizedIndex != -1)
                            {
                                var generalized = (DirectEqualityExplanation)target.equalities[generalizedIndex];
                                var newSource = generalizedIndex == 1 ? target.equalities[0].source : generalized.source;
                                var newTarget = generalizedIndex + 1 < target.equalities.Length ? target.equalities[generalizedIndex + 1].target : generalized.target;
                                return GeneralizeDirect(newSource, newTarget, generalized, otherDirect, genState, iteration, wrap);
                            }
                        }
                        else if (other.GetType() == typeof(RecursiveReferenceEqualityExplanation))
                        {
                            var otherRecursive = (RecursiveReferenceEqualityExplanation) other;
                            var generalizedIndex = Array.FindIndex(target.equalities, ee => ee.GetType() == typeof(RecursiveReferenceEqualityExplanation));
                            if (generalizedIndex != -1)
                            {
                                var generalized = (RecursiveReferenceEqualityExplanation)target.equalities[generalizedIndex];
                                var newSource = generalizedIndex == 1 ? target.equalities[0].source : generalized.source;
                                var newTarget = generalizedIndex + 1 < target.equalities.Length ? target.equalities[generalizedIndex + 1].target : generalized.target;
                                return GeneralizeRecursive(newSource, newTarget, generalized, otherRecursive, genState, iteration, wrap);
                            }
                        }
                    }

                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var otherTransitive = (TransitiveEqualityExplanation) other;
                if (target.equalities.Length != otherTransitive.equalities.Length)
                {
                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var sourceTerm = GetGeneralizedTerm(target.source, otherTransitive.source, genState, iteration, wrap);
                var targetTerm = GetGeneralizedTerm(target.target, otherTransitive.target, genState, iteration, wrap);
                var equalities = target.equalities.Zip(otherTransitive.equalities, (gen, cur) =>
                {
                    var nextArg = Tuple.Create(genState, cur, iteration, wrap);
                    return visit(gen, nextArg);
                }).ToArray();
                return new TransitiveEqualityExplanation(sourceTerm, targetTerm, equalities);
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, Tuple<GeneralizationState, EqualityExplanation, int, bool> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                var iteration = arg.Item3;
                var wrap = arg.Item4;
                if (other.GetType() != typeof(CongruenceExplanation))
                {
                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var otherCongruence = (CongruenceExplanation)other;
                if (target.source.Name != otherCongruence.source.Name || target.target.Name != otherCongruence.target.Name || target.sourceArgumentEqualities.Length != otherCongruence.sourceArgumentEqualities.Length)
                {
                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var sourceTerm = GetGeneralizedTerm(target.source, otherCongruence.source, genState, iteration, wrap);
                var targetTerm = GetGeneralizedTerm(target.target, otherCongruence.target, genState, iteration, wrap);

                if (sourceTerm.generalizationCounter >= 0 || targetTerm.generalizationCounter >= 0)
                {
                    return new TransitiveEqualityExplanation(sourceTerm, targetTerm, emptyEqualityExplanations);
                }

                var equalities = target.sourceArgumentEqualities.Zip(otherCongruence.sourceArgumentEqualities, (gen, cur) =>
                {
                    var nextArg = Tuple.Create(genState, cur, iteration, wrap);
                    return visit(gen, nextArg);
                }).ToArray();
                return new CongruenceExplanation(sourceTerm, targetTerm, equalities);
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation, int, bool> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                var iteration = arg.Item3;
                var wrap = arg.Item4;
                if (other.GetType() != typeof(TheoryEqualityExplanation))
                {
                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var otherTheoryExplanation = (TheoryEqualityExplanation)other;
                if (otherTheoryExplanation.TheoryName != target.TheoryName)
                {
                    return DefaultGeneralization(target, other, genState, iteration, wrap);
                }

                var sourceTerm = GetGeneralizedTerm(target.source, otherTheoryExplanation.source, genState, iteration, wrap);
                var targetTerm = GetGeneralizedTerm(target.target, otherTheoryExplanation.target, genState, iteration, wrap);
                return new TheoryEqualityExplanation(sourceTerm, targetTerm, target.TheoryName);
            }

            private EqualityExplanation GeneralizeRecursive(Term newSource, Term newTarget, RecursiveReferenceEqualityExplanation generalized, RecursiveReferenceEqualityExplanation other, GeneralizationState genState, int iteration, bool wrap)
            {
                var steps = new List<EqualityExplanation>();
                var generalizedSource = GetGeneralizedTerm(newSource, other.source, genState, iteration, wrap);

                // If the referenced equality has a different iteration offset than the source term, we need to add a step that converts
                // between generalizations of the different iterations (even though the concrete terms are the same).
                if (generalizedSource.iterationOffset != generalized.GenerationOffset)
                {
                    var originalSource = GetGeneralizedTerm(generalized.source, other.source, genState, iteration - generalized.GenerationOffset, wrap);
                    originalSource = CopyTermAndSetIterationOffset(originalSource, originalSource.iterationOffset + generalized.GenerationOffset);
                    steps.Add(new TransitiveEqualityExplanation(generalizedSource, originalSource, emptyEqualityExplanations));
                    generalizedSource = originalSource;
                }

                // Generalize target in terms of the iteration of the equality term
                var generalizedTarget = GetGeneralizedTerm(generalized.target, other.target, genState, iteration - generalized.GenerationOffset, wrap);
                generalizedTarget = CopyTermAndSetIterationOffset(generalizedTarget, generalizedTarget.iterationOffset + generalized.GenerationOffset);

                steps.Add(new RecursiveReferenceEqualityExplanation(generalizedSource, generalizedTarget, generalized.ReferencedExplanation, generalized.GenerationOffset));

                // Generalize target in terms of the current iteration
                var newTargetTerm = GetGeneralizedTerm(newTarget, other.target, genState, iteration, wrap);

                // Add step if the two are different
                if (generalizedTarget.id != newTargetTerm.id)
                {
                    steps.Add(new TransitiveEqualityExplanation(generalizedTarget, newTargetTerm, emptyEqualityExplanations));
                }

                if (steps.Count == 1)
                {
                    return steps.First();
                }
                else
                {
                    return new TransitiveEqualityExplanation(steps.First().source, steps.Last().target, steps.ToArray());
                }
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation, int, bool> arg)
            {
                var genState = arg.Item1;
                // Because of the validation phase these should always match.
                var other = (RecursiveReferenceEqualityExplanation) arg.Item2;
                var iteration = arg.Item3;
                var wrap = arg.Item4;

                return GeneralizeRecursive(target.source, target.target, target, other, genState, iteration, wrap);
            }
        }

        private EqualityExplanation EqualityExplanationGeneralizer(EqualityExplanation partiallyGeneralized, Tuple<EqualityExplanation, int> concrete, bool wrap)
        {
            return NonRecursiveEqualityExplanationGeneralizer.singleton.visit(partiallyGeneralized, Tuple.Create(this, concrete.Item1, concrete.Item2, wrap));
        }

        /// <summay>
        /// Recursively visits equality explanations and collects equalities between generalizations and other terms within these
        /// explanations.
        /// </summary>
        private class GeneralizationEqualitiesCollector : EqualityExplanationVisitor<object, Dictionary<Term, List<Term>>>
        {
            public static readonly GeneralizationEqualitiesCollector singleton = new GeneralizationEqualitiesCollector();

            private static void InsertEquality(Dictionary<Term, List<Term>> dict, Term gen, Term equalTo)
            {
                if (equalTo.ReferencesOtherIteration(gen.generalizationCounter)) return;

                if (!dict.TryGetValue(gen, out var equals))
                {
                    equals = new List<Term>();
                    dict[gen] = equals;
                }
                equals.Add(equalTo);
            }

            private static void Default(EqualityExplanation ee, Dictionary<Term, List<Term>> dict)
            {
                if (ee.source.generalizationCounter >= 0)
                {
                    InsertEquality(dict, ee.source, ee.target);
                }
                if (ee.target.generalizationCounter >= 0)
                {
                    InsertEquality(dict, ee.target, ee.source);
                }
            }

            public override object Direct(DirectEqualityExplanation target, Dictionary<Term, List<Term>> arg)
            {
                Default(target, arg);

                return null;
            }

            private static bool CanAssumeCongruenceEqs(Term t1, Term t2)
            {
                if (t1.generalizationCounter >= 0 || t2.generalizationCounter >= 0) return true;
                if (t1.Name != t2.Name || t1.GenericType != t2.GenericType || t1.Args.Length != t2.Args.Length) return false;
                return t1.Args.Zip(t2.Args, CanAssumeCongruenceEqs).All(x => x);
            }

            private static void AssumeCongruenceEqs(Term t1, Term t2, Dictionary<Term, List<Term>> eqs)
            {
                if (t1.generalizationCounter >= 0)
                {
                    if (eqs.TryGetValue(t1, out var existingEqualTos))
                    {
                        existingEqualTos.Add(t2);
                    }
                    else
                    {
                        eqs[t1] = new List<Term>() { t2 };
                    }
                }
                if (t2.generalizationCounter >= 0)
                {
                    if (eqs.TryGetValue(t2, out var existingEqualTos))
                    {
                        existingEqualTos.Add(t1);
                    }
                    else
                    {
                        eqs[t2] = new List<Term>() { t1 };
                    }
                }

                if (t1.generalizationCounter < 0 && t2.generalizationCounter < 0)
                {
                    for (var i = 0; i < t1.Args.Length; ++i)
                    {
                        AssumeCongruenceEqs(t1.Args[i], t2.Args[i], eqs);
                    }
                }
            } 

            public override object Transitive(TransitiveEqualityExplanation target, Dictionary<Term, List<Term>> arg)
            {
                if (target.equalities.Length == 0)
                {
                    if (target.source.generalizationCounter >= 0)
                    {
                        if (arg.TryGetValue(target.source, out var existingEqualTos))
                        {
                            existingEqualTos.Add(target.target);
                        }
                        else
                        {
                            arg[target.source] = new List<Term>() { target.target };
                        }
                    }
                    if (target.target.generalizationCounter >= 0)
                    {
                        if (arg.TryGetValue(target.target, out var existingEqualTos))
                        {
                            existingEqualTos.Add(target.source);
                        }
                        else
                        {
                            arg[target.target] = new List<Term>() { target.source };
                        }
                    }

                    if (target.source.generalizationCounter < 0 && target.target.generalizationCounter < 0 && CanAssumeCongruenceEqs(target.source, target.target))
                    {
                        AssumeCongruenceEqs(target.source, target.target, arg);
                    }

                    return null;
                }

                var equalToTerms = target.equalities.Select(ee => ee.source).ToList();
                equalToTerms.Add(target.source);
                equalToTerms.Add(target.target);
                equalToTerms = equalToTerms.Distinct().ToList();

                foreach (var equalToGeneration in equalToTerms.Where(t => !t.ReferencesOtherIteration(t.iterationOffset)).GroupBy(t => t.iterationOffset))
                {
                    foreach (var term in equalToGeneration)
                    {
                        if (term.generalizationCounter >= 0)
                        {
                            if (arg.TryGetValue(term, out var existingEqualTos))
                            {
                                existingEqualTos.AddRange(equalToGeneration);
                            }
                            else
                            {
                                arg[term] = new List<Term>(equalToGeneration);
                            }
                        }
                    }
                }

                foreach (var ee in target.equalities)
                {
                    visit(ee, arg);
                }

                return null;
            }

            public override object Congruence(CongruenceExplanation target, Dictionary<Term, List<Term>> arg)
            {
                Default(target, arg);

                for (var i = 0; i < target.source.Args.Length; ++i)
                {
                    var ee = target.sourceArgumentEqualities[i];
                    EqualityExplanation toVisit;
                    if (ee.GetType() == typeof(TransitiveEqualityExplanation))
                    {
                        var transEE = (TransitiveEqualityExplanation) ee;
                        toVisit = new TransitiveEqualityExplanation(target.source.Args[i], transEE.target, transEE.equalities);
                    }
                    else
                    {
                        toVisit = new TransitiveEqualityExplanation(target.source.Args[i], ee.target, new EqualityExplanation[] { ee });
                    }
                    visit(toVisit, arg);
                }

                return null;
            }

            public override object Theory(TheoryEqualityExplanation target, Dictionary<Term, List<Term>> arg)
            {
                Default(target, arg);

                return null;
            }

            public override object RecursiveReference(RecursiveReferenceEqualityExplanation target, Dictionary<Term, List<Term>> arg)
            {
                Default(target, arg);

                return null;
            }
        }

        private Dictionary<Term, List<Term>> CollectGeneralizationEqualities()
        {
            var collectedEqs = new Dictionary<Term, List<Term>>();

            foreach (var bindingInfo in generalizedTerms.Select(t => t.Item2))
            {
                foreach (var ee in bindingInfo.EqualityExplanations)
                {
                    GeneralizationEqualitiesCollector.singleton.visit(ee, collectedEqs);
                }
            }

            return collectedEqs;
        }

        /// <summary>
        /// Generates a set of generalizations that should be kept and in terms of which all other generalizations can be expressed.
        /// </summary>
        /// <remarks>
        /// A more detailed discussion of the algorithm is available in the GoogleDoc.
        /// </remarks>
        private List<int> SelectGeneralizationsToKeep(Dictionary<Term, List<Term>> eqs)
        {
            var gensToCover = generalizationTerms.Select(t => t.generalizationCounter).Distinct();
            var generalizationLookup = new Dictionary<int, Term>();
            foreach (var gen in gensToCover)
            {
                generalizationLookup[gen] = generalizationTerms.First(t => t.generalizationCounter == gen);
            }

            var coverageClosures = new Dictionary<int, HashSet<int>>();
            var trueCoverageClosures = new Dictionary<int, HashSet<int>>();
            var checkTrueCoverageGens = new HashSet<int>();

            // Reflexive
            foreach (var gen in gensToCover)
            {
                coverageClosures[gen] = new HashSet<int>() { gen };
                trueCoverageClosures[gen] = new HashSet<int>() { gen };
            }

            // Transitive
            foreach (var kv in eqs)
            {

                // CollectGeneralizationsFromTerm contains the term itself if it is a generalization => includes regular equality edges
                var perTermCoveringGens = kv.Value.Select(t => CollectGeneralizationsFromTerm(t).Distinct().ToList());
                var trueCoveringGens = perTermCoveringGens.Where(gens => gens.Count == 1).SelectMany(gens => gens);
                var checkGens = perTermCoveringGens.Where(gens => gens.Count > 1).SelectMany(gens => gens).Distinct();
                var coveringGens = perTermCoveringGens.SelectMany(ts => ts).ToList();
                foreach (var coveringGen in coveringGens)
                {
                    coverageClosures[coveringGen].Add(kv.Key.generalizationCounter);
                }

                foreach (var gen in trueCoveringGens)
                {
                    trueCoverageClosures[gen].Add(kv.Key.generalizationCounter);
                }
                checkTrueCoverageGens.UnionWith(checkGens);
            }
            CalculateCoverageTransitiveClosure(gensToCover, coverageClosures);
            CalculateCoverageTransitiveClosure(gensToCover, trueCoverageClosures);

            var chosenGens = new List<int>();
            var coveredGens = new HashSet<int>();

            ///////////// Phase 1 //////////////
            // Generate solution to simplified problem (core solution). These will be part of the final solution.
            while (!coveredGens.IsSupersetOf(gensToCover))
            {
                var maxCoverable = coverageClosures.Max(kv => kv.Value.Where(g => !coveredGens.Contains(g)).Count());
                var candidates = coverageClosures.Where(kv => kv.Value.Where(g => !coveredGens.Contains(g)).Count() == maxCoverable);
                var heuristic = candidates.Min(kv => generalizationLookup[kv.Key].Args.Length);
                var chosen = candidates.First(kv => generalizationLookup[kv.Key].Args.Length == heuristic);
                chosenGens.Add(chosen.Key);
                coveredGens.UnionWith(chosen.Value);
            }

            ///////////// Phase 2 //////////////
            // We add some more generalizations to get a solution to the original problem. Some of these may be redundant.
            // Any generalization that still hasn't been added will not be part of the final solution.
            var addedGens = new List<int>();
            foreach (var checkGen in checkTrueCoverageGens)
            {
                if (!chosenGens.Any(cg => trueCoverageClosures[cg].Contains(checkGen)))
                {
                    var candidates = trueCoverageClosures.Where(kv => kv.Value.Contains(checkGen));
                    var heuristic = candidates.Min(candidate => generalizationLookup[candidate.Key].Args.Length);
                    var chosen = candidates.First(candidate => generalizationLookup[candidate.Key].Args.Length == heuristic).Key;
                    chosenGens.Add(chosen);
                    addedGens.Add(chosen);
                }
            }

            ///////////// Phase 3 //////////////
            // Find redundancy and eliminate to get final solution.
            addedGens = addedGens.OrderByDescending(ag => generalizationLookup[ag].Args.Length).ToList();
            for (var i = 0; i < addedGens.Count;)
            {
                var addedGen = addedGens[i];

                // We explore the graph starting from the chosen gens to find out if addedGen is redundant.
                // Only generalizations that have addedGen in their coverage closure (can reach) but not in their true coverage closure
                // (guaranteed by phase 2) are interesting. N.B. others are also not needed to cover all arguments since they would also
                // have addedGen in their coverage closure.
                var candidateCoverers = new HashSet<int>(chosenGens.Where(gen => gen != addedGen && coverageClosures[gen].Contains(addedGen))
                    .SelectMany(gen => trueCoverageClosures[gen]));
                bool change = true;
                do
                {
                    var nextGeneration = eqs.Where(kv => kv.Value.Any(t => {
                        var generalizationSubterms = t.GetAllGeneralizationSubterms().Select(gen => gen.generalizationCounter).ToList();
                        return !generalizationSubterms.Contains(addedGen) && candidateCoverers.IsSupersetOf(generalizationSubterms);
                    })).Select(kv => kv.Key.generalizationCounter).ToList();
                    if (candidateCoverers.IsSupersetOf(nextGeneration))
                    {
                        change = false;
                    }
                    else
                    {
                        candidateCoverers.UnionWith(nextGeneration);
                    }
                } while (change && !candidateCoverers.Contains(addedGen));
                if (change)
                {
                    // exited through 2nd conditon, i.e. is redundant
                    addedGens.RemoveAt(i);
                    chosenGens.Remove(addedGen);
                }
                else
                {
                    ++i;
                }
            }

            return chosenGens;
        }

        /// <summary>
        /// Calculates the transitive closure of a dictionary.
        /// </summary>
        private static void CalculateCoverageTransitiveClosure(IEnumerable<int> gensToCover, Dictionary<int, HashSet<int>> coverageClosures)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var gen in gensToCover)
                {
                    var covering = coverageClosures[gen];
                    var toAdd = new List<int>();
                    foreach (var coveree in covering)
                    {
                        foreach (var newCoveree in coverageClosures[coveree])
                        {
                            if (!covering.Contains(newCoveree))
                            {
                                toAdd.Add(newCoveree);
                            }
                        }
                    }
                    changed |= toAdd.Any();
                    covering.UnionWith(toAdd);
                }
            } while (changed);
        }

        /// <summary>
        /// Replaces all occurences of generalizations in a term with their substitutions as indicated by the substitution map.
        /// </summary>
        private static Term SubstituteGeneralizationsUsingSubstitutionMap(Term t, Dictionary<int, Term> map)
        {
            if (t.generalizationCounter >= 0)
            {
                return new Term(map[t.generalizationCounter])
                {
                    Responsible = t.Responsible,
                    iterationOffset = t.iterationOffset,
                    isPrime = t.isPrime
                };
            }
            else
            {
                var replacementArgs = t.Args.Select(arg => SubstituteGeneralizationsUsingSubstitutionMap(arg, map)).ToArray();
                return new Term(t, replacementArgs);
            }
        }

        /// <summary>
        /// Replaces all occurences of generalizations in a term with their substitutions as indicated by the substitution map
        /// and updates the binding info with the new terms.
        /// </summary>
        private static Term SubstituteAndUpdateBindings(Term t, Dictionary<int, Term> map, BindingInfo bindingInfo)
        {
            Term resultTerm;
            if (t.generalizationCounter >= 0)
            {
                resultTerm = new Term(map[t.generalizationCounter])
                {
                    Responsible = t.Responsible,
                    iterationOffset = t.iterationOffset,
                    isPrime = t.isPrime
                };
            }
            else
            {
                var replacementArgs = t.Args.Select(arg => SubstituteAndUpdateBindings(arg, map, bindingInfo)).ToArray();
                resultTerm = new Term(t, replacementArgs);
            }

            // update bindings
            var boundTo = bindingInfo.bindings.Where(kv => kv.Value.Item2.id == t.id).ToList();
            foreach (var kv in boundTo)
            {
                bindingInfo.bindings[kv.Key] = Tuple.Create(kv.Value.Item1, resultTerm);
            }

            // update equalities
            foreach (var eqList in bindingInfo.equalities.Values)
            {
                for (var i = 0; i < eqList.Count; ++i)
                {
                    var cur = eqList[i];
                    if (cur.Item2.id == t.id)
                    {
                        eqList[i] = Tuple.Create(cur.Item1, resultTerm);
                    }
                }
            }

            // update match contexts
            var allConstraints = bindingInfo.bindings.SelectMany(kv => kv.Value.Item1)
                .Concat(bindingInfo.equalities.SelectMany(kv => kv.Value.Select(tuple => tuple.Item1)));
            foreach (var constraint in allConstraints)
            {
                for (var i = 0; i < constraint.Count; ++i)
                {
                    var el = constraint[i];
                    if (el.Item1.id == t.id)
                    {
                        constraint[i] = Tuple.Create(resultTerm, el.Item2);
                        break;
                    }
                }
            }

            return resultTerm;
        }

        private static IEnumerable<int> CollectGeneralizationsFromTerm(Term t)
        {
            if (t.generalizationCounter >= 0)
            {
                return Enumerable.Repeat(t.generalizationCounter, 1);
            }
            else
            {
                return t.Args.SelectMany(arg => CollectGeneralizationsFromTerm(arg));
            }
        }

        // Used for eliminating generlizations that are only necessary becuase of a few concrete interations that were eliminated
        // during equality explanation generalization. See GeneralizeEqualityExplanations() for more info.
        private readonly Dictionary<Term, List<Term>> eqsFromEliminatingIterations = new Dictionary<Term, List<Term>>();

        private Dictionary<int, Term> GenerateGeneralizationSubstitutions()
        {
            var substitutionMap = new Dictionary<int, Term>();

            var eqs = CollectGeneralizationEqualities();

            foreach (var kv in eqsFromEliminatingIterations)
            {
                if (eqs.TryGetValue(kv.Key, out var existingEqs))
                {
                    existingEqs.AddRange(kv.Value);
                }
                else
                {
                    eqs[kv.Key] = kv.Value;
                }
            }

            // We always prefer non-generalization terms
            foreach (var kv in eqs)
            {
                var nonGeneralTerm = kv.Value.FirstOrDefault(t => !t.ContainsGeneralization());
                if (nonGeneralTerm != null)
                {
                    substitutionMap[kv.Key.generalizationCounter] = nonGeneralTerm;
                }
            }
            
            // This causes the generalization selection to select this generalization and otherwise ignore it.
            var valuesToRemove = eqs.Keys.Where(t => substitutionMap.Keys.Contains(t.generalizationCounter)).ToList();
            foreach (var list in eqs.Values)
            {
                list.RemoveAll(item => valuesToRemove.Contains(item));
            }

            var gensToKeep = SelectGeneralizationsToKeep(eqs).Where(gen => !substitutionMap.ContainsKey(gen)).OrderBy(gen => gen).ToList();
            var generalizationTermsToKeep = gensToKeep.Select(gen => generalizationTerms.First(t => t.generalizationCounter == gen)).ToList();

            // Initialize the substitution map
            foreach (var generalizationTerm in generalizationTermsToKeep)
            {
                var newArgs = generalizationTerm.Args.Where(t => gensToKeep.Contains(t.generalizationCounter)).ToArray();
                var newGeneralizationTerm = new Term(generalizationTerm, newArgs);
                substitutionMap[generalizationTerm.generalizationCounter] = newGeneralizationTerm;
            }

            // Build up substitution map from there
            var substitutionOptions = eqs.GroupBy(kv => kv.Key.generalizationCounter)
                .Select(group => Tuple.Create(group.Key, group.SelectMany(kv => kv.Value)))
                .Where(tuple => !gensToKeep.Contains(tuple.Item1));
            var workQueue = new Queue<Tuple<int, IEnumerable<Term>>>(substitutionOptions);

            while (workQueue.Any())
            {
                var substitution = workQueue.Dequeue();

                var key = substitution.Item1;
                if (substitutionMap.ContainsKey(key)) continue;

                var options = substitution.Item2;

                var chosenSubstitution = options.FirstOrDefault(t => CollectGeneralizationsFromTerm(t).All(gen => substitutionMap.Keys.Contains(gen)));
                if (chosenSubstitution != null)
                {
                    substitutionMap.Add(key, SubstituteGeneralizationsUsingSubstitutionMap(chosenSubstitution, substitutionMap));
                }
                else
                {
                    workQueue.Enqueue(substitution);
                }
            }

            return substitutionMap;
        }

        /// <summary>
        /// Replaces occurences of generalizations in equality explanations with their substitution as indicated by the substitution map.
        /// </summary>
        private class EqualityExplanationSubstituter : EqualityExplanationVisitor<EqualityExplanation, Dictionary<int, Term>>
        {
            public static readonly EqualityExplanationSubstituter singleton = new EqualityExplanationSubstituter();

            public override EqualityExplanation Direct(DirectEqualityExplanation target, Dictionary<int, Term> arg)
            {
                var newSource = SubstituteGeneralizationsUsingSubstitutionMap(target.source, arg);
                var newTarget = SubstituteGeneralizationsUsingSubstitutionMap(target.target, arg);
                var newEquality = SubstituteGeneralizationsUsingSubstitutionMap(target.equality, arg);
                return new DirectEqualityExplanation(newSource, newTarget, newEquality);
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Dictionary<int, Term> arg)
            {
                var newSource = SubstituteGeneralizationsUsingSubstitutionMap(target.source, arg);
                var newTarget = SubstituteGeneralizationsUsingSubstitutionMap(target.target, arg);
                return new RecursiveReferenceEqualityExplanation(newSource, newTarget, target.ReferencedExplanation, target.GenerationOffset, target.isPrime);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, Dictionary<int, Term> arg)
            {
                var newSource = SubstituteGeneralizationsUsingSubstitutionMap(target.source, arg);
                var newTarget = SubstituteGeneralizationsUsingSubstitutionMap(target.target, arg);
                var newEqualities = target.equalities.Select(ee => visit(ee, arg)).ToArray();
                return new TransitiveEqualityExplanation(newSource, newTarget, newEqualities);
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, Dictionary<int, Term> arg)
            {
                var newSource = SubstituteGeneralizationsUsingSubstitutionMap(target.source, arg);
                var newTarget = SubstituteGeneralizationsUsingSubstitutionMap(target.target, arg);
                var newArgumentEqualities = target.sourceArgumentEqualities.Select(ee => visit(ee, arg)).ToArray();
                return new CongruenceExplanation(newSource, newTarget, newArgumentEqualities);
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, Dictionary<int, Term> arg)
            {
                var newSource = SubstituteGeneralizationsUsingSubstitutionMap(target.source, arg);
                var newTarget = SubstituteGeneralizationsUsingSubstitutionMap(target.target, arg);
                return new TheoryEqualityExplanation(newSource, newTarget, target.TheoryName);
            }
        }

        private class TheoryConstraintExplanationGeneralizer : EqualityExplanationVisitor<EqualityExplanation, Tuple<List<EqualityExplanation>, List<Instantiation>, BindingInfo, bool, List<int>>>
        {
            GeneralizationState generalizationState;

            public TheoryConstraintExplanationGeneralizer(GeneralizationState generalizationState)
            {
                this.generalizationState = generalizationState;
            }

            private EqualityExplanation DefaultExplanation(string theoryName, List<EqualityExplanation> allExplanations, List<Instantiation> highlightInfoInsts, BindingInfo nextBindingInfo, bool isWrap, List<int> instantiationNumberings, Term generalizedSource = null)
            {
                if (generalizedSource == null)
                {
                    var concreteSources = allExplanations.Select(ee => ee.source).ToList();
                    generalizedSource = generalizationState.generalizeTerms(concreteSources, highlightInfoInsts, nextBindingInfo, false, isWrap, instantiationNumberings);
                }

                var concreteTargets = allExplanations.Select(ee => ee.target).ToList();
                var generalizedTarget = generalizationState.generalizeTerms(concreteTargets, highlightInfoInsts, nextBindingInfo, false, isWrap, instantiationNumberings);

                return new TheoryEqualityExplanation(generalizedSource, generalizedTarget, theoryName);
            }

            public override EqualityExplanation Direct(DirectEqualityExplanation target, Tuple<List<EqualityExplanation>, List<Instantiation>, BindingInfo, bool, List<int>> arg) { throw new Exception("Unreachable"); }
            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<List<EqualityExplanation>, List<Instantiation>, BindingInfo, bool, List<int>> arg) { throw new Exception("Unreachable"); }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, Tuple<List<EqualityExplanation>, List<Instantiation>, BindingInfo, bool, List<int>> arg)
            {
                var allExplanations = arg.Item1;
                var highlightInfoInsts = arg.Item2;
                var nextBindingInfo = arg.Item3;
                var isWrap = arg.Item4;
                var instantiationNumberings = arg.Item5;

                var myLength = target.equalities.Length;
                var theoryName = ((TheoryEqualityExplanation)target.equalities.First()).TheoryName;
                if (allExplanations.Any(ee => ee.GetType() != typeof(TransitiveEqualityExplanation) || ((TransitiveEqualityExplanation) ee).equalities.Length != myLength))
                {
                    return DefaultExplanation(theoryName, allExplanations, highlightInfoInsts, nextBindingInfo, isWrap, instantiationNumberings);
                }

                var generalizedEqs = new EqualityExplanation[myLength];
                generalizedEqs[0] = DefaultExplanation(theoryName, allExplanations.Select(ee => ((TransitiveEqualityExplanation) ee).equalities[0]).ToList(), highlightInfoInsts, nextBindingInfo, isWrap, instantiationNumberings);
                var generalizedSource = generalizedEqs[0].source;
                var prevTarget = generalizedEqs[0].target;
                for (var i = 1; i < myLength; ++i)
                {
                    generalizedEqs[i] = DefaultExplanation(theoryName, allExplanations.Select(ee => ((TransitiveEqualityExplanation)ee).equalities[i]).ToList(), highlightInfoInsts, nextBindingInfo, isWrap, instantiationNumberings, prevTarget);
                    prevTarget = generalizedEqs[i].target;
                }

                return new TransitiveEqualityExplanation(generalizedSource, prevTarget, generalizedEqs);
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, Tuple<List<EqualityExplanation>, List<Instantiation>, BindingInfo, bool, List<int>> arg) { throw new Exception("Unreachable"); }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, Tuple<List<EqualityExplanation>, List<Instantiation>, BindingInfo, bool, List<int>> arg)
            {
                var allExplanations = arg.Item1;
                var highlightInfoInsts = arg.Item2;
                var nextBindingInfo = arg.Item3;
                var isWrap = arg.Item4;
                var instantiationNumberings = arg.Item5;

                return DefaultExplanation(target.TheoryName, allExplanations, highlightInfoInsts, nextBindingInfo, isWrap, instantiationNumberings);
            }
        }

        private void DoGeneralizationSubstitution()
        {
            var substitutionMap = GenerateGeneralizationSubstitutions();

            for (var i = 0; i < generalizedTerms.Count; ++i)
            {
                var step = generalizedTerms[i];
                var term = step.Item1;
                var bindingInfo = step.Item2;

                var substitution = SubstituteAndUpdateBindings(term, substitutionMap, bindingInfo);

                var assocTerms = step.Item3;
                var hasAssocTerms = assocTerms.Any();

                if (i == 0)
                {
                    var additionalTerms = bindingInfo.getDistinctBlameTerms();
                    additionalTerms.RemoveAll(t => substitution.isSubterm(t));
                    if (hasAssocTerms) additionalTerms.RemoveAll(t => assocTerms.Contains(t));
                    foreach (var t in additionalTerms)
                    {
                        SubstituteAndUpdateBindings(t, substitutionMap, bindingInfo);
                    }
                }

                if (hasAssocTerms)
                {
                    assocTerms = assocTerms.Select(t => SubstituteAndUpdateBindings(t, substitutionMap, bindingInfo)).ToList();
                }
                
                for (var idx = 0; idx < bindingInfo.EqualityExplanations.Length; ++idx)
                {
                    bindingInfo.EqualityExplanations[idx] = EqualityExplanationSubstituter.singleton.visit(bindingInfo.EqualityExplanations[idx], substitutionMap);
                }

                generalizedTerms[i] = Tuple.Create(substitution, bindingInfo, assocTerms);
            }
        }

        /// <summary>
        /// Eliminates trivial (x = x) equality explanation steps.
        /// </summary>
        private class EqualityExplanationSimplifier : EqualityExplanationVisitor<EqualityExplanation, object>
        {
            public static readonly EqualityExplanationSimplifier singleton = new EqualityExplanationSimplifier();

            public override EqualityExplanation Direct(DirectEqualityExplanation target, object arg)
            {
                if (Term.semanticTermComparer.Equals(target.source, target.target) && target.source.iterationOffset == target.target.iterationOffset) return null;
                else return target;
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                if (Term.semanticTermComparer.Equals(target.source, target.target) && target.source.iterationOffset == target.target.iterationOffset) return null;
                else return target;
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, object arg)
            {
                if (Term.semanticTermComparer.Equals(target.source, target.target) && target.source.iterationOffset == target.target.iterationOffset) return null;
                else
                {
                    var simplifiedExplanations = new List<EqualityExplanation>();
                    var i = 0;
                    var reverseIndices = Enumerable.Range(0, target.equalities.Length).Reverse();
                    while (i < target.equalities.Length)
                    {
                        var explanation = target.equalities[i];
                        var skipIndex = reverseIndices.First(idx => {
                            var current = explanation.source;
                            var other = target.equalities[idx].source;
                            return Term.semanticTermComparer.Equals(current, other) && current.iterationOffset == other.iterationOffset;
                        });
                        if (skipIndex != i)
                        {
                            i = skipIndex;
                            continue;
                        }

                        var simplifiedExplanation = visit(explanation, arg);
                        if (simplifiedExplanation != null)
                        {
                            simplifiedExplanations.Add(simplifiedExplanation);
                        }
                        ++i;
                    }
                    if (simplifiedExplanations.Count == 1)
                    {
                        return simplifiedExplanations.Single();
                    }
                    else
                    {
                        return new TransitiveEqualityExplanation(target.source, target.target, simplifiedExplanations.ToArray());
                    }
                }
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, object arg)
            {
                if (Term.semanticTermComparer.Equals(target.source, target.target) && target.source.iterationOffset == target.target.iterationOffset) return null;
                else
                {
                    var simplifiedArgumentExplanations = target.sourceArgumentEqualities.Select(ee => visit(ee, arg) ?? new TransitiveEqualityExplanation(ee.source, ee.target, emptyEqualityExplanations)).ToArray();
                    return new CongruenceExplanation(target.source, target.target, simplifiedArgumentExplanations);
                }
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, object arg)
            {
                if (Term.semanticTermComparer.Equals(target.source, target.target) && target.source.iterationOffset == target.target.iterationOffset) return null;
                else return target;
            }
        }

        /// <summary>
        /// Simplifies unifies term ids of terms referenced by the binding info and eliminates equality explanation steps that have
        /// become trivial (by generlization substitution and term unification).
        /// </summary>
        private void SimplifyBindingInfo(BindingInfo bindingInfo, Dictionary<string, IDLookupEntry> idLookup)
        {
            foreach (var ee in bindingInfo.EqualityExplanations)
            {
                EqualityExplanationTermVisitor.singleton.visit(ee, t => SimplifyTermIds(t, idLookup));
            }

            var simplifiedEqualityExplanations = new List<EqualityExplanation>();
            foreach (var equalityExplanation in bindingInfo.EqualityExplanations)
            {
                var simplifiedExplanation = EqualityExplanationSimplifier.singleton.visit(equalityExplanation, null);
                
                foreach (var recursiveReference in equalityExplanation.ReferenceBackPointers)
                {
                    recursiveReference.UpdateReference(simplifiedExplanation);
                }

                if (simplifiedExplanation != null)
                {
                    simplifiedEqualityExplanations.Add(simplifiedExplanation);
                }
            }
            bindingInfo.EqualityExplanations = simplifiedEqualityExplanations.Distinct().ToArray();

            foreach (var binding in bindingInfo.bindings.Values)
            {
                SimplifyTermIds(binding.Item2, idLookup);
            }

            var keys = bindingInfo.equalities.Keys.ToList();
            foreach (var key in keys)
            {
                var rhs = bindingInfo.bindings[key];
                var rhsId = rhs.Item2.id;
                var numberToRemove = bindingInfo.equalities[key].Count(t => t.Item2.id == rhsId);
                bindingInfo.equalities[key].RemoveAll(t => t.Item2.id == rhsId);
                if (!bindingInfo.equalities[key].Any())
                {
                    bindingInfo.equalities.Remove(key);
                }

                var matchContext = rhs.Item1;
                var idxs = Enumerable.Range(0, matchContext.Count).Where(i => matchContext[i].Count == 0).Take(numberToRemove).Reverse().ToList();
                foreach (var idx in idxs) matchContext.RemoveAt(idx);
            }
        }

        // Looking up arguments (these should be unified first) in order yields an IDLookupEntry that specifies the id to which
        // all terms with these arguments should be unified.
        private struct IDLookupEntry
        {
            public bool HasId;
            public int Id;
            public readonly Dictionary<int, IDLookupEntry> NextArg;

            public IDLookupEntry(bool HasId, int Id)
            {
                this.HasId = HasId;
                this.Id = Id;
                NextArg = new Dictionary<int, IDLookupEntry>();
            }
        }

        /// <summary>
        /// Unifies term identifiers in-place, i.e. terms are not copied.
        /// </summary>
        private void SimplifyTermIds(Term t, Dictionary<string, IDLookupEntry> idLookup)
        {
            if (t.generalizationCounter >= 0) return;
            if (t.Args.Length == 0) return;
            if (!idLookup.TryGetValue(t.Name, out var cursor))
            {
                cursor = new IDLookupEntry(t.Args.Length == 0, t.id);
                idLookup[t.Name] = cursor;
            }

            // Follow chain, adding entries if necessary.
            for (var i = 0; i < t.Args.Length; ++i)
            {
                var arg = t.Args[i];
                SimplifyTermIds(arg, idLookup);
                if (!cursor.NextArg.TryGetValue(arg.id, out var tmp))
                {
                    tmp = new IDLookupEntry(i == t.Args.Length - 1, t.id);
                    cursor.NextArg[arg.id] = tmp;
                }
                cursor = tmp;
            }

            if (cursor.HasId)
            {

                // unify
                if (t.id != cursor.Id)
                {
                    t.id = cursor.Id;
                }
            }
            else
            {

                // later terms with the same arguments will be assigned this term's id
                cursor.HasId = true;
                cursor.Id = t.id;
            }
        }

        private void SimplifyAfterGeneralizationSubstitution()
        {
            var idLookup = new Dictionary<string, IDLookupEntry>();
            foreach (var bindingInfo in generalizedTerms.Select(step => step.Item2))
            {
                SimplifyBindingInfo(bindingInfo, idLookup);
            }

            // Loop produced terms may include some additional structure above the terms matched to the next instantiation's trigger.
            // These would be missed by only simplifying the binding infos.
            foreach (var loopStep in generalizedTerms)
            {
                var loopTerm = loopStep.Item1;
                var assocTerms = loopStep.Item3;
                SimplifyTermIds(loopTerm, idLookup);
                assocTerms.RemoveAll(t => t.generalizationCounter >= 0);
                assocTerms.RemoveAll(t => loopTerm.isSubterm(t.id));
            }

            foreach (var assocTerm in generalizedTerms.SelectMany(step => step.Item3))
            {
                SimplifyTermIds(assocTerm, idLookup);
            }
        }

        private Term generalizeYieldTermPointWise(List<Instantiation> parentInsts, List<Instantiation> childInsts, BindingInfo generalizedBindingInfo, bool blameWrapAround, bool loopWrapAround)
        {
            var yieldTerms = parentInsts
                .Select(inst => inst.concreteBody)
                .Where(t => t != null);

            return generalizeTerms(yieldTerms, childInsts, generalizedBindingInfo, blameWrapAround, loopWrapAround);
        }

        /// <summary>
        /// Replaces occurences quantified variables in terms produced by quantifier instantiations with their generalizations.
        /// When the generalization algorithm is run on the resulting terms it sees the same terms in these locations such that they
        /// are not generalized away.
        /// </summary>
        /// <param name="concrete">The concrete term in which occurences of quantified variables should be replaced.</param>
        /// <param name="quantifier">The body of the quantifier that produced the concrete term.</param>
        /// <param name="concreteBindingInfo">The binding info used to generate the concrete term.</param>
        /// <param name="generalzedBindingInfo">The generalized binding info indicating what generalized terms each quantified variable is bound to in the loop explanation.</param>
        /// <param name="usedConcreteTerm">Gives back the concrete term that was actually used, i.e. a term where some rewritings may have been undone.</param>
        /// <returns>The concrete term updated to use the specified bindings or null if the algorithm failed. The algorithm fails if the
        /// structue of the concrete term (and the term obtained by reversing z3's rewritings) and the quantifier body do not match.</returns>
        private static Term GeneralizeAtBindings(Term concrete, Term quantifier, BindingInfo concreteBindingInfo, BindingInfo generalzedBindingInfo, out Term usedConcreteTerm)
        {
            usedConcreteTerm = concrete;
            Term replacement;
            if (quantifier.id == -1)
            {
                if (concreteBindingInfo.bindings.TryGetValue(quantifier, out var bound) && Term.semanticTermComparer.Equals(concrete, bound.Item2))
                {
                    // We have reached a quantified variable in the quantifier body => replace the concrete term with the term bound to that quantified variable
                    replacement = bound.Item2;
                }
                else
                {

                    //TODO: I think this happens because of nested quantifers.
                    replacement = concrete;
                }
            }
            else
            {

                //If the concrete term doesn't structurally match the qantifier reverse any rewritings made by z3
                var concreteContinue = /*concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name ? concrete.reverseRewrite :*/ concrete;
                usedConcreteTerm = concreteContinue;

                //recurse over the arguments
                replacement = GeneralizeChildrenBindings(concreteContinue, quantifier, concreteBindingInfo, generalzedBindingInfo, out usedConcreteTerm);

                /*if (replacement == null && !ReferenceEquals(concrete, concrete.reverseRewrite))
                {
                    replacement = GeneralizeChildrenBindings(concrete.reverseRewrite, quantifier, bindingInfo, out usedConcreteTerm);
                }*/
            }
            return replacement;
        }

        private static Term GeneralizeChildrenBindings(Term concrete, Term quantifier, BindingInfo concreteBindingInfo, BindingInfo generalizedBindingInfo, out Term usedConcreteTerm)
        {
            usedConcreteTerm = concrete;
            if (concrete == null) return null;
            var copy = new Term(concrete); //do not modify the original term
            usedConcreteTerm = new Term(concrete);
            if (!quantifier.ContainsFreeVar()) return copy; //nothing left to replace
            if (concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name || concrete.GenericType != quantifier.GenericType)
            {
                //If the concrete term doesn't structually match the quantifier body we fail. The caller will backtrack if possible.
                //return null;
                return concrete;
            }

            //recurse on arguments
            for (int i = 0; i < concrete.Args.Count(); i++)
            {
                var replacement = GeneralizeAtBindings(concrete.Args[i], quantifier.Args[i], concreteBindingInfo, generalizedBindingInfo, out var usedArg);
                /*if (replacement == null)
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
                         *//*
                        return GeneralizeChildrenBindings(concrete.reverseRewrite, quantifier, bindingInfo, out usedArg);
                    }
                }*/
                copy.Args[i] = replacement;
                usedConcreteTerm.Args[i] = usedArg;
            }
            return copy;
        }

        // Called after GeneralizeAtBindings() to obtain bindings for a match based on the modified quantifier yield.
        private static void UpdateBindingsForReplacementTerm(Term originalTerm, Term newTerm, BindingInfo bindingInfo, ConstraintType history, ConstraintType newHistory)
        {

            // Update bindings
            var relevantBindings = bindingInfo.bindings.Where(kv => kv.Value.Item2.id == originalTerm.id && (constraintsSat(history, kv.Value.Item1) || constraintsSat(newHistory, kv.Value.Item1))).ToList();
            foreach (var binding in relevantBindings) {
                bindingInfo.bindings[binding.Key] = Tuple.Create(binding.Value.Item1, newTerm);
            }

            // We update the match contexts separately since the same subterm A may occur several times in a trigger. In such
            // cases we will update the term that was bound to A the first time we encounter it. The next time we encounter A
            // we will find no relevantBindings but we still need to update the match contexts for this binding.
            foreach (var matchContext in bindingInfo.bindings.SelectMany(kv => kv.Value.Item1))
            {
                if (constraintsSat(history, matchContext))
                {
                    var length = matchContext.Count;
                    matchContext.Clear();
                    matchContext.AddRange(newHistory.Skip(newHistory.Count - length));
                }
            }

            // Update equalities
            foreach (var equality in bindingInfo.equalities.Values) {
                for (var i = 0; i < equality.Count; ++i) {
                    var lhs = equality[i];
                    if (lhs.Item2.id == originalTerm.id && constraintsSat(history, lhs.Item1)) {
                        equality[i] = Tuple.Create(newHistory.Skip(newHistory.Count - lhs.Item1.Count).ToList(), newTerm);
                        // Because of the way equality explanations are generalized it is not necessary to update them at this point.
                    }
                }
            }

            if (newTerm.generalizationCounter < 0) {
                for (var i = 0; i < Math.Min(originalTerm.Args.Length, newTerm.Args.Length); ++i)
                {
                    history.Add(Tuple.Create(originalTerm, i));
                    newHistory.Add(Tuple.Create(newTerm, i));
                    UpdateBindingsForReplacementTerm(originalTerm.Args[i], newTerm.Args[i], bindingInfo, history, newHistory);
                    history.RemoveAt(history.Count - 1);
                    newHistory.RemoveAt(newHistory.Count - 1);
                }
            }
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
        /// <param name="generalizedBindingInfo">Will be populated with the generalized version of the bindings of highlightInfoInsts.</param>
        /// <param name="blameWrapAround">If true, terms[n] triggers highlightInfoInsts[n+1]. If false terms[n] triggers highlightInfoInsts[n].</param>
        /// <param name="loopWrapAround">Indicates that this is the last step in the generalized matching loop explanation.</param>
        /// <returns>A generalization of all terms in terms.</returns>
        private Term generalizeTerms(IEnumerable<Term> terms, List<Instantiation> highlightInfoInsts, BindingInfo generalizedBindingInfo, bool blameWrapAround, bool loopWrapAround, List<int> instantiationNumberings = null)
        {
            // stacks for breath first traversal of all terms in parallel
            var todoStacks = terms.Select(t => new Stack<Term>(new[] { t })).ToArray();

            // map to 'vote' on generalization
            // also exposes outliers
            // term name + type + #Args -> #votes
            // 4th, 5th, and 6th element are id, generalization counter, and theory specific meaning (kept if all terms agree)
            // last element is a T term all terms are equal to or null
            var candidates = new Dictionary<string, Tuple<int, string, int, int, int, Tuple<string, string>, Term>>();
            var concreteHistories = terms.Select(_ => new Stack<ConstraintElementType>()).ToArray();
            var generalizedHistory = new Stack<ConstraintElementType>();

            while (true)
            {
                // check for backtrack condition
                Term mostRecent = null;
                if (generalizedHistory.Count > 0)
                {
                    mostRecent = generalizedHistory.Peek().Item1;
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
                        foreach (var concreteHistory in concreteHistories) concreteHistory.Pop();
                        foreach (var stack in todoStacks)
                        {
                            stack.Pop();
                        }
                        continue;
                    }
                }

                if (mostRecent != null)
                {
                    // Update the histories with the argument index for this child
                    var argIdx = Array.FindLastIndex(mostRecent.Args, t => t == null);
                    var top = generalizedHistory.Peek();
                    generalizedHistory.Pop();
                    generalizedHistory.Push(Tuple.Create(top.Item1, argIdx));
                    foreach (var history in concreteHistories)
                    {
                        top = history.Peek();
                        history.Pop();
                        history.Push(Tuple.Create(top.Item1, argIdx));
                    }
                }

                // find candidates for the next term.
                int i = 0;
                foreach (var currentTermAndBindings in todoStacks.Select(stack => stack.Peek())
                    .Zip(highlightInfoInsts.Skip(blameWrapAround ? 1 : 0).Concat(Enumerable.Repeat<Instantiation>(null, blameWrapAround ? 1 : 0)), Tuple.Create)
                    .Zip(concreteHistories, (t, h) => Tuple.Create(t.Item1, t.Item2, h)))
                {
                    var currentTerm = currentTermAndBindings.Item1;
                    var boundIn = currentTermAndBindings.Item2;
                    var history = currentTermAndBindings.Item3.Reverse().ToList();
                    var boundTo = boundIn?.bindingInfo.bindings.Keys.FirstOrDefault(k => boundIn.bindingInfo.bindings[k].Item2.id == currentTerm.id &&
                        constraintsSat(history, boundIn.bindingInfo.bindings[k].Item1));
                    if (boundTo != null && generalizedBindingInfo.bindings.TryGetValue(boundTo, out var existing) && existing.Item2.id < -1)
                    {
                        /* The term was bound to a quantified variable and was already encountered and generalized in this term,
                         * i.e. the same quantified variable occurs multiple times in the quantifer body. If this is the case
                         * for all concrete terms we reuse the exisiting generalization.
                         */
                        collectCandidateTerm(existing.Item2, boundIn.bindingInfo, i, candidates);
                    }
                    else
                    {
                        collectCandidateTerm(currentTerm, boundIn?.bindingInfo, i, candidates);
                    }
                    ++i;
                }

                // We don't want to override existing generalizations if we are in the wrap step since these generalizations are
                // generalizations for the next iteration. We don't want them to be used by the equality explanation generalization.
                var currTerm = getGeneralizedTerm(candidates, todoStacks, generalizedHistory, instantiationNumberings, !loopWrapAround);

                // Update binding info with generalized term if necessary.
                if (isBlameTerm(highlightInfoInsts, todoStacks, concreteHistories, blameWrapAround, out var bindingKeys))
                {
                    foreach (var bindingKey in bindingKeys)
                    {
                        if (!generalizedBindingInfo.bindings.TryGetValue(bindingKey, out var existing))
                        {
                            existing = Tuple.Create(new List<ConstraintType>(), currTerm);
                            generalizedBindingInfo.bindings[bindingKey] = existing;
                        }
                        else if (existing.Item2.id != currTerm.id)
                        {
                            throw new Exception("Trying to match two different terms against the same subpattern");
                        }
                        existing.Item1.Add(generalizedHistory.Reverse().ToList());
                    }
                }
                else if (isExplicitlyBlamed(highlightInfoInsts, todoStacks, blameWrapAround, out var blameIdxs))
                {
                    foreach (var idx in blameIdxs)
                    {
                        var existing = generalizedBindingInfo.explicitlyBlamedTerms[idx];
                        if (existing == null)
                        {
                            generalizedBindingInfo.explicitlyBlamedTerms[idx] = currTerm;
                        }
                        else if (existing.id != currTerm.id)
                        {
                            throw new Exception("Trying to match two different terms against the same subpattern");
                        }
                    }
                }
                if (isEqTerm(highlightInfoInsts, todoStacks, concreteHistories, blameWrapAround, out var keys))
                {
                    foreach (var key in keys)
                    {
                        if (!generalizedBindingInfo.equalities.TryGetValue(key, out var eqList))
                        {
                            eqList = new List<Tuple<ConstraintType, Term>>();
                            generalizedBindingInfo.equalities[key] = eqList;
                        }
                        eqList.Add(Tuple.Create(generalizedHistory.Reverse().ToList(), currTerm));
                    }
                }

                // always push the generalized term, because it is one term 'behind' the others
                
                generalizedHistory.Push(Tuple.Create(currTerm, -1));
                foreach (var pair in concreteHistories.Zip(todoStacks, Tuple.Create)) pair.Item1.Push(Tuple.Create(pair.Item2.Peek(), -1));
                // push children if applicable
                if (currTerm.Args.Length > 0 && currTerm.generalizationCounter < 0)
                {
                    pushSubterms(todoStacks);
                }

                // reset candidates for next round
                candidates.Clear();

                var expected = todoStacks[0].Count;
                if (todoStacks.Any(s => s.Count != expected)) throw new InvalidOperationException("Generalization produced illegal state.");
            }
        }

        private bool isBlameTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<ConstraintElementType>[] concreteHistories, bool flipped, out List<Term> boundTo)
        {
            boundTo = null;

            // Bindings in each iteration
            var blameKeyCandidates = todoStacks.Select(s => s.Peek()).Zip(concreteHistories, Tuple.Create)
                .Zip(childInsts.Skip(flipped ? 1 : 0), (checkTermAndHistory, checkInst) => checkInst.bindingInfo.bindings.Where(t => t.Value.Item2.id == checkTermAndHistory.Item1.id).Where(kv => {
                var constraints = kv.Value.Item1;
                return constraintsSat(checkTermAndHistory.Item2.Reverse().ToList(), constraints);
            }).Select(kv => kv.Key));

            // We only consider a generalized term to be bound to a subterm of the pattern if all concrete terms were bound to that subterm.
            var blameKeys = blameKeyCandidates.Skip(1).Aggregate(new HashSet<Term>(blameKeyCandidates.First()), (set, keys) =>
            {
                set.IntersectWith(keys);
                return set;
            });

            if (!blameKeys.Any()) return false;

            boundTo = blameKeys.ToList();
            return true;
        }

        private bool isExplicitlyBlamed(List<Instantiation> childInsts, Stack<Term>[] todoStacks, bool flipped, out List<int> idxs)
        {
            idxs = null;

            // Blame terms in each iteration
            var idxCandidates = todoStacks.Select(s => s.Peek())
                .Zip(childInsts.Skip(flipped ? 1 : 0), (checkTerm, checkInst) => Enumerable.Range(0, checkInst.bindingInfo.explicitlyBlamedTerms.Length)
                .Where(idx => checkInst.bindingInfo.explicitlyBlamedTerms[idx].id == checkTerm.id));

            // We only consider a generalized term to be bound to a subterm of the pattern if all concrete terms were bound to that subterm.
            var blameIdxs = idxCandidates.Skip(1).Aggregate(new HashSet<int>(idxCandidates.First()), (set, @is) =>
            {
                set.IntersectWith(@is);
                return set;
            });

            if (!blameIdxs.Any()) return false;

            idxs = blameIdxs.ToList();
            return true;
        }

        private bool isEqTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<ConstraintElementType>[] concreteHistories, bool flipped, out List<Term> boundTos)
        {
            //We try to find an equality that was used in the concrete case and assume that it should also be extended to the generalized case
            var eqsPerIteration = todoStacks.Select(s => s.Peek()).Zip(concreteHistories, Tuple.Create).Zip(childInsts.Skip(flipped ? 1 : 0), (checkTermAndHistory, checkInst) =>
            {
                var returnList = new List<Term>();
                foreach (var equality in checkInst.bindingInfo.equalities)
                {
                    var termAndMatchContext = equality.Value.FirstOrDefault(t => t.Item2.id == checkTermAndHistory.Item1.id);
                    if (termAndMatchContext != null)
                    {
                        var constraints = termAndMatchContext.Item1;
                        if (constraintsSat(checkTermAndHistory.Item2.Reverse().ToList(), constraints))
                        {
                            returnList.Add(equality.Key);
                        }
                    }
                }
                return Tuple.Create(returnList, checkTermAndHistory.Item1, checkInst, checkTermAndHistory.Item2.Reverse().ToList());
            }).ToList();

            //We introduce an equality if an equality was used in any concrete case.
            boundTos = eqsPerIteration.Aggregate(new HashSet<Term>(), (set, iterationResult) =>
            {
                set.UnionWith(iterationResult.Item1);
                return set;
            }).ToList();

            // Introduce an (identity) equality in all concrete cases that didn't use the equality.
            foreach (var boundTo in boundTos)
            {
                foreach (var iterationResult in eqsPerIteration)
                {
                    if (!iterationResult.Item1.Contains(boundTo))
                    {
                        var bindingInfo = iterationResult.Item3.bindingInfo;
                        if (!bindingInfo.equalities.TryGetValue(boundTo, out var equalityLhs))
                        {
                            equalityLhs = new List<Tuple<ConstraintType, Term>>();
                            bindingInfo.equalities[boundTo] = equalityLhs;
                        }
                        // Since the concrete term previously occured under some constraints and now becomes a distinct blame term,
                        // i.e. has no parents, we need to adjust the constraints for any of its subterms accordingly.
                        RemovePrefixFromConstraints(iterationResult.Item4, bindingInfo);
                        equalityLhs.Add(Tuple.Create(iterationResult.Item4, iterationResult.Item2));

                        // Add equality explanation
                        var eeLength = bindingInfo.EqualityExplanations.Length;
                        var newEqualityExplanations = new EqualityExplanation[eeLength + 1];
                        Array.Copy(bindingInfo.EqualityExplanations, newEqualityExplanations, eeLength);
                        newEqualityExplanations[eeLength] = new TransitiveEqualityExplanation(iterationResult.Item2, iterationResult.Item2, emptyEqualityExplanations);
                        bindingInfo.EqualityExplanations = newEqualityExplanations;
                    }
                }
            }

            return boundTos.Any();
        }

        private static void RemovePrefixFromConstraints(ConstraintType prefix, BindingInfo bindingInfo)
        {
            var allMatchContexts = bindingInfo.bindings.SelectMany(kv => kv.Value.Item1)
                .Concat(bindingInfo.equalities.SelectMany(kv => kv.Value.Select(t => t.Item1)));
            foreach (var context in allMatchContexts)
            {
                if (context.Count >= prefix.Count && prefix.Zip(context, (t1, t2) => t1.Item1.id == t2.Item1.id && t1.Item2 == t2.Item2).All(x => x))
                {
                    context.RemoveRange(0, prefix.Count);
                }
            }
        }

        private static bool constraintsSat(ConstraintType generalizedHistory, ConstraintType constraint)
        {
            return constraintsSat(generalizedHistory, Enumerable.Repeat(constraint, 1));
        }

        private static bool constraintsSat(ConstraintType gerneralizeHistory, IEnumerable<ConstraintType> constraints)
        {
            if (!constraints.Any()) return true;
            return (from constraint in constraints
                    where constraint.Count == gerneralizeHistory.Count
                    let slice = gerneralizeHistory.GetRange(gerneralizeHistory.Count - constraint.Count, constraint.Count)
                    select slice.Zip(constraint, (term1, term2) => term1.Item1.id == term2.Item1.id && term1.Item2 == term2.Item2))
                                .Any(intermediate => intermediate.All(val => val));
        }

        private bool TryGetExistingReplacement(Stack<Term>[] todoStacks, List<int> iterationNumberings, out Term existingGenTerm)
        {
            existingGenTerm = null;

            var guardTerm = todoStacks[0].Peek();

            if (replacementDict.TryGetValue(Tuple.Create(TranslateIteration(iterationNumberings, 0), guardTerm.id), out var existingGenTermList))
            {
                var existingGenTerms = new HashSet<Term>(existingGenTermList);
                // can only reuse existing term if ALL replaced terms agree on the same generalization.
                for (var i = 1; i < todoStacks.Length; i++)
                {
                    if (!replacementDict.TryGetValue(Tuple.Create(TranslateIteration(iterationNumberings, i), todoStacks[i].Peek().id), out var nextGenTerms))
                    {
                        // this generalization is incomplete
                        existingGenTerms.Clear();
                        break;
                    }
                    existingGenTerms.IntersectWith(nextGenTerms);
                }
                if (existingGenTerms.Any())
                {
                    var chosenTerm = existingGenTerms.FirstOrDefault(t => t.generalizationCounter < 0) ?? existingGenTerms.First();
                    existingGenTerm = new Term(chosenTerm);
                    if (existingGenTerm.generalizationCounter < 0 && existingGenTerm.generalizationCounter < 0)
                    {
                        for (var i = 0; i < existingGenTerm.Args.Length; ++i) existingGenTerm.Args[i] = null;
                    }
                    return true;
                }
            }
            return false;
        }

        private static int TranslateIteration(List<int> iterationNumberings, int idx)
        {
            return iterationNumberings == null ? idx : iterationNumberings[idx];
        }

        private Term getGeneralizedTerm(Dictionary<string, Tuple<int, string, int, int, int, Tuple<string, string>, Term>> candidates, Stack<Term>[] todoStacks,
            Stack<ConstraintElementType> generalizedHistory, List<int> instantiationNumberings, bool overrideReplacements)
        {
            Term currTerm;
            if (candidates.Count == 1)
            {
                // consensus -> decend further
                var value = candidates.Values.First();
                if (value.Item4 == -1)
                {
                    if (TryGetExistingReplacement(todoStacks, instantiationNumberings, out var existingGenTerm) && existingGenTerm.Name == value.Item2)
                    {
                        currTerm = new Term(existingGenTerm);
                    }
                    else
                    {
                        currTerm = new Term(value.Item2, new Term[value.Item3], value.Item5) { id = idCounter };
                        idCounter--;
                    }
                }
                else
                {
                    //agree on id
                    currTerm = new Term(value.Item2, new Term[value.Item3], value.Item5) { id = value.Item4 };
                }

                if (value.Item5 >= 0)
                {
                    Array.Copy(todoStacks[0].Peek().Args, currTerm.Args, value.Item3);
                }

                currTerm.Theory = value.Item6?.Item1;
                currTerm.TheorySpecificMeaning = value.Item6?.Item2;
            }
            else
            {
                // no consensus --> generalize
                // todo: if necessary, detect outlier
                var existingGeneralizations = candidates.Select(c => c.Value.Item7);
                var possibleGeneralization = existingGeneralizations.FirstOrDefault();
                if (possibleGeneralization != null && existingGeneralizations.All(t => t != null && t.generalizationCounter == possibleGeneralization.generalizationCounter))
                {
                    var argCopy = new Term[possibleGeneralization.Args.Length];
                    Array.Copy(possibleGeneralization.Args, argCopy, argCopy.Length);
                    currTerm = new Term(possibleGeneralization.Name, argCopy, possibleGeneralization.generalizationCounter) { id = idCounter };
                    --idCounter;
                }
                else
                {
                    currTerm = getGeneralizationTerm(todoStacks, instantiationNumberings);
                }
            }

            if (overrideReplacements || !Enumerable.Range(0, todoStacks.Count()).Any(i => replacementDict.ContainsKey(Tuple.Create(TranslateIteration(instantiationNumberings, i), todoStacks[i].Peek().id))))
            {
                for (var i = 0; i < todoStacks.Length; ++i)
                {
                    var stack = todoStacks[i];
                    var key = Tuple.Create(TranslateIteration(instantiationNumberings, i), stack.Peek().id);
                    if (!replacementDict.TryGetValue(key, out var generalizations))
                    {
                        generalizations = new List<Term>();
                        replacementDict[key] = generalizations;
                    }
                    generalizations.Add(currTerm);
                }
            }

            addToGeneralizedTerm(generalizedHistory, currTerm);
            currTerm.Responsible = todoStacks[todoStacks.Count() / 2].Peek().Responsible;
            return currTerm;
        }

        /// <summary>
        /// Adds the specified term to the arguments of its parent term.
        /// </summary>
        private static void addToGeneralizedTerm(Stack<ConstraintElementType> generalizedHistory, Term currTerm)
        {
            var genParent = generalizedHistory.Count > 0 ? generalizedHistory.Peek().Item1 : null;
            // connect to parent
            if (genParent != null)
            {
                var idx = Array.FindLastIndex(genParent.Args, t => t == null);
                genParent.Args[idx] = currTerm;
            }
        }

        private static readonly Term[] emptyTerms = new Term[0];
        private Term getGeneralizationTerm(Stack<Term>[] todoStacks, List<int> iterationNumberings)
        {
            if (TryGetExistingReplacement(todoStacks, iterationNumberings, out var existingReplacement))
            {
                generalizationTerms.Add(existingReplacement);
                return existingReplacement;
            }
            var t = new Term("T", emptyTerms, genCounter) { id = idCounter };
            generalizationTerms.Add(t);
            idCounter--;
            genCounter++;
            
            return t;
        }

        private static void pushSubterms(IEnumerable<Stack<Term>> todoStacks)
        {
            var expectedSize = todoStacks.First().Peek().Args.Length;
            foreach (var stack in todoStacks)
            {
                var curr = stack.Peek();
                if (curr.Args.Length != expectedSize) throw new ArgumentException("Tried to push subterms of terms that do not match.");
                foreach (var subterm in curr.Args)
                {
                    stack.Push(subterm);
                }
            }
        }

        private static readonly IEnumerable<Term> nonGenTerm = Enumerable.Repeat(new Term("", new Term[0], -1), 1);

        private void collectCandidateTerm(Term currentTerm, BindingInfo bindingInfo, int iteration, Dictionary<string, Tuple<int, string, int, int, int, Tuple<string, string>, Term>> candidates)
        {
            var key = currentTerm.Name + currentTerm.GenericType + currentTerm.Args.Length + "_" + currentTerm.generalizationCounter;

            var existingReplacements = bindingInfo?.bindings.Where(kv => kv.Value.Item2.id == currentTerm.id)
                .SelectMany(kv => bindingInfo.equalities.TryGetValue(kv.Key, out var eqs) ? eqs : Enumerable.Empty<Tuple<ConstraintType, Term>>())
                .SelectMany(t => replacementDict.TryGetValue(Tuple.Create(iteration, t.Item2.id), out var gen) ? gen : nonGenTerm);
            //TODO: keep tracking all existing replacements
            var generalization = existingReplacements?.FirstOrDefault();
            if (generalization == null || generalization.generalizationCounter < 0 || existingReplacements.Any(t => generalization.generalizationCounter != t.generalizationCounter))
            {
                generalization = null;
            } 

            if (!candidates.ContainsKey(key))
            {
                candidates[key] = Tuple.Create(0, currentTerm.Name + currentTerm.GenericType, currentTerm.Args.Length, currentTerm.id, currentTerm.generalizationCounter,
                    Tuple.Create(currentTerm.Theory, currentTerm.TheorySpecificMeaning), generalization);
            }
            else
            {
                var oldTuple = candidates[key];
                candidates[key] = Tuple.Create(oldTuple.Item1 + 1, oldTuple.Item2, oldTuple.Item3, oldTuple.Item4 == currentTerm.id ? oldTuple.Item4 : -1, oldTuple.Item5,  //-1 indicates disagreement on id / generalization counter
                    oldTuple.Item6 != null && currentTerm.Theory == oldTuple.Item6.Item1 && currentTerm.TheorySpecificMeaning == oldTuple.Item6.Item2 ? oldTuple.Item6 : null,
                    oldTuple.Item7 != null && generalization != null && oldTuple.Item7.id == generalization.id ? oldTuple.Item7 : null);
            }
        }

        /// <summary>
        /// Highlights structure that was added by a single iteration of the matching loop in the last term of the loop explanation by printing it in italic.
        /// </summary>
        private void HighlightNewTerms(Term newTerm, IEnumerable<Term> referenceTerms, PrettyPrintFormat format)
        {
            if (newTerm.id < 0 && !referenceTerms.Any(t => t.isSubterm(newTerm.id)))
            {
                var rule = format.getPrintRule(newTerm);
                rule.font = PrintConstants.ItalicFont;
                format.addTemporaryRule(newTerm.id.ToString(), rule);
                for (int i = 0; i < newTerm.Args.Length; ++i)
                {
                    var subterm = newTerm.Args[i];
                    HighlightNewTerms(subterm, referenceTerms, format.NextTermPrintingDepth(newTerm, i));
                }
            }
        }

        /// <summary>
        /// Highlight subterms of a generalized term in accordance with the generalized binding infos.
        /// </summary>
        public void tmpHighlightGeneralizedTerm(PrettyPrintFormat format, Term generalizedTerm, BindingInfo bindingInfo, bool last)
        {
            //If only a single generalization term (T_1) exists we print it without the subscript.
            var onlyOne = !generalizationTerms.Where(gen => gen.Args.Count() == 0).GroupBy(gen => gen.generalizationCounter).Skip(1).Any();

            //Print generalizations and terms that correspond to generalizations when wrapping around the loop in the correct color
            foreach (var term in generalizationTerms)
            {
                var rule = format.getPrintRule(term);
                rule.color = PrintConstants.generalizationColor;
                if (term.Args.Count() == 0)
                {
                    rule.prefix = new Func<bool, string>(isPrime => term.PrettyName + (term.generalizationCounter >= 0 && isPrime ? "'" : "") + (onlyOne || term.generalizationCounter < 0 ? "" : "_" + term.generalizationCounter) + (term.iterationOffset > 0 ? "_-" + term.iterationOffset : "") +
                        (term.generalizationCounter < 0 && format.showTermId ? (term.id >= 0 ? $"[{term.id}]" : $"[g{-term.id}{(isPrime ? "'" : "")}]") : ""));
                    rule.suffix = new Func<bool, string>(_ => "");
                }
                else
                {
                    rule.prefix = new Func<bool, string>(isPrime => term.PrettyName + (term.generalizationCounter >= 0 && isPrime ? "'" : "") + (term.generalizationCounter < 0 ? (format.showTermId ? (term.iterationOffset > 0 ? "_-" +
                        term.iterationOffset : "") + (term.id >= 0 ? $"[{term.id}]" : $"[g{-term.id}{(isPrime ? "'" : "")}]") : "") : "_" + (onlyOne ? term.generalizationCounter-1 : term.generalizationCounter) + (term.iterationOffset > 0 ? "_-" + term.iterationOffset : "")) + "(");
                }
                format.addTemporaryRule(term.id.ToString(), rule);
            }

            //highlight remaining terms using the generalized binding info. Note that these highlightings may override the highlightings for generalizations in some cases.
            //TODO: remove unecessary equalities / keep original dependent instantiations
            if (!generalizedTerm.dependentInstantiationsBlame.Any()) return;
            foreach (var term in bindingInfo.equalities.SelectMany(kv1 => kv1.Value.Select(t => t.Item2)))
            {
                term.highlightTemporarily(format, PrintConstants.equalityColor);
            }
            foreach (var termAndMatchContext in bindingInfo.bindings.Values)
            {
                if (termAndMatchContext != default(Tuple<List<ConstraintType>, Term>))
                {
                    termAndMatchContext.Item2.highlightTemporarily(format, PrintConstants.blameColor, termAndMatchContext.Item1);
                }
            }
            foreach (var term in bindingInfo.explicitlyBlamedTerms)
            {
                term.highlightTemporarily(format, PrintConstants.blameColor);
            }
            foreach (var termAndMatchContext in bindingInfo.bindings.Where(kv1 => kv1.Key.id == -1).Select(kv2 => kv2.Value))
            {
                if (termAndMatchContext != default(Tuple<List<ConstraintType>, Term>))
                {
                    termAndMatchContext.Item2.highlightTemporarily(format, PrintConstants.bindColor, termAndMatchContext.Item1);
                }
            }

            if (last)
            {
                var referenceTerms = Enumerable.Repeat(generalizedTerms.First().Item1, 1).Concat(generalizedTerms.First().Item3);
                HighlightNewTerms(generalizedTerms.Last().Item1, referenceTerms, format);
            }
        }

        public bool HasGeneralizationsForNextIteration()
        {
            return genReplacementTermsForNextIteration.Any();
        }

        /// <summary>
        /// Prints a section indicating what the generalization terms (T_1, T_2, ...) when starting the next loop iteration correspond to in terms
        /// of the result of the previous loop iteration.
        /// </summary>
        public void PrintGeneralizationsForNextIteration(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.switchToDefaultFormat();
            foreach (var binding in genReplacementTermsForNextIteration.GroupBy(kv => kv.Key.generalizationCounter).Select(group => group.First()).OrderBy(kv => kv.Key.generalizationCounter >= 0 ? kv.Key.generalizationCounter : kv.Value.generalizationCounter))
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
    }
}
