using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class Term : Common
    {
        public readonly string Name;
        public readonly string GenericType;
        public readonly Term[] Args;
        public int id = -1;
        public readonly int size;
        public Instantiation Responsible;
        public readonly List<Term> dependentTerms = new List<Term>();
        public readonly List<Instantiation> dependentInstantiationsBlame = new List<Instantiation>();
        public readonly List<Instantiation> dependentInstantiationsBind = new List<Instantiation>();
        private static string indentDiff = "¦ ";
        private static readonly Regex TypeRegex = new Regex(@"([\s\S]+)(<[\s\S]*>)");

        public Term(string name, Term[] args)
        {
            Match typeMatch = TypeRegex.Match(name);
            if (typeMatch.Success)
            {
                Name = string.Intern(typeMatch.Groups[1].Value);
                GenericType = string.Intern(typeMatch.Groups[2].Value);
            }
            else
            {
                Name = string.Intern(name);
                GenericType = "";
            }
            Args = args;
            foreach (Term t in Args)
            {
                size += t.size;
                t.dependentTerms.Add(this);
            }
            size += 1;
        }

        public Term(Term t)
        {
            Name = t.Name;
            Args = t.Args;
            Responsible = t.Responsible;
            id = t.id;
            size = t.size;
            GenericType = t.GenericType;
        }

        public void highlightTemporarily(PrettyPrintFormat format, Color color)
        {
            var tmp = format.getPrintRule(this).Clone();
            tmp.color = color;
            format.addTemporaryRule(id + "", tmp);
        }

        public void highlightTemporarily(PrettyPrintFormat format, Color color, List<List<Term>> pathConstraints)
        {
            var tmp = format.getPrintRule(this).Clone();
            tmp.color = color;
            tmp.historyConstraints.AddRange(pathConstraints);
            format.addTemporaryRule(id + "", tmp);
        }

        public bool isSubterm(Term subterm)
        {
            if (subterm.size > size) return false;

            var subtermsToCheck = new Queue<Term>();
            subtermsToCheck.Enqueue(this);
            while (subtermsToCheck.Count > 0)
            {
                var current = subtermsToCheck.Dequeue();

                if (current.size <= subterm.size)
                {
                    if (current.id == subterm.id) return true;
                    continue;
                }

                // term is larger, check subterms
                foreach (var arg in current.Args)
                {
                    subtermsToCheck.Enqueue(arg);
                }
            }
            return false;
        }

        public bool isDirectSubterm(Term subterm)
        {
            return subterm.size <= size && Args.Any(arg => arg == subterm);
        }

        public void printName(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.Append(Name);
            if (format.showType) content.Append(GenericType);
            if (format.showTermId) content.Append("[" + id + "]");
        }


        public bool PatternTermMatch(Term subPattern, out Dictionary<Term, Tuple<Term, List<List<Term>>>> bindingDict)
        {
            bindingDict = new Dictionary<Term, Tuple<Term, List<List<Term>>>>();
            var patternTermsToCheck = new Stack<Term>();
            patternTermsToCheck.Push(subPattern);
            var subtermsToCheck = new Stack<Term>();
            subtermsToCheck.Push(this);

            var history = new Stack<Term>();
            var visited = new HashSet<Term>();
            while (patternTermsToCheck.Count > 0)
            {
                var currentPatternTerm = patternTermsToCheck.Peek();
                var currentTerm = subtermsToCheck.Peek();
                if (visited.Contains(currentTerm))
                {
                    patternTermsToCheck.Pop();
                    subtermsToCheck.Pop();
                    if (currentPatternTerm.id != -1) history.Pop();
                    continue;
                }
                visited.Add(currentTerm);
                if (currentPatternTerm.id == -1)
                {
                    // this is a free variable
                    // todo: Bug!!
                    if (bindingDict.ContainsKey(currentTerm))
                    {
                        Debug.Assert(bindingDict[currentTerm].Item1 != currentPatternTerm);
                        bindingDict[currentTerm].Item2.Add(history.ToList());
                    }
                    else
                    {
                        var tuple = new Tuple<Term, List<List<Term>>>(currentTerm, new List<List<Term>>());
                        var historyConstraint = history.ToList();
                        historyConstraint.Reverse();
                        tuple.Item2.Add(historyConstraint);
                        bindingDict.Add(currentPatternTerm, tuple);
                    }
                    continue;
                }
                history.Push(currentTerm);

                if (currentTerm.Name != currentPatternTerm.Name ||
                    currentTerm.GenericType != currentPatternTerm.GenericType ||
                    currentTerm.Args.Length != currentPatternTerm.Args.Length)
                {
                    // pattern does not match -> abort
                    return false;
                }

                for (var i = 0; i < currentPatternTerm.Args.Length; i++)
                {
                    patternTermsToCheck.Push(currentPatternTerm.Args[i]);
                    subtermsToCheck.Push(currentTerm.Args[i]);
                }
            }

            return true;
        }

        public List<BindingInfo> matchPartiallyUsingBindings(Term pattern, ICollection<Term> boundTerms)
        {
            var bindingInfos = new List<BindingInfo>();
            var patternTraversal = new Queue<Term>();
            patternTraversal.Enqueue(pattern);
            while (patternTraversal.Count > 0)
            {
                var currentSubPattern = patternTraversal.Dequeue();
                var bindingInfo = matchUsingBindings(currentSubPattern, boundTerms);

                if (bindingInfo != null)
                {
                    bindingInfos.Add(bindingInfo);
                }

                // try children
                foreach (var arg in currentSubPattern.Args.Where(arg => arg.id != -1))
                {
                    patternTraversal.Enqueue(arg);
                }
            }

            return bindingInfos;
        }

        // match while verifying with known, bound terms
        private BindingInfo matchUsingBindings(Term patternTerm, ICollection<Term> boundTerms)
        {
            var matchinfo = new BindingInfo();

            var patternTermsToCheck = new Stack<Term>();
            patternTermsToCheck.Push(patternTerm);
            var subtermsToCheck = new Stack<Term>();
            subtermsToCheck.Push(this);

            var history = new Stack<Term>();
            var visited = new HashSet<Term>();
            while (patternTermsToCheck.Count > 0)
            {
                var currentPatternTerm = patternTermsToCheck.Peek();
                var currentTerm = subtermsToCheck.Peek();

                // backtrack
                if (visited.Contains(currentTerm))
                {
                    patternTermsToCheck.Pop();
                    subtermsToCheck.Pop();
                    if (currentPatternTerm.id != -1) history.Pop();
                    continue;
                }

                visited.Add(currentTerm);

                // check if free variable
                if (currentPatternTerm.id == -1)
                {
                    /*
                    if (boundTerms.All(term => term.id != currentTerm.id))
                    {
                        // this is not a valid binding
                        return null;
                    }
                    */

                    var historyConstraint = history.ToList();
                    historyConstraint.Reverse();

                    if (matchinfo.addBinding(currentPatternTerm, currentTerm))
                    {
                        matchinfo.addHistoryConstraint(currentTerm, historyConstraint);
                    }
                    else
                    {
                        return null;
                    }
                    continue;
                }

                history.Push(currentTerm);

                if (currentTerm.Name != currentPatternTerm.Name ||
                    currentTerm.GenericType != currentPatternTerm.GenericType ||
                    currentTerm.Args.Length != currentPatternTerm.Args.Length)
                {
                    // this does not match
                    return null;
                }

                for (var i = 0; i < currentPatternTerm.Args.Length; i++)
                {
                    patternTermsToCheck.Push(currentPatternTerm.Args[i]);
                    subtermsToCheck.Push(currentTerm.Args[i]);
                }
            }

            return matchinfo;
        }

        public void PrettyPrint(InfoPanelContent content, PrettyPrintFormat format)
        {
            PrettyPrint(content, new Stack<Color>(), format);
        }

        private bool PrettyPrint(InfoPanelContent content, Stack<Color> indentFormats, PrettyPrintFormat format)
        {
            var printRule = format.getPrintRule(this);
            var parentRule = format.GetParentPrintRule();
            var isMultiline = false;
            var breakIndices = new List<int>();
            var startLength = content.Length;
            var needsParenthesis = this.needsParenthesis(format, printRule, parentRule);

            content.switchFormat(InfoPanelContent.DefaultFont, printRule.color);

            // check for cutoff
            if (format.maxDepth == 1)
            {
                content.Append("...");
                return false;
            }

            if (printRule.indent) indentFormats.Push(printRule.color);
            if (needsParenthesis) content.Append('(');
            addPrefix(printRule, content, breakIndices);

            if (printChildren(format, printRule))
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    var t = Args[i];

                    // Note: DO NOT CHANGE ORDER (-> short circuit)
                    isMultiline = t.PrettyPrint(content, indentFormats, format.nextDepth(this, i))
                                  || isMultiline;

                    if (i < Args.Length - 1)
                    {
                        addInfix(printRule, content, breakIndices);
                    }
                }
            }

            addSuffix(printRule, content, breakIndices);
            if (needsParenthesis) content.Append(')');

            // are there any lines to break?
            var lineBreaks = linebreaksNecessary(content, format, isMultiline && (breakIndices.Count > 0), startLength);
            if (lineBreaks)
            {
                addLinebreaks(printRule, content, indentFormats, breakIndices);
            }
            else if (printRule.indent)
            {
                // just remove indent if necessary
                indentFormats.Pop();
            }

            return lineBreaks;
        }

        private bool needsParenthesis(PrettyPrintFormat format, PrintRule rule, PrintRule parentRule)
        {
            switch (rule.parentheses)
            {
                case PrintRule.ParenthesesSetting.Always:
                    return true;
                case PrintRule.ParenthesesSetting.Never:
                    return false;
                case PrintRule.ParenthesesSetting.Precedence:
                    if (format.history.Count == 0) return false;
                    if (parentRule.precedence < rule.precedence) return false;
                    if (!string.IsNullOrWhiteSpace(parentRule.prefix) &&
                        !string.IsNullOrWhiteSpace(parentRule.suffix))
                    { return false; }
                    if (!string.IsNullOrWhiteSpace(parentRule.prefix) &&
                        !string.IsNullOrWhiteSpace(parentRule.infix) &&
                        format.childIndex == 0)
                    { return false; }
                    if (!string.IsNullOrWhiteSpace(parentRule.infix) &&
                        !string.IsNullOrWhiteSpace(parentRule.suffix) &&
                        format.childIndex == format.history.Last().Args.Length - 1)
                    { return false; }
                    return format.history.Last().Name != Name || !rule.associative;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        private static bool linebreaksNecessary(InfoPanelContent content, PrettyPrintFormat format, bool isMultiline, int startLength)
        {
            if (format.maxWidth == 0) return false;
            return isMultiline || (content.Length - startLength > format.maxWidth);
        }

        private static void addLinebreaks(PrintRule rule, InfoPanelContent content,
            Stack<Color> indents, List<int> breakIndices)
        {
            var indentColors = indents.ToList();
            indentColors.Reverse();
            var offset = 0;
            var oldLength = content.Length;
            for (var i = 0; i < breakIndices.Count; i++)
            {
                if (rule.indent && i == breakIndices.Count - 1)
                {
                    indents.Pop();
                    indentColors.RemoveAt(indentColors.Count - 1);
                }

                // add the actual linebreak
                content.Insert(breakIndices[i] + offset, "\n");
                offset += content.Length - oldLength;
                oldLength = content.Length;

                // add the indents
                foreach (var color in indentColors)
                {
                    content.Insert(breakIndices[i] + offset, indentDiff, InfoPanelContent.DefaultFont, color);
                    offset += content.Length - oldLength;
                    oldLength = content.Length;
                }
            }
        }

        private static void addPrefix(PrintRule rule, InfoPanelContent content, ICollection<int> breakIndices)
        {
            content.Append(rule.prefix);
            if (!string.IsNullOrWhiteSpace(rule.prefix) &&
                rule.prefixLineBreak == PrintRule.LineBreakSetting.After)
            {
                breakIndices.Add(content.Length);
            }
        }

        private static void addInfix(PrintRule rule, InfoPanelContent content, ICollection<int> breakIndices)
        {
            content.switchFormat(InfoPanelContent.DefaultFont, rule.color);
            if (rule.infixLineBreak == PrintRule.LineBreakSetting.Before)
            {
                breakIndices.Add(content.Length);
            }
            content.Append(rule.infix);
            if (rule.infixLineBreak == PrintRule.LineBreakSetting.After)
            {
                breakIndices.Add(content.Length);
            }
        }

        private static void addSuffix(PrintRule rule, InfoPanelContent content, ICollection<int> breakIndices)
        {
            content.switchFormat(InfoPanelContent.DefaultFont, rule.color);
            if (!string.IsNullOrWhiteSpace(rule.suffix) &&
                rule.suffixLineBreak == PrintRule.LineBreakSetting.Before)
            {
                breakIndices.Add(content.Length);
            }
            content.Append(rule.suffix);
        }

        private bool printChildren(PrettyPrintFormat format, PrintRule rule)
        {
            if (Args.Length == 0)
            {
                return false;
            }
            return !format.rewritingEnabled || rule.printChildren;
        }

        public override string ToString()
        {
            return $"Term[{Name}] Identifier:{id}, #Children:{Args.Length}";
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            SummaryInfo(content);
            content.Append('\n');
            PrettyPrint(content, new Stack<Color>(), format);
        }

        public override bool HasChildren()
        {
            return Args.Length > 0 || dependentTerms.Count > 0;
        }

        public override IEnumerable<Common> Children()
        {
            foreach (var arg in Args)
            {
                yield return arg;
            }
            if (Responsible != null)
            {
                yield return new ForwardingNode("RESPONSIBLE INSTANTIATION", Responsible);
            }

            if (dependentTerms.Count > 0)
            {
                yield return Callback($"YIELDS TERMS [{dependentTerms.Count}]", () => dependentTerms);
            }

            if (dependentInstantiationsBlame.Count > 0)
            {
                yield return Callback($"YIELDS INSTANTIATIONS (BLAME) [{dependentInstantiationsBlame.Count}]", () => dependentInstantiationsBlame);
            }

            if (dependentInstantiationsBind.Count > 0)
            {
                yield return Callback($"YIELDS INSTANTIATIONS (BIND) [{dependentInstantiationsBind.Count}]", () => dependentInstantiationsBind);
            }
        }

        public override void SummaryInfo(InfoPanelContent content)
        {
            content.switchFormat(InfoPanelContent.SubtitleFont, Color.DarkCyan);
            content.Append("Term Info:\n");
            content.switchToDefaultFormat();
            content.Append("\nIdentifier: " + id).Append('\n');
            content.Append("Number of Children: " + Args.Length).Append('\n');
        }
    }
}