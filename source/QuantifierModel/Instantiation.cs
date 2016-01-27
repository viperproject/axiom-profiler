using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class Instantiation : Common
    {
        public Quantifier Quant;
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

        public void CopyTo(Instantiation inst)
        {
            inst.Quant = Quant;
            inst.Bindings = (Term[])Bindings.Clone();
            inst.Responsible = (Term[])Responsible.Clone();
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
            SummaryInfo(content);
            content.Append("Blamed terms:\n\n");

            foreach (var t in Responsible)
            {
                t.SummaryInfo(content);
                content.Append("\n");
                content.Append(t.PrettyPrint(format));
                content.Append("\n\n");
            }
            content.Append("\n");

            content.Append("Bound terms:\n\n");
            foreach (var t in Bindings)
            {
                t.SummaryInfo(content);
                content.Append("\n");
                content.Append(t.PrettyPrint(format));
                content.Append("\n\n");
            }

            content.Append("The quantifier body:\n\n");
            content.Append(Quant.BodyTerm.PrettyPrint(format));
            content.Append("\n\n");

            if (dependentTerms.Count > 0)
            {
                content.Append("The resulting term:\n\n");
                content.Append(dependentTerms[dependentTerms.Count - 1].PrettyPrint(format));
            }
        }

        private void findPatternMatch()
        {
            var bindings = new Dictionary<Term, string>();
            foreach (var pattern in Quant.Patterns())
            {
                foreach (var blamed in Responsible)
                {
                    if (matchBlameTermToPattern(pattern, blamed, bindings))
                    {
                        Console.WriteLine("Matched blameterm <-> pattern: ");
                        foreach (var binding in bindings)
                        {
                            Console.WriteLine(binding.Key + " binds to " + binding.Value);
                        }
                        bindings.Clear();
                    }
                }
            }
        }

        public bool matchBlameTermToPattern(Term pattern, Term blamed, Dictionary<Term, string> bindingDictionary)
        {
            var todo = new Queue<Term>();
            foreach (var term in pattern.Args)
            {
                todo.Enqueue(term);
            }


            while (todo.Count > 0)
            {
                var currentSubPattern = todo.Dequeue();

                if (checkTermMatch(currentSubPattern, blamed, bindingDictionary))
                {
                    return true;
                }

                // reset bindings, check the next subpattern
                bindingDictionary.Clear();
            }
            return false;
        }

        private static readonly Regex freeVarRegex = new Regex(@"#\d+");
        private bool checkTermMatch(Term pattern, Term blamed, Dictionary<Term, string> bindingDictionary)
        {
            var patternTerms = new Queue<Term>();
            patternTerms.Enqueue(pattern);

            var blameTerms = new Queue<Term>();
            blameTerms.Enqueue(blamed);

            while (patternTerms.Count > 0 && blameTerms.Count > 0)
            {
                var currPatternChild = patternTerms.Dequeue();
                var currBlameChild = blameTerms.Dequeue();

                if (freeVarRegex.IsMatch(currPatternChild.Name))
                {
                    // a free variable is bound
                    bindingDictionary.Add(currBlameChild, currPatternChild.Name);
                    continue;
                }

                if (currPatternChild.Name != currBlameChild.Name)
                {
                    // does not match -> abort
                    return false;
                }

                // size mismatch -> abort
                if (patternTerms.Count != blameTerms.Count) return false;

                // add terms of next level
                foreach (var term in currPatternChild.Args)
                {
                    patternTerms.Enqueue(term);
                }
                foreach (var term in currBlameChild.Args)
                {
                    blameTerms.Enqueue(term);
                }
            }
            return true;
        }

        public override void SummaryInfo(InfoPanelContent content)
        {
            content.Append("Instantiation ").Append('@').Append(LineNo + ":\n\n");
            content.Append(Quant.PrintName).Append('\n');
            content.Append("Depth: " + depth).Append('\n');
            content.Append("Cost: ").Append(Cost.ToString("F")).Append("\n\n");
        }

        public override IEnumerable<Common> Children()
        {
            if (Responsible != null)
            {
                List<Term> sortedResponsibleList = new List<Term>(Responsible);
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
        public List<ImportantInstantiation> ResponsibleInsts = new List<ImportantInstantiation>();

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
                yield return Callback("BLAME", delegate () { return Responsible; });
            }
            if (Bindings.Length > 0)
            {
                yield return Callback("BIND", delegate () { return Bindings; });
            }
        }

        public override Color ForeColor()
        {
            if (Quant.Weight == 0)
            {
                return Color.DarkSeaGreen;
            }
            return base.ForeColor();
        }
    }
}