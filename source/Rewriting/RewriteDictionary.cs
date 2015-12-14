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
            if (termTranslations.ContainsKey(t.Name))
            {
                return termTranslations[t.Name];
            }
            throw new KeyNotFoundException($"No rewrite rule for term {t}!");
        }

        public bool hasRule(Term t)
        {
            return specificTermTranslations.ContainsKey(t.id) || 
                termTranslations.ContainsKey(t.Name + t.GenericType) ||
                termTranslations.ContainsKey(t.Name);
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
            if (hasRule(ruleMatch))
            {
                removeRule(ruleMatch);
            }

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
        public string prefix;
        public string infix;
        public string suffix;
        public bool printChildren;
        public int precedence;
        public lineBreakSetting prefixLineBreak;
        public lineBreakSetting infixLineBreak;
        public lineBreakSetting suffixLineBreak;

        public enum lineBreakSetting { Before, After, None };

        public static RewriteRule DefaultRewriteRule(Term t, PrettyPrintFormat format)
        {
            var prefix = t.Name +
                (format.showType ? t.GenericType : "") +
                (format.showTermId ? "[" + t.id + "]" : "") +
                "(";
            return new RewriteRule
            {
                prefix = prefix,
                infix = ", ",
                suffix = ")",
                printChildren = true,
                prefixLineBreak = lineBreakSetting.After,
                infixLineBreak = lineBreakSetting.After,
                suffixLineBreak = lineBreakSetting.Before
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
        public RewriteDictionary rewriteDict = new RewriteDictionary();

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

        public RewriteRule getRewriteRule(Term t)
        {
            if (rewritingEnabled && rewriteDict.hasRule(t))
            {
                return rewriteDict.getRewriteRule(t);
            }
            return RewriteRule.DefaultRewriteRule(t, this);
        }
    }
}
