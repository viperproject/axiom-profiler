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
        private readonly List<Instantiation>[] loopInstantiationsWorkSpace;

        /* The generalized versions of the yield terms of the quantifier instantiations in the loop. Since a loop explanation starts and ends
         * with generalized terms there is one more generalizedTerm than there are quantifiers:
         * generalizedTerms[0] -loopInstantiations[0].Quant-> generalizedTerms[1] -loopInstantiations[1].Quant-> ... -loopInstantiations[n].Quant-> generalizedTerms[n+1] 
         */
        public readonly List<Term> generalizedTerms = new List<Term>();
        private readonly List<Term> generalizationTerms = new List<Term>(); //All generalization terms that were used (T_1, T_2, ...)
        private readonly Dictionary<Tuple<int, int>, List<Term>> replacementDict = new Dictionary<Tuple<int, int>, List<Term>>(); //Map from concrete terms to their generalized counterparts. N.b. this includes terms other than T_1, T_2, ... e.g. if only the term id was generalized away.
        private readonly Dictionary<int, BindingInfo> generalizedBindings = new Dictionary<int, BindingInfo>(); //Map from quantifiers (their body terms) to their generalized binding info
        private BindingInfo wrapBindings = null; //The binding info used to instantiate the first quantifier of the loop from the result of the last quantifier in the loop
        private readonly HashSet<Term> loopProducedAssocBlameTerms = new HashSet<Term>(); //blame terms required in addition to the term produced by the previous instantiation that are also produced by the matching loop
        private Term[] potGeneralizationDependencies = new Term[0]; //Terms that a newly generated generailzation term may depend on. I.e. if the Term T_3(T_1, T_2) is generated the Array conatins T_1 and T_2.
        private Dictionary<Term, Term> genReplacementTermsForNextIteration = new Dictionary<Term, Term>(); //A map from generalization terms in the first term of the loop explanation (e.g. T_1) to their counterparts in the result of a single matching loop iteration (e.g. plus(T_1, x) if the loop piles up plus terms)

        // associated info to generalized blame term 
        // (meaning other generalized blame terms that are not yield terms in the loop)
        public readonly Dictionary<Term, List<Term>> assocGenBlameTerm = new Dictionary<Term, List<Term>>();
        public readonly List<Term> wrapAssocGenBlameTerms = new List<Term>();

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

        public void generalize()
        {
            if (loopInstantiations.Length == 0) return;
            for (var it = 0; it < loopInstantiationsWorkSpace.Length+1; it++)
            {
                potGeneralizationDependencies = new Term[0];
                var i = (loopInstantiationsWorkSpace.Length + it - 1) % loopInstantiationsWorkSpace.Length;
                var j = it % loopInstantiationsWorkSpace.Length;

                var robustIdx = loopInstantiationsWorkSpace[i].Count / 2;
                var parent = loopInstantiationsWorkSpace[i][j <= i ? Math.Max(robustIdx - 1, 0) : robustIdx];
                var child = loopInstantiationsWorkSpace[j][robustIdx];
                var distinctBlameTerms = child.bindingInfo.getDistinctBlameTerms();

                Term parentConcreteTerm;
                Term generalizedYield;
                var isWrapInstantiation = it == loopInstantiations.Length;
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
                    parentConcreteTerm = distinctBlameTerms[loopResultIndex];
                    var concreteTerms = loopInstantiationsWorkSpace[i].Select(inst => inst.bindingInfo.getDistinctBlameTerms()[loopResultIndex]).Where(t => t != null);
                    generalizedYield = generalizeTerms(concreteTerms, loopInstantiationsWorkSpace[j], false, false);
                }
                else
                {
                    // Here we can simply use the terms produced by the previous instantiation.
                    parentConcreteTerm = parent.concreteBody;
                    generalizedYield = generalizeYieldTermPointWise(loopInstantiationsWorkSpace[i], loopInstantiationsWorkSpace[j], j <= i, isWrapInstantiation);
                }

                //This will later be used to find out which quantifier was used to generate this term.
                generalizedYield.Responsible = loopInstantiationsWorkSpace[i].First();
                generalizedYield.dependentInstantiationsBlame.Add(loopInstantiationsWorkSpace[j].First());

                generalizedTerms.Add(generalizedYield);

                potGeneralizationDependencies = generalizationTerms.Where(repl => repl.Args.Count() == 0)
                    .GroupBy(repl => repl.generalizationCounter).Select(group => group.First()).ToArray();

                // Other prerequisites:
                var boundTos = distinctBlameTerms.SelectMany(y => child.bindingInfo.bindings.Where(kv => kv.Value.id == y.id)).Select(kv => kv.Key);
                var boundTosToKeep = distinctBlameTerms.Where(y => !parentConcreteTerm.isSubterm(y)).SelectMany(y => child.bindingInfo.bindings.Where(kv => kv.Value.id == y.id)).Select(kv => kv.Key);

                foreach (var boundTo in boundTos)
                {
                    var instantiations = loopInstantiationsWorkSpace[j].Skip(isWrapInstantiation ? 1 : 0).ToList();
                    var terms = instantiations.Select(inst => inst.bindingInfo.bindings[boundTo]);
                    var otherGenTerm = generalizeTerms(terms, instantiations, false, isWrapInstantiation);
                    otherGenTerm.Responsible = loopInstantiationsWorkSpace[i].First();
                    otherGenTerm.dependentInstantiationsBlame.Add(loopInstantiationsWorkSpace[j].First());

                    //only a finite prefix of terms may be produced outside of the loop
                    var loopProduced = terms.Select(t => loopInstantiationsWorkSpace.Any(qInsts => qInsts.Any(inst => inst.concreteBody.isSubterm(t))));
                    loopProduced = loopProduced.SkipWhile(c => !c);
                    if (loopProduced.Any() && loopProduced.All(c => c))
                    {
                        loopProducedAssocBlameTerms.Add(otherGenTerm);
                    }

                    if (boundTosToKeep.Contains(boundTo))
                    {
                        if (isWrapInstantiation)
                        {
                            wrapAssocGenBlameTerms.Add(otherGenTerm);
                        }
                        else
                        {
                            if (!assocGenBlameTerm.ContainsKey(generalizedYield))
                            {
                                assocGenBlameTerm[generalizedYield] = new List<Term>();
                            }
                            assocGenBlameTerm[generalizedYield].Add(otherGenTerm);
                        }
                    }
                }

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

                    var newBody = GeneralizeBindings(inst.concreteBody, inst.Quant.BodyTerm.Args.Last(), generalizedBindings[inst.Quant.BodyTerm.id], afterNextBindingInfo, Enumerable.Empty<Term>());
                    if (newBody != null)
                    {
                        inst.concreteBody = newBody;
                    }
                    else
                    {
                        //TODO: try partial generalization?
                        Console.Out.WriteLine($"couldn't generalize bindings for {inst}");
                    }
                    loopInstantiationsWorkSpace[j][iterator] = inst;
                };
            }

            //generalize equality explanations
            for (var it = 0; it < loopInstantiations.Length; it++)
            {
                GeneralizeEqualityExplanations(it, false);
            }
            GeneralizeEqualityExplanations(0, true);

            DoGeneralizationSubstitution();
            SimplifyAfterGeneralizationSubstitution();

            var iterationFinalTerm = generalizedTerms.Last();
            var genBindings = GetGeneralizedBindingInfo(loopInstantiations[0][0]);
            foreach (var loopEndTerm in wrapBindings.getDistinctBlameTerms().Where(t => iterationFinalTerm.isSubterm(t.id)))
            {
                foreach (var boundTo in wrapBindings.bindings.Where(kv => kv.Value.id == loopEndTerm.id).Select(kv => kv.Key))
                {

                    // These might overwrite each other. That is ok.
                    MarkGeneralizations(genBindings.bindings[boundTo], loopEndTerm);
                }
            }
        }

        private void MarkGeneralizations(Term loopStart, Term loopEnd)
        {
            if (loopStart.generalizationCounter >= 0)
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

        private void GeneralizeEqualityExplanations(int instIndex, bool loopWrapAround)
        {
            var offset = loopWrapAround ? 1 : 0;
            var insts = loopInstantiations[instIndex].Skip(offset).ToList();
            var equalityExplanationsDict = new Dictionary<Term, List<Tuple<EqualityExplanation, int>>>();
            for (var i = 0; i < insts.Count; ++i)
            {
                var inst = insts[i];
                foreach (var ee in inst.bindingInfo.EqualityExplanations)
                {
                    var possibleBoundTo = inst.bindingInfo.bindings.Where(kv => kv.Value.id == ee.target.id).Where(kv => inst.bindingInfo.equalities.TryGetValue(kv.Key, out var equality) && equality.Contains(ee.source));
                    if (possibleBoundTo.Any())
                    {
                        var boundTo = possibleBoundTo.Single().Key;
                        if (!equalityExplanationsDict.TryGetValue(boundTo, out var equalityCollector))
                        {
                            equalityCollector = new List<Tuple<EqualityExplanation, int>>();
                            equalityExplanationsDict[boundTo] = equalityCollector;
                        }
                        equalityCollector.Add(Tuple.Create(ee, i + offset));
                    }
                }
            }

            var safeIndex = insts.Count / 2;
            var equalityExplanations = equalityExplanationsDict.Values.Where(l => safeIndex < l.Count).ToArray();

            BindingInfo generalizedBindingInfo;
            if (loopWrapAround)
            {
                generalizedBindingInfo = wrapBindings;
            }
            else
            {
                generalizedBindingInfo = generalizedBindings[insts[insts.Count / 2].Quant.BodyTerm.id];
            }

            var recursionPointFinder = new RecursionPointFinder();
            var candidates = new List<List<EqualityExplanation[]>>();
            for (var generation = 0; generation <= safeIndex; ++generation)
            {
                var generationSlice = loopInstantiations.Select(quantInstantiations => quantInstantiations[generation + offset].bindingInfo.EqualityExplanations);
                if (generation == safeIndex)
                {
                    generationSlice = generationSlice.Take(instIndex);
                }
                candidates.Add(generationSlice.ToList());
            }
            candidates.Reverse();
            generalizedBindingInfo.EqualityExplanations = equalityExplanations.Select(list => {
                recursionPointFinder.visit(list[safeIndex].Item1, Tuple.Create(new List<int>(), candidates));

                var validationFilter = ValidateRecursionPoints(recursionPointFinder.recursionPoints, list);
                var validExplanations = list.Zip(validationFilter, Tuple.Create).Where(filter => filter.Item2).Select(filter => filter.Item1);
                //TODO: warn if very few remain
                var explanationsWithoutRecursion = GeneralizeAtRecursionPoints(recursionPointFinder.recursionPoints, validExplanations.Select(pair => pair.Item1));

                recursionPointFinder.recursionPoints.Clear();

                var reversed = explanationsWithoutRecursion.Zip(validExplanations.Select(pair => pair.Item2), Tuple.Create).Reverse();
                var result = reversed.Skip(1).Aggregate(reversed.First().Item1, (gen, conc) => EqualityExplanationGeneralizer(gen, conc, loopWrapAround));
                NonRecursiveEqualityExplanationGeneralizer.singleton.Reset();
                return result;
            }).ToArray();
        }

        private class RecursionPointFinder: EqualityExplanationVisitor<object, Tuple<List<int>, List<List<EqualityExplanation[]>>>>
        {
            public Dictionary<List<int>, Tuple<int, int, int>> recursionPoints = new Dictionary<List<int>, Tuple<int, int, int>>();

            private static Tuple<int, int, int> FindRecursionPoint(EqualityExplanation explanation, List<List<EqualityExplanation[]>> candidates)
            {
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
                             * checks usually performed by the Equals() method.
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
                                var candidateLength = relevantEqualities.FirstOrDefault(explanationLengthPair => explanationLengthPair.Item1.target.id == equalityCandidate.target.id).Item2;
                                if (candidateLength > length)
                                {
                                    length = candidateLength;
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
                return null;
            }

            public override object Transitive(TransitiveEqualityExplanation target, Tuple<List<int>, List<List<EqualityExplanation[]>>> arg)
            {
                var path = arg.Item1;
                var candidates = arg.Item2;
                var recursionPoint = FindRecursionPoint(target, candidates);
                if (recursionPoint == null)
                {
                    var stepSize = 1;
                    for (var i = 0; i < target.equalities.Count(); i += stepSize)
                    {
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
                var recursionPoint = FindRecursionPoint(target, candidates);
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

        private bool[] ValidateRecursionPoints(Dictionary<List<int>, Tuple<int, int, int>> recursionPoints, List<Tuple<EqualityExplanation, int>> equalityExplanations)
        {
            var recursionPointVerifier = RecursionPointVerifier.singleton;
            var returnValue = Enumerable.Repeat(true, equalityExplanations.Count).ToArray();

            foreach (var recursionPoint in recursionPoints)
            {
                var path = recursionPoint.Key;
                var recursionOffset = recursionPoint.Value;
                var generationOffset = recursionOffset.Item1;
                var quantifer = recursionOffset.Item2;
                var equality = recursionOffset.Item3;

                int i = 0;
                for (; i < equalityExplanations.Count; ++i)
                {
                    if (equalityExplanations[i].Item2 >= generationOffset) break;
                    returnValue[i] = false;
                }

                for (; i < equalityExplanations.Count; ++i)
                {
                    var explanation = equalityExplanations[i].Item1;
                    var explanationGeneratiton = equalityExplanations[i].Item2;
                    var recursionTarget = loopInstantiations[quantifer][explanationGeneratiton - generationOffset].bindingInfo.EqualityExplanations[equality];
                    var arg = Tuple.Create<IEnumerable<int>, EqualityExplanation>(path, recursionTarget);
                    if (!recursionPointVerifier.visit(explanation, arg))
                    {
                        returnValue[i] = false;
                    }
                }
            }
            return returnValue;
        }

        private class RecursionPointGeneralizer: EqualityExplanationVisitor<EqualityExplanation, Tuple<IEnumerable<int>, Tuple<int, int>>>
        {
            public static readonly RecursionPointGeneralizer singleton = new RecursionPointGeneralizer();

            public override EqualityExplanation Direct(DirectEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<int, int>> arg)
            {
                var recursionPoint = arg.Item2;
                return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<int, int>> arg)
            {
                var path = arg.Item1;
                var indicator = path.Take(3).Count();
                if (indicator == 0)
                {
                    var recursionPoint = arg.Item2;
                    return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
                }
                else if (indicator == 2 && path.ElementAt(1) < 0)
                {
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
                    var index = path.First();
                    var nextPath = path.Skip(1);
                    var nextArg = Tuple.Create(nextPath, arg.Item2);

                    var newEqualities = new EqualityExplanation[target.equalities.Length];
                    Array.Copy(target.equalities, newEqualities, target.equalities.Length);
                    newEqualities[index] = visit(target.equalities[index], nextArg);

                    return new TransitiveEqualityExplanation(target.source, target.target, newEqualities);
                }
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, Tuple<IEnumerable<int>, Tuple<int, int>> arg)
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

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<int, int>> arg)
            {
                var recursionPoint = arg.Item2;
                return new RecursiveReferenceEqualityExplanation(target.source, target.target, recursionPoint.Item1, recursionPoint.Item2);
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<int, int>> arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private IEnumerable<EqualityExplanation> GeneralizeAtRecursionPoints(Dictionary<List<int>, Tuple<int, int, int>> recursionPoints, IEnumerable<EqualityExplanation> equalityExplanations)
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

            var safeIndex = loopInstantiations[0].Count / 2;
            foreach (var recursionPoint in recursionPoints.OrderByDescending(kv => kv.Key, alphabeticalComparer))
            {
                var recursionTargetIterationOffset = recursionPoint.Value.Item1;
                var recursionTargetQuantifier = recursionPoint.Value.Item2;
                var recursionTargetEqualityIndex = recursionPoint.Value.Item3;
                
                var numberingOffset = loopInstantiations.Take(recursionTargetQuantifier).Sum(instantiations => instantiations[safeIndex].bindingInfo.GetNumberOfTermAndEqualityNumberingsUsed()) + 1;
                var numberOfGeneralizedBlameTerms = assocGenBlameTerm[generalizedTerms[recursionTargetQuantifier]].Count + 1;
                var equalityNumber = numberingOffset + numberOfGeneralizedBlameTerms + recursionTargetEqualityIndex;

                var recursionInfo = Tuple.Create(equalityNumber, recursionTargetIterationOffset);
                for (int i = 0; i < currentGeneralizations.Length; ++i)
                {
                    currentGeneralizations[i] = recursionPointGeneralizer.visit(currentGeneralizations[i], Tuple.Create<IEnumerable<int>, Tuple<int, int>>(recursionPoint.Key, recursionInfo));
                }
            }
            return currentGeneralizations;
        }

        private class NonRecursiveEqualityExplanationGeneralizer: EqualityExplanationVisitor<EqualityExplanation, Tuple<GeneralizationState, EqualityExplanation, int, bool>>
        {
            public static readonly NonRecursiveEqualityExplanationGeneralizer singleton = new NonRecursiveEqualityExplanationGeneralizer();
            private static readonly EqualityExplanation[] emptyEqualityExplanations = new EqualityExplanation[0];
            private static readonly Term[] emptyTerms = new Term[0];
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
                        foreach (var generalization in possibleGeneralizations)
                        {
                            if (t1.id >= 0)
                            {
                                if (genState.replacementDict.Any(kv => kv.Key.Item2 == t1.id && kv.Key.Item1 > iteration - offset && kv.Value.Any(g => g.id == generalization.id)))
                                {
                                    t1 = generalization;
                                    t2 = generalization;
                                    break;
                                }
                            }
                            else
                            {
                                // within a single iteration replacementDict is injective => we get the correct concrete terms
                                var alternativePreviousGeneralizations = genState.replacementDict.Where(kv => kv.Key.Item1 > iteration - offset && kv.Value.Any(t => t.id == t1.id)).Select(kv => kv.Value);
                                if (alternativePreviousGeneralizations.All(alts => alts.Any(t => t.id == generalization.id)))
                                {
                                    t1 = generalization;
                                    t2 = generalization;
                                    break;
                                }
                            }
                        }
                    }

                    if (t1.id != t2.id || (offset - wrapOffset != t1.iterationOffset + 1 && t1.iterationOffset != 0))
                    {
                        Term newGen;
                        if (t1.Name == t2.Name && t1.Args.Length == t2.Args.Length)
                        {
                            var generalizedArgs = t1.Args.Zip(t2.Args, (a1, a2) => GetGeneralizedTerm(a1, a2, genState, iteration, wrap)).ToArray();
                            newGen = new Term(t1.Name, generalizedArgs)
                            {
                                id = genState.idCounter,
                                Responsible = t1.Responsible.Quant == t2.Responsible.Quant ? t1.Responsible : null
                            };
                            --genState.idCounter;
                        }
                        else
                        {
                            newGen = new Term("T", emptyTerms, genState.genCounter)
                            {
                                id = genState.idCounter,
                                Responsible = t1.Responsible.Quant == t2.Responsible.Quant ? t1.Responsible : null
                            };
                            ++genState.genCounter;
                            --genState.idCounter;
                        }

                        locallyProducedGeneralizations.Add(newGen);

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
                            for (var i = iteration; i < genState.loopInstantiations[0].Count(); ++i)
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

                if (sourceTerm.iterationOffset != eqTerm.iterationOffset)
                {
                    var originalSourceTerm = GetGeneralizedTerm(generalized.source, other.source, genState, iteration - eqTerm.iterationOffset, wrap);
                    originalSourceTerm = CopyTermAndSetIterationOffset(originalSourceTerm, originalSourceTerm.iterationOffset + eqTerm.iterationOffset);
                    steps.Add(new TransitiveEqualityExplanation(sourceTerm, originalSourceTerm, emptyEqualityExplanations));
                    sourceTerm = originalSourceTerm;
                }

                var targetTerm = GetGeneralizedTerm(generalized.target, other.target, genState, iteration - eqTerm.iterationOffset, wrap);
                targetTerm = CopyTermAndSetIterationOffset(targetTerm, targetTerm.iterationOffset + eqTerm.iterationOffset);
                steps.Add(new DirectEqualityExplanation(sourceTerm, targetTerm, eqTerm));

                var newTargetTerm = GetGeneralizedTerm(newTarget, other.target, genState, iteration, wrap);
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
                    if (target.equalities.Length > 1 && target.equalities.Length <= 3)
                    {
                        if (other.GetType() == typeof(DirectEqualityExplanation))
                        {
                            var otherDirect = (DirectEqualityExplanation) other;
                            var generalizedIndex = Array.FindIndex(target.equalities, ee => ee.GetType() == typeof(DirectEqualityExplanation));
                            var generalized = (DirectEqualityExplanation) target.equalities[generalizedIndex];
                            var newSource = generalizedIndex == 1 ? target.equalities[0].source : generalized.source;
                            var newTarget = generalizedIndex + 1 < target.equalities.Length ? target.equalities[generalizedIndex + 1].target : generalized.target;
                            return GeneralizeDirect(newSource, newTarget, generalized, otherDirect, genState, iteration, wrap);
                        }
                        else if (other.GetType() == typeof(RecursiveReferenceEqualityExplanation))
                        {
                            var otherRecursive = (RecursiveReferenceEqualityExplanation) other;
                            var generalizedIndex = Array.FindIndex(target.equalities, ee => ee.GetType() == typeof(RecursiveReferenceEqualityExplanation));
                            var generalized = (RecursiveReferenceEqualityExplanation) target.equalities[generalizedIndex];
                            var newSource = generalizedIndex == 1 ? target.equalities[0].source : generalized.source;
                            var newTarget = generalizedIndex + 1 < target.equalities.Length ? target.equalities[generalizedIndex + 1].target : generalized.target;
                            return GeneralizeRecursive(newSource, newTarget, generalized, otherRecursive, genState, iteration, wrap);
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

                if (generalizedSource.iterationOffset != generalized.GenerationOffset)
                {
                    var originalSource = GetGeneralizedTerm(generalized.source, other.source, genState, iteration - generalized.GenerationOffset, wrap);
                    originalSource = CopyTermAndSetIterationOffset(originalSource, originalSource.iterationOffset + generalized.GenerationOffset);
                    steps.Add(new TransitiveEqualityExplanation(generalizedSource, originalSource, emptyEqualityExplanations));
                    generalizedSource = originalSource;
                }

                var generalizedTarget = GetGeneralizedTerm(generalized.target, other.target, genState, iteration - generalized.GenerationOffset, wrap);
                generalizedTarget = CopyTermAndSetIterationOffset(generalizedTarget, generalizedTarget.iterationOffset + generalized.GenerationOffset);

                steps.Add(new RecursiveReferenceEqualityExplanation(generalizedSource, generalizedTarget, generalized.EqualityNumber, generalized.GenerationOffset));

                var newTargetTerm = GetGeneralizedTerm(newTarget, other.target, genState, iteration, wrap);
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

        private class GeneralizationEqualitiesCollector : EqualityExplanationVisitor<object, Dictionary<Term, List<Term>>>
        {
            public static readonly GeneralizationEqualitiesCollector singleton = new GeneralizationEqualitiesCollector();

            private static void InsertEquality(Dictionary<Term, List<Term>> dict, Term gen, Term equalTo)
            {
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

            public override object Transitive(TransitiveEqualityExplanation target, Dictionary<Term, List<Term>> arg)
            {
                var equalToTerms = target.equalities.Select(ee => ee.source).ToList();
                equalToTerms.Add(target.source);
                equalToTerms.Add(target.target);
                equalToTerms = equalToTerms.Distinct().ToList();

                foreach (var term in equalToTerms)
                {
                    if (term.generalizationCounter >= 0)
                    {
                        if (arg.TryGetValue(term, out var existingEqualTos))
                        {
                            existingEqualTos.AddRange(equalToTerms);
                        }
                        else
                        {
                            arg[term] = new List<Term>(equalToTerms);
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

            foreach (var bindingInfo in generalizedBindings.Values)
            {
                foreach (var ee in bindingInfo.EqualityExplanations)
                {
                    GeneralizationEqualitiesCollector.singleton.visit(ee, collectedEqs);
                }
            }
            foreach (var ee in wrapBindings.EqualityExplanations)
            {
                GeneralizationEqualitiesCollector.singleton.visit(ee, collectedEqs);
            }

            return collectedEqs;
        }

        private List<int> SelectGeneralizationsToKeep(Dictionary<Term, List<Term>> eqs)
        {
            var gensToCover = generalizationTerms.Select(t => t.generalizationCounter).Distinct();
            var generalizationLookup = new Dictionary<int, Term>();
            foreach (var gen in gensToCover)
            {
                generalizationLookup[gen] = generalizationTerms.First(t => t.generalizationCounter == gen);
            }

            var coverageClosures = new Dictionary<int, HashSet<int>>();

            // Reflexive
            foreach (var gen in gensToCover)
            {
                coverageClosures[gen] = new HashSet<int>() { gen };
            }

            // Transitive
            foreach (var kv in eqs)
            {

                // isSubtermGen is true if the term itself is the generalization in question => includes regular equality edges
                var coveringGens = gensToCover.Where(gen => kv.Value.Any(t => t.isSubtermGen(gen)));
                foreach (var coveringGen in coveringGens)
                {
                    coverageClosures[coveringGen].Add(kv.Key.generalizationCounter);
                }
            }
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

            var chosenGens = new List<int>();
            var coveredGens = new HashSet<int>();

            while (!coveredGens.IsSupersetOf(gensToCover))
            {
                var maxCoverable = coverageClosures.Max(kv => kv.Value.Count());
                var candidates = coverageClosures.Where(kv => kv.Value.Count() == maxCoverable);
                var heuristic = candidates.Min(kv => generalizationLookup[kv.Key].Args.Length);
                var chosen = candidates.First(kv => generalizationLookup[kv.Key].Args.Length == heuristic);
                chosenGens.Add(chosen.Key);
                coveredGens.UnionWith(chosen.Value);

                coverageClosures.Remove(chosen.Key);
                foreach (var value in coverageClosures.Values)
                {
                    value.ExceptWith(chosen.Value);
                }
            }
            
            return chosenGens;
        }

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

            var boundTo = bindingInfo.bindings.Where(kv => kv.Value.id == t.id).Select(kv => kv.Key).ToList();
            foreach (var key in boundTo)
            {
                bindingInfo.bindings[key] = resultTerm;
            }

            foreach (var eqList in bindingInfo.equalities.Values)
            {
                if (eqList.Any(e => e.id == t.id))
                {
                    eqList.RemoveAll(e => e.id == t.id);
                    eqList.Add(resultTerm);
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

        private Dictionary<int, Term> GenerateGeneralizationSubstitutions()
        {
            var substitutionMap = new Dictionary<int, Term>();

            var eqs = CollectGeneralizationEqualities();
            foreach (var kv in eqs)
            {
                var nonGeneralTerm = kv.Value.FirstOrDefault(t => !t.ContainsGeneralization());
                if (nonGeneralTerm != null)
                {
                    substitutionMap[kv.Key.generalizationCounter] = nonGeneralTerm;
                }
            }
            var valuesToRemove = eqs.Keys.Where(t => substitutionMap.Keys.Contains(t.generalizationCounter)).ToList();
            foreach (var list in eqs.Values)
            {
                list.RemoveAll(item => valuesToRemove.Contains(item));
            }

            var gensToKeep = SelectGeneralizationsToKeep(eqs).Where(gen => !substitutionMap.ContainsKey(gen));
            foreach (var gen in gensToKeep)
            {
                substitutionMap[gen] = generalizationTerms.First(t => t.generalizationCounter == gen);
            }

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
                    substitutionMap.Add(key, chosenSubstitution);
                }
                else
                {
                    workQueue.Enqueue(substitution);
                }
            }

            return substitutionMap;
        }

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
                return new RecursiveReferenceEqualityExplanation(newSource, newTarget, target.EqualityNumber, target.GenerationOffset, target.isPrime);
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

        private void DoGeneralizationSubstitution()
        {
            var substitutionMap = GenerateGeneralizationSubstitutions();

            var newGenTerms = new List<Term>();
            for (var i = 0; i < generalizedTerms.Count; ++i)
            {
                var term = generalizedTerms[i];
                var bindingInfo = i < generalizedTerms.Count - 1 ? generalizedBindings[loopInstantiations[i][0].Quant.BodyTerm.id] : wrapBindings;

                var substitution = SubstituteAndUpdateBindings(term, substitutionMap, bindingInfo);

                if (assocGenBlameTerm.TryGetValue(term, out var assocTerms))
                {
                    var newAssocTerms = assocTerms.Select(t => SubstituteAndUpdateBindings(t, substitutionMap, bindingInfo)).ToList();
                    assocGenBlameTerm.Remove(term);
                    assocGenBlameTerm.Add(substitution, newAssocTerms);
                }

                newGenTerms.Add(substitution);
                
                for (var idx = 0; idx < bindingInfo.EqualityExplanations.Length; ++idx)
                {
                    bindingInfo.EqualityExplanations[idx] = EqualityExplanationSubstituter.singleton.visit(bindingInfo.EqualityExplanations[idx], substitutionMap);
                }
            }

            for (var i = 0; i < wrapAssocGenBlameTerms.Count; ++i)
            {
                wrapAssocGenBlameTerms[i] = SubstituteAndUpdateBindings(wrapAssocGenBlameTerms[i], substitutionMap, wrapBindings);
            }

            generalizedTerms.Clear();
            generalizedTerms.AddRange(newGenTerms);
        }

        private class EqualityExplanationSimplifier : EqualityExplanationVisitor<EqualityExplanation, object>
        {
            public static readonly EqualityExplanationSimplifier singleton = new EqualityExplanationSimplifier();

            private static readonly EqualityExplanation[] NoExplanations = new EqualityExplanation[0];

            public override EqualityExplanation Direct(DirectEqualityExplanation target, object arg)
            {
                if (target.source.id == target.target.id && target.source.iterationOffset == target.target.iterationOffset) return null;
                else return target;
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                if (target.source.id == target.target.id && target.source.iterationOffset == target.target.iterationOffset) return null;
                else return target;
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, object arg)
            {
                if (target.source.id == target.target.id && target.source.iterationOffset == target.target.iterationOffset) return null;
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
                            return current.id == other.id && current.iterationOffset == other.iterationOffset;
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
                if (target.source.id == target.target.id && target.source.iterationOffset == target.target.iterationOffset) return null;
                else
                {
                    var simplifiedArgumentExplanations = target.sourceArgumentEqualities.Select(ee => visit(ee, arg) ?? new TransitiveEqualityExplanation(ee.source, ee.target, NoExplanations)).ToArray();
                    return new CongruenceExplanation(target.source, target.target, simplifiedArgumentExplanations);
                }
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, object arg)
            {
                if (target.source.id == target.target.id && target.source.iterationOffset == target.target.iterationOffset) return null;
                else return target;
            }
        }

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
                if (simplifiedExplanation == null)
                {
                    var boundTos = bindingInfo.bindings.Where(kv => kv.Value.id == equalityExplanation.target.id).Select(kv => kv.Key);
                    foreach (var boundTo in boundTos)
                    {
                        var lhs = bindingInfo.equalities[boundTo];
                        lhs.RemoveAll(t => t.id == equalityExplanation.source.id);
                        if (lhs.Count == 0)
                        {
                            bindingInfo.equalities.Remove(boundTo);
                        }
                    }
                }
                else
                {
                    simplifiedEqualityExplanations.Add(simplifiedExplanation);
                }
            }
            bindingInfo.EqualityExplanations = simplifiedEqualityExplanations.ToArray();
        }

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

        private void SimplifyTermIds(Term t, Dictionary<string, IDLookupEntry> idLookup)
        {
            if (!idLookup.TryGetValue(t.Name, out var cursor))
            {
                cursor = new IDLookupEntry(t.Args.Length == 0, t.id);
                idLookup[t.Name] = cursor;
            }
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
                if (t.id != cursor.Id)
                {
                    foreach (var bindingInfo in generalizedBindings.Values.Concat(Enumerable.Repeat(wrapBindings, 1)))
                    {
                        if (bindingInfo.matchContext.TryGetValue(t.id, out var contexts))
                        {
                            if (!bindingInfo.matchContext.TryGetValue(cursor.Id, out var newContexts))
                            {
                                newContexts = new List<List<Term>>();
                                bindingInfo.matchContext[cursor.Id] = newContexts;
                            }
                            newContexts.AddRange(contexts);
                            bindingInfo.matchContext.Remove(t.id);
                        }
                    }

                    t.id = cursor.Id;
                }
            }
            else
            {
                cursor.HasId = true;
                cursor.Id = t.id;
            }
        }

        private void SimplifyAfterGeneralizationSubstitution()
        {
            var idLookup = new Dictionary<string, IDLookupEntry>();
            foreach (var bindingInfo in generalizedBindings.Values)
            {
                SimplifyBindingInfo(bindingInfo, idLookup);
            }
            SimplifyBindingInfo(wrapBindings, idLookup);

            foreach (var loopTerm in generalizedTerms)
            {
                SimplifyTermIds(loopTerm, idLookup);

                if (assocGenBlameTerm.TryGetValue(loopTerm, out var assocTerms))
                {
                    assocTerms.RemoveAll(t => t.generalizationCounter >= 0);
                    if (assocTerms.Count == 0)
                    {
                        assocGenBlameTerm.Remove(loopTerm);
                    }
                }
            }
            wrapAssocGenBlameTerms.RemoveAll(t => t.generalizationCounter >= 0);

            foreach (var assocTerm in assocGenBlameTerm.Values.SelectMany(x => x))
            {
                SimplifyTermIds(assocTerm, idLookup);
            }
            foreach (var assocTerm in wrapAssocGenBlameTerms)
            {
                SimplifyTermIds(assocTerm, idLookup);
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
        /// <param name="childBindingInfo">The binding info for the instantiation triggered by the concrete term that will be updated.</param>
        /// <param name="history">The history of parent terms used to check match contexts.</param>
        /// <returns>The concrete term updated to use the specified bindings or null if the algorithm failed. The algorithm fails if the
        /// structue of the concrete term (and the term obtained by reversing z3's rewritings) and the quantifier body do not match.</returns>
        private static Term GeneralizeBindings(Term concrete, Term quantifier, BindingInfo bindingInfo, BindingInfo childBindingInfo, IEnumerable<Term> history)
        {
            Term replacement;
            if (quantifier.id == -1)
            {

                // We have reached a quantified variable in the quantifier body => replace the concrete term with the term bound to that quantified variable
                replacement = bindingInfo.bindings[quantifier];
            }
            else
            {

                //If the concrete term doesn't structurally match the qantifier reverse any rewritings made by z3
                var concreteContinue = concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name ? concrete.reverseRewrite : concrete;

                //recurse over the arguments
                replacement = GeneralizeChildrenBindings(concreteContinue, quantifier, bindingInfo, childBindingInfo, history.Concat(Enumerable.Repeat(concrete, 1)));
            }

            if (childBindingInfo != null && childBindingInfo.matchContext.TryGetValue(concrete.id, out var constraints) && constraintsSat(history.ToList(), constraints))
            {
                var keys = childBindingInfo.bindings.Where(kv => kv.Value == concrete).Select(kv => kv.Key).ToList();
                foreach (var key in keys)
                {
                    childBindingInfo.bindings[key] = replacement;
                }

                foreach (var list in childBindingInfo.equalities.Values)
                {
                    if (list.Contains(concrete))
                    {
                        list.Remove(concrete);
                        list.Add(replacement);
                    }
                }

                childBindingInfo.matchContext.Remove(concrete.id);
                childBindingInfo.matchContext[replacement.id] = constraints;
            }
            return replacement;
        }

        private static Term GeneralizeChildrenBindings(Term concrete, Term quantifier, BindingInfo bindingInfo, BindingInfo childBindingInfo, IEnumerable<Term> history)
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
                var replacement = GeneralizeBindings(concrete.Args[i], quantifier.Args[i], bindingInfo, childBindingInfo, history);
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
                        return GeneralizeChildrenBindings(concrete.reverseRewrite, quantifier, bindingInfo, childBindingInfo, history);
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
            // 4th and 5th element are id and generalization counter (kept if all terms agree)
            // last element is a T term all terms are equal to or null
            var candidates = new Dictionary<string, Tuple<int, string, int, int, int, Term>>();
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
                    localBindingInfo = highlightInfoInsts[highlightInfoInsts.Count / 2].bindingInfo.Clone();
                    localBindingInfo.matchContext.Clear();
                    localBindingInfo.bindings.Clear();
                    localBindingInfo.equalities.Clear();
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
                    localBindingInfo = highlightInfoInsts[highlightInfoInsts.Count / 2].bindingInfo.Clone();
                    localBindingInfo.matchContext.Clear();
                    localBindingInfo.bindings.Clear();
                    localBindingInfo.equalities.Clear();
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
                int i = 0;
                foreach (var currentTermAndBindings in todoStacks.Select(stack => stack.Peek()).Zip(highlightInfoInsts.Skip(blameWrapAround ? 1 : 0), Tuple.Create))
                {
                    var currentTerm = currentTermAndBindings.Item1;
                    var boundIn = currentTermAndBindings.Item2;
                    var boundTo = boundIn.bindingInfo.bindings.Keys.FirstOrDefault(k => boundIn.bindingInfo.bindings[k].id == currentTerm.id);
                    if (boundTo != null && localBindingInfo.bindings.TryGetValue(boundTo, out var existing) && existing.id < -1)
                    {
                        /* The term was bound to a quantified variable and was already encountered and generalized in this term,
                         * i.e. the same quantified variable occurs multiple times in the quantifer body. If this is the case
                         * for all concrete terms we reuse the exisiting generalization.
                         */
                        collectCandidateTerm(existing, boundIn.bindingInfo, i, candidates);
                    }
                    else
                    {
                        collectCandidateTerm(currentTerm, boundIn.bindingInfo, i, candidates);
                    }
                    ++i;
                }

                var currTerm = getGeneralizedTerm(candidates, todoStacks, generalizedHistory, !loopWrapAround);

                // Update binding info with generalized term if necessary.
                if (isBlameTerm(highlightInfoInsts, todoStacks, concreteHistory, blameWrapAround, out var bindingKey))
                {
                    if (localBindingInfo.bindings.TryGetValue(bindingKey, out var existing) && existing.id != currTerm.id)
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
                if (isEqTerm(highlightInfoInsts, todoStacks, concreteHistory, blameWrapAround, out var key))
                {
                    if (!localBindingInfo.equalities.TryGetValue(key, out var eqList))
                    {
                        eqList = new List<Term>();
                        localBindingInfo.equalities[key] = eqList;
                    }
                    if (!eqList.Contains(currTerm))
                    {
                        eqList.Add(currTerm);
                    }
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
                if (currTerm.Args.Length > 0 && currTerm.generalizationCounter < 0)
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

        private bool isEqTerm(List<Instantiation> childInsts, Stack<Term>[] todoStacks, Stack<Term> concreteHistory, bool flipped, out Term boundTo)
        {
            //We try to find an equality that was used in the concrete case and assume that it should also be extended to the generalized case
            boundTo = null;
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
                        boundTo = equality.Key;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool constraintsSat(List<Term> gerneralizeHistory, List<List<Term>> constraints)
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

            if (replacementDict.TryGetValue(Tuple.Create(0, guardTerm.id), out var existingGenTermList))
            {
                var existingGenTerms = new HashSet<Term>(existingGenTermList);
                // can only reuse existing term if ALL replaced terms agree on the same generalization.
                for (var i = 1; i < todoStacks.Length; i++)
                {
                    if (!replacementDict.TryGetValue(Tuple.Create(i, todoStacks[i].Peek().id), out var nextGenTerms))
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

        private Term getGeneralizedTerm(Dictionary<string, Tuple<int, string, int, int, int, Term>> candidates, Stack<Term>[] todoStacks, Stack<Term> generalizedHistory, bool overrideReplacements)
        {
            Term currTerm;
            if (candidates.Count == 1)
            {
                // consensus -> decend further
                var value = candidates.Values.First();
                if (value.Item4 == -1)
                {
                    if (TryGetExistingReplacement(todoStacks, out var existingGenTerm) && existingGenTerm.Name == value.Item2)
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
            }
            else
            {
                // no consensus --> generalize
                // todo: if necessary, detect outlier
                var existingGeneralizations = candidates.Select(c => c.Value.Item6);
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
                    currTerm = getGeneralizedTerm(todoStacks);
                }
            }

            if (overrideReplacements || !Enumerable.Range(0, todoStacks.Count()).Any(i => replacementDict.ContainsKey(Tuple.Create(i, todoStacks[i].Peek().id))))
            {
                for (var i = 0; i < todoStacks.Length; ++i)
                {
                    var stack = todoStacks[i];
                    var key = Tuple.Create(i, stack.Peek().id);
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

        private static readonly IEnumerable<Term> nonGenTerm = Enumerable.Repeat(new Term("", new Term[0], -1), 1);

        private void collectCandidateTerm(Term currentTerm, BindingInfo bindingInfo, int iteration, Dictionary<string, Tuple<int, string, int, int, int, Term>> candidates)
        {
            var key = currentTerm.Name + currentTerm.GenericType + currentTerm.Args.Length + "_" + currentTerm.generalizationCounter;

            var existingReplacements = bindingInfo.bindings.Where(kv => kv.Value.id == currentTerm.id)
                .SelectMany(kv => bindingInfo.equalities.TryGetValue(kv.Key, out var eqs) ? eqs : Enumerable.Empty<Term>())
                .SelectMany(t => replacementDict.TryGetValue(Tuple.Create(iteration, t.id), out var gen) ? gen : nonGenTerm);
            //TODO: track all
            var generalization = existingReplacements.FirstOrDefault();
            if (generalization == null || generalization.generalizationCounter < 0 || existingReplacements.Any(t => generalization.generalizationCounter != t.generalizationCounter))
            {
                generalization = null;
            } 

            if (!candidates.ContainsKey(key))
            {
                candidates[key] = new Tuple<int, string, int, int, int, Term>
                    (0, currentTerm.Name + currentTerm.GenericType, currentTerm.Args.Length, currentTerm.id, currentTerm.generalizationCounter, generalization);
            }
            else
            {
                var oldTuple = candidates[key];
                candidates[key] = new Tuple<int, string, int, int, int, Term>
                    (oldTuple.Item1 + 1, oldTuple.Item2, oldTuple.Item3, oldTuple.Item4 == currentTerm.id ? oldTuple.Item4 : -1, oldTuple.Item5,
                    oldTuple.Item6 != null && oldTuple.Item6.id == generalization.id ? oldTuple.Item6 : null); //-1 indicates disagreement on id / generalization counter
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
                    HighlightNewTerms(subterm, referenceTerm, format.NextTermPrintingDepth(newTerm, i));
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
                    rule.prefix = new Func<bool, string>(isPrime => term.Name + (term.generalizationCounter >= 0 && isPrime ? "'" : "") + (onlyOne || term.generalizationCounter < 0 ? "" : "_" + term.generalizationCounter) + (term.iterationOffset > 0 ? "_-" + term.iterationOffset : "") +
                        (term.generalizationCounter < 0 && format.showTermId ? $"[g{-term.id}{(isPrime ? "'" : "")}]" : ""));
                    rule.suffix = new Func<bool, string>(_ => "");
                }
                else
                {
                    rule.prefix = new Func<bool, string>(isPrime => term.Name + (term.generalizationCounter >= 0 && isPrime ? "'" : "") + (term.generalizationCounter < 0 ? (format.showTermId ? (term.iterationOffset > 0 ? "_-" +
                        term.iterationOffset : "") + $"[g{-term.id}{(isPrime ? "'" : "")}]" : "") : "_" + (onlyOne ? term.generalizationCounter-1 : term.generalizationCounter) + (term.iterationOffset > 0 ? "_-" + term.iterationOffset : "")) + "(");
                }
                format.addTemporaryRule(term.id.ToString(), rule);
            }

            //highlight remaining terms using the generalized binding info. Note that these highlightings may override the highlightings for generalizations in some cases.
            //TODO: remove unecessary equalities / keep original dependent instantiations
            if (!generalizedTerm.dependentInstantiationsBlame.Any()) return;
            var bindingInfo = last ? wrapBindings : generalizedBindings[generalizedTerm.dependentInstantiationsBlame.First().Quant.BodyTerm.id];
            foreach (var term in bindingInfo.equalities.SelectMany(kv1 => kv1.Value))
            {
                term.highlightTemporarily(format, PrintConstants.equalityColor);
            }
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
