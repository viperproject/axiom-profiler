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
        private readonly List<List<Instantiation>> subgraphInstantiations;
        // number of elements in cycle (the repeating strucutre)
        private int cycleSize;
        // number of elements in cycle (when it was just a path)
        private int simpleSize;
        public InstantiationSubgraph(ref List<List<Node>> subgraph, int size, int simpleSZ)
        {
            cycleSize = size;
            simpleSize = simpleSZ;
            subgraphInstantiations = new List<List<Instantiation>>();
            foreach (List<Node> cycle in subgraph)
            {
                if (cycle.Count != cycleSize) break;
                List<Instantiation> cycleInstantiations = new List<Instantiation>();
                foreach (Node node in cycle)
                {
                    cycleInstantiations.Add((Instantiation)(node.UserData));
                }
                subgraphInstantiations.Add(cycleInstantiations);
            }
        }

        // To generate the info panel content
        public void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (subgraphInstantiations.Count < 3)
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
            int termNumbering = printPathHead(content, format, subgraphInstantiations[0][0]);

            for (int i = 1; i < cycleSize; i++)
            {
                termNumbering = printInstantiationWithPredecessor(content, format, 
                    subgraphInstantiations[0][i], subgraphInstantiations[0][i-1], termNumbering);
            }

            content.switchToDefaultFormat();
            content.Append("\n\nThis instantiation yields:\n\n");
            subgraphInstantiations[0][cycleSize-1].concreteBody.PrettyPrint(content, format);
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
            // TODO
        }
    }
}
