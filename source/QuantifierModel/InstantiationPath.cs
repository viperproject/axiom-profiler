using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.CycleDetection;
using AxiomProfiler.PrettyPrinting;

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
            printCycleInfo(content, format);
            content.switchFormat(InfoPanelContent.TitleFont, Color.Black);
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
                printInstantiationWithPredecessor(content, format, current, previous, cycleDetector);
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
                if (term is Term && generalization.IsReplaced(((Term)term).id))
                {
                    ((Term)term).highlightTemporarily(format, Color.DeepPink);
                }
                else
                {
                    highlightGens(term.Args, format, generalization);
                }
            }
        }

        private static void printInstantiationWithPredecessor(InfoPanelContent content, PrettyPrintFormat format,
            Instantiation current, Instantiation previous, CycleDetection.CycleDetection cycDetect)
        {
            current.tempHighlightBlameBindTerms(format);
            var potGens = previous.concreteBody.Args;
            var generalization = cycDetect.getGeneralization();
            highlightGens(potGens, format, generalization);

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("\n\nThis instantiation yields:\n\n");
            content.switchToDefaultFormat();
            previous.concreteBody.PrettyPrint(content, format);

            // Other prerequisites:
            var otherRequiredTerms = current.bindingInfo.getDistinctBlameTerms()
                .FindAll(term => current.bindingInfo.equalities.Any(eq => current.bindingInfo.bindings[eq.Key] == term) ||
                        !previous.concreteBody.isSubterm(term)).ToList();
            if (otherRequiredTerms.Count > 0)
            {
                content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
                content.Append("\n\nTogether with the following term(s):");
                content.switchToDefaultFormat();
                foreach (var distinctBlameTerm in otherRequiredTerms)
                {
                    content.Append("\n\n");
                    distinctBlameTerm.PrettyPrint(content, format);
                }
            }

            if (current.bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
                content.Append("\n\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                foreach (var equality in current.bindingInfo.equalities)
                {
                    current.bindingInfo.bindings[equality.Key].printName(content, format);
                    foreach (var term in equality.Value)
                    {
                        content.Append(" = ");
                        term.printName(content, format);
                    }
                    content.Append('\n');
                }
            }

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("\n\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.switchToDefaultFormat();
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            format.restoreAllOriginalRules();
        }

        private static void printPathHead(InfoPanelContent content, PrettyPrintFormat format, Instantiation current)
        {
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("\nStarting from the following term(s):\n\n");
            content.switchToDefaultFormat();
            current.tempHighlightBlameBindTerms(format);
            foreach (var distinctBlameTerm in current.bindingInfo.getDistinctBlameTerms())
            {
                distinctBlameTerm.PrettyPrint(content, format);
            }

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.switchToDefaultFormat();
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            format.restoreAllOriginalRules();
        }

        private void printCycleInfo(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (!hasCycle()) return;
            var cycle = cycleDetector.getCycleQuantifiers();
            content.switchFormat(InfoPanelContent.TitleFont, Color.Red);
            content.Append("Possible matching loop found!\n");
            content.switchToDefaultFormat();
            content.Append("Number of repetitions: ").Append(cycleDetector.getRepetiontions() + "\n");
            content.Append("Length: ").Append(cycle.Count + "\n");
            content.Append("Loop: ");
            content.Append(string.Join(" -> ", cycle.Select(quant => quant.PrintName)));
            content.Append("\n");

            printPreamble(content, true);

            content.switchFormat(InfoPanelContent.TitleFont, Color.Black);
            content.Append("\n\nGeneralized Loop Iteration:\n\n");

            var generalizationState = cycleDetector.getGeneralization();
            var generalizedTerms = generalizationState.generalizedTerms;

            // print last yield term before printing the complete loop
            // to give the user a term to match the highlighted pattern to
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("\nStarting anywhere with the following term(s):\n\n");
            content.switchToDefaultFormat();
            var insts = cycleDetector.getCycleInstantiations().GetEnumerator();
            insts.MoveNext();
            printGeneralizedTermWithPrerequisites(content, format, generalizationState, generalizedTerms.First(), insts.Current, true, false);

            var count = 1;
            var loopYields = generalizedTerms.GetRange(1, generalizedTerms.Count - 1);
            foreach (var term in loopYields)
            {
                format.restoreAllOriginalRules();
                content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
                content.Append("\nApplication of ");
                content.Append(insts.Current?.Quant.PrintName);
                content.switchToDefaultFormat();
                content.Append("\n\n");

                // print quantifier body with pattern
                insts.Current?.tempHighlightBlameBindTerms(format);
                insts.Current?.Quant.BodyTerm.PrettyPrint(content, format);
                content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
                content.Append("\n\nThis yields:\n\n");
                content.switchToDefaultFormat();

                insts.MoveNext();
                printGeneralizedTermWithPrerequisites(content, format, generalizationState, term, insts.Current, false, count == cycle.Count);
                count++;
            }
            format.restoreAllOriginalRules();
            content.Append("\n\n");
        }

        private static void printGeneralizedTermWithPrerequisites(InfoPanelContent content, PrettyPrintFormat format,
            GeneralizationState generalizationState, Term term, Instantiation instantiation, bool first, bool last)
        {
            generalizationState.tmpHighlightGeneralizedTerm(format, term);
            term.PrettyPrint(content, format);
            content.Append("\n");

            if (last ||
                !generalizationState.assocGenBlameTerm.ContainsKey(term) ||
                generalizationState.assocGenBlameTerm[term].Count <= 0)
                return;

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("\nTogether with the following term(s):");
            content.switchToDefaultFormat();
            var otherRequirements = generalizationState.assocGenBlameTerm[term];

            foreach (var req in otherRequirements)
            {
                content.Append("\n\n");
                generalizationState.tmpHighlightGeneralizedTerm(format, req);
                req.PrettyPrint(content, format);
            }

            var bindingInfo = generalizationState.generalizedBindingInfo(instantiation);
            if (bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
                content.Append("\n\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                foreach (var equality in bindingInfo.equalities)
                {
                    bindingInfo.bindings[equality.Key].printName(content, format);
                    foreach (var t in equality.Value)
                    {
                        content.Append(" = ");
                        t.printName(content, format);
                    }
                    content.Append('\n');
                }
            }
        }

        private void printPreamble(InfoPanelContent content, bool withGen)
        {
            content.Append("\nHighlighted terms are ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.LimeGreen);
            content.Append("matched");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.Goldenrod);
            content.Append("matched using equality");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.Coral);
            content.Append("blamed");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.DeepSkyBlue);
            content.Append("bound");
            content.switchToDefaultFormat();
            if (withGen)
            {
                content.Append(" or ");
                content.switchFormat(InfoPanelContent.DefaultFont, Color.DeepPink);
                content.Append("change between iterations");
                content.switchToDefaultFormat();
            }
            content.Append(".\n");
        }

        private static void legacyInstantiationInfo(InfoPanelContent content, PrettyPrintFormat format, Instantiation instantiation)
        {
            instantiation.printNoMatchdisclaimer(content);
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkMagenta);
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
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkMagenta);
            content.Append("Bound terms:\n\n");
            content.switchToDefaultFormat();
            foreach (var t in instantiation.Bindings)
            {
                content.Append("\n");
                t.PrettyPrint(content, format);
                content.Append("\n\n");
            }

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkMagenta);
            content.Append("Quantifier Body:\n\n");

            instantiation.concreteBody?.PrettyPrint(content, format);
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }
    }
}
