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
            content.switchFormat(InfoPanelContent.TitleFont, Color.Black);
            content.Append("Path explanation:");
            content.switchToDefaultFormat();
            content.Append("\n\nLength: " + Length()).Append('\n');
            content.Append("Highlighted terms are ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.Coral);
            content.Append("blamed or matched");
            content.switchToDefaultFormat();
            content.Append(" and ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.DeepSkyBlue);
            content.Append("bound");
            content.switchToDefaultFormat();
            content.Append(", respectively.\n");

            Instantiation previous = null;
            Instantiation current = null;
            foreach (var instantiation in pathInstantiations)
            {
                current = instantiation;
                instantiation.tempHighlightBlameBindTerms(format);
                if (previous == null)
                {
                    content.Append("\nStarting from the following term(s):\n");

                    foreach (var distinctBlameTerm in instantiation.getDistinctBlameTerms())
                    {
                        distinctBlameTerm.PrettyPrint(content, format);
                    }
                    previous = instantiation;
                    format.restoreAllOriginalRules();
                    continue;
                }


                // Other prerequisites:
                var otherRequiredTerms = instantiation.getDistinctBlameTerms()
                    .FindAll(term => !previous.dependentTerms.Last().isSubterm(term)).ToList();

                if (otherRequiredTerms.Count > 0)
                {
                    content.switchToDefaultFormat();
                    content.Append("\nTogether with the following term(s):\n");
                    foreach (var distinctBlameTerm in otherRequiredTerms)
                    {
                        distinctBlameTerm.PrettyPrint(content, format);
                    }
                }

                // Quantifier info
                content.switchToDefaultFormat();
                content.Append("\n\nApplication of ").Append(previous.Quant.PrintName);
                content.Append("\n\n");

                // Quantifier body with highlights (if applicable)
                previous.matchedPattern?.highlightTemporarily(format, Color.Coral);
                previous.Quant.BodyTerm.PrettyPrint(content, format);

                content.switchToDefaultFormat();
                content.Append("\n\nThis instantiation yields:\n\n");
                previous.dependentTerms.Last().PrettyPrint(content, format);

                format.restoreAllOriginalRules();

                previous = instantiation;
            }

            if (current == null) return;
            // Quantifier info for last in chain
            content.switchToDefaultFormat();
            content.Append("\n\nApplication of ").Append(previous.Quant.PrintName);
            content.Append("\n\n");
            current.matchedPattern?.highlightTemporarily(format, Color.Coral);
            current.Quant.BodyTerm.PrettyPrint(content, format);

            content.switchToDefaultFormat();
            content.Append("\n\nThis instantiation yields:\n\n");
            if (previous.dependentTerms.Last() != null)
            {
                previous.dependentTerms.Last().PrettyPrint(content, format);
            }
            format.restoreAllOriginalRules();
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }
    }
}
