using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler.QuantifierModel
{
    public class Instantiation : Common
    {
        public Quantifier Quant;
        public Term concreteBody;
        public Term[] Bindings;
        public Term[] Responsible;
        public readonly List<Term> dependentTerms = new List<Term>();
        public int LineNo;
        public double Cost;
        public readonly List<Instantiation> ResponsibleInstantiations = new List<Instantiation>();
        public readonly List<Instantiation> DependantInstantiations = new List<Instantiation>();
        public int Z3Generation;
        int depth;
        int wdepth = -1;
        public int DeepestSubpathDepth;
        public string uniqueID => LineNo.ToString();
        private BindingInfo _bindingInfo;

        public BindingInfo bindingInfo
        {
            get
            {
                if (!didPatternMatch) processPattern();
                return _bindingInfo;
            }
        }

        public void CopyTo(Instantiation inst)
        {
            inst.Quant = Quant;
            inst.Bindings = (Term[])Bindings.Clone();
            inst.Responsible = (Term[])Responsible.Clone();
            inst.concreteBody = new Term(concreteBody);
        }

        public int Depth
        {
            get
            {
                if (depth != 0)
                {
                    return depth;
                }

                int max = (from t in Responsible where t.Responsible != null select t.Responsible.Depth)
                    .Concat(new[] { 0 }).Max();
                depth = max + 1;
                return depth;
            }
        }

        public int WDepth
        {
            get
            {
                if (wdepth == -1)
                {
                    int max = (from t in Responsible where t.Responsible != null select t.Responsible.WDepth)
                        .Concat(new[] { 0 }).Max();
                    wdepth = max + Quant.Weight;
                }
                return wdepth;
            }
        }


        public override string ToString()
        {
            string result = $"Instantiation[{Quant.PrintName}] @line: {LineNo}, Depth: {Depth}, Cost: {Cost}";
            return result;
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (bindingInfo != null)
            {
                FancyInfoPanelText(content, format);
            }
            else
            {
                legacyInfoPanelText(content, format);
            }
        }

        private void legacyInfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            printNoMatchdisclaimer(content);
            SummaryInfo(content);
            content.switchFormat(PrintConstants.SubtitleFont, Color.DarkMagenta);
            content.Append("Blamed terms:\n\n");
            content.switchToDefaultFormat();

            foreach (var t in Responsible)
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
            foreach (var t in Bindings)
            {
                content.Append("\n");
                t.PrettyPrint(content, format);
                content.Append("\n\n");
            }

            content.switchToDefaultFormat();
            content.Append("The quantifier body:\n\n");
            Quant.BodyTerm.PrettyPrint(content, format);
            content.Append("\n\n");

            content.switchToDefaultFormat();
            content.Append("The resulting term:\n\n");
            concreteBody.PrettyPrint(content, format);
        }

        public void printNoMatchdisclaimer(InfoPanelContent content)
        {
            content.switchFormat(PrintConstants.ItalicFont, PrintConstants.warningTextColor);
            content.Append("No pattern match found. Possible reasons include (hidden) equalities\nand / or automatic term simplification.\n");
            content.switchToDefaultFormat();
        }

        private void FancyInfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (isAmbiguous)
            {
                content.switchFormat(PrintConstants.ItalicFont, PrintConstants.warningTextColor);
                content.Append( "Pattern match is ambiguos. Most likely match presented.\n");
                content.switchToDefaultFormat();
            }
            SummaryInfo(content);
            content.Append("Highlighted terms are ");
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
            content.Append(".\n\n");

            tempHighlightBlameBindTerms(format);

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("Blamed Terms:\n\n");
            content.switchToDefaultFormat();

            foreach (var t in bindingInfo.getDistinctBlameTerms())
            {
                content.Append("\n");
                t.PrettyPrint(content, format);
                content.Append("\n\n");
            }

            if (bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                foreach (var equality in bindingInfo.equalities)
                {
                    bindingInfo.bindings[equality.Key].PrettyPrint(content, format);
                    foreach (var t in equality.Value)
                    {
                        content.Append("\n=\n");
                        t.PrettyPrint(content, format);
                    }
                    content.Append("\n\n");
                }

                bindingInfo.PrintEqualitySubstitution(content, format);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("Binding information:\n\n");
            content.switchToDefaultFormat();

            foreach (var bindings in bindingInfo.getBindingsToFreeVars())
            {
                content.Append(bindings.Key.Name).Append(" was bound to:\n");
                bindings.Value.PrettyPrint(content, format);
                content.switchToDefaultFormat();
                content.Append("\n\n");
            }

            if (bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                foreach (var equality in bindingInfo.equalities)
                {
                    bindingInfo.bindings[equality.Key].PrettyPrint(content, format);
                    foreach (var term in equality.Value)
                    {
                        content.Append("\n=\n");
                        term.PrettyPrint(content, format);
                    }
                    content.Append("\n\n");
                }
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\n\nThe quantifier body:\n\n");
            content.switchToDefaultFormat();
            Quant.BodyTerm.PrettyPrint(content, format);
            content.Append("\n\n");
            format.restoreAllOriginalRules();

            content.switchToDefaultFormat();
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("The resulting term:\n\n");
            content.switchToDefaultFormat();
            concreteBody.PrettyPrint(content, format);
        }

        public void tempHighlightBlameBindTerms(PrettyPrintFormat format)
        {
            if (bindingInfo == null) return;
            bindingInfo.fullPattern.highlightTemporarily(format, PrintConstants.patternMatchColor);
            foreach (var binding in bindingInfo.bindings)
            {
                var patternTerm = binding.Key;
                var term = binding.Value;
                var color = PrintConstants.blameColor;
                if (patternTerm.id == -1) color = PrintConstants.bindColor;

                patternTerm.highlightTemporarily(format, color, bindingInfo.patternMatchContext[patternTerm.id]);
                term.highlightTemporarily(format, color, bindingInfo.matchContext[term.id]);
                if (!bindingInfo.equalities.ContainsKey(patternTerm)) continue;

                // highlight replaced, equal terms as well
                foreach (var eqTerm in bindingInfo.equalities[patternTerm])
                {
                    eqTerm.highlightTemporarily(format, PrintConstants.equalityColor, bindingInfo.matchContext[eqTerm.id]);
                }
            }
        }

        private void processPattern()
        {
            if (didPatternMatch) return;
            didPatternMatch = true;
            var bindingCandidates = findAllMatches();
            switch (bindingCandidates.Count)
            {
                case 0:
                    return;
                case 1:
                    _bindingInfo = bindingCandidates[0];
                    return;
                default:
                    _bindingInfo = bindingCandidates.FirstOrDefault(candidate => candidate.validate());
                    break;
            }
            if (_bindingInfo != null) return;
            isAmbiguous = true;
            _bindingInfo = bindingCandidates[0];
        }

        private List<BindingInfo> findAllMatches()
        {
            var plausibleMatches = new List<BindingInfo>();
            foreach (var pattern in Quant.Patterns())
            {
                var matches = parallelDescent(pattern);
                foreach (var match in matches)
                {
                    if (match.finalize(Responsible.ToList(), Bindings.ToList())) plausibleMatches.Add(match);
                }
            }
            return plausibleMatches.OrderBy(match => match.numEq).ToList();
        }

        private IEnumerable<BindingInfo> parallelDescent(Term pattern)
        {
            // list with empty binding info to collect all possible matches
            var plausibleMatches = new List<BindingInfo> {new BindingInfo(pattern, Responsible)};
            
            var patternQueue = new Queue<Term>();

            enqueueSubPatterns(pattern, patternQueue);

            while (patternQueue.Count > 0)
            {
                var currentPattern = patternQueue.Dequeue();
                var currMatches = new List<BindingInfo>();
                foreach (var match in plausibleMatches)
                {
                    currMatches.AddRange(match.allNextMatches(currentPattern));
                }
                plausibleMatches = currMatches;

                enqueueSubPatterns(currentPattern, patternQueue);
            }

            return plausibleMatches;
        }

        private static void enqueueSubPatterns(Term pattern, Queue<Term> patternQueue)
        {
            foreach (var arg in pattern.Args)
            {
                patternQueue.Enqueue(arg);
            }
        }


        // The dictionary stores the following information:
        // The key is a the free variable term.
        // The value is structured as follows:
        // It's a tuple with the first item being bound term.
        // The second item is a list of histories such that occurrences of bound terms in blamed terms,
        // that were actually bound in that position, can be distinguished from occurrences that were not bound.
        // A history is represented as a list of terms.
        // That's why the value type is Tuple<Term, List<List<Term>>>.
        private bool didPatternMatch;
        private bool isAmbiguous;

        public override void SummaryInfo(InfoPanelContent content)
        {
            content.switchFormat(PrintConstants.TitleFont, PrintConstants.instantiationTitleColor);
            content.Append("Instantiation ").Append('@').Append(LineNo + ":\n");
            content.switchToDefaultFormat();

            content.Append('\n').Append(Quant.PrintName).Append('\n');
            content.Append("Depth: " + depth).Append('\n');
            content.Append("Cost: ").Append(Cost.ToString("F")).Append("\n\n");
        }

        public override IEnumerable<Common> Children()
        {
            if (Responsible != null)
            {
                var sortedResponsibleList = new List<Term>(Responsible);
                sortedResponsibleList.Sort((t1, t2) =>
                {
                    int d1 = t1.Responsible?.Depth ?? 0;
                    int d2 = t2.Responsible?.Depth ?? 0;
                    return d2.CompareTo(d1);
                });
                yield return Callback($"BLAME INSTANTIATIONS [{sortedResponsibleList.Count(t => t.Responsible != null)}]", () => sortedResponsibleList
                    .Where(t => t.Responsible != null).Select(t => t.Responsible));
                yield return Callback($"BLAME TERMS [{sortedResponsibleList.Count}]", () => sortedResponsibleList);
            }

            if (Bindings.Length > 0)
            {
                yield return Callback($"BIND [{Bindings.Length}]", () => Bindings);
            }

            if (dependentTerms.Count > 0)
            {
                yield return Callback($"YIELDS TERMS [{dependentTerms.Count}]", () => dependentTerms);
            }

            if (DependantInstantiations.Count <= 0) yield break;

            DependantInstantiations.Sort((i1, i2) => i1.LineNo.CompareTo(i2.LineNo));
            yield return Callback($"YIELDS INSTANTIATIONS [{DependantInstantiations.Count}]", () => DependantInstantiations);
        }
    }

    public class ImportantInstantiation : Instantiation
    {
        public int UseCount;
        public int DepCount;
        public readonly List<ImportantInstantiation> ResponsibleInsts = new List<ImportantInstantiation>();

        public ImportantInstantiation(Instantiation par)
        {
            Quant = par.Quant;
            Bindings = par.Bindings;
            Responsible = par.Responsible;
            LineNo = par.LineNo;
            Cost = par.Cost;
            Z3Generation = par.Z3Generation;
        }

        public override IEnumerable<Common> Children()
        {
            if (ResponsibleInsts != null)
            {
                ResponsibleInsts.Sort(delegate (ImportantInstantiation i1, ImportantInstantiation i2)
                {
                    if (i1.WDepth == i2.WDepth) return i2.Depth.CompareTo(i1.Depth);
                    return i2.WDepth.CompareTo(i1.WDepth);
                });
                foreach (var i in ResponsibleInsts)
                    yield return i;
                yield return Callback("BLAME", () => Responsible);
            }
            if (Bindings.Length > 0)
            {
                yield return Callback("BIND", () => Bindings);
            }
        }

        public override Color ForeColor()
        {
            return Quant.Weight == 0 ? Color.DarkSeaGreen : base.ForeColor();
        }
    }
}