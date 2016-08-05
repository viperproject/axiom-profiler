using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler.QuantifierModel
{
    public class Literal : Common
    {
        public bool Negated;
        public Term Term;
        public int Id;
        public Term Clause;
        public Term[] Explanation;
        public Common Cause;
        public Literal Inverse;
        public Literal[] Implied;

        public override string ToString()
        {
            string t;
            bool isBin = Term.Args.Length == 2;

            if (Negated && Term.Name == "or")
            {
                Negated = false;
                Term = new Term("And", LogProcessor.NegateAll(Term.Args));
            }

            if (isBin)
                if (Term.Name.Any(char.IsLetterOrDigit))
                {
                    isBin = false;
                }

            if (Term == null) t = "(nil)";
            else if (isBin)
            {
                var content0 = new InfoPanelContent();
                var content1 = new InfoPanelContent();
                Term.Args[0].PrettyPrint(content0, PrettyPrintFormat.DefaultPrettyPrintFormat());
                Term.Args[1].PrettyPrint(content1, PrettyPrintFormat.DefaultPrettyPrintFormat());
                content0.finalize();
                content1.finalize();
                t = $"{content0}  {Term.Name}  {content1}";
            }
            else
            {
                var content = new InfoPanelContent();
                Term.PrettyPrint(content, PrettyPrintFormat.DefaultPrettyPrintFormat());
                content.finalize();
                t = content.ToString();
            }
            return string.Format("{0}p{1}  {3}{2}", Negated ? "~" : "", Id, t, Implied == null ? "" : "[+" + Implied.Length + "] ");
        }

        public override IEnumerable<Common> Children()
        {
            if (Term != null)
                yield return Term;
            if (Explanation != null)
            {
                yield return Callback("EXPLANATION [" + Explanation.Length + "]", () => Explanation);
            }
            if (Clause != null)
            {
                yield return new LabelNode("FROM CLAUSE:");
                yield return Clause;
            }
            if (Cause != null)
                yield return Callback("CAUSE", () => new Common[] { Cause });
            if (Inverse != null)
                yield return Callback("INVERSE", () => new Common[] { Inverse });
            if (Implied != null && Implied.Length > 0)
                yield return Callback("IMPLIED [" + Implied.Length + "]", () => Implied);
        }

        public override Color ForeColor()
        {
            return Clause != null ? Color.IndianRed : base.ForeColor();
        }
    }

    public class ResolutionLiteral : Literal
    {
        public List<ResolutionLiteral> Results = new List<ResolutionLiteral>();
        public int LevelDifference;

        public override IEnumerable<Common> Children()
        {
            foreach (var e in Results) yield return e;
        }

        public override Color ForeColor()
        {
            if (LevelDifference == 0)
                return Color.IndianRed;
            else return base.ForeColor();
        }

        public override bool HasChildren()
        {
            return Results.Count > 0;
        }
        public ResolutionLiteral Find(int id)
        {
            if (this.Id == id) return this;
            foreach (var c in Results)
            {
                var r = c.Find(id);
                if (r != null) return r;
            }
            return null;
        }
    }
}