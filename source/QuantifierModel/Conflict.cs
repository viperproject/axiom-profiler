using System.Collections.Generic;
using System.Text;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler.QuantifierModel
{
    public class Conflict : Common
    {
        public List<Literal> Literals = new List<Literal>();
        public Literal[] ResolutionLits;
        public int LineNo;
        public int Id;
        public int Cost;
        public double InstCost;
        public ResolutionLiteral ResolutionRoot;

        public bool Useful
        {
            get
            {
                foreach (Literal l in Literals)
                    if (l.Term != null) return true;
                return false;
            }
        }

        public void PrintAsCsv(StringBuilder sb, int id)
        {
            sb.AppendFormat("{0},{1},{2},{3}", id++, LineNo, Cost, Literals.Count);
            foreach (var l in Literals)
                sb.AppendFormat(",\"{0}\"", l);
            sb.Append("\r\n");
        }

        public static StringBuilder CsvHeader()
        {
            return new StringBuilder("#,Line#,LineDelta (Cost),#Lits,Lits\r\n");
        }

        public override string ToString()
        {
            return string.Format("Confl#{3} {0} lits, {1:0} ops {2:0} inst", Literals.Count, Cost / 100.0, InstCost, Id);

        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            foreach (Literal l in Literals)
            {
                content.Append(l + "\n");
            }
        }

        public override IEnumerable<Common> Children()
        {
            if (ResolutionLits.Length > 0)
                yield return Common.Callback("RESOLVED FROM", () => ResolutionLits);
            if (ResolutionRoot != null)
                yield return Common.Callback("RESOLVED FROM", () => new Common[] { ResolutionRoot });
            yield return Common.Callback("CAUSES", delegate ()
            {
                var r = new List<Common>();
                foreach (var l in Literals)
                {
                    if (l.Inverse != null)
                    {
                        if (l.Inverse.Clause != null)
                            r.Add(l.Inverse);
                        else if (l.Inverse.Cause != null)
                            r.Add(l.Inverse.Cause);
                    }
                }
                return r;
            });
            foreach (var l in Literals)
                yield return l;
        }
    }
}