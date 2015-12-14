using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Z3AxiomProfiler.Rewriting;

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
            RewriteRule rewriteRule = format.getRewriteRule(this);
            bool isMultiline = false;
            List<int> breakIndices = new List<int>();
            int startLength = builder.Length;
            indentBuilder.Append(indentDiff);

            addFormatStringWithLinebreak(rewriteRule.prefix, builder, rewriteRule.prefixLineBreak, breakIndices);

            if (printChildren(format, rewriteRule))
            {
                for(var i = 0; i < Args.Length; i++)
                {
                    var t = Args[i];
                    // Note: DO NOT CHANGE ORDER (-> short circuit)
                    isMultiline = t.PrettyPrint(builder, indentBuilder, format.nextDepth())
                                  || isMultiline;
                    if (i < Args.Length - 1)
                    {
                        addFormatStringWithLinebreak(rewriteRule.infix, builder, rewriteRule.infixLineBreak, breakIndices);
                    }
                }
            }
            else if (showCutoffDots(format, rewriteRule))
            {
                builder.Append("...");
            }

            addFormatStringWithLinebreak(rewriteRule.suffix, builder, rewriteRule.suffixLineBreak, breakIndices);

            // check if line split is necessary
            if (!isMultiline && builder.Length - startLength <= format.maxWidth
                || format.maxWidth == 0 || format.maxDepth == 1)
            {
                indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                return false;
            }

            // split necessary
            addLinebreaks(builder, indentBuilder, breakIndices);
            return true;
        }

        private static void addLinebreaks(StringBuilder builder, StringBuilder indentBuilder, List<int> breakIndices)
        {
            var offset = 0;
            var oldLength = builder.Length;
            for (var i = 0; i < breakIndices.Count; i++)
            {
                if (i == breakIndices.Count - 1)
                {
                    indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                }
                builder.Insert(breakIndices[i] + offset, "\n" + indentBuilder);
                offset += builder.Length - oldLength;
                oldLength = builder.Length;
            }
        }

        private static void addFormatStringWithLinebreak(string add, StringBuilder builder,
            RewriteRule.lineBreakSetting lineBreak, List<int> breakIndices)
        {
            if (lineBreak == RewriteRule.lineBreakSetting.Before)
            {
                breakIndices.Add(builder.Length);
            }
            builder.Append(add);
            if (lineBreak == RewriteRule.lineBreakSetting.After)
            {
                breakIndices.Add(builder.Length);
            }
        }

        private bool printChildren(PrettyPrintFormat format, RewriteRule rule)
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

        private bool showCutoffDots(PrettyPrintFormat format, RewriteRule rule)
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

        public override string InfoPanelText(PrettyPrintFormat format)
        {
            StringBuilder s = new StringBuilder();
            s.Append(SummaryInfo());
            s.Append('\n');
            s.Append(PrettyPrint(format));
            return s.ToString();
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

        public override string SummaryInfo()
        {
            StringBuilder s = new StringBuilder();
            s.Append("Term Info:\n\n");
            s.Append("Identifier: ").Append(id).Append('\n');
            s.Append("Number of Children: ").Append(Args.Length).Append('\n');
            return s.ToString();
        }
    }
}