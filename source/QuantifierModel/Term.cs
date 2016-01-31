using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
                    if (current == subterm) return true;
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
                    if (bindingDict.ContainsKey(currentTerm))
                    {
                        Debug.Assert(bindingDict[currentTerm].Item1 != currentPatternTerm);
                        bindingDict[currentTerm].Item2.Add(history.ToList());
                    }
                    else
                    {
                        var tuple = new Tuple<Term, List<List<Term>>>(currentTerm, new List<List<Term>>());
                        tuple.Item2.Add(history.ToList());
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

        public bool PrettyPrint(InfoPanelContent content, StringBuilder indentBuilder, PrettyPrintFormat format)
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

            if (printRule.indent) indentBuilder.Append(indentDiff);
            if (needsParenthesis) content.Append('(');
            addPrefix(printRule, content, breakIndices);

            if (printChildren(format, printRule))
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    var t = Args[i];

                    // Note: DO NOT CHANGE ORDER (-> short circuit)
                    isMultiline = t.PrettyPrint(content, indentBuilder, format.nextDepth(this, i))
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
                addLinebreaks(printRule, content, indentBuilder, breakIndices);
            }
            else if (printRule.indent)
            {
                // just remove indent if necessary
                indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
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
            StringBuilder indentBuilder, List<int> breakIndices)
        {
            var offset = 0;
            var oldLength = content.Length;
            for (var i = 0; i < breakIndices.Count; i++)
            {
                if (rule.indent && i == breakIndices.Count - 1)
                {
                    indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                }

                content.Insert(breakIndices[i] + offset, "\n" + indentBuilder);
                offset += content.Length - oldLength;
                oldLength = content.Length;
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
            PrettyPrint(content, new StringBuilder(), format);
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

        public bool matchesPattern(Term pattern, out List<List<Term>> historyConstraints)
        {
            historyConstraints = new List<List<Term>>();
            var possibleStartPoints = new List<Tuple<Term, List<Term>>>();
            var todo = new Stack<Term>();
            todo.Push(pattern);

            // find entry point, if this term is a subterm of the pattern
            var currentPath = new List<Term>();
            while (todo.Count > 0)
            {
                var current = todo.Pop();
                if (current.Name != "Pattern")
                {
                    // we do not want the pattern term in the history constraint.
                    currentPath.Add(current);
                }


                if (current.Args.Length > 0 && current.Name != Name)
                {
                    foreach (var child in current.Args)
                    {
                        todo.Push(child);
                    }
                    continue;
                }

                // possible match candidate
                if (current.Name == Name)
                {
                    possibleStartPoints.Add(new Tuple<Term, List<Term>>(current, new List<Term>(currentPath)));
                }

                // backtrack
                currentPath.RemoveAt(currentPath.Count - 1);
            }

            // check if stuff below matches as well.
            foreach (var startPoint in possibleStartPoints)
            {
                var patternTraversalStack = new Stack<Term>();
                patternTraversalStack.Push(startPoint.Item1);
                var thisTraversalStack = new Stack<Term>();
                thisTraversalStack.Push(this);
                var match = true;

                while (patternTraversalStack.Count > 0)
                {
                    var current = patternTraversalStack.Pop();
                    var thisCurrent = thisTraversalStack.Pop();

                    if (current.id == -1)
                    {
                        // free variable
                        continue;
                    }

                    if (current.Name != thisCurrent.Name ||
                        current.GenericType != thisCurrent.GenericType ||
                        current.Args.Length != thisCurrent.Args.Length)
                    {
                        // does not match!
                        match = false;
                        break;
                    }

                    for (var i = 0; i < current.Args.Length; i++)
                    {
                        patternTraversalStack.Push(current.Args[i]);
                        thisTraversalStack.Push(thisCurrent.Args[i]);
                    }
                }

                if (match)
                {
                    // match given the recorded history
                    historyConstraints.Add(startPoint.Item2);
                }
            }

            return historyConstraints.Count > 0;
        }
    }
}