using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.CycleDetection;
using AxiomProfiler.PrettyPrinting;
using System;
using AxiomProfiler.Utilities;

namespace AxiomProfiler.QuantifierModel
{
    public class InstantiationPath : IPrintable
    {
        private readonly List<Instantiation> pathInstantiations;

        public InstantiationPath()
        {
            pathInstantiations = new List<Instantiation>();
        }

        public InstantiationPath(InstantiationPath other) : this()
        {
            pathInstantiations.AddRange(other.pathInstantiations);
        }

        public InstantiationPath(Instantiation inst) : this()
        {
            pathInstantiations.Add(inst);
        }

        public double Cost()
        {
            return pathInstantiations.Sum(instantiation => instantiation.Cost);
        }

        public int Length()
        {
            return pathInstantiations.Count;
        }

        public void prepend(Instantiation inst)
        {
            pathInstantiations.Insert(0, inst);
        }

        public void append(Instantiation inst)
        {
            pathInstantiations.Add(inst);
        }

        public void appendWithOverlap(InstantiationPath other)
        {
            var joinIdx = other.pathInstantiations.FindIndex(inst => !pathInstantiations.Contains(inst));
            if (other.Length() == 0 || joinIdx == -1)
            {
                return;
            }
            pathInstantiations.AddRange(other.pathInstantiations.GetRange(joinIdx, other.pathInstantiations.Count - joinIdx));
        }

        /// <summary>
        /// Provides some statistics about how often each quantifier occurs in the path.
        /// </summary>
        public IEnumerable<Tuple<Tuple<Quantifier, Term, Term>, int>> Statistics()
        {
            return pathInstantiations.Zip(pathInstantiations.Skip(1), Tuple.Create)
                .SelectMany(i => i.Item2.bindingInfo.bindings.Where(kv => i.Item1.dependentTerms.Any(t => t.id == kv.Value.Item2.id))
                    .Select(kv => Tuple.Create(i.Item2.Quant, i.Item2.bindingInfo.fullPattern, kv.Key)))
                    .GroupBy(x => x).Select(group => Tuple.Create(group.Key, group.Count()));
        }

        /// <summary>
        /// Indicates the number of different quantifiers in the path, also distinguishing them by type of incomming edge
        /// (what part of the trigger the result of the preceeding instantiation matched).
        /// </summary>
        public int NumberOfDistinctQuantifierFingerprints()
        {
            if (!pathInstantiations.Any()) return 0;

            var perInstantiationFingerprints = pathInstantiations.Zip(pathInstantiations.Skip(1), (prev, next) => next.bindingInfo.bindings
                .Where(kv => prev.dependentTerms.Any(t => t.id == kv.Value.Item2.id))
                .Select(kv => Tuple.Create(next.Quant, next.bindingInfo.fullPattern, kv.Key)))
                .ToList();
            var fingerprintGroupings = perInstantiationFingerprints.SelectMany(x => x).GroupBy(x => x);
            var stats = new Dictionary<Tuple<Quantifier, Term, Term>, int>();
            foreach (var group in fingerprintGroupings)
            {
                stats[group.Key] = group.Count();
            }
            var orderedStats = stats.OrderByDescending(kv => kv.Value);

            // We basically select fingerprints greedily until we have at least one fingerprint for each
            // instantiation.
            var firstInstantiation = pathInstantiations.First();
            var firstQuant = orderedStats.FirstOrDefault(stat => stat.Key.Item1 == firstInstantiation.Quant &&
                stat.Key.Item2 == firstInstantiation.bindingInfo.fullPattern).Key;
            for (var i = 0; i < perInstantiationFingerprints.Count;)
            {
                var fingerprints = perInstantiationFingerprints[i];
                if (fingerprints.Contains(firstQuant))
                {
                    foreach (var fingerprint in fingerprints)
                    {
                        --stats[fingerprint];
                    }
                    perInstantiationFingerprints.RemoveAt(i);
                }
                else
                {
                    ++i;
                }
            }

            var count = 1;
            while (orderedStats.Any() && orderedStats.First().Value > 0)
            {
                var curQuant = orderedStats.First().Key;
                ++count;

                for (var i = 0; i < perInstantiationFingerprints.Count;)
                {
                    var fingerprints = perInstantiationFingerprints[i];
                    if (fingerprints.Contains(curQuant))
                    {
                        foreach (var fingerprint in fingerprints)
                        {
                            --stats[fingerprint];
                        }
                        perInstantiationFingerprints.RemoveAt(i);
                    }
                    else
                    {
                        ++i;
                    }
                }
            }

            return count;
        }

        private CycleDetection.CycleDetection cycleDetector;

        private bool hasCycle()
        {
            if (cycleDetector == null)
            {
                cycleDetector = new CycleDetection.CycleDetection(pathInstantiations, 3);
            }
            return cycleDetector.hasCycle();
        }

        public void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (hasCycle())
            {
                printCycleInfo(content, format);
                return;
            }
            
            content.switchFormat(PrintConstants.TitleFont, PrintConstants.defaultTextColor);
            content.Append("Path explanation:");
            content.switchToDefaultFormat();
            content.Append("\n\nLength: " + Length()).Append('\n');
            printPreamble(content, false);

            var pathEnumerator = pathInstantiations.GetEnumerator();
            if (!pathEnumerator.MoveNext() || pathEnumerator.Current == null) return; // empty path
            var current = pathEnumerator.Current;

            // first thing
            content.switchToDefaultFormat();

            var termNumbering = 1;
            if (current.bindingInfo == null)
            {
                legacyInstantiationInfo(content, format, current);
            }
            else
            {
                termNumbering = printPathHead(content, format, current);
            }
            
            while (pathEnumerator.MoveNext() && pathEnumerator.Current != null)
            {
                // between stuff
                var previous = current;
                current = pathEnumerator.Current;
                if (current.bindingInfo == null)
                {
                    legacyInstantiationInfo(content, format, current);
                    continue;
                }
                termNumbering = printInstantiationWithPredecessor(content, format, current, previous, cycleDetector, termNumbering);
            }

            // Quantifier info for last in chain
            content.switchToDefaultFormat();
            content.Append("\n\nThis instantiation yields:\n\n");

            if (current.concreteBody != null)
            {
                current.concreteBody.PrettyPrint(content, format);
            }
        }

        private static int printInstantiationWithPredecessor(InfoPanelContent content, PrettyPrintFormat format,
            Instantiation current, Instantiation previous, CycleDetection.CycleDetection cycDetect, int termNumbering)
        {
            current.tempHighlightBlameBindTerms(format);

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\n\nThis instantiation yields:\n\n");
            content.switchToDefaultFormat();

            if (!format.termNumbers.TryGetValue(previous.concreteBody, out var termNumber))
            {
                termNumber = termNumbering;
                ++termNumbering;
                format.termNumbers[previous.concreteBody] = termNumber;
            }

            var numberingString = $"({termNumber}) ";
            content.Append($"{numberingString}");
            previous.concreteBody.PrettyPrint(content, format, numberingString.Length);

            // Other prerequisites:
            var otherRequiredTerms = current.bindingInfo.getDistinctBlameTerms()
                .FindAll(term => current.bindingInfo.equalities.Any(eq => current.bindingInfo.bindings[eq.Key].Item2 == term) ||
                        !previous.concreteBody.isSubterm(term)).ToList();
            if (otherRequiredTerms.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nTogether with the following term(s):");
                content.switchToDefaultFormat();
                foreach (var distinctBlameTerm in otherRequiredTerms)
                {
                    if (!format.termNumbers.TryGetValue(distinctBlameTerm, out termNumber))
                    {
                        termNumber = termNumbering;
                        ++termNumbering;
                        format.termNumbers[distinctBlameTerm] = termNumber;
                    }

                    numberingString = $"({termNumber}) ";
                    content.Append($"\n\n{numberingString}");
                    distinctBlameTerm.PrettyPrint(content, format, numberingString.Length);
                    content.switchToDefaultFormat();
                }
            }

            if (current.bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                format.printContextSensitive = false;
                foreach (var equality in current.bindingInfo.equalities)
                {
                    var effectiveTerm = current.bindingInfo.bindings[equality.Key].Item2;
                    foreach (var term in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                    {
                        var explanation = current.bindingInfo.EqualityExplanations.First(ee => ee.source.id == term.id && ee.target.id == effectiveTerm.id);
                        if (!format.equalityNumbers.TryGetValue(explanation, out var eeNumber))
                        {
                            eeNumber = termNumbering;
                            ++termNumbering;
                            format.equalityNumbers[explanation] = eeNumber;
                        }
                        if (format.ShowEqualityExplanations)
                        {
                            explanation.PrettyPrint(content, format, eeNumber);
                        }
                        else
                        {
                            numberingString = $"({eeNumber}) ";
                            content.switchToDefaultFormat();
                            content.Append(numberingString);
                            var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                            term.PrettyPrint(content, format, numberingString.Length);
                            content.switchToDefaultFormat();
                            content.Append($"\n{indentString}= (explanation omitted)\n{indentString}");
                            effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                        }
                        content.Append("\n\n");
                    }
                }
                format.printContextSensitive = true;

                current.bindingInfo.PrintEqualitySubstitution(content, format);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\n\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.switchToDefaultFormat();
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            content.switchToDefaultFormat();
            format.restoreAllOriginalRules();
            return termNumbering;
        }

        private static int printPathHead(InfoPanelContent content, PrettyPrintFormat format, Instantiation current)
        {
            var termNumbering = 1;
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nStarting from the following term(s):\n\n");
            content.switchToDefaultFormat();
            current.tempHighlightBlameBindTerms(format);
            var blameTerms = current.bindingInfo.getDistinctBlameTerms();
            var distinctBlameTerms = blameTerms
                .Where(req => current.bindingInfo.equalities.Keys.All(k => current.bindingInfo.bindings[k].Item2 != req));
            foreach (var distinctBlameTerm in distinctBlameTerms)
            {
                if (!format.termNumbers.TryGetValue(distinctBlameTerm, out var termNumber))
                {
                    termNumber = termNumbering;
                    ++termNumbering;
                    format.termNumbers[distinctBlameTerm] = termNumber;
                }
                var numberingString = $"({termNumber}) ";
                content.Append(numberingString);
                distinctBlameTerm.PrettyPrint(content, format, numberingString.Length);
                content.switchToDefaultFormat();
                content.Append("\n\n");
            }

            if (current.bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();
                
                format.printContextSensitive = false;
                foreach (var equality in current.bindingInfo.equalities)
                {
                    var effectiveTerm = current.bindingInfo.bindings[equality.Key].Item2;
                    foreach (var term in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                    {
                        var explanation = current.bindingInfo.EqualityExplanations.First(ee => ee.source.id == term.id && ee.target.id == effectiveTerm.id);
                        if (!format.equalityNumbers.TryGetValue(explanation, out var eeNumber))
                        {
                            eeNumber = termNumbering;
                            ++termNumbering;
                            format.equalityNumbers[explanation] = eeNumber;
                        }
                        if (format.ShowEqualityExplanations)
                        {
                            explanation.PrettyPrint(content, format, eeNumber);
                        }
                        else
                        {
                            var numberingString = $"({eeNumber}) ";
                            content.switchToDefaultFormat();
                            content.Append(numberingString);
                            var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                            term.PrettyPrint(content, format, numberingString.Length);
                            content.switchToDefaultFormat();
                            content.Append($"\n{indentString}= (explanation omitted)\n{indentString}");
                            effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                        }
                        content.Append("\n\n");
                    }
                }
                format.printContextSensitive = true;

                current.bindingInfo.PrintEqualitySubstitution(content, format);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.switchToDefaultFormat();
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            content.switchToDefaultFormat();
            format.restoreAllOriginalRules();
            return termNumbering;
        }

        /// <summary>
        /// Returns the iteration offset used by an equality explanation.
        /// </summary>
        private class EqualityExplanationShiftCollector: EqualityExplanationVisitor<int, object>
        {
            public static readonly EqualityExplanationShiftCollector singleton = new EqualityExplanationShiftCollector();

            private static int CombineShifts(int s1, int s2)
            {
                if (s1 == 0) return s2;
                if (s2 == 0) return s1;
                if (s1 == s2) return s1;
                return -1;
            }

            public override int Direct(DirectEqualityExplanation target, object arg)
            {
                var shift = target.source.iterationOffset;
                shift = CombineShifts(shift, target.target.iterationOffset);
                shift = CombineShifts(shift, target.equality.iterationOffset);

                return shift;
            }

            public override int Transitive(TransitiveEqualityExplanation target, object arg)
            {
                var shift = target.source.iterationOffset;
                shift = CombineShifts(shift, target.target.iterationOffset);
                foreach (var equalityExplanation in target.equalities)
                {
                    shift = CombineShifts(shift, visit(equalityExplanation, arg));
                    if (shift == -1) break;
                }

                return shift;
            }

            public override int Congruence(CongruenceExplanation target, object arg)
            {
                var shift = target.source.iterationOffset;
                shift = CombineShifts(shift, target.target.iterationOffset);
                foreach (var equalityExplanation in target.sourceArgumentEqualities)
                {
                    shift = CombineShifts(shift, visit(equalityExplanation, arg));
                    if (shift == -1) break;
                }

                return shift;
            }

            public override int Theory(TheoryEqualityExplanation target, object arg)
            {
                return CombineShifts(target.source.iterationOffset, target.target.iterationOffset);
            }

            public override int RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                var shift = target.source.iterationOffset;
                shift = CombineShifts(shift, target.target.iterationOffset);
                shift = CombineShifts(shift, target.GenerationOffset);

                return shift;
            }
        }

        /// <summary>
        /// The explanations produced during generalization reference equalities backwards. We want to display them
        /// such that we show how we construct the next equality from the current one. We, therefore, need to shift
        /// the iteration offsets accordingly. Additionally we introduce primes instead of the iteration offset for the
        /// next equality.
        /// </summary>
        private class EqualityExplanationShifter: EqualityExplanationVisitor<EqualityExplanation, int>
        {
            public static readonly EqualityExplanationShifter singleton = new EqualityExplanationShifter();

            private static Term MakePrime(Term t)
            {
                if (t.id >= 0) return t;
                var newArgs = t.Args.Select(a => a.iterationOffset == 0 ? MakePrime(a) : RemoveIterationOffset(a)).ToArray();
                return new Term(t, newArgs) { isPrime = true };
            }

            private static Term RemoveIterationOffset(Term t)
            {
                if (t.iterationOffset == 0) return t;
                var newArgs = t.Args.Select(a => a.iterationOffset == 0 ? MakePrime(a) : RemoveIterationOffset(a)).ToArray();
                return new Term(t, newArgs) { iterationOffset = 0 };
            }

            public override EqualityExplanation Direct(DirectEqualityExplanation target, int arg)
            {
                var newSource = target.source.iterationOffset == 0 ? MakePrime(target.source) : RemoveIterationOffset(target.source);
                var newTarget = target.target.iterationOffset == 0 ? MakePrime(target.target) : RemoveIterationOffset(target.target);
                var newEquality = target.equality.iterationOffset == 0 ? MakePrime(target.equality) : RemoveIterationOffset(target.equality);
                return new DirectEqualityExplanation(newSource, newTarget, newEquality);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, int arg)
            {
                var newSource = target.source.iterationOffset == 0 ? MakePrime(target.source) : RemoveIterationOffset(target.source);
                var newTarget = target.target.iterationOffset == 0 ? MakePrime(target.target) : RemoveIterationOffset(target.target);
                var newEqualities = target.equalities.Select(ee => visit(ee, arg)).ToArray();
                return new TransitiveEqualityExplanation(newSource, newTarget, newEqualities);
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, int arg)
            {
                var newSource = target.source.iterationOffset == 0 ? MakePrime(target.source) : RemoveIterationOffset(target.source);
                var newTarget = target.target.iterationOffset == 0 ? MakePrime(target.target) : RemoveIterationOffset(target.target);
                var newEqualities = target.sourceArgumentEqualities.Select(ee => visit(ee, arg)).ToArray();
                return new CongruenceExplanation(newSource, newTarget, newEqualities);
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, int arg)
            {
                var newSource = target.source.iterationOffset == 0 ? MakePrime(target.source) : RemoveIterationOffset(target.source);
                var newTarget = target.target.iterationOffset == 0 ? MakePrime(target.target) : RemoveIterationOffset(target.target);
                return new TheoryEqualityExplanation(newSource, newTarget, target.TheoryName);
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, int arg)
            {
                var newSource = target.source.iterationOffset == 0 ? MakePrime(target.source) : RemoveIterationOffset(target.source);
                var newTarget = target.target.iterationOffset == 0 ? MakePrime(target.target) : RemoveIterationOffset(target.target);
                return new RecursiveReferenceEqualityExplanation(newSource, newTarget, target.ReferencedExplanation, 0, target.GenerationOffset == 0);
            }
        }

        private void printCycleInfo(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (!hasCycle()) return;
            var cycle = cycleDetector.getCycleQuantifiers();
            var generalizationState = cycleDetector.getGeneralization();

            if (generalizationState.TrueLoop)
            {
                content.switchFormat(PrintConstants.TitleFont, PrintConstants.warningTextColor);
                content.Append("Possible matching loop found!\n");
            }
            else
            {
                content.switchFormat(PrintConstants.BoldFont, PrintConstants.defaultTextColor);
                content.Append("Repating pattern but ");
                content.switchFormat(PrintConstants.TitleFont, PrintConstants.defaultTextColor);
                content.Append("no");
                content.switchFormat(PrintConstants.BoldFont, PrintConstants.defaultTextColor);
                content.Append(" matching loop found!\n");
            }
            content.switchToDefaultFormat();
            content.Append($"Number of{(generalizationState.TrueLoop ? " loop" : "")} iterations: ").Append(cycleDetector.GetNumRepetitions() + "\n");
            content.Append($"Number of quantifiers in {(generalizationState.TrueLoop ? "loop" : "pattern")}: ").Append(cycle.Count + "\n");
            if (generalizationState.TrueLoop)
            {
                content.Append("Loop: ");
            }
            else
            {
                content.Append("Pattern: ");
            }
            content.Append(string.Join(" -> ", cycle.Select(quant => quant.PrintName)));
            content.Append("\n");

            printPreamble(content, true);

            content.switchFormat(PrintConstants.TitleFont, PrintConstants.defaultTextColor);
            content.Append($"\n\nGeneralized{(generalizationState.TrueLoop ? "Loop" : "")} Iteration:\n\n");
            
            var generalizedTerms = generalizationState.generalizedTerms;

            var additionalTerms = generalizedTerms.Take(generalizedTerms.Count - 1).SelectMany(t => t.Item3
                .Where(req => !t.Item2.equalities.SelectMany(eq => eq.Value).Any(eq => Term.semanticTermComparer.Equals(eq.Item2, req)))
                .Where(req => t.Item2.equalities.Keys.All(k => t.Item2.bindings[k].Item2 != req))
                .Select(term => Tuple.Create(t.Item2, term))).ToLookup(t => t.Item2.ContainsGeneralization());
            var additionalSetTerms = additionalTerms[true].ToList();
            var additionalSingleTerms = additionalTerms[false].ToList();

            var numbering = 1;
            foreach (var term in additionalSetTerms.Concat(additionalSingleTerms))
            {
                format.termNumbers[term.Item2] = numbering;
                ++numbering;
            }

            foreach (var step in generalizedTerms)
            {
                format.termNumbers[step.Item1] = numbering;
                ++numbering;

                foreach (var kv in step.Item4)
                {
                    foreach (var tc in kv.Value)
                    {
                        var constraintExplanation = step.Item5.FirstOrDefault(ee => Term.semanticTermComparer.Equals(ee.target, tc));
                        if (constraintExplanation != null)
                        {
                            format.equalityNumbers[constraintExplanation] = numbering;
                        }
                        format.termNumbers[tc] = numbering;
                        ++numbering;
                    }
                }

                foreach (var ee in step.Item2.EqualityExplanations)
                {
                    format.equalityNumbers[ee] = numbering;
                    ++numbering;
                }
            }
            
            var alreadyIntroducedGeneralizations = new HashSet<int>();
            if (additionalSetTerms.Any())
            {

                // print last yield term before printing the complete loop
                // to give the user a term to match the highlighted pattern to
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nStarting anywhere with the following set(s) of term(s):\n\n");
                content.switchToDefaultFormat();

                var setGroups = additionalSetTerms.GroupBy(t => Tuple.Create(t.Item2.Responsible, generalizationState.IsProducedByLoop(t.Item2)));
                foreach (var group in setGroups)
                {
                    var responsible = group.Key.Item1;
                    var loopProduced = group.Key.Item2;
                    if (responsible != null)
                    {
                        content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                        content.Append($"Generated by {responsible.Quant.PrintName} ({(loopProduced ? "" : "not ")}part of the current loop):\n");
                        content.switchToDefaultFormat();
                    }
                    else
                    {
                        content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                        content.Append($"Asserted:\n");
                        content.switchToDefaultFormat();
                    }

                    foreach (var bindingInfoAndTerm in group)
                    {
                        generalizationState.tmpHighlightGeneralizedTerm(format, bindingInfoAndTerm.Item2, bindingInfoAndTerm.Item1, false);

                        var newlyIntroducedGeneralizations = bindingInfoAndTerm.Item2.GetAllGeneralizationSubtermsAndDependencies()
                        .GroupBy(gen => gen.generalizationCounter).Select(g => g.First())
                        .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                        PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                        alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                        var termNumber = format.termNumbers[bindingInfoAndTerm.Item2];
                        var numberingString = $"({termNumber}) ";
                        content.Append($"{numberingString}");

                        bindingInfoAndTerm.Item2.PrettyPrint(content, format, numberingString.Length);
                        content.switchToDefaultFormat();
                        content.Append("\n\n");
                    }
                }

                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nand the following term(s):\n\n");
                content.switchToDefaultFormat();
            }
            else
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nStarting anywhere with the following term(s):\n\n");
                content.switchToDefaultFormat();
            }

            foreach (var bindingInfoAndTerm in additionalSingleTerms)
            {
                generalizationState.tmpHighlightGeneralizedTerm(format, bindingInfoAndTerm.Item2, bindingInfoAndTerm.Item1, false);

                var termNumber = format.termNumbers[bindingInfoAndTerm.Item2];
                var numberingString = $"({termNumber}) ";
                content.Append($"{numberingString}");

                bindingInfoAndTerm.Item2.PrettyPrint(content, format, numberingString.Length);
                content.switchToDefaultFormat();
                content.Append("\n\n");
            }

            var insts = cycleDetector.getCycleInstantiations().GetEnumerator();
            insts.MoveNext();

            var recursiveEqualityExplanations = new List<Tuple<int, EqualityExplanation>>();
            var firstStep = generalizationState.generalizedTerms.First();
            printGeneralizedTermWithPrerequisites(content, format, generalizationState, alreadyIntroducedGeneralizations, firstStep.Item1, firstStep.Item2, firstStep.Item3, firstStep.Item4, firstStep.Item5, false, recursiveEqualityExplanations);

            var count = 1;
            var loopSteps = generalizedTerms.Skip(1);
            var numberOfSteps = loopSteps.Count();
            foreach (var step in loopSteps)
            {
                format.restoreAllOriginalRules();
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nApplication of ");
                content.Append(insts.Current?.Quant.PrintName);
                content.switchToDefaultFormat();
                content.Append("\n\n");

                // print quantifier body with pattern
                insts.Current?.tempHighlightBlameBindTerms(format);
                insts.Current?.Quant.BodyTerm.PrettyPrint(content, format);
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nThis yields:\n\n");
                content.switchToDefaultFormat();

                insts.MoveNext();
                format.restoreAllOriginalRules();
                printGeneralizedTermWithPrerequisites(content, format, generalizationState, alreadyIntroducedGeneralizations, step.Item1, step.Item2, step.Item3, step.Item4, step.Item5, count == numberOfSteps, recursiveEqualityExplanations);
                count++;
            }

            if (recursiveEqualityExplanations.Any())
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nEqualities for Next Iteration(s):\n\n");

                var offsetGroups = recursiveEqualityExplanations.GroupBy(ee => EqualityExplanationShiftCollector.singleton.visit(ee.Item2, null)).OrderBy(group => group.Key).AsEnumerable();
                var nonGeneralizedEqualities = Enumerable.Empty<int>();
                if (offsetGroups.First().Key == -1)
                {
                    nonGeneralizedEqualities = offsetGroups.First().Select(tuple => tuple.Item1);
                    offsetGroups = offsetGroups.Skip(1);
                }

                foreach (var group in offsetGroups)
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append($"In {group.Key} Iteration(s) the Following Equalities Will Be Used:\n\n");

                    foreach (var equalityExplanation in group)
                    {
                        var equalityNumber = equalityExplanation.Item1;
                        var shiftedEqualityExplanation = EqualityExplanationShifter.singleton.visit(equalityExplanation.Item2, group.Key);

                        var newlyIntroducedGeneralizationTerms = new List<Term>();
                        EqualityExplanationTermVisitor.singleton.visit(shiftedEqualityExplanation, t => newlyIntroducedGeneralizationTerms.AddRange(t.GetAllGeneralizationSubterms()));

                        var newlyIntroducedGeneralizations = newlyIntroducedGeneralizationTerms
                                   .GroupBy(gen => gen.generalizationCounter).Select(g => g.First())
                                   .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                        PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                        alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                        content.switchToDefaultFormat();
                        var numberingString = $"({equalityNumber}') ";
                        content.Append(numberingString);
                        shiftedEqualityExplanation.source.PrettyPrint(content, format, numberingString.Length);
                        EqualityExplanationPrinter.singleton.visit(shiftedEqualityExplanation, Tuple.Create(content, format, false, numberingString.Length));
                        content.switchToDefaultFormat();
                        content.Append("\n\n");
                    }
                }

                if (nonGeneralizedEqualities.Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("The Following Equalities Reference Several Iterations and Could Not Be Generalized:\n\n");
                    content.switchToDefaultFormat();
                    content.Append(String.Join(", ", nonGeneralizedEqualities.Select(e => $"({e})")));
                }
            }

            format.restoreAllOriginalRules();
            content.Append("\n\n");
        }

        /// <summary>
        /// Prints a description for the T terms used in a term.
        /// </summary>
        private static void PrintNewlyIntroducedGeneralizations(InfoPanelContent content, PrettyPrintFormat format, IEnumerable<Term> newlyIntroducedGeneralizations)
        {
            if (newlyIntroducedGeneralizations.Any())
            {
                var dependentGeneralizationLookup = newlyIntroducedGeneralizations.ToLookup(gen => gen.Args.Count() > 0);
                var hasIndependent = dependentGeneralizationLookup[false].Any();
                if (hasIndependent)
                {
                    content.Append("For any term(s) ");

                    var ordered = dependentGeneralizationLookup[false].OrderBy(gen => -gen.id);
                    ordered.First().PrettyPrint(content, format);
                    content.switchToDefaultFormat();
                    foreach (var gen in ordered.Skip(1))
                    {
                        content.Append(", ");
                        gen.PrettyPrint(content, format);
                        content.switchToDefaultFormat();
                    }
                }
                if (dependentGeneralizationLookup[true].Any())
                {
                    if (hasIndependent)
                    {
                        content.Append(" and corresponding term(s) ");
                    }
                    else
                    {
                        content.Append("For corresponding term(s) ");
                    }

                    var ordered = dependentGeneralizationLookup[true].OrderBy(gen => -gen.id);
                    ordered.First().PrettyPrint(content, format);
                    content.switchToDefaultFormat();
                    foreach (var gen in ordered.Skip(1))
                    {
                        content.Append(", ");
                        gen.PrettyPrint(content, format);
                        content.switchToDefaultFormat();
                    }
                }
                content.Append(":\n");
            }
        }

        /// <summary>
        /// Checks wheter an equality explanation references another iteration and should, therefore, be explained after
        /// the generalized iteration.
        /// </summary>
        private class EqualityExplanationIsRecursiveVisitor: EqualityExplanationVisitor<bool, object>
        {
            public static readonly EqualityExplanationIsRecursiveVisitor singleton = new EqualityExplanationIsRecursiveVisitor();

            private static bool HasRecursiveTerm(EqualityExplanation target)
            {
                return target.source.ReferencesOtherIteration() || target.target.ReferencesOtherIteration();
            }

            public override bool Direct(DirectEqualityExplanation target, object arg)
            {
                return HasRecursiveTerm(target);
            }

            public override bool Transitive(TransitiveEqualityExplanation target, object arg)
            {
                return HasRecursiveTerm(target) || target.equalities.Any(e => visit(e, arg));
            }

            public override bool Congruence(CongruenceExplanation target, object arg)
            {
                return HasRecursiveTerm(target) || target.sourceArgumentEqualities.Any(e => visit(e, arg));
            }

            public override bool Theory(TheoryEqualityExplanation target, object arg)
            {
                return HasRecursiveTerm(target);
            }

            public override bool RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                return true;
            }
        }

        private static void printGeneralizedTermWithPrerequisites(InfoPanelContent content, PrettyPrintFormat format, GeneralizationState generalizationState, ISet<int> alreadyIntroducedGeneralizations,
            Term term, BindingInfo bindingInfo, List<Term> assocTerms, Dictionary<string, List<Term>> theoryConstraintTerms, List<EqualityExplanation> theoryConstraintExplanations,
            bool last, List<Tuple<int, EqualityExplanation>> recursiveEqualityExplanations)
        {
            generalizationState.tmpHighlightGeneralizedTerm(format, term, bindingInfo, last);
            foreach (var ee in theoryConstraintExplanations)
            {
                ee.source.highlightTemporarily(format, PrintConstants.equalityColor);
            }

            var newlyIntroducedGeneralizations = term.GetAllGeneralizationSubtermsAndDependencies()
                    .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                    .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
            PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
            alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

            var distinctBlameTerms = bindingInfo.getDistinctBlameTerms();
            var termNumber = format.termNumbers[term];
            var numberingString = $"({termNumber}) ";
            content.switchToDefaultFormat();
            content.Append(numberingString);
            term.PrettyPrint(content, format, numberingString.Length);
            content.Append('\n');

            if (theoryConstraintTerms.Any())
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nAdded Theory Constraints:");
                content.switchToDefaultFormat();
                foreach (var kv in theoryConstraintTerms)
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append($"\n\nConstraints Added by the {kv.Key} theory:\n\n");
                    content.switchToDefaultFormat();

                    foreach (var tc in kv.Value)
                    {
                        var constraintExplanation = theoryConstraintExplanations.FirstOrDefault(ee => ee.target == tc);
                        if (constraintExplanation == null)
                        {
                            newlyIntroducedGeneralizations = tc.GetAllGeneralizationSubtermsAndDependencies()
                                .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                                .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                            PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                            alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                            termNumber = format.termNumbers[tc];
                            numberingString = $"({termNumber}) ";
                            content.Append(numberingString);
                            tc.PrettyPrint(content, format, numberingString.Length);
                        }
                        else
                        {
                            var newlyIntroducedGeneralizationTerms = new List<Term>();
                            EqualityExplanationTermVisitor.singleton.visit(constraintExplanation, u => newlyIntroducedGeneralizationTerms.AddRange(u.GetAllGeneralizationSubterms()));

                            newlyIntroducedGeneralizations = newlyIntroducedGeneralizationTerms
                                .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                                .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                            PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                            alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                            constraintExplanation.PrettyPrint(content, format, format.equalityNumbers[constraintExplanation]);
                        }
                        content.switchToDefaultFormat();
                        content.Append("\n\n");
                    }
                }
            }

            if (last)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nTerms for the Next Iteration:\n\n");
                content.switchToDefaultFormat();
                generalizationState.PrintGeneralizationsForNextIteration(content, format);

                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nBindings to Start Next Iteration:\n\n");
                content.switchToDefaultFormat();

                foreach (var binding in bindingInfo.getBindingsToFreeVars())
                {
                    if (binding.Value == null)
                    {
                        content.Append($"A generalized binding for {binding.Key.Name} in the next iteration\ncould not be generated (This pattern cannot repeat indefinetly).");
                    }
                    else
                    {
                        content.Append(binding.Key.Name).Append(" will be bound to:\n");
                        binding.Value.PrettyPrint(content, format);
                        content.switchToDefaultFormat();
                        content.Append("\n\n");
                    }
                }

                return;
            }

            if (bindingInfo.equalities.Count > 0)
            {
                var equalitySetLookup = bindingInfo.equalities.ToLookup(eq => eq.Key.ContainsGeneralization() || eq.Value.Any(t => t.Item2.ContainsGeneralization()));

                format.printContextSensitive = false;
                if (equalitySetLookup[true].Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nWith a Set of equalities:\n");
                    content.switchToDefaultFormat();

                    foreach (var equality in equalitySetLookup[true])
                    {
                        var effectiveTerm = bindingInfo.bindings[equality.Key].Item2;
                        foreach (var t in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                        {
                            content.Append('\n');
                            termNumber = format.GetEqualityNumber(t, effectiveTerm);

                            var explanation = bindingInfo.EqualityExplanations.Single(ee => ee.source.id == t.id && ee.target.id == effectiveTerm.id);
                            var isRecursive = EqualityExplanationIsRecursiveVisitor.singleton.visit(explanation, null);
                            if (isRecursive)
                            {
                                recursiveEqualityExplanations.Add(Tuple.Create(termNumber, explanation));
                            }

                            if (format.ShowEqualityExplanations && !isRecursive)
                            {
                                var newlyIntroducedGeneralizationTerms = new List<Term>();
                                EqualityExplanationTermVisitor.singleton.visit(explanation, u => newlyIntroducedGeneralizationTerms.AddRange(u.GetAllGeneralizationSubterms()));

                                newlyIntroducedGeneralizations = newlyIntroducedGeneralizationTerms
                                    .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                                    .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                                PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                                alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                                explanation.PrettyPrint(content, format, termNumber);
                            }
                            else
                            {
                                newlyIntroducedGeneralizations = t.GetAllGeneralizationSubtermsAndDependencies()
                                    .Concat(effectiveTerm.GetAllGeneralizationSubtermsAndDependencies())
                                    .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                                    .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                                PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                                alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                                numberingString = $"({termNumber}) ";
                                content.switchToDefaultFormat();
                                content.Append(numberingString);
                                var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                                t.PrettyPrint(content, format, numberingString.Length);
                                content.switchToDefaultFormat();
                                content.Append($"\n{indentString}= (explanation omitted)\n{indentString}");
                                effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                            }
                            content.switchToDefaultFormat();
                            content.Append('\n');
                        }
                    }
                }

                if (equalitySetLookup[false].Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nRelevant equalities:\n");
                    content.switchToDefaultFormat();

                    foreach (var equality in equalitySetLookup[false])
                    {
                        var effectiveTerm = bindingInfo.bindings[equality.Key].Item2;
                        foreach (var t in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                        {
                            content.Append('\n');
                            termNumber = format.GetEqualityNumber(t, effectiveTerm);

                            var explanation = bindingInfo.EqualityExplanations.Single(ee => ee.source.id == t.id && ee.target.id == effectiveTerm.id);
                            var isRecursive = EqualityExplanationIsRecursiveVisitor.singleton.visit(explanation, null);
                            if (isRecursive)
                            {
                                recursiveEqualityExplanations.Add(Tuple.Create(termNumber, explanation));
                            }


                            if (format.ShowEqualityExplanations && !isRecursive)
                            {
                                var newlyIntroducedGeneralizationTerms = new List<Term>();
                                EqualityExplanationTermVisitor.singleton.visit(explanation, u => newlyIntroducedGeneralizationTerms.AddRange(u.GetAllGeneralizationSubterms()));

                                newlyIntroducedGeneralizations = newlyIntroducedGeneralizationTerms
                                    .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                                    .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                                PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                                alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                                explanation.PrettyPrint(content, format, termNumber);
                            }
                            else
                            {
                                newlyIntroducedGeneralizations = t.GetAllGeneralizationSubtermsAndDependencies()
                                    .Concat(effectiveTerm.GetAllGeneralizationSubtermsAndDependencies())
                                    .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                                    .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                                PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                                alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                                numberingString = $"({termNumber}) ";
                                content.switchToDefaultFormat();
                                content.Append(numberingString);
                                var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                                t.PrettyPrint(content, format, numberingString.Length);
                                content.switchToDefaultFormat();
                                content.Append($"\n{indentString}= (explanation omitted)\n{indentString}");
                                effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                            }
                            content.switchToDefaultFormat();
                            content.Append('\n');
                        }
                    }
                }
                format.printContextSensitive = true;

                bindingInfo.PrintEqualitySubstitution(content, format);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nBinding information:");
            content.switchToDefaultFormat();

            foreach (var bindings in bindingInfo.getBindingsToFreeVars())
            {
                content.Append("\n\n");
                content.Append(bindings.Key.Name).Append(" was bound to:\n");
                bindings.Value.PrettyPrint(content, format);
                content.switchToDefaultFormat();
            }

            content.Append("\n");
        }

        private void printPreamble(InfoPanelContent content, bool withGen)
        {
            content.Append("\nHighlighted terms are ");
            content.switchFormat(PrintConstants.DefaultFont, PrintConstants.patternMatchColor);
            content.Append("matched");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(PrintConstants.DefaultFont, PrintConstants.equalityColor);
            content.Append("matched using equality");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(PrintConstants.DefaultFont, PrintConstants.blameColor);
            content.Append("blamed");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(PrintConstants.DefaultFont, PrintConstants.bindColor);
            content.Append("bound");
            content.switchToDefaultFormat();
            if (withGen)
            {
                content.Append(" or ");
                content.switchFormat(PrintConstants.DefaultFont, PrintConstants.generalizationColor);
                content.Append("generalized");
                content.switchToDefaultFormat();
                content.Append(".\n\"");
                content.switchFormat(PrintConstants.DefaultFont, PrintConstants.generalizationColor);
                content.Append(">...<");
                content.switchToDefaultFormat();
                content.Append("\" indicates that a generalization is hidden below the max term depth");
            }
            content.Append(".\n");
        }

        private static void legacyInstantiationInfo(InfoPanelContent content, PrettyPrintFormat format, Instantiation instantiation)
        {
            instantiation.printNoMatchdisclaimer(content);
            content.switchFormat(PrintConstants.SubtitleFont, Color.DarkMagenta);
            content.Append("\n\nBlamed terms:\n\n");
            content.switchToDefaultFormat();

            foreach (var t in instantiation.Responsible)
            {
                content.Append("\n");
                t.PrettyPrint(content, format);
                content.Append("\n\n");
            }

            content.Append('\n');
            content.switchToDefaultFormat();
            content.switchFormat(PrintConstants.SubtitleFont, Color.DarkMagenta);
            content.Append("Bound terms:\n\n");
            content.switchToDefaultFormat();
            foreach (var t in instantiation.Bindings)
            {
                content.Append("\n");
                t.PrettyPrint(content, format);
                content.Append("\n\n");
            }

            content.switchFormat(PrintConstants.SubtitleFont, Color.DarkMagenta);
            content.Append("Quantifier Body:\n\n");

            instantiation.concreteBody?.PrettyPrint(content, format);
            content.switchToDefaultFormat();
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }

        public bool TryGetLoop(out IEnumerable<System.Tuple<Quantifier, Term>> loop)
        {
            loop = null;
            if (!hasCycle()) return false;
            loop = cycleDetector.getCycleInstantiations().Take(cycleDetector.getCycleQuantifiers().Count)
                .Select(inst => System.Tuple.Create(inst.Quant, inst.bindingInfo.fullPattern));
            return true;
        }

        public bool TryGetCyclePath(out InstantiationPath cyclePath)
        {
            cyclePath = null;
            if (!hasCycle()) return false;
            var cycleInstantiations = cycleDetector.getCycleInstantiations();
            cyclePath = new InstantiationPath();
            foreach (var inst in cycleInstantiations)
            {
                cyclePath.append(inst);
            }
            return true;
        }

        public int GetNumRepetitions()
        {
            return cycleDetector.GetNumRepetitions();
        }
    }
}