using System;
using System.Collections.Generic;
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
        public Instantiation Responsible;
        private readonly List<Term> dependentTerms = new List<Term>();
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
                t.dependentTerms.Add(this);
            }
        }

        public Term(Term t)
        {
            Name = t.Name;
            Args = t.Args;
            Responsible = t.Responsible;
            id = t.id;
        }


        private bool PrettyPrint(StringBuilder builder, StringBuilder indentBuilder, PrettyPrintFormat format)
        {
            var printRule = format.getPrintRule(this);
            var parentRule = format.getPrintRule(format.parentTerm);
            var isMultiline = false;
            var breakIndices = new List<int>();
            var startLength = builder.Length;
            var needsParenthesis = this.needsParenthesis(format, printRule, parentRule);

            if (printRule.indent) indentBuilder.Append(indentDiff);
            if (needsParenthesis) builder.Append('(');
            addPrefix(printRule, builder, breakIndices);

            if (printChildren(format, printRule))
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    var t = Args[i];

                    // Note: DO NOT CHANGE ORDER (-> short circuit)
                    isMultiline = t.PrettyPrint(builder, indentBuilder, format.nextDepth(this, i))
                                  || isMultiline;

                    if (i < Args.Length - 1)
                    {
                        addInfix(printRule, builder, breakIndices);
                    }
                }
            }
            else if (showCutoffDots(format, printRule))
            {
                builder.Append("...");
            }

            addSuffix(printRule, builder, breakIndices);
            if (needsParenthesis) builder.Append(')');

            // are there any lines to break?
            isMultiline = isMultiline && (breakIndices.Count > 0);
            if (!linebreaksNecessary(builder, format, isMultiline, startLength))
            {
                if (printRule.indent)
                {
                    indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                }
                return false;
            }

            // split necessary
            addLinebreaks(printRule, builder, indentBuilder, breakIndices);
            return true;
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
                    if (format.parentTerm == null) return false;
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
                        format.childIndex == format.parentTerm.Args.Length - 1)
                    { return false; }
                    return format.parentTerm.Name != Name || !rule.associative;
                default:
                    throw new ArgumentOutOfRangeException("Invalid enum value!");
            }
        }

        private static bool linebreaksNecessary(StringBuilder builder, PrettyPrintFormat format, bool isMultiline, int startLength)
        {
            if (format.maxWidth == 0) return false;
            return isMultiline || (builder.Length - startLength > format.maxWidth);
        }

        private static void addLinebreaks(PrintRule rule, StringBuilder builder,
            StringBuilder indentBuilder, List<int> breakIndices)
        {
            var offset = 0;
            var oldLength = builder.Length;
            for (var i = 0; i < breakIndices.Count; i++)
            {
                if (rule.indent && i == breakIndices.Count - 1)
                {
                    indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                }

                builder.Insert(breakIndices[i] + offset, "\n" + indentBuilder);
                offset += builder.Length - oldLength;
                oldLength = builder.Length;
            }
        }

        private static void addPrefix(PrintRule rule, StringBuilder builder, ICollection<int> breakIndices)
        {
            builder.Append(rule.prefix);
            if (!string.IsNullOrWhiteSpace(rule.prefix) &&
                rule.prefixLineBreak == PrintRule.LineBreakSetting.After)
            {
                breakIndices.Add(builder.Length);
            }
        }

        private static void addInfix(PrintRule rule, StringBuilder builder, ICollection<int> breakIndices)
        {
            if (rule.infixLineBreak == PrintRule.LineBreakSetting.Before)
            {
                breakIndices.Add(builder.Length);
            }
            builder.Append(rule.infix);
            if (rule.infixLineBreak == PrintRule.LineBreakSetting.After)
            {
                breakIndices.Add(builder.Length);
            }
        }

        private static void addSuffix(PrintRule rule, StringBuilder builder, ICollection<int> breakIndices)
        {
            if (!string.IsNullOrWhiteSpace(rule.suffix) &&
                rule.suffixLineBreak == PrintRule.LineBreakSetting.Before)
            {
                breakIndices.Add(builder.Length);
            }
            builder.Append(rule.suffix);
        }

        private bool printChildren(PrettyPrintFormat format, PrintRule rule)
        {
            if (Args.Length == 0)
            {
                return false;
            }
            var formatSpec = format.maxDepth == 0 || format.maxDepth > 1;
            if (!format.rewritingEnabled)
            {
                return formatSpec;
            }
            return formatSpec && rule.printChildren;
        }

        private bool showCutoffDots(PrettyPrintFormat format, PrintRule rule)
        {
            if (Args.Length == 0 || (format.rewritingEnabled && !rule.printChildren))
            {
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            return $"Term[{Name}] Identifier:{id}, #Children:{Args.Length}";
        }

        public string PrettyPrint(PrettyPrintFormat format)
        {
            StringBuilder builder = new StringBuilder();
            PrettyPrint(builder, new StringBuilder(), format);
            return builder.ToString();
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            SummaryInfo(content);
            content.Append('\n');
            content.Append(PrettyPrint(format));
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
            content.Append("Term Info:\n\n");
            content.Append("Identifier: " + id).Append('\n');
            content.Append("Number of Children: " + Args.Length).Append('\n');
        }
    }
}