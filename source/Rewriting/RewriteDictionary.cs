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
        private readonly Dictionary<int, RewriteRule> specificTermTranslations = new Dictionary<int, RewriteRule>();
        private readonly Dictionary<string, RewriteRule> termTranslations = new Dictionary<string, RewriteRule>();

        public RewriteRule getRewriteRule(Term t)
        {
            if (specificTermTranslations.ContainsKey(t.id))
            {
                return specificTermTranslations[t.id];
            }
            if (termTranslations.ContainsKey(t.Name + t.GenericType))
            {
                return termTranslations[t.Name + t.GenericType];
            }
            return termTranslations.ContainsKey(t.Name) ? termTranslations[t.Name] : null;
        }

        public bool hasRule(string ruleMatch)
        {
            int id;
            return int.TryParse(ruleMatch, out id) ?
                specificTermTranslations.ContainsKey(id) :
                termTranslations.ContainsKey(ruleMatch);
        }

        public void removeRule(string ruleMatch)
        {
            int id;
            if (int.TryParse(ruleMatch, out id))
            {
                specificTermTranslations.Remove(id);
            }
            termTranslations.Remove(ruleMatch);
        }

        public void addRule(string ruleMatch, RewriteRule rule)
        {
            int id;
            if (int.TryParse(ruleMatch, out id))
            {
                specificTermTranslations.Add(id, rule);
            }
            termTranslations.Add(ruleMatch, rule);
        }

        public IEnumerable<KeyValuePair<string, RewriteRule>> getAllRules()
        {
            return specificTermTranslations
                .OrderBy(kvPair => kvPair.Key)
                .Select(translation => new KeyValuePair<string, RewriteRule>(translation.Key + "", translation.Value))
                .Concat(termTranslations.OrderBy(kvPair => kvPair.Key));
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
