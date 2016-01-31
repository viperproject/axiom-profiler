using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
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

        public PrintRule getRewriteRule(string match)
        {

            int id;
            if (int.TryParse(match, out id) && specificTermTranslations.ContainsKey(id))
            {
                return specificTermTranslations[id];
            }
            if (termTranslations.ContainsKey(match))
            {
                return termTranslations[match];
            }
            throw new KeyNotFoundException($"No rewrite rule for match {match}!");
        }

        public string getMatch(Term t)
        {
            if (specificTermTranslations.ContainsKey(t.id))
            {
                return t.id + "";
            }
            if (termTranslations.ContainsKey(t.Name + t.GenericType))
            {
                return t.Name + t.GenericType;
            }
            if (termTranslations.ContainsKey(t.Name))
            {
                return t.Name;
            }
            throw new KeyNotFoundException($"No rewrite rule for term {t}!");
        }

        public KeyValuePair<string, PrintRule> getGeneralTermTranslationPair(Term t)
        {
            if (termTranslations.ContainsKey(t.Name + t.GenericType))
            {
                return new KeyValuePair<string, PrintRule>(t.Name + t.GenericType, termTranslations[t.Name + t.GenericType]);
            }

            return new KeyValuePair<string, PrintRule>(t.Name, termTranslations[t.Name]);
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
            int id;
            if (int.TryParse(ruleMatch, out id))
            {
                specificTermTranslations[id] = rule;
            }
            termTranslations[ruleMatch] = rule;
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
        public bool isDefault;
        public List<List<Term>> historyConstraints;

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
                isDefault = true,
                precedence = 0,
                historyConstraints = new List<List<Term>>(),
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

        public PrintRule Clone()
        {
            return new PrintRule
            {
                prefix = prefix,
                infix = infix,
                suffix = suffix,
                color = color,
                prefixLineBreak = prefixLineBreak,
                infixLineBreak = infixLineBreak,
                suffixLineBreak = suffixLineBreak,
                associative = associative,
                historyConstraints = new List<List<Term>>(historyConstraints),
                indent = indent,
                isDefault = isDefault,
                parentheses = parentheses,
                precedence = precedence,
                printChildren = printChildren
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
        public readonly List<Term> history = new List<Term>();
        public int childIndex = -1; // which child of the parent the current term is.
        public PrintRuleDictionary printRuleDict = new PrintRuleDictionary();
        private readonly Dictionary<string, PrintRule> originalRulesReplacedByTemp = new Dictionary<string, PrintRule>();

        public PrettyPrintFormat nextDepth(Term parent, int childNo)
        {
            var nextFormat =  new PrettyPrintFormat
            {
                maxWidth = maxWidth,
                maxDepth = maxDepth == 0 ? 0 : maxDepth - 1,
                showTermId = showTermId,
                showType = showType,
                rewritingEnabled = rewritingEnabled,
                printRuleDict = printRuleDict,
                childIndex = childNo
            };
            nextFormat.history.AddRange(history);
            nextFormat.history.Add(parent);
            return nextFormat;
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
                var rule =  printRuleDict.getRewriteRule(t);
                if (historyConstraintSatisfied(rule))
                {
                    return rule;
                }
            }
            return PrintRule.DefaultRewriteRule(t, this);
        }

        public PrintRule GetParentPrintRule()
        {
            if (history.Count == 0) return null;
            return getPrintRule(history.Last());
        }

        private bool historyConstraintSatisfied(PrintRule rule)
        {
            return (from historyConstraint in rule.historyConstraints
                    where historyConstraint.Count <= history.Count
                        let slice = history.GetRange(history.Count - historyConstraint.Count, historyConstraint.Count)
                        where slice.SequenceEqual(historyConstraint) select historyConstraint).Any();
        }

        public void addTemporaryRule(string match, PrintRule rule)
        {

            // save old rule
            PrintRule oldRule = null;
            if (printRuleDict.hasRule(match))
            {
                oldRule = printRuleDict.getRewriteRule(match);
            }
            // save original rule only if it was not already a temporary rule.
            if (!originalRulesReplacedByTemp.ContainsKey(match))
            {
                originalRulesReplacedByTemp.Add(match, oldRule);
            }
            
            // add new one
            printRuleDict.addRule(match, rule);
        }

        public void restoreOriginalRule(string match)
        {
            // no original save -> nothing to do
            // maybe it was already replaced...
            if (!originalRulesReplacedByTemp.ContainsKey(match)) return;

            
            if (originalRulesReplacedByTemp[match] != null && !originalRulesReplacedByTemp[match].isDefault)
            {
                // insert old rule again (unles its just the default rule or null)
                printRuleDict.addRule(match, originalRulesReplacedByTemp[match]);
            }
            else
            {
                // otherwise just delete the temporary rule
                printRuleDict.removeRule(match);
            }
            originalRulesReplacedByTemp.Remove(match);
        }

        public void restoreAllOriginalRules()
        {
            foreach (var rule in originalRulesReplacedByTemp)
            {
                if (rule.Value == null || rule.Value.isDefault)
                {
                    printRuleDict.removeRule(rule.Key);
                }
                else
                {
                    printRuleDict.addRule(rule.Key, rule.Value);
                }
            }
            originalRulesReplacedByTemp.Clear();
        }
    }
}
