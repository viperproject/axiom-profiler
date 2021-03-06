using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AxiomProfiler.PrettyPrinting;
using System;

namespace AxiomProfiler.QuantifierModel
{
    public class Instantiation : Common
    {
        private static readonly Term[] emptyTerms = new Term[0];

        private class DirectEqualityColloctor : EqualityExplanationVisitor<IEnumerable<Term>, object>
        {
            public static readonly DirectEqualityColloctor singleton = new DirectEqualityColloctor();

            public override IEnumerable<Term> Congruence(CongruenceExplanation target, object arg)
            {
                return target.sourceArgumentEqualities.SelectMany(ee => visit(ee, arg));
            }

            public override IEnumerable<Term> Direct(DirectEqualityExplanation target, object arg) { yield return target.equality; }

            public override IEnumerable<Term> RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg) { return Enumerable.Empty<Term>(); }

            public override IEnumerable<Term> Theory(TheoryEqualityExplanation target, object arg) { return Enumerable.Empty<Term>(); }

            public override IEnumerable<Term> Transitive(TransitiveEqualityExplanation target, object arg)
            {
                return target.equalities.SelectMany(ee => visit(ee, arg));
            }
        }

        public bool flag = false;
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
                    var requiredEqualityExplanations = bindingInfo.EqualityExplanations.Where(expl => !bindingInfo.BoundTerms.Contains(expl.target));
                    var tmp = bindingInfo.TopLevelTerms
                        .Concat(requiredEqualityExplanations.Select(expl => expl.target))
                        .Concat(requiredEqualityExplanations.SelectMany(ee => DirectEqualityColloctor.singleton.visit(ee, null)))
                        .Concat(bindingInfo.explicitlyBlamedTerms);
		    int len = 0;
		    foreach (var k in tmp) {
			    len++;
		    }
		    _Responsible = new Term[len];
		    len = 0;
		    foreach (var k in tmp) {
			    _Responsible[len++] = k;
		    }
                }
                return _Responsible;
            }
        }
        public readonly string InstantiationMethod;

        public Term[] Bindings
        {
            get
            {
                return bindingInfo.BoundTerms;
            }
        }

        public Instantiation(BindingInfo bindingInfo, string method)
        {
            this.bindingInfo = bindingInfo;
            InstantiationMethod = method;
        }

        public Instantiation Copy()
        {
            return new Instantiation(bindingInfo, InstantiationMethod)
            {
                Quant = Quant,
                concreteBody = concreteBody
            };
        }

        public Instantiation CopyForBindingInfoModification()
        {
            var copy = new Instantiation(bindingInfo.Clone(), InstantiationMethod)
            {
                Quant = Quant,
                concreteBody = concreteBody
            };
            foreach (var dependentTerm in dependentTerms)
            {
                copy.dependentTerms.Add(dependentTerm);
            }
            return copy;
        }

        public int Depth
        {
            get
            {
                if (flag)
                {
#if DEBUG
                    throw new Exception("Found cycle in causality graph!");
#else
                    return 0;
#endif
                }

                if (depth != 0)
                {
                    return depth;
                }

                flag = true;
                int max = (from t in Responsible where t.Responsible != null select t.Responsible.Depth)
                    .Concat(new[] { 0 }).Max();
                flag = false;
                depth = max + 1;
                return depth;
            }
        }

        public int WDepth
        {
            get
            {
                if (flag)
                {
#if DEBUG
                    throw new Exception("Found cycle in causality graph!");
#else
                    return 0;
#endif
                }

                if (wdepth == -1)
                {
                    flag = true;
                    int max = (from t in Responsible where t.Responsible != null select t.Responsible.WDepth)
                        .Concat(new[] { 0 }).Max();
                    flag = false;
                    wdepth = max + Quant.Weight;
                }
                return wdepth;
            }
        }

        public override string ToString()
        {
            string result = $"Instantiation[{Quant.PrintName}] @line: {LineNo}, Depth: {Depth}, Longest Subpath Length: {DeepestSubpathDepth}, Cost: {Cost}";
            return result;
        }

        public void printNoMatchdisclaimer(InfoPanelContent content)
        {
            content.switchFormat(PrintConstants.ItalicFont, PrintConstants.warningTextColor);
            content.Append("No pattern match found. Possible reasons include (hidden) equalities\nand / or automatic term simplification.\n");
            content.switchToDefaultFormat();
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            SummaryInfo(content);
            content.Append("Highlighted terms are ");
            if (bindingInfo.IsPatternMatch())
            {
                content.switchFormat(PrintConstants.DefaultFont, PrintConstants.patternMatchColor);
                content.Append("matched");
                content.switchToDefaultFormat();
                content.Append(" or ");
                content.switchFormat(PrintConstants.DefaultFont, PrintConstants.equalityColor);
                content.Append(PrintConstants.LargeTextMode ? "matched using\nequality" : "matched using equality");
                content.switchToDefaultFormat();
                content.Append(" or ");
            }
            content.switchFormat(PrintConstants.DefaultFont, PrintConstants.blameColor);
            content.Append("blamed");
            content.switchToDefaultFormat();
            content.Append(" or ");
            content.switchFormat(PrintConstants.DefaultFont, PrintConstants.bindColor);
            content.Append("bound");
            content.switchToDefaultFormat();
            content.Append(".\n\n");

            tempHighlightBlameBindTerms(format);

            if (!bindingInfo.IsPatternMatch())
            {
                content.switchFormat(PrintConstants.BoldFont, PrintConstants.instantiationTitleColor);
                if (InstantiationMethod == "theory-solving")
                {
                    content.Append($"Instantiated by the {Quant.Namespace} theory solver.\n\n");
                }
                else
                {
                    content.Append($"Instantiated using {InstantiationMethod}.\n\n");
                }
                content.switchToDefaultFormat();

                if (bindingInfo.explicitlyBlamedTerms.Any())
                {
                    content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                    content.Append("Blamed Terms:\n");
                    content.switchToDefaultFormat();
                }

                var termNumbering = 1;

                foreach (var t in bindingInfo.explicitlyBlamedTerms)
                {
                    if (!format.termNumbers.TryGetValue(t, out var termNumber))
                    {
                        termNumber = termNumbering;
                        ++termNumbering;
                        format.termNumbers[t] = termNumber;
                    }
                    var numberingString = $"({termNumber}) ";
                    content.Append($"\n{numberingString}");
                    t.PrettyPrint(content, format, numberingString.Length);
                    content.switchToDefaultFormat();
                    content.Append("\n\n");
                }
            }

            if (bindingInfo.IsPatternMatch())
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("Blamed Terms:\n\n");
                content.switchToDefaultFormat();

                var termNumbering = 1;

                var blameTerms = bindingInfo.getDistinctBlameTerms();
                var distinctBlameTerms = blameTerms.Where(req => bindingInfo.TopLevelTerms.Contains(req) ||
                    (!bindingInfo.equalities.SelectMany(eq => eq.Value).Any(t => t.Item2.id == req.id) &&
                    !bindingInfo.equalities.Keys.Any(k => bindingInfo.bindings[k].Item2 == req)));

                foreach (var t in distinctBlameTerms)
                {
                    if (!format.termNumbers.TryGetValue(t, out var termNumber))
                    {
                        termNumber = termNumbering;
                        ++termNumbering;
                        format.termNumbers[t] = termNumber;
                    }
                    var numberingString = $"({termNumber}) ";
                    content.Append($"\n{numberingString}");
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
                    foreach (var equality in bindingInfo.equalities)
                    {
                        var effectiveTerm = bindingInfo.bindings[equality.Key].Item2;
                        foreach (var term in equality.Value.Select(t => t.Item2).Distinct(Term.semanticTermComparer))
                        {
                            EqualityExplanation explanation;
#if !DEBUG
                        try
                        {
#endif
                            explanation = bindingInfo.EqualityExplanations.First(ee => ee.source.id == term.id && ee.target.id == effectiveTerm.id);
#if !DEBUG
                        }
                        catch (Exception)
                        {
                            explanation = new TransitiveEqualityExplanation(term, effectiveTerm, new EqualityExplanation[0]);
                        }
#endif
                            if (!format.equalityNumbers.TryGetValue(explanation, out var termNumber))
                            {
                                termNumber = termNumbering;
                                ++termNumbering;
                                format.equalityNumbers[explanation] = termNumber;
                            }

                            if (format.ShowEqualityExplanations)
                            {
                                explanation.PrettyPrint(content, format, termNumber);
                            }
                            else
                            {
                                var numberingString = $"({termNumber}) ";
                                content.switchToDefaultFormat();
                                content.Append(numberingString);
                                var indentString = $"�{String.Join("", Enumerable.Repeat(" ", numberingString.Length - 1))}";
                                term.PrettyPrint(content, format, numberingString.Length);
                                content.switchToDefaultFormat();
                                content.Append($"\n{indentString}= (explanation omitted)\n{indentString}");
                                effectiveTerm.PrettyPrint(content, format, numberingString.Length);
                            }
                            content.Append("\n\n");
                        }
                    }
                    format.printContextSensitive = true;

                    bindingInfo.PrintEqualitySubstitution(content, format);
                }
            }

            if (Bindings.Any())
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("Binding information:");
                content.switchToDefaultFormat();

                foreach (var bindings in bindingInfo.getBindingsToFreeVars())
                {
                    content.Append("\n\n");
                    content.Append(bindings.Key.PrettyName).Append(" was bound to:\n");
                    bindings.Value.PrettyPrint(content, format);
                    content.switchToDefaultFormat();
                }
            }

            if (Quant.BodyTerm != null)
            {
                content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
                content.Append("\n\n\nThe quantifier body:\n\n");
                content.switchToDefaultFormat();
                Quant.BodyTerm.PrettyPrint(content, format);
                content.Append("\n\n");
            }
            format.restoreAllOriginalRules();

            content.switchToDefaultFormat();
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("The resulting term:\n\n");
            content.switchToDefaultFormat();
            concreteBody.PrettyPrint(content, format);

            format.restoreAllOriginalRules();
        }

        public void tempHighlightBlameBindTerms(PrettyPrintFormat format)
        {
            if (!bindingInfo.IsPatternMatch())
            {
                foreach (var binding in bindingInfo.bindings)
                {
                    var patternTerm = binding.Key;
                    var term = binding.Value;
                    var color = PrintConstants.bindColor;

                    patternTerm.highlightTemporarily(format, color, bindingInfo.patternMatchContext[patternTerm.id]);
                    term.Item2.highlightTemporarily(format, color, term.Item1);
                }
                foreach (var blameTerm in bindingInfo.explicitlyBlamedTerms)
                {
                    blameTerm.highlightTemporarily(format, PrintConstants.blameColor);
                }
                return;
            }

            // highlight replaced, equal terms as well
            foreach (var eqTerm in bindingInfo.equalities.SelectMany(kv => kv.Value))
            {
                eqTerm.Item2.highlightTemporarily(format, PrintConstants.equalityColor, eqTerm.Item1);
            }

            bindingInfo.fullPattern?.highlightTemporarily(format, PrintConstants.patternMatchColor);
            foreach (var binding in bindingInfo.bindings)
            {
                var patternTerm = binding.Key;
                var term = binding.Value;
                var color = PrintConstants.blameColor;
                if (patternTerm.id == -1) color = PrintConstants.bindColor;

                patternTerm.highlightTemporarily(format, color, bindingInfo.patternMatchContext[patternTerm.id]);
                term.Item2.highlightTemporarily(format, color, term.Item1);
            }
        }

        public override void SummaryInfo(InfoPanelContent content)
        {
            content.switchFormat(PrintConstants.TitleFont, PrintConstants.instantiationTitleColor);
            content.Append("Instantiation ").Append('@').Append(LineNo + ":\n");
            content.switchToDefaultFormat();

            content.Append('\n').Append(Quant.PrintName).Append('\n');
            content.Append("Depth: " + depth).Append('\n');
            content.Append($"Longest Subpath Length: {DeepestSubpathDepth.ToString("F")}\n");
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

        public ImportantInstantiation(Instantiation par) : base(par.bindingInfo, par.InstantiationMethod)
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
