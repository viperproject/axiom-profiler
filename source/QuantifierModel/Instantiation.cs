using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        public readonly List<Tuple<Term, Term>> equalityInformation = new List<Tuple<Term, Term>>();

        private BindingInfo bindingInfo;

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
            processPattern();
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
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkMagenta);
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
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkMagenta);
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

            if (dependentTerms.Count <= 0) return;

            content.switchToDefaultFormat();
            content.Append("The resulting term:\n\n");
            dependentTerms[dependentTerms.Count - 1].PrettyPrint(content, format);
        }

        public void printNoMatchdisclaimer(InfoPanelContent content)
        {
            content.switchFormat(InfoPanelContent.ItalicFont, Color.Red);
            content.Append(isAmbiguous
                ? "Pattern match detection failed due to ambiguosity. Multiple patterns matched.\n"
                : "No pattern match found. Possible reasons include (hidden) equalities\nand / or automatic term simplification.\n");
            content.switchToDefaultFormat();
        }

        private void FancyInfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            SummaryInfo(content);
            content.Append("Highlighted terms are ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.Coral);
            content.Append("blamed or matched");
            content.switchToDefaultFormat();
            content.Append(" and ");
            content.switchFormat(InfoPanelContent.DefaultFont, Color.DeepSkyBlue);
            content.Append("bound");
            content.switchToDefaultFormat();
            content.Append(", respectively.\n\n");

            bindingInfo.fullPattern.highlightTemporarily(format, Color.Coral);
            tempHighlightBlameBindTerms(format);

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("Blamed Terms:\n\n");

            foreach (var t in bindingInfo.getDistinctBlameTerms())
            {
                content.Append("\n");
                t.PrettyPrint(content, format);
                content.Append("\n\n");
            }

            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("Binding information:\n\n");
            content.switchToDefaultFormat();

            foreach (var bindings in bindingInfo.getBindingsToFreeVars())
            {
                content.Append(bindings.Key.Name).Append(" was bound to ");
                bindings.Value.printName(content, format);
                content.Append('\n');
            }

            if (bindingInfo.equalities.Count > 0)
            {
                content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
                content.Append("\n\nRelevant equalities:\n\n");
                content.switchToDefaultFormat();

                foreach (var equality in bindingInfo.equalities)
                {
                    bindingInfo.bindings[equality.Key].printName(content, format);
                    foreach (var term in equality.Value)
                    {
                        content.Append(" = ");
                        term.printName(content, format);
                    }
                    content.Append('\n');
                }
            }

            content.Append("\n\nThe quantifier body:\n\n");
            Quant.BodyTerm.PrettyPrint(content, format);
            content.Append("\n\n");
            format.restoreAllOriginalRules();

            if (dependentTerms.Count <= 0) return;

            content.switchToDefaultFormat();
            content.Append("The resulting term:\n\n");
            dependentTerms[dependentTerms.Count - 1].PrettyPrint(content, format);
        }

        public void tempHighlightBlameBindTerms(PrettyPrintFormat format)
        {
            if (bindingInfo == null) return;

            foreach (var context in bindingInfo.matchContext)
            {
                var term = context.Key;
                var pathConstraints = context.Value;
                var color = Bindings.Any(bnd => bnd.id == term.id) ? Color.DeepSkyBlue : Color.Coral;
                term.highlightTemporarily(format, color, pathConstraints);
            }
        }

        private Dictionary<Term, List<List<Term>>> _blameTermsToPathConstraints;
        public Dictionary<Term, List<List<Term>>> blameTermsToPathConstraints
        {
            get
            {
                if (!didPatternMatch)
                {
                    processPattern();
                }
                return _blameTermsToPathConstraints;
            }
        }

        public List<Term> getDistinctBlameTerms()
        {
            if (!didPatternMatch)
            {
                buildBlameTermPathConstraints();
            }
            return (from termConstraintPair in _blameTermsToPathConstraints
                    where termConstraintPair.Value.Count == 0
                    select termConstraintPair.Key).ToList();
        }

        private void buildBlameTermPathConstraints()
        {
            _blameTermsToPathConstraints = new Dictionary<Term, List<List<Term>>>();
            var blameTerms = new List<Term>(Responsible);
            blameTerms.Sort((term1, term2) => term2.size.CompareTo(term1.size));

            foreach (var blameTerm in blameTerms)
            {
                var pathConstraints = new List<List<Term>>();
                foreach (var blameTermEntry in _blameTermsToPathConstraints
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
                _blameTermsToPathConstraints[blameTerm] = pathConstraints;
            }
        }

        private void processPattern()
        {
            if (didPatternMatch) return;
            var bindingCandidates = findAllMatches();
            if (bindingCandidates.Count == 1) bindingInfo = bindingCandidates[0];
            else if(bindingCandidates.Count > 1)
            {
                isAmbiguous = true;
            }
            didPatternMatch = true;
            return;
            

            var bindingInfos = new List<Dictionary<Term, Tuple<Term, List<List<Term>>>>>();
            foreach (var pattern in Quant.Patterns())
            {
                Dictionary<Term, Tuple<Term, List<List<Term>>>> currBind;
                List<Tuple<Term, Term>> currEqInfo;

                if (!matchesBlameTerms(pattern, out currBind, out currEqInfo)) continue;

                _matchedPattern = pattern;
                equalityInformation.Clear();
                equalityInformation.AddRange(currEqInfo);
                bindingInfos.Add(currBind);
            }
            if (bindingInfos.Count > 1)
            {
                equalityInformation.Clear();
                _matchedPattern = null;
                isAmbiguous = true;
            }
            else if (bindingInfos.Count == 1)
            {
                _freeVariableToBindingsAndPathConstraints = bindingInfos[0];
            }
            didPatternMatch = true;
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
            return plausibleMatches;
        }

        private List<BindingInfo> parallelDescent(Term pattern)
        {
            var plausibleMatches = new List<BindingInfo>(); // empty list to collect all possible matches
            plausibleMatches.Add(new BindingInfo(pattern, Responsible, Bindings)); // empty binding info
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
        private Term _matchedPattern;
        private bool didPatternMatch;
        public bool isAmbiguous;
        public Term matchedPattern
        {
            get
            {
                if (!didPatternMatch)
                {
                    processPattern();
                }
                return _matchedPattern;
            }
        }


        private Dictionary<Term, Tuple<Term, List<List<Term>>>> _freeVariableToBindingsAndPathConstraints;
        public Dictionary<Term, Tuple<Term, List<List<Term>>>> freeVariableToBindingsAndPathConstraints
        {
            get
            {
                if (!didPatternMatch)
                {
                    processPattern();
                }
                return _freeVariableToBindingsAndPathConstraints;
            }
        }
        private bool matchesBlameTerms(Term pattern, out Dictionary<Term, Tuple<Term, List<List<Term>>>> bindingInfo,
            out List<Tuple<Term, Term>> equaltiyInfo)
        {
            equaltiyInfo = new List<Tuple<Term, Term>>();
            var blameTerms = getDistinctBlameTerms();
            bindingInfo = new Dictionary<Term, Tuple<Term, List<List<Term>>>>();
            // Number of distinct terms does not match.
            // (e.g. multipattern on single blame term or single pattern on multiple terms)
            if (pattern.Args.Length != blameTerms.Count) return false;



            foreach (var patternPermutation in allPermutations(pattern.Args.ToList()))
            {
                equaltiyInfo.Clear();
                bindingInfo.Clear();
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
                        if (bindingInfo.ContainsKey(keyValuePair.Key))
                        {
                            var term1 = bindingInfo[keyValuePair.Key].Item1;
                            var term2 = keyValuePair.Value.Item1;
                            // check if the thing bound to the free variable is consistent with whats at the current position.
                            if (term1.id == term2.id)
                            {
                                // consistent, merge history constraints (e.g. add all histories)
                                bindingInfo[keyValuePair.Key].Item2
                                    .AddRange(keyValuePair.Value.Item2);
                                continue;
                            }

                            if (equalityLookUp(term1, term2))
                            {
                                equaltiyInfo.Add(new Tuple<Term, Term>(term1, term2));
                                continue;
                            }

                            // inconsistent
                            stop = true;
                            break;
                        }
                        bindingInfo.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }

                if (stop) continue;
                return true;
            }
            bindingInfo.Clear();
            equaltiyInfo.Clear();
            return false;
        }

        private static bool equalityLookUp(Term term1, Term term2)
        {
            Term searchTerm;
            Term lookUpTerm;
            if (term1.dependentTerms.Count < term2.dependentTerms.Count)
            {
                searchTerm = term1;
                lookUpTerm = term2;
            }
            else
            {
                searchTerm = term2;
                lookUpTerm = term1;
            }

            return searchTerm.dependentTerms
                .Where(dependentTerm => dependentTerm.Name == "=")
                .Any(dependentTerm => dependentTerm.Args.Any(term => term.id == lookUpTerm.id));
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
                yield return Callback("BLAME", () => Responsible);
            }
            if (Bindings.Length > 0)
            {
                yield return Callback("BIND", () => Bindings);
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