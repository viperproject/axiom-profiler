using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.CycleDetection;
using AxiomProfiler.PrettyPrinting;
using System;
using AxiomProfiler.Utilities;
using Microsoft.Msagl.Drawing;
namespace AxiomProfiler.QuantifierModel
{
    class InstantiationSubgraph : IPrintable
    {
        private readonly List<Instantiation> subgraphInstantiations;
        // number of elements in cycle (the repeating strucutre)
        private int cycleSize;

        public InstantiationSubgraph(ref List<List<Node>> subgraph, int size)
        {
            cycleSize = size;
            subgraphInstantiations = new List<Instantiation>();
            foreach (List<Node> cycle in subgraph)
            {
                if (cycle.Count != cycleSize) break;
                List<Instantiation> cycleInstantiations = new List<Instantiation>();
                foreach (Node node in cycle)
                {
                    cycleInstantiations.Add((Instantiation)(node.UserData));
                }
                subgraphInstantiations.AddRange(cycleInstantiations);
            }
        }

        // To generate the info panel content
        public void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            if ((subgraphInstantiations.Count / cycleSize) < 3)
            {
                PrintPathInfo(content, format);;
            } else
            {
                PrintCycleInfo(content, format);
            }
        }


        // When there is no repeating cycle
        // only take the first cycle even when there are two
        // Requires the subgraph to be a path
        public void PrintPathInfo(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.switchFormat(PrintConstants.TitleFont, PrintConstants.defaultTextColor);
            content.Append("Path explanation:");
            content.switchToDefaultFormat();
            content.Append("\n\nLength: " + cycleSize).Append('\n');

            printPreamble(content, false);
            int termNumbering = printPathHead(content, format, subgraphInstantiations[0]);

            for (int i = 1; i < cycleSize; i++)
            {
                termNumbering = printInstantiationWithPredecessor(content, format, 
                    subgraphInstantiations[i], subgraphInstantiations[i-1], termNumbering);
            }

            content.switchToDefaultFormat();
            content.Append("\n\nThis instantiation yields:\n\n");
            subgraphInstantiations[cycleSize-1].concreteBody.PrettyPrint(content, format);
        }


        // Preamble, gives information on how to interpret info panel
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

        private int printPathHead(InfoPanelContent content, PrettyPrintFormat format, Instantiation current)
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
                        EqualityExplanation explanation;
#if !DEBUG
                        try
                        {
#endif
                            explanation = current.bindingInfo.EqualityExplanations.First(ee => ee.source.id == term.id && ee.target.id == effectiveTerm.id);
#if !DEBUG
                        }
                        catch (Exception)
                        {
                            explanation = new TransitiveEqualityExplanation(term, effectiveTerm, new EqualityExplanation[0]);
                        }
#endif
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

            try
            {
                current.Quant.BodyTerm.PrettyPrint(content, format);
            }
            catch (Exception)
            {
                content.Append("Exception was thrown while printing the body of the quantifier\n");

            }
            content.switchToDefaultFormat();
            format.restoreAllOriginalRules();
            return termNumbering;
        }

        private int printInstantiationWithPredecessor(InfoPanelContent content, PrettyPrintFormat format,
    Instantiation current, Instantiation previous, int termNumbering)
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
                        EqualityExplanation explanation;
#if !DEBUG
                        try
                        {
#endif
                            explanation = current.bindingInfo.EqualityExplanations.First(ee => ee.source.id == term.id && ee.target.id == effectiveTerm.id);
#if !DEBUG
                        }
                        catch (Exception)
                        {
                            explanation = new TransitiveEqualityExplanation(term, effectiveTerm, new EqualityExplanation[0]);
                        }
#endif
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
            try
            {
                current.Quant.BodyTerm.PrettyPrint(content, format);
            }
            catch (Exception)
            {
                content.Append("Exception was thrown while printing the body of the quantifier\n");
            }
            content.switchToDefaultFormat();
            format.restoreAllOriginalRules();
            return termNumbering;
        }

        // When a cycle repeats atleat 3 times
        public void PrintCycleInfo(InfoPanelContent content, PrettyPrintFormat format)
        {
            List<Quantifier> cycle = new List<Quantifier>();
            for (int i = 0; i < cycleSize; i++)
            {
                cycle.Add(subgraphInstantiations[i].Quant);
            }
            GeneralizationState generalizationState = new GeneralizationState(cycleSize, subgraphInstantiations);
            generalizationState.generalize();

            if (generalizationState.TrueLoop)
            {
                content.switchFormat(PrintConstants.TitleFont, PrintConstants.warningTextColor);
                content.Append("Possible matching loop found!\n");
            }
            else
            {
                content.switchFormat(PrintConstants.BoldFont, PrintConstants.defaultTextColor);
                content.Append("Repeating pattern but ");
                content.switchFormat(PrintConstants.TitleFont, PrintConstants.defaultTextColor);
                content.Append("no");
                content.switchFormat(PrintConstants.BoldFont, PrintConstants.defaultTextColor);
                content.Append(" matching loop found!\n");
            }
            content.switchToDefaultFormat();
            content.Append($"Number of{(generalizationState.TrueLoop ? " loop" : "")} iterations: ").Append((subgraphInstantiations.Count / cycleSize) + "\n");
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
            content.Append($"\n\nGeneralized{(generalizationState.TrueLoop ? " Loop" : "")} Iteration:\n\n");

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

            var insts = subgraphInstantiations.GetEnumerator();
            insts.MoveNext();
            var recursiveEqualityExplanations = new List<Tuple<int, EqualityExplanation>>();
            var firstStep = generalizationState.generalizedTerms.First();
            
            printGeneralizedTermWithPrerequisites(content, format, generalizationState, alreadyIntroducedGeneralizations, firstStep.Item1, firstStep.Item2, firstStep.Item3, false, recursiveEqualityExplanations);

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
                if (insts.Current?.Quant.BodyTerm != null)
                {
                    content.Append("\n\n");

                    // print quantifier body with pattern
                    insts.Current.tempHighlightBlameBindTerms(format);
                    insts.Current.Quant.BodyTerm.PrettyPrint(content, format);
                }
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nThis yields:\n\n");
                content.switchToDefaultFormat();

                insts.MoveNext();
                format.restoreAllOriginalRules();
                printGeneralizedTermWithPrerequisites(content, format, generalizationState, alreadyIntroducedGeneralizations, step.Item1, step.Item2, step.Item3, count == numberOfSteps, recursiveEqualityExplanations);
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

        private static void printGeneralizedTermWithPrerequisites(InfoPanelContent content, PrettyPrintFormat format, GeneralizationState generalizationState, ISet<int> alreadyIntroducedGeneralizations,
            Term term, BindingInfo bindingInfo, List<Term> assocTerms, bool last, List<Tuple<int, EqualityExplanation>> recursiveEqualityExplanations)
        {
            generalizationState.tmpHighlightGeneralizedTerm(format, term, bindingInfo, last);

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

            if (last)
            {
                if (generalizationState.HasGeneralizationsForNextIteration())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nTerms for the Next Iteration:\n\n");
                    content.switchToDefaultFormat();
                    generalizationState.PrintGeneralizationsForNextIteration(content, format);
                }

                if (bindingInfo.getBindingsToFreeVars().Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("\nBindings to Start Next Iteration:\n\n");
                    content.switchToDefaultFormat();

                    foreach (var binding in bindingInfo.getBindingsToFreeVars())
                    {
                        if (binding.Value == null)
                        {
                            content.Append($"A generalized binding for {binding.Key.PrettyName} in the next iteration\ncould not be generated (This pattern cannot repeat indefinetly).");
                        }
                        else
                        {
                            content.Append(binding.Key.PrettyName).Append(" will be bound to:\n");
                            binding.Value.PrettyPrint(content, format);
                            content.switchToDefaultFormat();
                            content.Append("\n\n");
                        }
                    }
                }

                return;
            }

            if (bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nRelevant equalities:\n");
                content.switchToDefaultFormat();
                var equalitySetLookup = bindingInfo.equalities.ToLookup(eq => eq.Key.ContainsGeneralization() || eq.Value.Any(t => t.Item2.ContainsGeneralization()));

                format.printContextSensitive = false;
                if (equalitySetLookup[true].Any())
                {
                    foreach (var equality in equalitySetLookup[true])
                    {
                        var effectiveTerm = bindingInfo.bindings[equality.Key].Item2;
                        foreach (var t in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                        {
                            content.Append('\n');
                            termNumber = format.GetEqualityNumber(t, effectiveTerm);

                            EqualityExplanation explanation;
#if !DEBUG
                            try
                            {
#endif
                                explanation = bindingInfo.EqualityExplanations.First(ee => ee.source.id == t.id && ee.target.id == effectiveTerm.id);
#if !DEBUG
                            }
                            catch (Exception)
                            {
                                explanation = new TransitiveEqualityExplanation(term, effectiveTerm, new EqualityExplanation[0]);
                            }
#endif
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
                    foreach (var equality in equalitySetLookup[false])
                    {
                        var effectiveTerm = bindingInfo.bindings[equality.Key].Item2;
                        foreach (var t in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                        {
                            content.Append('\n');
                            termNumber = format.GetEqualityNumber(t, effectiveTerm);

                            EqualityExplanation explanation;
#if !DEBUG
                            try
                            {
#endif
                                explanation = bindingInfo.EqualityExplanations.First(ee => ee.source.id == t.id && ee.target.id == effectiveTerm.id);
#if !DEBUG
                            }
                            catch (Exception)
                            {
                                explanation = new TransitiveEqualityExplanation(term, effectiveTerm, new EqualityExplanation[0]);
                            }
#endif
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

            var bindingsToFreeVars = bindingInfo.getBindingsToFreeVars();
            if (bindingsToFreeVars.Any())
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nBinding information:");
                content.switchToDefaultFormat();

                foreach (var bindings in bindingsToFreeVars)
                {
                    content.Append("\n\n");
                    content.Append(bindings.Key.PrettyName).Append(" was bound to:\n");
                    bindings.Value.PrettyPrint(content, format);
                    content.switchToDefaultFormat();
                }

                content.Append("\n");
            }
        }

        private class EqualityExplanationShifter : EqualityExplanationVisitor<EqualityExplanation, int>
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

        private class EqualityExplanationIsRecursiveVisitor : EqualityExplanationVisitor<bool, object>
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

        private class EqualityExplanationShiftCollector : EqualityExplanationVisitor<int, object>
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
    }
}
