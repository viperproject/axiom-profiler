using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class FunApp : Common
    {
        public FunSymbol Fun;
        public Partition[] Args;
        public Partition Value;

        public string ShortName()
        {
            if (Args.Length > 0)
            {
                StringBuilder sb = new StringBuilder(Fun.DisplayName);
                sb.Append("(");
                foreach (var a in Args)
                    sb.Append(a.ShortName()).Append(", ");
                sb.Length = sb.Length - 2;
                sb.Append(")");
                return sb.ToString();
            }
            return Fun.DisplayName;
        }

        public override string ToString()
        {
            string self = ShortName();
            if (Value.BestApp() != this)
                return self + " = " + Value.ShortName();

            return self;
        }

        private IEnumerable<Common> Eq()
        {
            return (from other in Value.Values
                where other != this
                select new ForwardingNode("= " + other.ShortName(), other));
        }

        public override IEnumerable<Common> Children()
        {
            foreach (var p in Args)
                yield return p.BestApp();
            yield return CallbackExp("EQ", Eq());
            yield return CallbackExp("USERS", Filter());
        }

        private IEnumerable<Common> Filter()
        {
            foreach (var fs in Fun.AllSymbols)
            {
                FunSymbol ffs = new FunSymbol();
                ffs.Name = fs.Name;
                foreach (var fapp in fs.Apps)
                {
                    foreach (var a in fapp.Args)
                    {
                        if (a == Value)
                        {
                            ffs.Apps.Add(fapp);
                            break;
                        }
                    }
                }
                if (ffs.Apps.Count > 0)
                    yield return ffs;
            }
        }
    }
}