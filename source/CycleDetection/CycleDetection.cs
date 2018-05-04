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
        public readonly List<Term> generalizedTerms = new List<Term>();
        private readonly List<Term> genReplacements = new List<Term>();
        private readonly Dictionary<int, Term> replacementDict = new Dictionary<int, Term>();
        private readonly Dictionary<int, BindingInfo> generalizedBindings = new Dictionary<int, BindingInfo>();
        private BindingInfo wrapBindings = null;
        private HashSet<Term> loopProducedAssocBlameTerms = new HashSet<Term>();
        private Term[] potGeneralizationDependencies = new Term[0];
        private Dictionary<Term, Term> genReplacementTermsForNextIteration = new Dictionary<Term, Term>();

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
                var disitinctBlameTerms = child.bindingInfo.getDistinctBlameTerms();

                Term generalizedYield;
                if (it == 0)
                {
                    var loopResultIndex = Enumerable.Range(0, disitinctBlameTerms.Count).First(y => parent.concreteBody.isSubterm(disitinctBlameTerms[y]));
                    var concreteTerms = loopInstantiations[j].Select(inst => inst.bindingInfo.getDistinctBlameTerms()[loopResultIndex]).Where(t => t != null);
                    generalizedYield = generalizeTerms(concreteTerms, loopInstantiations[j], false, false);
                }
                else
                {
                    generalizedYield = generalizeYieldTermPointWise(loopInstantiations[i], loopInstantiations[j], j <= i, it == loopInstantiations.Length);
                }
                generalizedYield.dependentInstantiationsBlame.Add(loopInstantiations[j].First());
                generalizedTerms.Add(generalizedYield);

                potGeneralizationDependencies = genReplacements.Where(repl => repl.Args.Count() == 0)
                    .GroupBy(repl => repl.generalizationCounter).Select(group => group.First()).ToArray();

                // Other prerequisites:
                var idxList = Enumerable.Range(0, disitinctBlameTerms.Count)
                                        .Where(y => {
                                            var yTerm = disitinctBlameTerms[y];
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

                //generalize equality explanations
                generalizeEqualityExplanations(j, it == loopInstantiations.Length);

                loopInstantiations[j] = loopInstantiations[j].Select(inst =>
                {
                    var tmp = inst.Copy();
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
            if (genReplacements.Contains(loopStart))
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

        private void generalizeEqualityExplanations(int instIndex, bool loopWrapAround)
        {
            var insts = loopInstantiations[instIndex];
            var equalityExplanations = insts.First().bindingInfo.EqualityExplanations.Select(ee => new List<EqualityExplanation>() { ee }).ToArray();
            foreach (var inst in insts.Skip(1))
            {
                foreach (var listAndElement in equalityExplanations.Zip(inst.bindingInfo.EqualityExplanations, Tuple.Create))
                {
                    listAndElement.Item1.Add(listAndElement.Item2);
                }
            }

            BindingInfo generalizedBindingInfo;
            if (loopWrapAround)
            {
                generalizedBindingInfo = wrapBindings;
            }
            else
            {
                generalizedBindingInfo = generalizedBindings[insts[insts.Count / 2].Quant.BodyTerm.id];
            }

            var safeIndex = insts.Count / 2;
            var recursionPointFinder = new RecursionPointFinder();
            var candidates = new List<List<EqualityExplanation[]>>();
            for (var generation = 0; generation <= safeIndex; ++generation)
            {
                var generationSlice = loopInstantiations.Select(quantInstantiations => quantInstantiations[generation].bindingInfo.EqualityExplanations);
                if (generation == safeIndex)
                {
                    generationSlice = generationSlice.Take(instIndex);
                }
                candidates.Add(generationSlice.ToList());
            }
            candidates.Reverse();
            generalizedBindingInfo.EqualityExplanations = equalityExplanations.Select(list => {
                recursionPointFinder.visit(list[safeIndex], Tuple.Create(new List<int>(), candidates));

                var validationFilter = ValidateRecursionPoints(recursionPointFinder.recursionPoints, list);
                var validExplanations = list.Zip(validationFilter, Tuple.Create).Where(filter => filter.Item2).Select(filter => filter.Item1);
                //TODO: warn if very few remain
                var explanationsWithoutRecursion = GeneralizeAtRecursionPoints(recursionPointFinder.recursionPoints, validExplanations);

                recursionPointFinder.recursionPoints.Clear();

                var reversed = explanationsWithoutRecursion.Reverse();
                return reversed.Aggregate(EqualityExplanationGeneralizer);
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

            public override bool RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<IEnumerable<int>, EqualityExplanation> arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private bool[] ValidateRecursionPoints(Dictionary<List<int>, Tuple<int, int, int>> recursionPoints, List<EqualityExplanation> equalityExplanations)
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

                for (int i = 0; i < generationOffset; ++i) returnValue[i] = false;

                for (int i = generationOffset; i < equalityExplanations.Count; ++i)
                {
                    var explanation = equalityExplanations[i];
                    var recursionTarget = loopInstantiations[quantifer][i - generationOffset].bindingInfo.EqualityExplanations[equality];
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

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<IEnumerable<int>, Tuple<int, int>> arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private static IEnumerable<EqualityExplanation> GeneralizeAtRecursionPoints(Dictionary<List<int>, Tuple<int, int, int>> recursionPoints, IEnumerable<EqualityExplanation> equalityExplanations)
        {
            var recursionPointGeneralizer = RecursionPointGeneralizer.singleton;
            var currentGeneralizations = equalityExplanations.Reverse().ToArray();

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
            foreach (var recursionPoint in recursionPoints.OrderByDescending(kv => kv.Key, alphabeticalComparer))
            {
                var recursionInfo = Tuple.Create(recursionPoint.Value.Item2 + recursionPoint.Value.Item3, recursionPoint.Value.Item1);
                for (int i = 0; i < currentGeneralizations.Length; ++i)
                {
                    currentGeneralizations[i] = recursionPointGeneralizer.visit(currentGeneralizations[i], Tuple.Create<IEnumerable<int>, Tuple<int, int>>(recursionPoint.Key, recursionInfo));
                }
            }
            return currentGeneralizations;
        }

        private class NonRecursiveEqualityExplanationGeneralizer: EqualityExplanationVisitor<EqualityExplanation, Tuple<GeneralizationState, EqualityExplanation>>
        {
            public static readonly NonRecursiveEqualityExplanationGeneralizer singleton = new NonRecursiveEqualityExplanationGeneralizer();
            private static readonly EqualityExplanation[] emptyEqualityExplanations = new EqualityExplanation[0];

            private static Term GetGeneralizedTerm(Term gen, Term other, GeneralizationState genState)
            {
                var t1 = gen;
                var t2 = other;
                if (t1.id != t2.id)
                {
                    if (t1.id >= 0 && genState.replacementDict.TryGetValue(t1.id, out var generalization))
                    {
                        t1 = generalization;
                    }
                    if (genState.replacementDict.TryGetValue(t2.id, out generalization))
                    {
                        t2 = generalization;
                    }
                    //TODO: use new generalization, reference correct iteration
                    if (t2.id < 0) return t2;
                    if (t1.id != t2.id)
                    {
                        t1 = new Term("generalization", new Term[0])
                        {
                            id = -123
                        };
                    }
                }
                return t1;
            }

            private static EqualityExplanation DefaultGeneralization(EqualityExplanation target, EqualityExplanation other, GeneralizationState genState)
            {
                var sourceTerm = GetGeneralizedTerm(target.source, other.source, genState);
                var targetTerm = GetGeneralizedTerm(target.target, other.target, genState);
                return new TransitiveEqualityExplanation(sourceTerm, targetTerm, emptyEqualityExplanations);
            }

            public override EqualityExplanation Direct(DirectEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                if  (other.GetType() != typeof(DirectEqualityExplanation))
                {
                    return DefaultGeneralization(target, other, genState);
                }

                var otherDirect = (DirectEqualityExplanation) other;
                var sourceTerm = GetGeneralizedTerm(target.source, otherDirect.source, genState);
                var targetTerm = GetGeneralizedTerm(target.target, otherDirect.target, genState);
                var eqTerm = GetGeneralizedTerm(target.equality, otherDirect.equality, genState);
                return new DirectEqualityExplanation(sourceTerm, targetTerm, eqTerm);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                if (other.GetType() != typeof(TransitiveEqualityExplanation))
                {
                    return DefaultGeneralization(target, other, genState);
                }

                var otherTransitive = (TransitiveEqualityExplanation) other;
                if (target.equalities.Length != otherTransitive.equalities.Length)
                {
                    return DefaultGeneralization(target, other, genState);
                }

                var sourceTerm = GetGeneralizedTerm(target.source, otherTransitive.source, genState);
                var targetTerm = GetGeneralizedTerm(target.target, otherTransitive.target, genState);
                var equalities = target.equalities.Zip(otherTransitive.equalities, (gen, cur) =>
                {
                    var nextArg = Tuple.Create(genState, cur);
                    return visit(gen, nextArg);
                }).ToArray();
                return new TransitiveEqualityExplanation(sourceTerm, targetTerm, equalities);
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, Tuple<GeneralizationState, EqualityExplanation> arg)
            {
                var genState = arg.Item1;
                var other = arg.Item2;
                if (other.GetType() != typeof(CongruenceExplanation))
                {
                    return DefaultGeneralization(target, other, genState);
                }

                var otherCongruence = (CongruenceExplanation)other;
                if (target.sourceArgumentEqualities.Length != otherCongruence.sourceArgumentEqualities.Length)
                {
                    return DefaultGeneralization(target, other, genState);
                }

                var sourceTerm = GetGeneralizedTerm(target.source, otherCongruence.source, genState);
                var targetTerm = GetGeneralizedTerm(target.target, otherCongruence.target, genState);
                var equalities = target.sourceArgumentEqualities.Zip(otherCongruence.sourceArgumentEqualities, (gen, cur) =>
                {
                    var nextArg = Tuple.Create(genState, cur);
                    return visit(gen, nextArg);
                }).ToArray();
                return new CongruenceExplanation(sourceTerm, targetTerm, equalities);
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<GeneralizationState, EqualityExplanation> arg)
            {
                // Because of the validation phase these should always match.
                return target;
            }
        }

        private EqualityExplanation EqualityExplanationGeneralizer(EqualityExplanation partiallyGeneralized, EqualityExplanation concrete)
        {
            return NonRecursiveEqualityExplanationGeneralizer.singleton.visit(partiallyGeneralized, Tuple.Create(this, concrete));
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
            if (concrete.Args.Count() != quantifier.Args.Count() || concrete.Name != quantifier.Name)
            {
                return null;
            }
            for (int i = 0; i < concrete.Args.Count(); i++)
            {
                var replacement = GeneralizeBindings(concrete.Args[i], quantifier.Args[i], bindingInfo);
                if (replacement == null)
                {
                    if (ReferenceEquals(concrete, concrete.reverseRewrite))
                    {
                        return null;
                    }
                    else
                    {
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

        private Term generalizeTerms(IEnumerable<Term> terms, List<Instantiation> highlightInfoInsts, bool blameWrapAround, bool loopWrapAround)
        {
            // stacks for breath first traversal of all terms in parallel
            var todoStacks = terms.Select(t => new Stack<Term>(new[] { t })).ToArray();

            // map to 'vote' on generalization
            // also exposes outliers
            // term name + type + #Args -> #votes
            // last element is id (kept if all terms agree)
            var candidates = new Dictionary<string, Tuple<int, string, int, int, int>>();
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
                genReplacements.Add(existingReplacement);
                return existingReplacement;
            }
            var t = new Term("T", potGeneralizationDependencies, genCounter) { id = idCounter };
            genReplacements.Add(t);
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
                    (oldTuple.Item1 + 1, oldTuple.Item2, oldTuple.Item3, oldTuple.Item4 == currentTerm.id ? oldTuple.Item4 : -1, oldTuple.Item5 == currentTerm.generalizationCounter ? oldTuple.Item5 : -1);
            }
        }

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

        public void tmpHighlightGeneralizedTerm(PrettyPrintFormat format, Term generalizedTerm, bool last)
        {
            var onlyOne = !genReplacements.Where(gen => gen.Args.Count() == 0 && gen.generalizationCounter >= 0).GroupBy(gen => gen.generalizationCounter).Skip(1).Any();
            var allGeneralizations = last ? genReplacements.Concat(genReplacementTermsForNextIteration.Values) : genReplacements;
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
