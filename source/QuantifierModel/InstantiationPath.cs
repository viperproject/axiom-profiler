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
            content.Append("Path explanation:");
            content.Append("\n------------------------\n");
            content.Append("Length: " + Length()).Append('\n');

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

                // add the rule for highlighting the blame term
                var terms = findBlameTerms(previous, instantiation);
                var restoreRules = new List<Tuple<string, PrintRule>>();
                foreach (var term in terms)
                {
                    var previousRule = format.getPrintRule(term);
                    var needRestore = format.printRuleDict.hasRule(term) &&
                                      format.printRuleDict.getMatch(term) == term.id + "";
                    if (needRestore)
                    {
                        restoreRules.Add(new Tuple<string, PrintRule>(term.id + "", previousRule));
                    }
                    highlightBlameTerm(format, previousRule.Clone(), term);
                }
                

                // print the blame term
                content.Append("\n\n");
                previous.SummaryInfo(content);
                content.switchToDefaultFormat();
                content.Append("\nThis instantiation yields:\n\n");
                previous.dependentTerms.Last().PrettyPrint(content, new StringBuilder(), format);

                // restore old formatting
                foreach (var term in terms)
                {
                    format.printRuleDict.removeRule(term.id + ""); 
                }
                foreach (var restoreRule in restoreRules)
                {
                    format.printRuleDict.addRule(restoreRule.Item1, restoreRule.Item2);
                }

                previous = instantiation;
            }
        }

        private static void highlightBlameTerm(PrettyPrintFormat format, PrintRule highlightRule, Term term)
        {
            highlightRule.color = Color.Red;
            if (format.printRuleDict.hasRule(term.id + ""))
            {
                format.printRuleDict.removeRule(term.id + "");
                format.printRuleDict.addRule(term.id + "", highlightRule);
            }
            else
            {
                format.printRuleDict.addRule(term.id + "", highlightRule);
            }
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }

        private List<Term> findBlameTerms(Instantiation parent, Instantiation child)
        {
            var termsToCheck = new List<Term>();
            termsToCheck.AddRange(child.Responsible);
            return termsToCheck.FindAll(term => parent.dependentTerms.Contains(term)).ToList();
        }

        private List<Term> findBondTermsInBlameterm(Instantiation child, Term blameTerm)
        {
            var termsToCheck = new Queue<Term>();
            termsToCheck.Enqueue(blameTerm);

            var results = new List<Term>();

            while (termsToCheck.Count > 0)
            {
                var current = termsToCheck.Dequeue();
                if (child.Responsible.Contains(current))
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
