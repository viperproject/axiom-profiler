using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
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
                t.PrettyPrint(content, new StringBuilder(), format);
                content.Append("\n\n");
            }
            content.Append("\n");

            content.switchToDefaultFormat();
            content.Append("Number of unique ones: " + getDistinctBlameTerms().Count);
            content.Append('\n');

            content.Append("Bound terms:\n\n");
            foreach (var t in Bindings)
            {
                t.SummaryInfo(content);
                content.Append("\n");
                t.PrettyPrint(content, new StringBuilder(), format);
                content.Append("\n\n");
            }

            var pattern = findMatchingPattern();
            if (pattern != null)
            {
                var tmp = format.getPrintRule(pattern).Clone();
                tmp.color = Color.Coral;
                format.addTemporaryRule(pattern.id + "", tmp);
            }

            content.switchToDefaultFormat();
            content.Append("The quantifier body:\n\n");
            Quant.BodyTerm.PrettyPrint(content, new StringBuilder(), format);
            content.Append("\n\n");

            format.restoreAllOriginalRules();

            if (dependentTerms.Count > 0)
            {
                content.switchToDefaultFormat();
                content.Append("The resulting term:\n\n");
                dependentTerms[dependentTerms.Count - 1].PrettyPrint(content, new StringBuilder(), format);
            }
        }

        public Dictionary<Term, List<List<Term>>> blameTermsToPathConstraints;
        public List<Term> getDistinctBlameTerms()
        {
            if (blameTermsToPathConstraints == null)
            {
                buildBlameTermPathConstraints();
            }

            return (from termConstraintPair in blameTermsToPathConstraints
                    where termConstraintPair.Value.Count == 0 select termConstraintPair.Key).ToList();
        }

        private void buildBlameTermPathConstraints()
        {
            blameTermsToPathConstraints = new Dictionary<Term, List<List<Term>>>();
            var blameTerms = new List<Term>(Responsible);
            blameTerms.Sort((term1, term2) => term2.size.CompareTo(term1.size));

            foreach (var blameTerm in blameTerms)
            {
                var pathConstraints = new List<List<Term>>();
                foreach (var blameTermEntry in blameTermsToPathConstraints
                    .Where(blameTermEntry => blameTermEntry.Key.isDirectSubterm(blameTerm)))
                {
                    // either add new list with constraint or copy the existing ones and add more terms to 
                    // the path constraints.
                    if (blameTermEntry.Value.Count == 0)
                    {
                        var newConstraint = new List<Term>();
                        newConstraint.Add(blameTermEntry.Key);
                        pathConstraints.Add(newConstraint);
                    }
                    else
                    {
                        foreach (var constraintCopy in blameTermEntry.Value
                            .Select(pathConstraint => new List<Term>(pathConstraint)))
                        {
                            constraintCopy.Add(blameTermEntry.Key);
                            pathConstraints.Add(constraintCopy);
                        }
                    }
                }

                // store for later use
                blameTermsToPathConstraints[blameTerm] = pathConstraints;
            }
        }

        public Term findMatchingPattern()
        {
            if(matchedPattern != null) return matchedPattern;
            return Quant.Patterns().FirstOrDefault(pattern => matchesBlameTerms(pattern));
        }


        // The dictionary stores the following information:
        // The key is a the free variable term.
        // The value is structured as follows:
        // It's a tuple with the first item being bound term.
        // The second item is a list of histories such that occurrences of bound terms in blamed terms,
        // that were actually bound in that position, can be distinguished from occurrences that were not bound.
        // A history is represented as a list of terms.
        // That's why the value type is Tuple<Term, List<List<Term>>>.
        public Term matchedPattern;
        public Dictionary<Term, Tuple<Term, List<List<Term>>>> freeVariableToBindingsAndPathConstraints;
        private bool matchesBlameTerms(Term pattern)
        {
            var blameTerms = getDistinctBlameTerms();

            // Number of distinct terms does not match.
            // (e.g. multipattern on single blame term or single pattern on multiple terms)
            if (pattern.Args.Length != blameTerms.Count) return false;

            freeVariableToBindingsAndPathConstraints = new Dictionary<Term, Tuple<Term, List<List<Term>>>>();

            foreach (var patternPermutation in allPermutations(pattern.Args.ToList()))
            {
                freeVariableToBindingsAndPathConstraints.Clear();

                var patternEnum = patternPermutation.GetEnumerator();
                var blameTermEnum = blameTerms.GetEnumerator();
                // this permutation does not match, continue
                var stop = false;

                while (patternEnum.MoveNext() && blameTermEnum.MoveNext() && !stop)
                {
                    Dictionary<Term, Tuple<Term, List<List<Term>>>> dict;
                    if (!blameTermEnum.Current.PatternTermMatch(patternEnum.Current, out dict))
                    {
                        stop = true;
                        break;
                    }

                    foreach (var keyValuePair in dict)
                    {
                        if (freeVariableToBindingsAndPathConstraints.ContainsKey(keyValuePair.Key))
                        {
                            // check if the thing bound to the free variable is consistent with whats at the current position.
                            if (freeVariableToBindingsAndPathConstraints[keyValuePair.Key].Item1 == keyValuePair.Value.Item1)
                            {
                                // consistent, merge history constraints (e.g. add all histories)
                                freeVariableToBindingsAndPathConstraints[keyValuePair.Key].Item2.AddRange(keyValuePair.Value.Item2);
                                continue;
                            }
                            // inconsistent
                            stop = true;
                            break;
                        }
                        freeVariableToBindingsAndPathConstraints.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }

                // todo: disregard multiple possible matches for now.
                if (stop) continue;
                matchedPattern = pattern;
                return true;
            }

            freeVariableToBindingsAndPathConstraints = null;
            return false;
        }

        private IEnumerable<List<Term>> allPermutations(List<Term> originalList)
        {
            var i = 0;
            var arr = originalList.Select(term => new Tuple<int, Term>(i++, term))
                                                  .OrderBy(elem => elem.Item1).ToArray();
            yield return arr.Select(item => item.Item2).ToList();

            if (arr.Length <= 1) yield break;
            var j = arr.Length - 1;

            while (true)
            {
                // find longest increasing tail
                while (j > 0 && arr[j - 1].Item1 > arr[j].Item1) { j--; }
                if (j == 0) break;

                // elem to be swapped
                var swapIdx = j - 1;

                // find smalles elem in tail bigger than that at swapIdx
                var swapBiggerIdx = arr.Length - 1;
                while (arr[swapIdx].Item1 > arr[swapBiggerIdx].Item1) swapBiggerIdx++;

                // actually swap
                swap(arr, swapIdx, swapBiggerIdx);

                // reverse the tail
                swapIdx = arr.Length - 1;
                while (j < swapIdx)
                {
                    swap(arr, j, swapIdx);
                    j--;
                    swapIdx++;
                }
                yield return arr.Select(item => item.Item2).ToList();
            }
        }

        private static void swap(Tuple<int, Term>[] arr, int i, int j)
        {
            var tmp = arr[i];
            arr[i] = arr[j];
            arr[j] = tmp;
        }


        public override void SummaryInfo(InfoPanelContent content)
        {
            content.switchFormat(InfoPanelContent.TitleFont, Color.DarkRed);
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