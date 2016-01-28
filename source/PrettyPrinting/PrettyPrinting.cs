using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Z3AxiomProfiler.QuantifierModel;

namespace Z3AxiomProfiler.PrettyPrinting
{
    public class PrintRuleDictionary
    {
        private readonly Dictionary<int, PrintRule> specificTermTranslations = new Dictionary<int, PrintRule>();
        private readonly Dictionary<string, PrintRule> termTranslations = new Dictionary<string, PrintRule>();

        public PrintRule getRewriteRule(Term t)
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

        public void addRule(string ruleMatch, PrintRule rule)
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

        public IEnumerable<KeyValuePair<string, PrintRule>> getAllRules()
        {
            return specificTermTranslations
                .OrderBy(kvPair => kvPair.Key)
                .Select(translation => new KeyValuePair<string, PrintRule>(translation.Key + "", translation.Value))
                .Concat(termTranslations.OrderBy(kvPair => kvPair.Key));
        }
    }



    public class PrintRule
    {
        public string prefix;
        public string infix;
        public string suffix;
        public Color color;
        public bool printChildren;
        public bool associative;
        public bool indent;
        public int precedence;
        public LineBreakSetting prefixLineBreak;
        public LineBreakSetting infixLineBreak;
        public LineBreakSetting suffixLineBreak;
        public ParenthesesSetting parentheses;

        public enum LineBreakSetting { Before = 0, After = 1, None = 2 };
        public enum ParenthesesSetting { Always = 0, Precedence = 1, Never = 2 };

        public static PrintRule DefaultRewriteRule(Term t, PrettyPrintFormat format)
        {
            var prefix = t.Name +
                (format.showType ? t.GenericType : "") +
                (format.showTermId ? "[" + t.id + "]" : "") +
                "(";
            return new PrintRule
            {
                prefix = prefix,
                infix = ", ",
                suffix = ")",
                color = Color.DarkSlateGray,
                printChildren = true,
                associative = false,
                indent = true,
                precedence = 0,
                prefixLineBreak = LineBreakSetting.After,
                infixLineBreak = LineBreakSetting.After,
                suffixLineBreak = LineBreakSetting.Before,
                parentheses = ParenthesesSetting.Never
            };
        }

        public static string lineBreakSettingToString(LineBreakSetting setting)
        {
            switch (setting)
            {
                case LineBreakSetting.Before:
                    return "Before";
                case LineBreakSetting.After:
                    return "After";
                case LineBreakSetting.None:
                    return "None";
                default:
                    return "Invalid / Unknown";
            }
        }

        public static LineBreakSetting lineBreakSettingFromString(string setting)
        {
            setting = setting.ToLower();
            switch (setting)
            {
                case "before":
                    return LineBreakSetting.Before;
                case "after":
                    return LineBreakSetting.After;
                case "none":
                    return LineBreakSetting.None;
                default:
                    throw new ArgumentException($"Unknown linebreak setting {setting}!");
            }
        }

        public static string parenthesesSettingsToString(ParenthesesSetting setting)
        {
            switch (setting)
            {
                case ParenthesesSetting.Always:
                    return "Always";
                case ParenthesesSetting.Precedence:
                    return "Precedence";
                case ParenthesesSetting.Never:
                    return "Never";
                default:
                    return "Invalid / Unknown";
            }
        }

        public static ParenthesesSetting parenthesesSettingsFromString(string setting)
        {
            setting = setting.ToLower();
            switch (setting)
            {
                case "always":
                    return ParenthesesSetting.Always;
                case "precedence":
                    return ParenthesesSetting.Precedence;
                case "never":
                    return ParenthesesSetting.Never;
                default:
                    throw new ArgumentException($"Unknown parentheses setting {setting}!");
            }
        }
    }

    public class PrettyPrintFormat
    {
        public int maxWidth;
        public int maxDepth;
        public bool showType;
        public bool showTermId;
        public bool rewritingEnabled;
        public Term parentTerm;
        public int childIndex = -1; // which child of the parent the current term is.
        public PrintRuleDictionary printRuleDict = new PrintRuleDictionary();

        public PrettyPrintFormat nextDepth(Term parent, int childNo)
        {
            return new PrettyPrintFormat
            {
                maxWidth = maxWidth,
                maxDepth = maxDepth == 0 ? 0 : maxDepth - 1,
                showTermId = showTermId,
                showType = showType,
                rewritingEnabled = rewritingEnabled,
                printRuleDict = printRuleDict,
                parentTerm = parent,
                childIndex = childNo
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
                printRuleDict = new PrintRuleDictionary()
            };
        }

        public PrintRule getPrintRule(Term t)
        {
            if (t == null) return null;
            if (rewritingEnabled && printRuleDict.hasRule(t))
            {
                return printRuleDict.getRewriteRule(t);
            }
            return PrintRule.DefaultRewriteRule(t, this);
        }
    }
}
