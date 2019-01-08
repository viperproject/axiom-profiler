using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using AxiomProfiler.PrettyPrinting;
using AxiomProfiler.QuantifierModel.TheoryMeaning;

namespace AxiomProfiler.QuantifierModel
{
    using ConstraintType = List<Tuple<Term, int>>;

    public class Term : Common
    {
        public class SemanticTermComparer : IEqualityComparer<Term>
        {
            public bool Equals(Term t1, Term t2)
            {
                return t1.generalizationCounter >= 0 ? t1.generalizationCounter == t2.generalizationCounter : t1.id == t2.id && t1.Name == t2.Name && t1.isPrime == t2.isPrime && t1.iterationOffset == t2.iterationOffset;
            }

            public int GetHashCode(Term t)
            {
                return t.generalizationCounter >= 0 ? t.generalizationCounter : t.id;
            }
        }
        public static readonly SemanticTermComparer semanticTermComparer = new SemanticTermComparer();

        public readonly string Name;
        public string TheorySpecificMeaning = null;
        public string Theory = null;

        public string PrettyName
        {
            get
            {
                if (TheorySpecificMeaning == null)
                {
                    return Name;
                }
                return TheoryMeaningInterpretation.singleton.GetPrettyStringForTheoryMeaning(Theory, TheorySpecificMeaning);
            }
        }

        public readonly string GenericType;
        public Term[] Args;
        public int id = -1;
        public readonly int size;
        public Instantiation Responsible;
        public readonly List<Term> dependentTerms = new List<Term>();
        public readonly List<Instantiation> dependentInstantiationsBlame = new List<Instantiation>();
        public readonly List<Instantiation> dependentInstantiationsBind = new List<Instantiation>();
        private static readonly Regex TypeRegex = new Regex(@"([\s\S]+)(<[\s\S]*>)");
        public Term reverseRewrite = null;
        public int generalizationCounter = -1;
        public int varIdx = -1;

        // isPrime and generationOffset represent a similar concept: they indicate that a generalized term is written in terms of a different
        // (generalized) loop iteration than the one we are currently in. iterationOffset is used by the algorithms whereas isPrime is used for
        // printing. Furthermore an iterationOffset > 0 indicates that the term comes from a preceeding iteration whereas isPrime is usually used
        // indicate a term from a future iteration.
        public int iterationOffset = 0;
        public bool isPrime = false;

        public Term(string name, Term[] args, int generalizationCounter = -1)
        {
            this.generalizationCounter = generalizationCounter;
            Match typeMatch = TypeRegex.Match(name);
            if (typeMatch.Success)
            {
                Name = string.Intern(typeMatch.Groups[1].Value);
                GenericType = string.Intern(typeMatch.Groups[2].Value);
            }
            else
            {
                Name = string.Intern(name);
                GenericType = "";
            }
            Args = args;
            reverseRewrite = this;

            // Note: the null check was added to have it easier to construct the
            // generalized terms top down.
            // During parsing, args should never contain 'null' as Z3 only builds terms
            // bottom up.
            foreach (var t in Args.Where(t => t != null))
            {
                size += t.size;
                t.dependentTerms.Add(this);
            }
            size += 1;
        }

        public Term(Term t, Term[] newArgs = null)
        {
            Name = t.Name;
            TheorySpecificMeaning = t.TheorySpecificMeaning;
            Theory = t.Theory;
            Args = newArgs ?? (Term[]) t.Args.Clone();
            Responsible = t.Responsible;
            id = t.id;
            size = t.size;
            GenericType = t.GenericType;
            if (ReferenceEquals(t.reverseRewrite, t))
            {
                reverseRewrite = this;
            }
            else
            {
                reverseRewrite = t.reverseRewrite;
            }
            generalizationCounter = t.generalizationCounter;
            iterationOffset = t.iterationOffset;

            dependentInstantiationsBlame = new List<Instantiation>(t.dependentInstantiationsBlame);
            dependentInstantiationsBind = new List<Instantiation>(t.dependentInstantiationsBind);
            dependentTerms = new List<Term>(t.dependentTerms);
            varIdx = t.varIdx;
        }

        public Term DeepCopy()
        {
            return new Term(this, Args.Select(arg => arg.DeepCopy()).ToArray());
        }

        public IEnumerable<Term> QuantifiedVariables()
        {
            if (id == -1)
            {
                yield return this;
            } else
            {
                foreach (var arg in Args)
                {
                    foreach (var result in arg.QuantifiedVariables())
                    {
                        yield return result;
                    }
                }
            }
        }

        public bool ContainsQuantifiedVar()
        {
            return QuantifiedVariables().Any();
        }

        public bool ContainsGeneralization()
        {
            return GetAllGeneralizationSubterms().Any();
        }

        public IEnumerable<Term> GetAllGeneralizationSubterms()
        {
            if (generalizationCounter >= 0)
            {
                return Enumerable.Repeat(this, 1);
            }
            else
            {
                return Args.SelectMany(arg => arg.GetAllGeneralizationSubterms());
            }
        }

        /// <summary>
        /// Also contains T_1 for T_2(T_1).
        /// </summary>
        public IEnumerable<Term> GetAllGeneralizationSubtermsAndDependencies()
        {
            if (generalizationCounter >= 0)
            {
                return Enumerable.Repeat(this, 1).Concat(Args.SelectMany(arg => arg.GetAllGeneralizationSubtermsAndDependencies()));
            }
            else
            {
                return Args.SelectMany(arg => arg.GetAllGeneralizationSubtermsAndDependencies());
            }
        }

        /// <summary>
        /// Indicates whether the term includes references to any iteration other than the one indicated.
        /// </summary>
        public bool ReferencesOtherIteration(int iteration = 0)
        {
            return iterationOffset != iteration || Args.Any(arg => arg.ReferencesOtherIteration(iteration));
        }

        public void highlightTemporarily(PrettyPrintFormat format, Color color)
        {
            var tmp = format.getPrintRule(this).Clone();
            tmp.color = color;
            format.addTemporaryRule(id + "", tmp);
        }

        public void highlightTemporarily(PrettyPrintFormat format, Color color, ConstraintType pathConstraint)
        {
            highlightTemporarily(format, color, Enumerable.Repeat(pathConstraint, 1));
        }

        public void highlightTemporarily(PrettyPrintFormat format, Color color, IEnumerable<ConstraintType> pathConstraints)
        {
            var baseRule = format.getPrintRule(this);
            IEnumerable<PrintRule> newRules;
            if (pathConstraints.Any())
            {
                newRules = pathConstraints.Select(constraint =>
                {
                    var tmp = baseRule.Clone();
                    tmp.color = color;
                    tmp.historyConstraints = constraint;
                    return tmp;
                });
            }
            else
            {
                var tmp = baseRule.Clone();
                tmp.color = color;
                tmp.historyConstraints = new ConstraintType();
                newRules = Enumerable.Repeat(tmp, 1);
            }

            if (id == -1)
            {
                foreach (var tmp in newRules)
                {
                    format.addTemporaryRule(Name, tmp);
                }
            }
            else
            {
                foreach (var tmp in newRules)
                {
                    format.addTemporaryRule(id + "", tmp);
                }
            }
        }

        public bool isSubterm(Term subterm)
        {
            if (subterm.size > size) return false;

            var subtermsToCheck = new Queue<Term>();
            subtermsToCheck.Enqueue(this);
            while (subtermsToCheck.Count > 0)
            {
                var current = subtermsToCheck.Dequeue();

                if (current.size <= subterm.size)
                {
                    if (current.id == subterm.id) return true;
                    continue;
                }

                // term is larger, check subterms
                foreach (var arg in current.Args)
                {
                    subtermsToCheck.Enqueue(arg);
                }
            }
            return false;
        }

        public bool isSubterm(int subtermId)
        {
            if (id == subtermId) return true;
            return Args.Any(t => t.isSubterm(subtermId));
        }

        public void printName(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.Append(Name);
            if (format.showType) content.Append(GenericType);
            if (iterationOffset > 0) content.Append("_-" + iterationOffset);
            if (format.showTermId) content.Append("[" + (id >= 0 ? id.ToString() : "g" + -id) + (isPrime ? "'" : "") + "]");
        }

        public void PrettyPrint(InfoPanelContent content, PrettyPrintFormat format, int indent = 0)
        {
            var indentColors = Enumerable.Empty<Color>();
            if (indent >= 2)
            {
                indentColors = indentColors.Concat(Enumerable.Repeat(PrintConstants.defaultTextColor, 1));
                indentColors = indentColors.Concat(Enumerable.Repeat(Color.Transparent, indent - 2));
            }
            indentColors = indentColors.Concat(Enumerable.Repeat(PrintConstants.defaultTextColor, format.CurrentEqualityExplanationPrintingDepth));
                
            PrettyPrint(content, new Stack<Color>(indentColors), format);
        }

        private bool PrettyPrint(InfoPanelContent content, Stack<Color> indentFormats, PrettyPrintFormat format)
        {
            var printRule = format.getPrintRule(this);
            var parentRule = format.GetParentPrintRule();
            var isMultiline = false;
            var breakIndices = new List<int>();
            var startLength = content.Length;
            var needsParenthesis = this.needsParenthesis(format, printRule, parentRule);

            content.switchFormat(printRule.font ?? PrintConstants.DefaultFont, printRule.color);

            // check for cutoff
            if (format.MaxTermPrintingDepth == 1)
            {
                if (ContainsGeneralization())
                {
                    content.switchFormat(printRule.font ?? PrintConstants.DefaultFont, PrintConstants.generalizationColor);
                    content.Append(">...<");
                }
                else
                {
                    content.Append("...");
                }
                return false;
            }

            if (printRule.indent) indentFormats.Push(printRule.color);
            if (needsParenthesis) content.Append('(');
            addPrefix(printRule, content, breakIndices);

            if (printChildren(format, printRule))
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    var t = Args[i];

                    // Note: DO NOT CHANGE ORDER (-> short circuit)
                    isMultiline = t.PrettyPrint(content, indentFormats, format.NextTermPrintingDepth(this, i))
                                  || isMultiline;

                    if (i < Args.Length - 1)
                    {
                        addInfix(printRule, content, breakIndices);
                    }
                }
            }

            addSuffix(printRule, content, breakIndices);
            if (needsParenthesis) content.Append(')');

            // are there any lines to break?
            var lineBreaks = linebreaksNecessary(content, format, isMultiline && (breakIndices.Count > 0), startLength);
            if (lineBreaks)
            {
                addLinebreaks(printRule, content, indentFormats, breakIndices);
            }
            else if (printRule.indent)
            {
                // just remove indent if necessary
                indentFormats.Pop();
            }

            return lineBreaks;
        }

        private bool needsParenthesis(PrettyPrintFormat format, PrintRule rule, PrintRule parentRule)
        {
            if (TheorySpecificMeaning != null)
            {
                return false;
            }
            switch (rule.parentheses)
            {
                case PrintRule.ParenthesesSetting.Always:
                    return true;
                case PrintRule.ParenthesesSetting.Never:
                    return false;
                case PrintRule.ParenthesesSetting.Precedence:
                    if (format.history.Count == 0) return false;
                    if (parentRule.precedence < rule.precedence) return false;
                    if (!string.IsNullOrWhiteSpace(parentRule.prefix(false)) &&
                        !string.IsNullOrWhiteSpace(parentRule.suffix(false)))
                    { return false; }
                    if (!string.IsNullOrWhiteSpace(parentRule.prefix(false)) &&
                        !string.IsNullOrWhiteSpace(parentRule.infix(false)) &&
                        format.childIndex == 0)
                    { return false; }
                    if (!string.IsNullOrWhiteSpace(parentRule.infix(false)) &&
                        !string.IsNullOrWhiteSpace(parentRule.suffix(false)) &&
                        format.childIndex == format.history.Last().Item1.Args.Length - 1)
                    { return false; }
                    return format.history.Last().Item1.Name != Name || !rule.associative;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        private static bool linebreaksNecessary(InfoPanelContent content, PrettyPrintFormat format, bool isMultiline, int startLength)
        {
            if (format.maxWidth == 0) return false;
            return isMultiline || (content.Length - startLength > format.maxWidth);
        }

        private static void addLinebreaks(PrintRule rule, InfoPanelContent content,
            Stack<Color> indents, List<int> breakIndices)
        {
            var indentColors = indents.ToList();
            indentColors.Reverse();
            var offset = 0;
            var oldLength = content.Length;
            for (var i = 0; i < breakIndices.Count; i++)
            {
                if (rule.indent && i == breakIndices.Count - 1)
                {
                    indents.Pop();
                    indentColors.RemoveAt(indentColors.Count - 1);
                }

                // add the actual linebreak
                content.Insert(breakIndices[i] + offset, "\n");
                offset += content.Length - oldLength;
                oldLength = content.Length;

                // add the indents
                foreach (var color in indentColors)
                {
                    content.Insert(breakIndices[i] + offset, color == Color.Transparent ? " " : PrintConstants.indentDiff, PrintConstants.DefaultFont, color);
                    offset += content.Length - oldLength;
                    oldLength = content.Length;
                }
            }
        }

        private void addPrefix(PrintRule rule, InfoPanelContent content, ICollection<int> breakIndices)
        {
            var prefix = rule.prefix(isPrime);
            content.Append(prefix);
            if (!string.IsNullOrWhiteSpace(prefix) &&
                rule.prefixLineBreak == PrintRule.LineBreakSetting.After)
            {
                breakIndices.Add(content.Length);
            }
        }

        private void addInfix(PrintRule rule, InfoPanelContent content, ICollection<int> breakIndices)
        {
            content.switchFormat(PrintConstants.DefaultFont, rule.color);
            if (rule.infixLineBreak == PrintRule.LineBreakSetting.Before)
            {
                breakIndices.Add(content.Length);
            }
            content.Append(rule.infix(isPrime));
            if (rule.infixLineBreak == PrintRule.LineBreakSetting.After)
            {
                breakIndices.Add(content.Length);
            }
        }

        private void addSuffix(PrintRule rule, InfoPanelContent content, ICollection<int> breakIndices)
        {
            var suffix = rule.suffix(isPrime);
            content.switchFormat(rule.font ?? PrintConstants.DefaultFont, rule.color);
            if (!string.IsNullOrWhiteSpace(suffix) &&
                rule.suffixLineBreak == PrintRule.LineBreakSetting.Before)
            {
                breakIndices.Add(content.Length);
            }
            content.Append(suffix);
        }

        private bool printChildren(PrettyPrintFormat format, PrintRule rule)
        {
            if (Args.Length == 0)
            {
                return false;
            }
            return !format.rewritingEnabled || rule.printChildren;
        }

        public override string ToString()
        {
            return $"Term[{PrettyName}] Identifier:{id}, #Children:{Args.Length}";
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            SummaryInfo(content);
            content.Append('\n');
            PrettyPrint(content, new Stack<Color>(), format);
        }

        public override bool HasChildren()
        {
            return Args.Length > 0 || dependentTerms.Count > 0;
        }

        public override IEnumerable<Common> Children()
        {
            foreach (var arg in Args)
            {
                yield return arg;
            }
            if (Responsible != null)
            {
                yield return new ForwardingNode("RESPONSIBLE INSTANTIATION", Responsible);
            }

            if (dependentTerms.Count > 0)
            {
                yield return Callback($"YIELDS TERMS [{dependentTerms.Count}]", () => dependentTerms);
            }

            if (dependentInstantiationsBlame.Count > 0)
            {
                yield return Callback($"YIELDS INSTANTIATIONS (BLAME) [{dependentInstantiationsBlame.Count}]", () => dependentInstantiationsBlame);
            }

            if (dependentInstantiationsBind.Count > 0)
            {
                yield return Callback($"YIELDS INSTANTIATIONS (BIND) [{dependentInstantiationsBind.Count}]", () => dependentInstantiationsBind);
            }
        }

        public override void SummaryInfo(InfoPanelContent content)
        {
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("Term Info:\n");
            content.switchToDefaultFormat();
            content.Append("\nIdentifier: " + id).Append('\n');
            content.Append("Number of Children: " + Args.Length).Append('\n');
        }
    }
}