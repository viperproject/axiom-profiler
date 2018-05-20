using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.PrettyPrinting;
using System;

namespace AxiomProfiler.QuantifierModel
{
    public class Instantiation : Common
    {
        private class DirectEqualityCollector : EqualityExplanationVisitor<IEnumerable<Term>, object>
        {
            public override IEnumerable<Term> Direct(DirectEqualityExplanation target, object arg)
            {
                yield return target.equality;
            }

            public override IEnumerable<Term> Transitive(TransitiveEqualityExplanation target, object arg)
            {
                var result = Enumerable.Empty<Term>();
                foreach (var eq in target.equalities)
                {
                    result = result.Concat(visit(eq, arg));
                }
                return result;
            }

            public override IEnumerable<Term> Congruence(CongruenceExplanation target, object arg)
            {
                var result = Enumerable.Empty<Term>();
                foreach (var eq in target.sourceArgumentEqualities)
                {
                    result = result.Concat(visit(eq, arg));
                }
                return result;
            }

            public override IEnumerable<Term> RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }
        private static readonly DirectEqualityCollector directEqualityCollector = new DirectEqualityCollector();

        public Quantifier Quant;
        public Term concreteBody;
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
        public readonly BindingInfo bindingInfo;
        private Term[] _Responsible = null;
        public Term[] Responsible
        {
            get
            {
                if (_Responsible == null)
                {
                    _Responsible = bindingInfo.TopLevelTerms
                        .Concat(bindingInfo.EqualityExplanations.Select(expl => expl.target))
                        //.Concat(bindingInfo.EqualityExplanations.SelectMany(expl => directEqualityCollector.visit(expl, null)))
                        .ToArray();
                }
                return _Responsible;
            }
        }

        public Term[] Bindings
        {
            get
            {
                return bindingInfo.BoundTerms;
            }
        }

        public Instantiation(BindingInfo bindingInfo)
        {
            this.bindingInfo = bindingInfo;
        }

        public Instantiation Copy()
        {
            return new Instantiation(bindingInfo)
            {
                Quant = Quant,
                concreteBody = concreteBody
            };
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
            
            var termNumberings = new List<Tuple<Term, int>>();

            var blameTerms = bindingInfo.getDistinctBlameTerms();
            var distinctBlameTerms = blameTerms.Where(bt => blameTerms.All(super => bt == super || !super.isSubterm(bt)))
                .Where(req => !bindingInfo.equalities.SelectMany(eq => eq.Value).Any(t => t.id == req.id))
                .Where(req => bindingInfo.equalities.Keys.All(k => bindingInfo.bindings[k] != req));
            foreach (var t in distinctBlameTerms)
            {
                var termNumber = bindingInfo.GetTermNumber(t) + 1;
                var numberingString = $"({termNumber}) ";
                content.Append($"\n{numberingString}");
                termNumberings.Add(Tuple.Create(t, termNumber));
                t.PrettyPrint(content, format, numberingString.Length);
                content.switchToDefaultFormat();
                content.Append("\n\n");
            }

            if (bindingInfo.equalities.Count > 0)
            {
                var numberOfTopLevelTerms = bindingInfo.getDistinctBlameTerms().Count;

                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                format.printContextSensitive = false;
                var equalityNumberings = new List<Tuple<IEnumerable<Term>, int>>();
                foreach (var equality in bindingInfo.equalities)
                {
                    var effectiveTerm = bindingInfo.bindings[equality.Key];
                    foreach (var term in equality.Value)
                    {
                        var termNumber = numberOfTopLevelTerms + bindingInfo.GetEqualityNumber(term, effectiveTerm) + 1;
                        equalityNumberings.Add(new Tuple<IEnumerable<Term>, int>(new Term[] { term, effectiveTerm }, termNumber));
                        if (format.ShowEqualityExplanations)
                        {
                            var explanation = bindingInfo.EqualityExplanations.Single(ee => ee.source.id == term.id && ee.target.id == effectiveTerm.id);
                            explanation.PrettyPrint(content, format, termNumber);
                        }
                        else
                        {
                            var numberingString = $"({termNumber}) ";
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

                bindingInfo.PrintEqualitySubstitution(content, format, termNumberings, equalityNumberings);
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("Binding information:");
            content.switchToDefaultFormat();

            foreach (var bindings in bindingInfo.getBindingsToFreeVars())
            {
                content.Append("\n\n");
                content.Append(bindings.Key.Name).Append(" was bound to:\n");
                bindings.Value.PrettyPrint(content, format);
                content.switchToDefaultFormat();
            }

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\n\n\nThe quantifier body:\n\n");
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

            // highlight replaced, equal terms as well
            foreach (var eqTerm in bindingInfo.equalities.SelectMany(kv => kv.Value))
            {
                eqTerm.highlightTemporarily(format, PrintConstants.equalityColor, bindingInfo.matchContext[eqTerm.id]);
            }

            bindingInfo.fullPattern.highlightTemporarily(format, PrintConstants.patternMatchColor);
            foreach (var binding in bindingInfo.bindings)
            {
                var patternTerm = binding.Key;
                var term = binding.Value;
                var color = PrintConstants.blameColor;
                if (patternTerm.id == -1) color = PrintConstants.bindColor;

                patternTerm.highlightTemporarily(format, color, bindingInfo.patternMatchContext[patternTerm.id]);
                term.highlightTemporarily(format, color, bindingInfo.matchContext[term.id]);
            }
        }

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

        public ImportantInstantiation(Instantiation par) : base(par.bindingInfo)
        {
            Quant = par.Quant;
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