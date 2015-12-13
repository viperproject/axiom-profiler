using System.Collections.Generic;
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
        public Instantiation Responsible;
        public string identifier = "None";
        private readonly int Depth;
        public Term NegatedVersion;
        private readonly List<Term> dependentTerms = new List<Term>();
        public readonly List<Instantiation> dependentInstantiationsBlame = new List<Instantiation>();
        public readonly List<Instantiation> dependentInstantiationsBind = new List<Instantiation>();

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
                Depth = Depth > t.Depth ? Depth : t.Depth;
                t.dependentTerms.Add(this);
            }
            Depth++;
        }

        public Term(Term t)
        {
            Name = t.Name;
            Args = t.Args;
            Depth = t.Depth;
            Responsible = t.Responsible;
            identifier = t.identifier;
        }

        public string Sig => Name + "/" + Args.Length;

        public void CWriteTo(StringBuilder s, bool states)
        {
            if (s.Length > 1096) return;

            if (Sig == "$ptr/2" && Args[1].Sig == "$ref/1")
            {
                if (states)
                {
                    s.Append("((");
                    Args[0].CWriteTo(s, states);
                    s.Append(") ");
                }
                Args[1].Args[0].CWriteTo(s, states);
                if (states)
                    s.Append(")");

            }
            else if ((Sig == "$select.mem/2" || Sig == "select1/2" || Sig == "$read_ptr_m/3") && Args[1].Sig == "$dot/2")
            {

                s.Append("(");
                Args[1].Args[0].CWriteTo(s, states);
                s.Append(")->");
                Args[1].Args[1].CWriteTo(s, states);
                if (states)
                {
                    s.Append(" @ ");
                    Args[0].CWriteTo(s, states);
                }
            }
            else if (Sig == "$read_ptr_m/3")
            {
                s.Append("*(");
                Args[1].CWriteTo(s, states);
                s.Append(")");
                if (states)
                {
                    s.Append(" @ ");
                    Args[0].CWriteTo(s, states);
                }
            }
            else if (Sig == "$dot/2")
            {
                s.Append("&(");
                Args[0].CWriteTo(s, states);
                s.Append(")->");
                Args[1].CWriteTo(s, states);
            }
            else if (Sig == "$ptr/2" && Args[1].Sig == "$ghost_ref/2")
            {
                s.Append("&(");
                Args[1].Args[0].CWriteTo(s, states);
                s.Append(")->");
                Args[1].Args[1].CWriteTo(s, states);
            }
            else if (Sig == "$ptr/2" && Args[1].Sig == "$st_ref_cnt_ptr/1" && Args[1].Args[0].Sig == "select1/2")
            {
                s.Append("&(");
                Args[1].Args[0].Args[1].CWriteTo(s, states);
                s.Append(")->ref_cnt");
                if (states)
                {
                    s.Append(" @ ");
                    Args[1].Args[0].Args[0].CWriteTo(s, states);
                }
            }
            else if (Sig == "$rev_ref_cnt_ptr/1" && Args[0].Sig == "$ptr/2" && Args[0].Args[1].Sig == "$st_ref_cnt_ptr/1" && Args[0].Args[1].Args[0].Sig == "select1/2")
            {
                s.Append("%");
                Args[0].Args[1].Args[0].Args[1].CWriteTo(s, states);
            }
            else if (Sig == "$ghost_emb/1" && Args[0].Sig == "$ghost_ref/2")
            {
                s.Append("%");
                Args[0].Args[0].CWriteTo(s, states);
            }
            else if (Sig.StartsWith("$select.$map") && Args.Length == 2)
            {
                Args[0].CWriteTo(s, states);
                s.Append("[");
                Args[1].CWriteTo(s, states);
                s.Append("]");
            }
            else if (Sig == "$ts_emb/1" && Args[0].Sig == "select1/2")
            {
                s.Append("(");
                Args[0].Args[1].CWriteTo(s, states);
                s.Append(")->$$emb");
                if (states)
                {
                    s.Append(" @ ");
                    Args[0].Args[0].CWriteTo(s, states);
                }
            }
            else if (Sig == "$emb/2")
            {
                s.Append("(");
                Args[1].CWriteTo(s, states);
                s.Append(")->$$emb");
                if (states)
                {
                    s.Append(" @ ");
                    Args[0].CWriteTo(s, states);
                }
            }
            else if (Sig == "$ptr/2" && !states)
            {
                Args[1].CWriteTo(s, states);
            }
            else if (Sig == "$owns_set_field/1")
            {
                s.Append("$owns");
            }
            else if (Sig == "$index_within/2" && Args[0].Sig == "$idx/3" && Args[0].Args[0] == Args[1] && Args[0].Args[2].Sig == "$typ/1" && Args[0].Args[2].Args[0] == Args[1])
            {
                Args[0].Args[1].CWriteTo(s, states);
            }
            else if (Sig.StartsWith("MapType") && Sig.Contains("Select/"))
            {
                Args[0].CWriteTo(s, states);
                s.Append("[");
                for (int i = 1; i < Args.Length; ++i)
                {
                    if (i != 1) s.Append(", ");
                    Args[i].CWriteTo(s, states);
                }
                s.Append("]");
            }
            else if (Sig.EndsWith("/1") && Name.StartsWith("U_2_"))
            {
                Args[0].CWriteTo(s, states);
            }
            else
            {
                s.Append(Name);
                if (Args.Length == 0) return;
                s.Append('(');
                for (int i = 0; i < Args.Length; ++i)
                {
                    if (i != 0) s.Append(", ");
                    Args[i].CWriteTo(s, states);
                }
                s.Append(')');
            }
        }

        public string AsCString(bool states)
        {
            StringBuilder sb = new StringBuilder("C: ");
            CWriteTo(sb, states);
            return sb.ToString();
        }

        private static string indentDiff = "¦ ";

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
                builder.Append('[').Append(identifier).Append(']');
            }
            builder.Append('(');
        }

        public override string ToString()
        {
            return $"Term[{Name}] Identifier:{identifier}, Depth:{Depth}, #Children:{Args.Length}";
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
            s.Append("Identifier: ").Append(identifier).Append('\n');
            s.Append("Depth: ").Append(Depth).Append('\n');
            s.Append("Number of Children: ").Append(Args.Length).Append('\n');
            return s.ToString();
        }
    }
}