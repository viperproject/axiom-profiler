using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.CycleDetection;
using AxiomProfiler.PrettyPrinting;
using System;

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

        public IEnumerable<Tuple<Tuple<Quantifier, Term>, int>> Statistics()
        {
            return pathInstantiations.Skip(pathInstantiations.First().bindingInfo == null ? 1 : 0) //it may, in some cases, be impossible to calculate the binding info for the first instatntiation in a path
                .GroupBy(i => Tuple.Create(i.Quant, i.bindingInfo.fullPattern)).Select(group => Tuple.Create(group.Key, group.Count()));
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

            if (current.bindingInfo == null)
            {
                legacyInstantiationInfo(content, format, current);
            }
            else
            {
                printPathHead(content, format, current);
            }

            var termNumbering = 1;
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

        private static void highlightGens(Term[] potGens, PrettyPrintFormat format, GeneralizationState generalization) 
        {
            foreach (var term in potGens)
            {
                if (term is Term && generalization.IsReplaced(term.id))
                {
                    term.highlightTemporarily(format, PrintConstants.generalizationColor);
                }
                else
                {
                    highlightGens(term.Args, format, generalization);
                }
            }
        }

        private static int printInstantiationWithPredecessor(InfoPanelContent content, PrettyPrintFormat format,
            Instantiation current, Instantiation previous, CycleDetection.CycleDetection cycDetect, int termNumbering)
        {
            current.tempHighlightBlameBindTerms(format);
            var potGens = previous.concreteBody.Args;
            var generalization = cycDetect.getGeneralization();
            highlightGens(potGens, format, generalization);
            var termNumberings = new List<Tuple<Term, int>>();

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\n\nThis instantiation yields:\n\n");
            content.switchToDefaultFormat();
            var numberingString = $"({termNumbering}) ";
            content.Append($"{numberingString}");
            termNumberings.Add(Tuple.Create(previous.concreteBody, termNumbering));
            ++termNumbering;
            previous.concreteBody.PrettyPrint(content, format, numberingString.Length);

            // Other prerequisites:
            var otherRequiredTerms = current.bindingInfo.getDistinctBlameTerms()
                .FindAll(term => current.bindingInfo.equalities.Any(eq => current.bindingInfo.bindings[eq.Key] == term) ||
                        !previous.concreteBody.isSubterm(term)).ToList();
            if (otherRequiredTerms.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nTogether with the following term(s):");
                content.switchToDefaultFormat();
                foreach (var distinctBlameTerm in otherRequiredTerms)
                {
                    numberingString = $"({termNumbering}) ";
                    content.Append($"\n\n{numberingString}");
                    termNumberings.Add(Tuple.Create(distinctBlameTerm, termNumbering));
                    ++termNumbering;
                    distinctBlameTerm.PrettyPrint(content, format, numberingString.Length);
                    content.switchToDefaultFormat();
                }
            }

            if (current.bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                var equalityNumberings = new List<Tuple<IEnumerable<Term>, int>>();
                foreach (var equality in current.bindingInfo.equalities)
                {
                    var effectiveTerm = current.bindingInfo.bindings[equality.Key];
                    equalityNumberings.Add(new Tuple<IEnumerable<Term>, int>(equality.Value.Concat(Enumerable.Repeat(effectiveTerm, 1)), termNumbering));
                    numberingString = $"({termNumbering}) ";
                    ++termNumbering;
                    content.Append(numberingString);
                    effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                    var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                    foreach (var term in equality.Value)
                    {
                        content.switchToDefaultFormat();
                        content.Append($"\n{indentString}=\n{indentString}");
                        term.PrettyPrint(content, format, numberingString.Length);
                    }
                    content.Append("\n\n");
                }

                current.bindingInfo.PrintEqualitySubstitution(content, format, termNumberings, equalityNumberings);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\n\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.switchToDefaultFormat();
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            format.restoreAllOriginalRules();
            return termNumbering;
        }

        private static int printPathHead(InfoPanelContent content, PrettyPrintFormat format, Instantiation current)
        {
            var termNumbering = 1;
            var termNumberings = new List<Tuple<Term, int>>();
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nStarting from the following term(s):\n\n");
            content.switchToDefaultFormat();
            current.tempHighlightBlameBindTerms(format);
            var blameTerms = current.bindingInfo.getDistinctBlameTerms();
            var distinctBlameTerms = blameTerms.Where(bt => blameTerms.All(super => bt == super || !super.isSubterm(bt)))
                .Where(req => !current.bindingInfo.equalities.SelectMany(eq => eq.Value).Contains(req))
                .Where(req => current.bindingInfo.equalities.Keys.All(k => current.bindingInfo.bindings[k] != req));
            foreach (var distinctBlameTerm in distinctBlameTerms)
            {
                var numberingString = $"({termNumbering}) ";
                content.Append(numberingString);
                termNumberings.Add(Tuple.Create(distinctBlameTerm, termNumbering));
                ++termNumbering;
                distinctBlameTerm.PrettyPrint(content, format, numberingString.Length);
                content.Append("\n\n");
                content.switchToDefaultFormat();
            }

            if (current.bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                var equalityNumberings = new List<Tuple<IEnumerable<Term>, int>>();
                format.printContextSensitive = false;
                foreach (var equality in current.bindingInfo.equalities)
                {
                    var effectiveTerm = current.bindingInfo.bindings[equality.Key];
                    equalityNumberings.Add(new Tuple<IEnumerable<Term>, int>(equality.Value.Concat(Enumerable.Repeat(effectiveTerm, 1)), termNumbering));
                    var numberingString = $"({termNumbering}) ";
                    ++termNumbering;
                    content.Append(numberingString);
                    effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                    var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                    foreach (var term in equality.Value)
                    {
                        content.switchToDefaultFormat();
                        content.Append($"\n{indentString}=\n{indentString}");
                        term.PrettyPrint(content, format, numberingString.Length);
                    }
                    content.Append("\n\n");
                }
                format.printContextSensitive = true;

                current.bindingInfo.PrintEqualitySubstitution(content, format, termNumberings, equalityNumberings);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.switchToDefaultFormat();
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            format.restoreAllOriginalRules();
            return termNumbering;
        }

        private void printCycleInfo(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (!hasCycle()) return;
            var cycle = cycleDetector.getCycleQuantifiers();
            content.switchFormat(PrintConstants.TitleFont, PrintConstants.warningTextColor);
            content.Append("Possible matching loop found!\n");
            content.switchToDefaultFormat();
            content.Append("Number of loop iterations: ").Append(cycleDetector.GetNumRepetitions() + "\n");
            content.Append("Number of quantifiers in loop: ").Append(cycle.Count + "\n");
            content.Append("Loop: ");
            content.Append(string.Join(" -> ", cycle.Select(quant => quant.PrintName)));
            content.Append("\n");

            printPreamble(content, true);

            content.switchFormat(PrintConstants.TitleFont, PrintConstants.defaultTextColor);
            content.Append("\n\nGeneralized Loop Iteration:\n\n");

            var generalizationState = cycleDetector.getGeneralization();
            var generalizedTerms = generalizationState.generalizedTerms;

            // print last yield term before printing the complete loop
            // to give the user a term to match the highlighted pattern to
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nStarting anywhere with the following term(s):\n\n");
            content.switchToDefaultFormat();
            var insts = cycleDetector.getCycleInstantiations().GetEnumerator();
            insts.MoveNext();
            
            var alreadyIntroducedGeneralizations = new HashSet<int>();

            var termNumbering = 1;
            termNumbering = printGeneralizedTermWithPrerequisites(content, format, generalizationState, alreadyIntroducedGeneralizations, generalizedTerms.First(), insts.Current, true, false, termNumbering);

            var count = 1;
            var loopYields = generalizedTerms.GetRange(1, generalizedTerms.Count - 1);
            foreach (var term in loopYields)
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
                termNumbering = printGeneralizedTermWithPrerequisites(content, format, generalizationState, alreadyIntroducedGeneralizations, term, insts.Current, false, count == loopYields.Count, termNumbering);
                count++;
            }
            format.restoreAllOriginalRules();
            content.Append("\n\n");
        }

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

        private static int printGeneralizedTermWithPrerequisites(InfoPanelContent content, PrettyPrintFormat format,
            GeneralizationState generalizationState, ISet<int> alreadyIntroducedGeneralizations, Term term, Instantiation instantiation, bool first, bool last, int termNumbering)
        {
            var termNumberings = new List<Tuple<Term, int>>();
            generalizationState.tmpHighlightGeneralizedTerm(format, term, last);

            var newlyIntroducedGeneralizations = term.GetAllGeneralizationSubterms()
                    .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                    .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
            PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
            alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

            var numberingString = $"({termNumbering}) ";
            content.Append(numberingString);
            term.PrettyPrint(content, format, numberingString.Length);
            content.Append("\n");

            termNumberings.Add(Tuple.Create(term, termNumbering));
            ++termNumbering;

            var bindingInfo = last ? generalizationState.GetWrapAroundBindingInfo() : generalizationState.GetGeneralizedBindingInfo(term.dependentInstantiationsBlame.First());

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
                    content.Append(binding.Key.Name).Append(" will be bound to:\n");
                    binding.Value.PrettyPrint(content, format);
                    content.switchToDefaultFormat();
                    content.Append("\n\n");
                }

                return termNumbering;
            }

            if (generalizationState.assocGenBlameTerm.TryGetValue(term, out var otherRequirements))
            {
                foreach (var req in otherRequirements)
                {
                    generalizationState.tmpHighlightGeneralizedTerm(format, req, last);
                }

                var constantTermsLookup = otherRequirements
                    .Where(req => !bindingInfo.equalities.SelectMany(eq => eq.Value).Contains(req))
                    .Where(req => bindingInfo.equalities.Keys.All(k => bindingInfo.bindings[k] != req))
                    .ToLookup(t => t.ContainsGeneralization());
                var setTems = constantTermsLookup[true];
                var constantTerms = constantTermsLookup[false];

                if (constantTerms.Count() > 0)
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nTogether with the following term(s):");
                    content.switchToDefaultFormat();

                    foreach (var req in constantTerms)
                    {
                        numberingString = $"({termNumbering}) ";
                        content.Append($"\n\n{numberingString}");
                        termNumberings.Add(Tuple.Create(req, termNumbering));
                        ++termNumbering;

                        req.PrettyPrint(content, format, numberingString.Length);
                    }
                    content.Append("\n");
                }

                if (setTems.Count() > 0)
                {
                    foreach (var req in setTems)
                    {
                        content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                        content.Append($"\nTogether with a set of terms generated by {req.Responsible.Quant.PrintName}\n({(generalizationState.IsProducedByLoop(req) ? "" : "not ")}part of the current matching loop) with the shape:\n\n");
                        content.switchToDefaultFormat();

                        newlyIntroducedGeneralizations = req.GetAllGeneralizationSubterms()
                            .GroupBy(gen => gen.generalizationCounter).Select(group => group.First())
                            .Where(gen => !alreadyIntroducedGeneralizations.Contains(gen.generalizationCounter));
                        PrintNewlyIntroducedGeneralizations(content, format, newlyIntroducedGeneralizations);
                        alreadyIntroducedGeneralizations.UnionWith(newlyIntroducedGeneralizations.Select(gen => gen.generalizationCounter));

                        content.switchToDefaultFormat();
                        numberingString = $"({termNumbering}) ";
                        content.Append(numberingString);
                        termNumberings.Add(Tuple.Create(req, termNumbering));
                        ++termNumbering;

                        req.PrettyPrint(content, format, numberingString.Length);
                        content.Append("\n");
                    }
                }
            }

            if (bindingInfo.equalities.Count > 0)
            {
                var equalitySetLookup = bindingInfo.equalities.ToLookup(eq => eq.Key.ContainsGeneralization() || eq.Value.Any(t => t.ContainsGeneralization()));

                format.printContextSensitive = false;
                var equalityNumberings = new List<Tuple<IEnumerable<Term>, int>>();
                if (equalitySetLookup[true].Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nWith a Set of equalities:\n");
                    content.switchToDefaultFormat();

                    foreach (var equality in equalitySetLookup[true])
                    {
                        content.Append("\n");
                        var effectiveTerm = bindingInfo.bindings[equality.Key];
                        equalityNumberings.Add(new Tuple<IEnumerable<Term>, int>(equality.Value.Concat(Enumerable.Repeat(effectiveTerm, 1)), termNumbering));
                        numberingString = $"({termNumbering}) ";
                        ++termNumbering;
                        content.Append(numberingString);
                        effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                        var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                        foreach (var t in equality.Value)
                        {
                            content.switchToDefaultFormat();
                            content.Append($"\n{indentString}=\n{indentString}");
                            t.PrettyPrint(content, format, numberingString.Length);
                        }
                        content.Append("\n");
                    }
                }

                if (equalitySetLookup[false].Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nRelevant equalities:\n");
                    content.switchToDefaultFormat();

                    foreach (var equality in equalitySetLookup[false])
                    {
                        content.Append("\n");
                        var effectiveTerm = bindingInfo.bindings[equality.Key];
                        equalityNumberings.Add(new Tuple<IEnumerable<Term>, int>(equality.Value.Concat(Enumerable.Repeat(effectiveTerm, 1)), termNumbering));
                        numberingString = $"({termNumbering}) ";
                        ++termNumbering;
                        content.Append(numberingString);
                        effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                        var indentString = $"¦{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                        foreach (var t in equality.Value)
                        {
                            content.switchToDefaultFormat();
                            content.Append($"\n{indentString}=\n{indentString}");
                            t.PrettyPrint(content, format, numberingString.Length);
                        }
                        content.Append("\n");
                    }
                }
                format.printContextSensitive = true;

                bindingInfo.PrintEqualitySubstitution(content, format, termNumberings, equalityNumberings);
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

            return termNumbering;
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