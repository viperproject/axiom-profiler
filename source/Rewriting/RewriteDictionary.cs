using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Z3AxiomProfiler.QuantifierModel;

namespace Z3AxiomProfiler.Rewriting
{
    public class RewriteDictionary
    {
        public readonly Dictionary<string, RewriteRule> termTranslations = new Dictionary<string, RewriteRule>();

        public RewriteRule getRewriteRule(Term t)
        {
            if (termTranslations.ContainsKey(t.identifier))
            {
                return termTranslations[t.identifier];
            }
            if (termTranslations.ContainsKey(t.Name + t.GenericType))
            {
                return termTranslations[t.Name + t.GenericType];
            }
            if (termTranslations.ContainsKey(t.Name))
            {
                return termTranslations[t.Name];
            }
            return null;
        }
    }

    public class RewriteRule
    {
        public string prefix = "";
        public string infix = "";
        public string suffix = "";
        public bool printChildren = true;


        public static RewriteRule DefaultRewriteRule()
        {
            return new RewriteRule
            {
                prefix = "(",
                infix = ", ",
                suffix = ")",
                printChildren = true
            };
        }
    }

    public class PrettyPrintFormat
    {
        public int maxWidth;
        public int maxDepth;
        public bool showType;
        public bool showTermId;
        public bool rewritingEnabled;
        public RewriteDictionary rewriteDict;

        public PrettyPrintFormat nextDepth()
        {
            return new PrettyPrintFormat
            {
                maxWidth = maxWidth,
                maxDepth = maxDepth == 0 ? 0 : maxDepth - 1,
                showTermId = showTermId,
                showType = showType,
                rewritingEnabled = rewritingEnabled,
                rewriteDict = rewriteDict
            };
        }

        public static PrettyPrintFormat DefaultPrettyPrintFormat()
        {
            return new PrettyPrintFormat
            {
                maxWidth = 80,
                maxDepth = 0,
                showTermId = true,
                showType = true,
                rewritingEnabled = false,
                rewriteDict = new RewriteDictionary()
            };
        }

        public bool getRewriteRule(Term t, out RewriteRule rewriteRule)
        {
            if (rewritingEnabled && rewriteDict != null)
            {
                rewriteRule = rewriteDict.getRewriteRule(t);
                return rewriteRule != null;
            }
            rewriteRule = RewriteRule.DefaultRewriteRule();
            return false;
        }
    }
}
