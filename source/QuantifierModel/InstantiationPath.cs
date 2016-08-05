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
                printInstantiationWithPredecessor(content, format, current, previous);
            }

            // Quantifier info for last in chain
            content.switchToDefaultFormat();
            content.Append("\n\nThis instantiation yields:\n\n");

            if (current.dependentTerms.Last() != null)
            {
                current.dependentTerms.Last().PrettyPrint(content, format);
            }
        }

        private static void printInstantiationWithPredecessor(InfoPanelContent content, PrettyPrintFormat format,
            Instantiation current, Instantiation previous)
        {
            current.tempHighlightBlameBindTerms(format);

            content.switchToDefaultFormat();
            content.Append("\n\nThis instantiation yields:\n\n");
            previous.dependentTerms.Last().PrettyPrint(content, format);

            // Other prerequisites:
            var otherRequiredTerms = current.bindingInfo.getDistinctBlameTerms()
                .FindAll(term => !previous.dependentTerms.Last().isSubterm(term)).ToList();
            if (otherRequiredTerms.Count > 0)
            {
                content.switchToDefaultFormat();
                content.Append("\n\nTogether with the following term(s):\n\n");
                foreach (var distinctBlameTerm in otherRequiredTerms)
                {
                    distinctBlameTerm.PrettyPrint(content, format);
                    content.Append('\n');
                }
            }

            content.switchToDefaultFormat();
            content.Append("\n\nApplication of ");
            content.Append(current.Quant.PrintName);
            content.Append("\n\n");

            current.Quant.BodyTerm.PrettyPrint(content, format);
            format.restoreAllOriginalRules();
        }

        private static void printPathHead(InfoPanelContent content, PrettyPrintFormat format, Instantiation current)
        {
            content.Append("\nStarting from the following term(s):\n\n");
            current.tempHighlightBlameBindTerms(format);
            foreach (var distinctBlameTerm in current.bindingInfo.getDistinctBlameTerms())
            {
                distinctBlameTerm.PrettyPrint(content, format);
            }

            content.switchToDefaultFormat();
            content.Append("\nApplication of ");
            content.Append(current.Quant.PrintName);
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
            content.switchToDefaultFormat();

            var generalizationState = cycleDetector.getGeneralization();
            var generalizedTerms = generalizationState.generalizedTerms;

            // print last yield term before printing the complete loop
            // to give the user a term to match the highlighted pattern to
            content.Append("\nStarting anywhere with the following term(s):\n\n");
            printGeneralizedTermWithPrerequisites(content, format, generalizationState, generalizedTerms.Last(),true, false);

            var insts = cycleDetector.getCycleInstantiations().GetEnumerator();
            insts.MoveNext();
            var count = 1;
            foreach (var term in generalizedTerms)
            {
                format.restoreAllOriginalRules();
                content.switchToDefaultFormat();
                content.Append("\nApplication of ");
                content.Append(insts.Current?.Quant.PrintName);
                content.Append("\n\n");

                // print quantifier body with pattern
                insts.Current?.tempHighlightBlameBindTerms(format);
                insts.Current?.Quant.BodyTerm.PrettyPrint(content, format);
                content.switchToDefaultFormat();
                content.Append("\n\nThis yields:\n\n");

                printGeneralizedTermWithPrerequisites(content, format, generalizationState, term, false, count == cycle.Count);
                count++;
                insts.MoveNext();
            }
            format.restoreAllOriginalRules();
            content.Append("\n\n");
        }

        private static void printGeneralizedTermWithPrerequisites(InfoPanelContent content, PrettyPrintFormat format,
            GeneralizationState generalizationState, Term term, bool first, bool last)
        {
            generalizationState.tmpHighlightGeneralizedTerm(format, term, first);
            term.PrettyPrint(content, format);
            content.Append("\n");

            if (last ||
                !generalizationState.assocGenBlameTerm.ContainsKey(term) ||
                generalizationState.assocGenBlameTerm[term].Count <= 0)
                return;

            content.switchToDefaultFormat();
            content.Append("\nTogether with the following term(s):\n\n");
            var otherRequirements = generalizationState.assocGenBlameTerm[term];

            foreach (var req in otherRequirements)
            {
                generalizationState.tmpHighlightGeneralizedTerm(format, req, first);
                req.PrettyPrint(content, format);
                content.Append("\n\n");
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
                content.switchFormat(InfoPanelContent.DefaultFont, Color.BlueViolet);
                content.Append("generalized");
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

            instantiation.dependentTerms.Last()?.PrettyPrint(content, format);
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }
    }
}
