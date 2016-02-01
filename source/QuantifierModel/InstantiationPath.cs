using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler.QuantifierModel
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

        public void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.switchToDefaultFormat();
            content.Append("Path explanation:");
            content.Append("\n------------------------\n");
            content.Append("Length: " + Length()).Append('\n');
            content.Append("Highlighted terms are ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.Coral);
            content.Append("blamed or matched");
            content.switchToDefaultFormat();
            content.Append(" and ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.DeepSkyBlue);
            content.Append("bound");
            content.switchToDefaultFormat();
            content.Append(".\n\n");

            Instantiation previous = null;
            Instantiation current = null;
            foreach (var instantiation in pathInstantiations)
            {
                current = instantiation;
                if (previous == null)
                {
                    previous = instantiation;
                    continue;
                }

                // Quantifier info
                content.Append("Application of ").Append(previous.Quant.PrintName);
                content.Append("\n\n");

                // Quantifier body with highlights (if applicable)
                var previousPattern = previous.findMatchingPattern();
                if (previousPattern != null)
                {
                    var tmp = format.getPrintRule(previousPattern).Clone();
                    tmp.color = Color.Coral;
                    format.addTemporaryRule(previousPattern.id + "", tmp);
                }
                previous.Quant.BodyTerm.PrettyPrint(content, new StringBuilder(), format);

                var currentPattern = instantiation.findMatchingPattern();
                if (currentPattern != null)
                {
                    // blame terms
                    foreach (var blameTermsToPathConstraint in instantiation.blameTermsToPathConstraints)
                    {
                        var tmp = format.getPrintRule(blameTermsToPathConstraint.Key).Clone();
                        tmp.color = Color.Coral;
                        // add all history constraints
                        tmp.historyConstraints.AddRange(blameTermsToPathConstraint.Value);

                        format.addTemporaryRule(blameTermsToPathConstraint.Key.id + "", tmp);
                    }
                    
                    // bound terms
                    foreach (var termWithConstraints in instantiation.freeVariableToBindingsAndPathConstraints.Values)
                    {
                        var tmp = format.getPrintRule(termWithConstraints.Item1).Clone();
                        tmp.color = Color.DeepSkyBlue;
                        // add all history constraints
                        tmp.historyConstraints.AddRange(termWithConstraints.Item2);

                        format.addTemporaryRule(termWithConstraints.Item1.id + "", tmp);
                    }
                }

                content.Append("\nThis instantiation yields:\n\n");
                previous.dependentTerms.Last().PrettyPrint(content, new StringBuilder(), format);

                format.restoreAllOriginalRules();

                previous = instantiation;
            }
        }

        private static PrintRule getHighlightRule(PrettyPrintFormat format, Term term, List<Tuple<string, PrintRule>> restoreRules)
        {
            var previousRule = format.getPrintRule(term);
            var needRestore = format.printRuleDict.hasRule(term) &&
                              format.printRuleDict.getMatch(term) == term.id + "";
            if (needRestore)
            {
                restoreRules.Add(new Tuple<string, PrintRule>(term.id + "", previousRule));
            }
            return previousRule.Clone();
        }

        private static void highlightTerm(PrettyPrintFormat format, PrintRule highlightRule, Term term, Color color)
        {
            highlightRule.color = color;
            format.printRuleDict.addRule(term.id + "", highlightRule);
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }

        private List<Term> findBlameTerms(Instantiation parent, Instantiation child)
        {
            var termsToCheck = new Queue<Term>();
            foreach (var dependentTerm in parent.dependentTerms)
            {
                termsToCheck.Enqueue(dependentTerm);
            }
            var results = new List<Term>();

            while (termsToCheck.Count > 0)
            {
                var current = termsToCheck.Dequeue();
                if (child.Responsible.Contains(current) && !results.Contains(current))
                {
                    results.Add(current);
                    foreach (var term in current.Args)
                    {
                        termsToCheck.Enqueue(term);
                    }
                }
            }

            return results;
        }

        private List<Term> findBoundTermsInBlameTerm(Instantiation child, Term blameTerm)
        {
            var termsToCheck = new Queue<Term>();
            termsToCheck.Enqueue(blameTerm);

            var results = new List<Term>();

            while (termsToCheck.Count > 0)
            {
                var current = termsToCheck.Dequeue();
                // Note: Some terms might appear multiple times. We just want them once.
                if (child.Bindings.Contains(current) && !results.Contains(current))
                {
                    results.Add(current);
                }
                else
                {
                    foreach (var term in current.Args)
                    {
                        termsToCheck.Enqueue(term);
                    }
                }
            }

            return results;
        }
    }
}
