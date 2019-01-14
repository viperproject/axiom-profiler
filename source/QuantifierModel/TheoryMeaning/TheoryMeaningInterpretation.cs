using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AxiomProfiler.QuantifierModel.TheoryMeaning
{
    class TheoryMeaningInterpretation
    {
        public static TheoryMeaningInterpretation singleton = new TheoryMeaningInterpretation();
        private static readonly char[] space = new char[] {' '};
        private static readonly Regex findDivArguments = new Regex(@"^\(\/ (\w+|\(.+\)) (\w+|\(.+\))\)$");
        private static readonly Regex findNegArgument = new Regex(@"^\(- (\w+|\(.+\))\)$");

        private TheoryMeaningInterpretation() {}

        private static string InvertArith(string expr)
        {
            if (expr.StartsWith("-"))
            {
                return expr.Substring(1);
            }
            else
            {
                return "-" + expr;
            }
        }

        public string GetPrettyStringForTheoryMeaning(string theory, string meaning)
        {
            if (meaning.StartsWith("(") && meaning.EndsWith(")"))
            {
                var words = meaning.Substring(1, meaning.Length - 2).Split(space);
                switch (words[0])
                {
                    case "/":
                        {
                            var match = findDivArguments.Match(meaning);
                            if (match.Success)
                            {
                                var arg1 = GetPrettyStringForTheoryMeaning(theory, match.Groups[1].Value);
                                var arg2 = GetPrettyStringForTheoryMeaning(theory, match.Groups[2].Value);
                                var sign = false;
                                if (arg1.StartsWith("-"))
                                {
                                    sign = true;
                                    arg1 = arg1.Substring(1);
                                }
                                if (arg2.StartsWith("-"))
                                {
                                    sign = !sign;
                                    arg2 = arg2.Substring(1);
                                }
                                if (arg1.Contains("/"))
                                {
                                    arg1 = "(" + arg1 + ")";
                                }
                                if (arg2.Contains("/"))
                                {
                                    arg2 = "(" + arg2 + ")";
                                }
                                return (sign ? "-" : "") + arg1 + "/" + arg2;
                            }
                        }
                        break;
                    case "-":
                        {
                            var match = findNegArgument.Match(meaning);
                            if (match.Success) {
                                return InvertArith(GetPrettyStringForTheoryMeaning(theory, match.Groups[1].Value));
                            }
                        }
                        break;
                    case "root-obj":
                        {
                            if (words[1] == "x" && words.Length == 3)
                            {
                                return "0";
                            }
                            else if (words[1] == "(+")
                            {
                                if (words[2] == "x")
                                {
                                    var innerExpr = string.Join(" ", words, 4, words.Length - 4);
                                    return InvertArith(GetPrettyStringForTheoryMeaning(theory, innerExpr));

                                }
                                else if (int.TryParse(words[words.Length - 1], out var rootNumber) && (rootNumber == 1 || rootNumber == 2))
                                {
                                    var neg = rootNumber == 1;
                                    if (words[2] == "(^" && words[3] == "x" && int.TryParse(words[4].Replace(")", ""), out var ithRoot))
                                    {
                                        var innerExpr = string.Join(" ", words, 5, words.Length - 6);
                                        innerExpr = InvertArith(GetPrettyStringForTheoryMeaning(theory, innerExpr));
                                        if (ithRoot % 2 == 0)
                                        {
                                            if (ithRoot == 2)
                                            {
                                                return (neg ? "-" : "") + "sqrt(" + innerExpr + ")";
                                            }
                                            else if (ithRoot % 10 == 2)
                                            {
                                                return (neg ? "-" : "") + ithRoot + "nd_rt(" + innerExpr + ")";
                                            }
                                            else
                                            {
                                                return (neg ? "-" : "") + ithRoot + "th_rt(" + innerExpr + ")";
                                            }
                                        }
                                        else if (neg)
                                        {
                                            if (ithRoot == 1)
                                            {
                                                return innerExpr;
                                            }

                                            var sign = false;
                                            if (innerExpr.StartsWith("-"))
                                            {
                                                sign = true;
                                                innerExpr = innerExpr.Substring(1);
                                            }

                                            if (ithRoot == 3)
                                            {
                                                return (sign ? "-" : "") + "cbrt(" + innerExpr + ")";
                                            }
                                            else
                                            {
                                                var number = (sign ? "-" : "") + ithRoot;
                                                switch (ithRoot % 10)
                                                {
                                                    case 1:
                                                        number += "st";
                                                        break;
                                                    case 3:
                                                        number += "rd";
                                                        break;
                                                    default:
                                                        number += "th";
                                                        break;
                                                }
                                                return number + "_rt(" + innerExpr + ")";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            return meaning;
        }
    }

    abstract class TheoryInterpreter
    {
        public abstract string GetPrettyString(string meaning);
    }
}
