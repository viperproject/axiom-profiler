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
            RewriteRule rewriteRule;
            var rewrite = format.getRewriteRule(this, out rewriteRule);
            bool isMultiline = false;
            int[] breakIndices;
            int breakIndex = 0;
            int startLength = builder.Length;
            var indent = true;

            if (rewrite)
            {
                indent = !string.IsNullOrWhiteSpace(rewriteRule.prefix);
                breakIndices = new int[Args.Length + (indent ? 1 : 0)];
            }
            else
            {
                breakIndices = new int[Args.Length + 1];
            }

            if (rewrite)
            {
                builder.Append(rewriteRule.prefix);
            }
            else
            {
                addStandardHeader(builder, format);
            }

            // do not record break index if no break is necessary (because prefix is omitted
            if (indent)
            {
                indentBuilder.Append(indentDiff);
                breakIndices[breakIndex] = builder.Length;
                breakIndex++;
            }
            var printChildren = (!rewrite && (format.maxDepth == 0 || format.maxDepth > 1)) ||
                                (rewrite && rewriteRule.printChildren);

            if (printChildren)
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    // Note: DO NOT CHANGE ORDER (-> short circuit)
                    isMultiline = Args[i].PrettyPrint(builder, indentBuilder, format.nextDepth())
                                  || isMultiline;

                    if (i != Args.Length - 1)
                    {
                        builder.Append(rewrite ? rewriteRule.infix : ", ");
                    }

                    breakIndices[breakIndex] = builder.Length;
                    breakIndex++;
                }
            }
            else if (!rewrite)
            {
                // only use this in standard mode
                builder.Append("...");
            }

            builder.Append(rewrite ? rewriteRule.suffix: ")");

            // check if line split is necessary
            if (!isMultiline && builder.Length - startLength <= format.maxWidth
                || format.maxWidth == 0 || format.maxDepth == 1)
            {
                // split not necessary
                // unindent again for the return to parent level
                if (indent)
                {
                    indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                }
                return false;
            }

            // split necessary
            int offset = 0;
            int oldLength = builder.Length;
            for (int i = 0; i < breakIndices.Length; i++)
            {
                if (i == breakIndices.Length - 1)
                {
                    // unindent again
                    if (indent)
                    {
                        indentBuilder.Remove(indentBuilder.Length - indentDiff.Length, indentDiff.Length);
                    }
                }
                builder.Insert(breakIndices[i] + offset, "\n" + indentBuilder);
                offset += builder.Length - oldLength;
                oldLength = builder.Length;
            }
            return true;
        }

        private void addStandardHeader(StringBuilder builder, PrettyPrintFormat format)
        {
            builder.Append(Name);
            if (format.showType)
            {
                builder.Append(GenericType);
            }
            if (format.showTermId)
            {
                builder.Append("[").Append(id).Append(']');
            }
            builder.Append('(');
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