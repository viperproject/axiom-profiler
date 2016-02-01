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
            content.Append(".\n");

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
                content.Append("\n\nApplication of ").Append(previous.Quant.PrintName);
                content.Append("\n\n");

                // Quantifier body with highlights (if applicable)
                highlightPattern(format, previous.matchedPattern);
                previous.Quant.BodyTerm.PrettyPrint(content, new StringBuilder(), format);

                highlightBlameBindTerms(format, instantiation.matchedPattern, instantiation);

                content.Append("\n\nThis instantiation yields:\n\n");
                previous.dependentTerms.Last().PrettyPrint(content, new StringBuilder(), format);

                format.restoreAllOriginalRules();

                previous = instantiation;
            }


        }

        private static void highlightPattern(PrettyPrintFormat format, Term previousPattern)
        {
            if (previousPattern != null)
            {
                var tmp = format.getPrintRule(previousPattern).Clone();
                tmp.color = Color.Coral;
                format.addTemporaryRule(previousPattern.id + "", tmp);
            }
        }

        private static void highlightBlameBindTerms(PrettyPrintFormat format, Term currentPattern, Instantiation instantiation)
        {
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
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }
    }
}
