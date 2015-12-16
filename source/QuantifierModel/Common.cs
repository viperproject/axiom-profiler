using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler.QuantifierModel
{
    public abstract class Common
    {
        public virtual string InfoPanelText(PrettyPrintFormat format) { return ToString(); }

        public virtual string SummaryInfo() { return ToString(); }
        public abstract IEnumerable<Common> Children();
        public virtual bool HasChildren() { return true; }
        public virtual bool AutoExpand() { return false; }
        public virtual Color ForeColor() { return Color.Black; }

        protected static IEnumerable<T> ConvertIEnumerable<T, S>(IEnumerable<S> x)
            where S : T
        {
            return x.Select(y => (T)y);
        }

        public static CallbackNode Callback<T>(string name, MyFunc<IEnumerable<T>> fn)
            where T : Common
        {
            return new CallbackNode(name, () => ConvertIEnumerable<Common, T>(fn()));
        }

        public static CallbackNode CallbackExp<T>(string name, IEnumerable<T> iter)
            where T : Common
        {
            List<T> cache = new List<T>(iter);
            var res = new CallbackNode(name + " [" + cache.Count + "]", () => ConvertIEnumerable<Common, T>(cache));
            if (cache.Count <= 3)
                res.autoExpand = true;
            return res;
        }
    }

    public class ForwardingNode : Common
    {
        public Common Fwd;
        string name;

        public ForwardingNode(string n, Common c)
        {
            name = n;
            Fwd = c;
        }
        public override IEnumerable<Common> Children()
        {
            return Fwd.Children();
        }
        public override Color ForeColor()
        {
            return Fwd.ForeColor();
        }

        public override bool HasChildren()
        {
            return Fwd.HasChildren();
        }
        public override string InfoPanelText(PrettyPrintFormat format)
        {
            return Fwd.InfoPanelText(format);
        }
        public override string ToString()
        {
            return name;
        }
    }

    public class LabelNode : Common
    {
        string name;

        public LabelNode(string s)
        {
            name = s;
        }

        public override IEnumerable<Common> Children()
        {
            yield break;
        }

        public override string ToString()
        {
            return name;
        }

        public override bool HasChildren()
        {
            return false;
        }
    }

    public delegate T MyFunc<out T>();

    public class CallbackNode : Common
    {
        MyFunc<IEnumerable<Common>> callback;
        string name;
        internal bool autoExpand;

        public CallbackNode(string name, MyFunc<IEnumerable<Common>> cb)
        {
            this.name = name;
            callback = cb;
        }

        public override string ToString()
        {
            return name;
        }

        public override IEnumerable<Common> Children()
        {
            return callback();
        }

        public override bool AutoExpand()
        {
            return autoExpand;
        }
    }

}