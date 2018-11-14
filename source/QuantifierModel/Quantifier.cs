using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler.QuantifierModel
{
    public class Quantifier : Common
    {
        public string Qid;
        public string PrintName;
        public string BoogieBody;
        public Term BodyTerm;
        public readonly List<Instantiation> Instances = new List<Instantiation>();
        public double CrudeCost;

        public int CurDepth;
        public int MaxDepth;
        public int UsefulInstances;
        public int GeneratedConflicts;

        public override string ToString()
        {
            string result = $"Quantifier[{PrintName}], #instances: {Instances.Count}, Cost: {Cost.ToString("F")}, #conflicts: {GeneratedConflicts}";
            return result;
        }

        public List<Term> Patterns()
        {
            return BodyTerm.Args.Where(term => term.Name == "pattern").ToList();
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            SummaryInfo(content);
            BodyTerm.PrettyPrint(content, format);
        }

        // ToDo: find better implementation!
        public int Weight => 1;

        private Common TheMost(string tag, Comparison<Instantiation> cmp)
        {
            Instances.Sort(cmp);
            int len = 100;
            if (Instances.Count < len)
                len = Instances.Count;
            Common[] first = new Common[len];
            for (int i = 0; i < len; ++i)
                first[i] = Instances[i];
            return new CallbackNode(tag, () => first);
        }

        public override IEnumerable<Common> Children()
        {

            if (BodyTerm == null)
            {
                BodyTerm = new Term("?", new Term[] { });
            }

            return new[] {Callback("REAL COST", () => new Common[] {new LabelNode(RealCost + "")}),
                BodyTerm,
                TheMost("DEEP", (i1, i2) => i2.Depth.CompareTo(i1.Depth)),
                TheMost("COSTLY", (i1, i2) => i2.Cost.CompareTo(i1.Cost)),
                TheMost("FIRST", (i1, i2) => i1.LineNo.CompareTo(i2.LineNo)),
                TheMost("LAST", (i1, i2) => i2.LineNo.CompareTo(i1.LineNo)),
                Callback("ALL [" + Instances.Count + "]", () => Instances)};
        }

        public override Color ForeColor()
        {
            return Weight == 0 ? Color.IndianRed : base.ForeColor();
        }

        private double InstanceCost(Instantiation inst, int lev)
        {
            if (lev != 0 && inst.Quant == this) return 0;
            return 1 + (from ch in inst.DependantInstantiations
                        let cnt = ch.Responsible.Count(other => other.Responsible != null)
                        select InstanceCost(ch, lev + 1) / cnt)
                .Sum();
        }

        private double realCost;
        public double RealCost
        {
            // this is slow
            get
            {
                if (realCost != 0) return realCost;
                foreach (var inst in Instances)
                    realCost += InstanceCost(inst, 0);
                return realCost;
            }
        }

        public double Cost => CrudeCost + Instances.Count;

        public override void SummaryInfo(InfoPanelContent content)
        {
            content.switchFormat(PrintConstants.TitleFont, PrintConstants.instantiationTitleColor);
            content.Append("Quantifier Info:\n");
            content.switchToDefaultFormat();
            content.Append("\nPrint name: ").Append(PrintName).Append('\n');
            content.Append("QId: ").Append(Qid).Append('\n');
            content.Append("Number of Instantiations: " + Instances.Count).Append('\n');
            content.Append("Cost: " + Cost).Append('\n');
            content.Append("Number of Conflicts: " + GeneratedConflicts).Append("\n\n");
        }
    }
}