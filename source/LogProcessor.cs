//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AxiomProfiler.QuantifierModel;

namespace AxiomProfiler
{
    public class LogProcessor
    {
        private int curlineNo = 0;
        private int beginCheckSeen = 0;
        private bool interestedInCurrentCheck = true;
        private readonly int checkToConsider = 0; //0 is a special value that forces all checks to be processed
        private int eofSeen = 0;
        private Conflict curConfl;
        private readonly List<Literal> cnflResolveLits = new List<Literal>();
        private Instantiation lastInst;
        private Term decideClause;
        private static readonly Term[] EmptyTerms = new Term[0];
        private readonly Dictionary<string, List<string>> boogieFiles = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> shortnameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Literal> literalById = new Dictionary<int, Literal>();
        private FunSymbol currentFun;
        private int cnflCount;
        private ResolutionLiteral currResRoot, currResNode;
        private bool _modelInState;
        private readonly bool skipDecisions;
        private readonly Dictionary<string, int> literalToTermId = new Dictionary<string, int>();

        //TODO: estimate capacity based on file size
        private readonly Dictionary<Term, Term> proofStepClosures = new Dictionary<Term, Term>(300_000); //every term that is obtained form a certain term
        private readonly Dictionary<Term, HashSet<Term>> proofStepClosuresReverse = new Dictionary<Term, HashSet<Term>>(300_000); //inverse for more efficient updates
        private readonly Dictionary<Term, Term> reverseRewriteClosure = new Dictionary<Term, Term>(40_000); //keeps track of term rewritings by z3
        private readonly Dictionary<Term, HashSet<Term>> reverseRewriteClusureReverse = new Dictionary<Term, HashSet<Term>>(40_000); //inverse for more efficient updates
        private static readonly String[] proofRuleNames =
            { "th-lemma", "hyper-res", "true-axiom", "asserted", "goal", "mp", "refl", "symm", "trans", "trans*", "monotonicity",
            "quant-intro", "distributivity", "and-elim", "not-or-elim", "rewrite", "rewrite*", "pull-quant", "pull-quant*", "push-quant", "elim-unused",
            "der", "quant-inst", "hypothesis", "lemma", "unit-resolution", "iff-true", "iff-false", "commutativity", "def-axiom", "intro-def",
            "apply-def", "iff~", "nnf-pos", "nnf-neg", "nnf*", "cnf*", "sk", "mp~", "th-lemma", "hyper-res" };

        public readonly Model model = new Model();

        private readonly EqualityExplanation[] emptyEqualityExplanation = new EqualityExplanation[0];

        public LogProcessor(List<FileInfo> bplFileInfos, bool skipDecisions, int cons)
        {
            checkToConsider = cons;
            lastInst = null;
            curlineNo = 0;
            this.skipDecisions = skipDecisions;
            if (bplFileInfos != null)
            {
                LoadBoogieFiles(bplFileInfos);
            }
        }

        public void LoadBoogieFiles(List<FileInfo> bplFileInfos)
        {
            List<string> doubleShortNames = new List<string>();
            foreach (FileInfo fi in bplFileInfos)
            {
                string shortname;
                string basename = fi.Name.Replace(".", "").Replace("-", "").Replace("_", "").ToLower();
                if (basename.Length > 8)
                    shortname = basename.Substring(0, 8);
                else
                    shortname = basename;

                if (boogieFiles.ContainsKey(shortname))
                { //CLEMENT: This test is most certainly a bug
                    doubleShortNames.Add(shortname);
                    MessageBox.Show("Overlapping shortname for boogiefiles: " + shortname);
                }
                else if (fi.Exists)
                {
                    shortnameMap[shortname] = fi.Name;
                    List<string> lines = new List<string>();
                    using (TextReader rd = new StreamReader(fi.OpenRead()))
                    {
                        string line;
                        while ((line = rd.ReadLine()) != null)
                        {
                            lines.Add(line);
                        }
                    }
                    boogieFiles[fi.Name] = lines;
                }
            }
            foreach (string shortname in doubleShortNames)
            {
                if (shortnameMap.ContainsKey(shortname))
                {
                    shortnameMap.Remove(shortname);
                }
            }
        }

        string StringReplaceIgnoreCase(string input, string search, string repl)
        {
            string regexppattern = string.Format("^{0}", search);
            Regex re = new Regex(regexppattern, RegexOptions.IgnoreCase);

            return re.Replace(input, repl);
        }

        void loadBoogieToken(Quantifier quant)
        {
            Match match = Regex.Match(quant.Qid, @"^(?<shortname>.+)\.(?<lineNo>[0-9]+):(?<colNo>[0-9]+)$");

            if (!match.Success)
            {
                return;
            }

            var shortname = match.Groups["shortname"].Value;
            var colNo = int.Parse(match.Groups["colNo"].Value) - 1;
            var lineNo = int.Parse(match.Groups["lineNo"].Value) - 1;

            var result = LoadBoogieToken_internal(quant, shortname, colNo, lineNo);
            if (result != null)
                quant.BoogieBody = result.Trim();
        }

        private string LoadBoogieToken_internal(Quantifier quant, string shortname, int colNo, int lineNo)
        {
            if (shortname != "bg" && shortname != "bv" && shortname != "unknown" && shortnameMap.ContainsKey(shortname))
            {
                string fullname = shortnameMap[shortname];
                quant.Qid = StringReplaceIgnoreCase(quant.Qid, shortname, fullname);
                List<string> lines = boogieFiles[fullname];
                if (lines.Count > lineNo && lines[lineNo].Length > colNo)
                {
                    string cur = lines[lineNo] + "\n";
                    int pos = colNo;
                    int lev = 1;

                    string tmp = cur.Substring(pos);
                    if (!tmp.StartsWith("forall") && !tmp.StartsWith("exists"))
                    {
                        string c2 = cur.Substring(0, pos);
                        int x = c2.LastIndexOf("forall");
                        if (x < 0)
                            x = c2.LastIndexOf("exists");
                        if (x >= 0)
                        {
                            pos = x;
                            tmp = cur.Substring(pos);
                        }
                    }

                    if (!(tmp.StartsWith("forall") || tmp.StartsWith("exists")))
                    {
                        while (pos >= 0 && (cur[pos] == '(' || char.IsWhiteSpace(cur[pos]))) pos--;
                        while (pos >= 0 && cur[pos] != '\n') pos--;
                        pos++;
                        return cur.Substring(pos);
                    }
                    else
                    {
                        StringBuilder res = new StringBuilder();
                        int lastRet = 0;
                        while (true)
                        {
                            if (pos >= cur.Length)
                            {
                                ++lineNo;
                                if (lines.Count <= lineNo) break;
                                pos = 0;
                                cur = lines[lineNo] + "\n";
                                continue;
                            }
                            switch (cur[pos])
                            {
                                case ')': lev--; break;
                                case '(': lev++; break;
                                case '\n': lastRet = res.Length; break;
                                default: break;
                            }
                            if (lev == 0) break;
                            res.Append(cur[pos]);
                            pos++;
                        }
                        return res.ToString();
                    }
                }
                else
                {
                    if (lines.Count > 0)
                        Console.WriteLine("not enough lines: {0}", quant.Qid);
                }
            }

            return null;
        }

        Term GetTerm(string key)
        {
            if (key == ";") return null;
            int id = parseIdentifier(RemoveParen(key));
            return model.terms[id];
        }

        private static string RemoveParen(string key)
        {
            if (key[key.Length - 1] == ')')
                key = key.Replace(")", "");
            return key;
        }

        Term[] GetArgs(string[] words, int off, int endExclusive = int.MaxValue)
        {
            if (words.Length <= off)
                return EmptyTerms;
            var exploreEnd = Math.Min(words.Length, endExclusive);
            Term[] args = new Term[exploreEnd - off];
            for (int i = 0; i + off < exploreEnd; ++i)
                args[i] = GetTerm(words[i + off]);
            return args;
        }

        public void ComputeCost()
        {
            if (eofSeen == 0 && beginCheckSeen <= 1)
            {
                Console.WriteLine("Warning: no [eof] marker found; log might be incomplete");
            }
            else if (beginCheckSeen > 1 && checkToConsider == 0)
            {
                Console.WriteLine("This log file contains multiple checks; they will be merged and displayed as one, but the data could be invalid, confusing, or both. Use /c:N (for 1 <= N <= {0}) to show a single one.", beginCheckSeen);
            }

            for (int i = model.instances.Count - 1; i >= 0; i--)
            {
                Instantiation inst = model.instances[i];
                double deps = inst.Responsible.Count(t => t.Responsible != null);
                foreach (Term t in inst.Responsible.Where(t => t.Responsible != null))
                {
                    t.Responsible.Cost += inst.Cost / deps;
                    t.Responsible.Quant.CrudeCost += inst.Cost / deps;
                }
            }
        }

        private long GetId(string w)
        {
            return long.Parse(w.Substring(1));
        }

        private Term GetProofTerm(string w)
        {
            w = RemoveParen(w);
            if (w[0] == '#')
            {
                long id = GetId(RemoveParen(w));
                Common tmp;
                if (model.proofSteps.TryGetValue(id, out tmp) && tmp is Term)
                {
                    return (Term)tmp;
                }
            }
            return new Term(w, EmptyTerms);
        }


        char[] sep = { ' ', '\r', '\n' };

        public void ParseSingleLine(string line)
        {
            curlineNo++;
            if (line == "")
            {
                // ignore blank lines
                return;
            }
            if (beginCheckSeen > checkToConsider && checkToConsider > 0)
            {
                // ignore all lines after the current check number.
                // TODO: implement a way to stop comepletely instead of skipping.
                return;
            }

            line = SanitizeInputLine(line);
            string[] words = line.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            if (ParseModelLine(words))
            {
                return;
            }
            ParseTraceLine(line, words);
        }

        private static string SanitizeInputLine(string line)
        {
            // sanitize input
            if (line[line.Length - 1] == ' ')
            {
                line = line.Substring(0, line.Length - 1);
            }
            if (line.StartsWith("[unit-resolution") || line.StartsWith("[th-lemma"))
            {
                line = "#0 := " + line;
            }
            line = line.Replace("(not #", "(not_#");
            return line;
        }


        private bool ParseModelLine(string[] words)
        {
            if (words.Length == 1)
            {
                switch (words[0])
                {
                    case "}":
                        currentFun = null;
                        return true;
                    case "partitions:": // V1
                    case "Counterexample:":
                        model.NewModel();
                        return true;
                    case ".":
                    case "END_OF_MODEL":
                        eofSeen++;
                        return true;
                }
                if (model.IsV1Part(words[0])) return true;
                return false;
            }

            if (words[0] == "***")
            {
                switch (words[1])
                {
                    case "MODEL":
                        model.NewModel();
                        return true;
                    case "END_MODEL":
                        eofSeen++;
                        _modelInState = false;
                        return true;
                    case "STATE":
                        _modelInState = true;
                        return true;
                }
            }

            if (_modelInState) return true;

            switch (words[0])
            {
                case "labels:":
                case "Z3":
                case "function":
                    return true;
            }

            if (currentFun == null && model.IsV1Part(words[0]) && (words.Length < 3 || words[2] != "{"))
            {
                return ParseV1ModelLine(words);
            }

            if (words.Length < 3) return false;
            return DoParseModelLine(words);
        }

        private bool DoParseModelLine(string[] words)
        {
            if (words[words.Length - 2] != "->") return false;
            if (currentFun != null)
            {
                if (words[0] == "else") return true;
                FunApp fapp = new FunApp();
                fapp.Args = new Partition[words.Length - 2];
                for (int i = 0; i < fapp.Args.Length; ++i)
                    fapp.Args[i] = model.PartitionByName(words[i]);
                fapp.Value = model.PartitionByName(words[words.Length - 1]);
                fapp.Fun = currentFun;
                currentFun.Apps.Add(fapp);
                fapp.Value.Values.Add(fapp);
                return true;
            }
            else if (words.Length == 3)
            {
                FunSymbol fs = model.FunSymbolByName(words[0]);
                if (words[2] == "{")
                {
                    currentFun = fs;
                }
                else
                {
                    FunApp fapp = new FunApp();
                    fapp.Args = new Partition[0];
                    fapp.Fun = fs;
                    fapp.Value = model.PartitionByName(words[words.Length - 1]);
                    fs.Apps.Add(fapp);
                    fapp.Value.Values.Add(fapp);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool ParseV1ModelLine(string[] words)
        {
            if (words.Length == 1) return true;
            for (int i = 0; i < words.Length; ++i)
                words[i] = words[i].Replace("{", "").Replace("}", "").Replace(":int", "");
            int end = words.Length;
            string val = null;
            if (end - 2 > 0 && words[end - 2] == "->")
            {
                end--;
                val = words[end];
                end--;
            }
            string[] tmp = new string[3];
            tmp[1] = "->";
            int err = 0;
            if (val != null)
            {
                Partition part = model.PartitionByName(val);
                model.modelPartsByName[words[0]] = part;
                tmp[2] = val;
            }
            else
            {
                tmp[2] = words[0];
            }
            for (int i = 1; i < end; ++i)
            {
                tmp[0] = words[i];
                if (!DoParseModelLine(tmp)) err++;
            }
            return err == 0;
        }

        private delegate bool Predicate2<in T, in TS>(T t, TS s);

        private static bool ForAll2<T, S>(T[] a1, S[] a2, Predicate2<T, S> pred)
        {
            if (a1.Length != a2.Length)
                throw new ArgumentException();
            return !a1.Where((t, i) => !pred(t, a2[i])).Any();
        }

        private string[] StripGeneration(string[] words, out int x)
        {
            x = -1;
            if (words.Length <= 3 || words[words.Length - 2] != ";")
            {
                return words;
            }

            string[] copy = new string[words.Length - 2];
            Array.Copy(words, copy, copy.Length);
            x = int.Parse(words[words.Length - 1]);
            return copy;
        }

        private static Term Negate(Term a)
        {
            return a.Name == "not" ? a.Args[0] : new Term("not", new []{ a });
        }

        private int parseIdentifier(string logId)
        {
            int id;
            var sanitizedLogId = logId.Replace("#", "");
            if (int.TryParse(sanitizedLogId, out id))
            {
                return id;
            }

            if (literalToTermId.ContainsKey(logId))
            {
                return literalToTermId[logId];
            }
            var term = model.terms.Values.First(t => t.Name == logId);

            if (term == null) throw new FileFormatException($"Cannot parse logfile with term id {logId}!");

            literalToTermId.Add(logId, term.id);
            return term.id;
        }

        internal static Term[] NegateAll(Term[] oargs)
        {
            Term[] args = new Term[oargs.Length];
            for (int i = 0; i < args.Length; ++i)
            {
                args[i] = Negate(oargs[i]);
            }
            return args;
        }

        private IEnumerable<string> SplitEqualities(string explanation)
        {
            var start = 0;
            var depth = 0;
            var len = explanation.Length;
            for (var i = 0; i < len; ++i)
            {
                if (explanation[i] == '(')
                {
                    if (depth == 0) start = i+1;
                    ++depth;
                }
                else if (explanation[i] == ')')
                {
                    --depth;
                    if (depth == 0) yield return explanation.Substring(start, i - start - 1);
                    else if (depth < 0) break;
                }
            }
        }

        private BindingInfo GetBindingInfoFromMatch(string[] logInfo, int off, Term pattern, Term[] bindings)
        {
            int endIndex = logInfo.Length;
            int i = off;
            var topLevelTerms = new List<Term>();
            var explanations = bindings.Select(b => GetExplanationToRoot(b.id)).ToList();
            var bindingsRoots = explanations.Select(e => e.target).ToArray();
            while (i < endIndex)
            {
                if (logInfo[i][0] == '#')
                {
                    topLevelTerms.Add(GetTerm(logInfo[i]));
                    ++i;
                }
                else
                {
                    var sourceId = parseIdentifier(logInfo[i].Substring(1));
                    var targetId = parseIdentifier(logInfo[i + 1].Substring(0, logInfo[i + 1].Length - 1));
                    explanations.Add(GetExplanation(sourceId, targetId));
                    i += 2;
                }
            }
            return new BindingInfo(pattern, bindingsRoots, topLevelTerms, explanations);
        }

        private class ExplanationFinalizer : EqualityExplanationVisitor<EqualityExplanation, LogProcessor>
        {
            public override EqualityExplanation Direct(DirectEqualityExplanation target, LogProcessor arg)
            {
                return target;
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, LogProcessor arg)
            {
                return target;
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, LogProcessor arg)
            {
                var argumentExplanations = target.sourceArgumentEqualities.Select(e => arg.GetExplanation(e.source.id, e.target.id));
                return new CongruenceExplanation(target.source, target.target, argumentExplanations.ToArray());
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, LogProcessor arg)
            {
                return target;
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, LogProcessor arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private class ExplanationReverser : EqualityExplanationVisitor<EqualityExplanation, object>
        {
            public override EqualityExplanation Direct(DirectEqualityExplanation target, object arg)
            {
                return new DirectEqualityExplanation(target.target, target.source, target.equality);
            }

            public override EqualityExplanation Transitive(TransitiveEqualityExplanation target, object arg)
            {
                var numEqualities = target.equalities.Length;
                var reversedChildren = new EqualityExplanation[numEqualities];
                for (var i = 0; i < numEqualities; ++i)
                {
                    reversedChildren[i] = visit(target.equalities[numEqualities - i - 1], arg);
                }
                return new TransitiveEqualityExplanation(target.target, target.source, reversedChildren);
            }

            public override EqualityExplanation Congruence(CongruenceExplanation target, object arg)
            {
                var reversedChildren = new EqualityExplanation[target.sourceArgumentEqualities.Length];
                foreach (var equality in target.sourceArgumentEqualities)
                {
                    for (var newIndex = Array.FindIndex(target.target.Args, t => t.id == equality.target.id); newIndex != -1; newIndex = Array.FindIndex(target.target.Args, newIndex + 1, t => t.id == equality.target.id))
                    {
                        if (reversedChildren[newIndex] == null)
                        {
                            reversedChildren[newIndex] = visit(equality, arg);
                            break;
                        }
                    }
                }
                return new CongruenceExplanation(target.target, target.source, reversedChildren);
            }

            public override EqualityExplanation Theory(TheoryEqualityExplanation target, object arg)
            {
                return new TheoryEqualityExplanation(target.target, target.source, target.TheoryName);
            }

            public override EqualityExplanation RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private class ExplanationExtractor : EqualityExplanationVisitor<IEnumerable<EqualityExplanation>, object>
        {
            public override IEnumerable<EqualityExplanation> Direct(DirectEqualityExplanation target, object arg)
            {
                yield return target;
            }

            public override IEnumerable<EqualityExplanation> Transitive(TransitiveEqualityExplanation target, object arg)
            {
                return target.equalities;
            }

            public override IEnumerable<EqualityExplanation> Congruence(CongruenceExplanation target, object arg)
            {
                yield return target;
            }

            public override IEnumerable<EqualityExplanation> Theory(TheoryEqualityExplanation target, object arg)
            {
                yield return target;
            }

            public override IEnumerable<EqualityExplanation> RecursiveReference(RecursiveReferenceEqualityExplanation target, object arg)
            {
                throw new InvalidOperationException("Equality explanation shouldn't already be generalized!");
            }
        }

        private readonly ExplanationFinalizer explanationFinalizer = new ExplanationFinalizer();
        private readonly ExplanationReverser explanationReverser = new ExplanationReverser();
        private readonly ExplanationExtractor explanationExtractor = new ExplanationExtractor();

        private readonly Dictionary<string, EqualityExplanation> equalityExplanationCache = new Dictionary<string, EqualityExplanation>();

        private EqualityExplanation GetExplanationToRoot(int id)
        {
            var explanations = new List<EqualityExplanation>();
            int it;
            for (it = id; model.equalityExplanations.TryGetValue(it, out var explanation); it = explanation.target.id)
            {
                explanations.Add(explanation);
            }
            if (explanations.Count() == 1)
            {
                return explanations.Single();
            }
            else
            {
                return new TransitiveEqualityExplanation(model.terms[id], model.terms[it], explanations.Select(ee => explanationFinalizer.visit(ee, this)).ToArray());
            }
        }

        private EqualityExplanation GetExplanation(int sourceId, int targetId)
        {
            var key = $"{sourceId}, {targetId}";
            if (equalityExplanationCache.TryGetValue(key, out var existing)) return existing;

            EqualityExplanation knownExplanation = null;
            var sourceExplanations = new List<EqualityExplanation>();
            for (int sourceIterator = sourceId; model.equalityExplanations.TryGetValue(sourceIterator, out var equalityExplanation); sourceIterator = equalityExplanation.target.id)
            {
                if (sourceIterator == targetId)
                {
                    var targetTerm = model.terms[targetId];
                    knownExplanation = new TransitiveEqualityExplanation(targetTerm, targetTerm, emptyEqualityExplanation);
                    break;
                }
                if (equalityExplanationCache.TryGetValue($"{sourceIterator}, {targetId}", out knownExplanation)) break;
                sourceExplanations.Add(equalityExplanation);
            }
            var targetExplanations = new List<EqualityExplanation>();
            if (knownExplanation == null)
            {
                for (int targetIterator = targetId; model.equalityExplanations.TryGetValue(targetIterator, out var equalityExplanation); targetIterator = equalityExplanation.target.id)
                {
                    if (targetIterator == sourceId)
                    {
                        var sourceTerm = model.terms[sourceId];
                        knownExplanation = new TransitiveEqualityExplanation(sourceTerm, sourceTerm, emptyEqualityExplanation);
                        break;
                    }
                    if (equalityExplanationCache.TryGetValue($"{sourceId}, {targetIterator}", out knownExplanation)) break;
                    targetExplanations.Add(equalityExplanation);
                }
            }

            if (knownExplanation != null)
            {
                if (knownExplanation.source.id == sourceId)
                {
                    var explanations = explanationExtractor.visit(knownExplanation, null).Concat(targetExplanations.Select(e => explanationReverser.visit(e, null)).Select(e => explanationFinalizer.visit(e, this)).Reverse());
                    var explanation = new TransitiveEqualityExplanation(model.terms[sourceId], model.terms[targetId], explanations.ToArray());
                    equalityExplanationCache[key] = explanation;
                    return explanation;
                }
                else
                {
                    var explanations = sourceExplanations.Select(e => explanationFinalizer.visit(e, this)).Concat(explanationExtractor.visit(knownExplanation, null));
                    var explanation = new TransitiveEqualityExplanation(model.terms[sourceId], model.terms[targetId], explanations.ToArray());
                    equalityExplanationCache[key] = explanation;
                    return explanation;
                }
            }

            sourceExplanations.Reverse();
            targetExplanations.Reverse();

            var sourceCheck = sourceExplanations.Any() ? sourceExplanations.First().target.id : sourceId;
            var targetCheck = targetExplanations.Any() ? targetExplanations.First().target.id : targetId;
            if (sourceCheck != targetCheck) throw new ArgumentException("The terms provided to GetExplanation() were not equal");

            var numSource = sourceExplanations.Count;
            var numTarget = targetExplanations.Count;
            if (numSource < numTarget)
            {
                sourceExplanations.AddRange(Enumerable.Repeat<EqualityExplanation>(null, numTarget - numSource));
            }
            else
            {
                targetExplanations.AddRange(Enumerable.Repeat<EqualityExplanation>(null, numSource - numTarget));
            }

            var withoutCommonPrefix = sourceExplanations.Zip(targetExplanations, Tuple.Create).SkipWhile(t => t.Item1 != null && t.Item1.Equals(t.Item2));
            var relevantSourceExplanations = withoutCommonPrefix.Select(t => t.Item1).Reverse().Where(e => e != null);
            var relevantTargetExplanations = withoutCommonPrefix.Select(t => t.Item2).Where(e => e != null).Select(e => explanationReverser.visit(e, null));
            var explanationPath = relevantSourceExplanations.Concat(relevantTargetExplanations).Select(e => explanationFinalizer.visit(e, this));
            if (explanationPath.Take(2).Count() == 1)
            {
                var explanation = explanationPath.Single();
                equalityExplanationCache[key] = explanation;
                return explanation;
            }
            else
            {
                var explanation = new TransitiveEqualityExplanation(model.terms[sourceId], model.terms[targetId], explanationPath.ToArray());
                equalityExplanationCache[key] = explanation;
                return explanation;
            }
        }

        private void ParseTraceLine(string line, string[] words)
        {
            switch (words[0])
            {
                case "[mk-quant]":
                    {
                        Term[] args = GetArgs(words, 3);
                        Term t = new Term("FORALL", args)
                        {
                            id = parseIdentifier(words[1])
                        };
                        model.terms[parseIdentifier(words[1])] = t;

                        if (args.Length != 0)
                        {
                            Quantifier q = CreateQuantifier(words[1], words[2]);
                            q.BodyTerm = t;
                            if (words[2] != "null")
                                q.PrintName = words[2] + "[" + words[1] + "]";
                            else
                                q.PrintName = words[1];
                        }
                    }
                    break;

                case "[mk-var]":
                    {
                        var id = parseIdentifier(words[1]);
                        model.terms[id] = new Term("qvar_" + id, EmptyTerms);
                    }
                    break;

                case "[mk-app]":
                    {
                        Term[] args = GetArgs(words, 3);

                        //We are only interested in the result of a proof step (i.e. the last argument)
                        Term t = proofRuleNames.Contains(words[2]) ? new Term(args.Last()) : new Term(words[2], args);
                        if (proofRuleNames.Contains(words[2]))
                        {
                            if (words[2] == "rewrite" || words[2] == "rewrite*" || words[2] == "unit-resolution")
                            {
                                Term from, to;
                                if (words[2] == "unit-resolution")
                                {
                                    from = args.First();
                                    to = t;
                                }
                                else
                                {
                                    var equality = args.Last();
                                    if ((equality.Name != "=" && equality.Name != "iff" && equality.Name != "~") || equality.Args.Count() != 2)
                                    {
                                        if (equality.id == -1) return;
                                        throw new Exception("Unexpected result term for rewrite proof step.");
                                    }
                                    //TODO: result always rhs?
                                    from = GetOrId(reverseRewriteClosure, equality.Args[0]);
                                    to = equality.Args[1];
                                }
                                reverseRewriteClosure[to] = from;

                                if (!reverseRewriteClusureReverse.TryGetValue(from, out var fromPaths))
                                {
                                    fromPaths = new HashSet<Term>();
                                    reverseRewriteClusureReverse.Add(from, fromPaths);
                                }
                                fromPaths.Add(to);

                                if (reverseRewriteClusureReverse.TryGetValue(to, out var pathBeginnings))
                                {
                                    fromPaths.UnionWith(pathBeginnings);

                                    foreach (var pathBeginning in pathBeginnings)
                                    {
                                        reverseRewriteClosure[pathBeginning] = from;
                                    }
                                }
                            }

                            var prerequisiteClosure = new HashSet<Term>(args);
                            foreach (var arg in args)
                            {
                                if (proofStepClosuresReverse.TryGetValue(arg, out var closure))
                                {
                                    prerequisiteClosure.UnionWith(closure);
                                }
                            }
                            var newProofStepClosure = GetOrId(proofStepClosures, t);

                            if (!proofStepClosuresReverse.TryGetValue(newProofStepClosure, out var reverse))
                            {
                                proofStepClosuresReverse[newProofStepClosure] = prerequisiteClosure;
                            }
                            else
                            {
                                reverse.UnionWith(prerequisiteClosure);
                                proofStepClosuresReverse[newProofStepClosure] = reverse;
                            }

                            foreach (var prerequisite in prerequisiteClosure)
                            {
                                proofStepClosures[prerequisite] = newProofStepClosure;
                            }
                        }
                        model.terms[parseIdentifier(words[1])] = t;
                        t.id = parseIdentifier(words[1]);
                    }
                    break;
                    
                case "[attach-enode]":
                    {
                        Term t = GetTerm(words[1]);
                        int gen = int.Parse(words[2]);
                        if (lastInst != null && t.Responsible != null && t.Responsible != lastInst)
                        {
                            // make a copy of the term, since we are overriding the Responsible field
                            //TODO: shallow copy
                            t = new Term(t);
                            model.terms[parseIdentifier(words[1])] = t;
                        }
                        else if (lastInst != null)
                        {
                            t.Responsible = lastInst;
                            lastInst.dependentTerms.Add(t);
                        }
                    }
                    break;

                case "[eq-expl]":
                    {
                        var fromId = parseIdentifier(words[1]);
                        if (model.equalityExplanations.ContainsKey(fromId)) equalityExplanationCache.Clear();
                        if (words[2] == "root")
                        {
                            model.equalityExplanations.Remove(fromId);
                            break;
                        }
                        var fromTerm = model.terms[fromId];
                        var toTerm = GetTerm(words.Last());
                        switch (words[2])
                        {
                            case "lit":
                                model.equalityExplanations[fromId] = new DirectEqualityExplanation(fromTerm, toTerm, GetTerm(words[3]));
                                break;
                            case "cg":
                                var equalitiesRegex = new Regex("\\((#[0-9]+) (#[0-9]+)\\) ");
                                var matches = equalitiesRegex.Matches(line).Cast<Match>();
                                if (matches.Any())
                                {
                                    var argumentEqualities = matches.Select(match => new TransitiveEqualityExplanation(GetTerm(match.Groups[1].Value), GetTerm(match.Groups[2].Value), emptyEqualityExplanation)).ToArray();
                                    model.equalityExplanations[fromId] = new CongruenceExplanation(fromTerm, toTerm, argumentEqualities);
                                }
                                else
                                {
                                    Console.Out.WriteLine($"Congruence equality explanation had unexpected shape (line: {curlineNo})");
                                    model.equalityExplanations[fromId] = new TransitiveEqualityExplanation(fromTerm, toTerm, emptyEqualityExplanation);
                                }
                                break;
                            case "ax":
                                model.equalityExplanations[fromId] = new TransitiveEqualityExplanation(fromTerm, toTerm, emptyEqualityExplanation);
                                break;
                            case "th":
                                model.equalityExplanations[fromId] = new TheoryEqualityExplanation(fromTerm, toTerm, words[3]);
                                break;
                            default:
                                Console.Out.WriteLine($"Unexpected equality explanation: {words[2]} (line: {curlineNo})");
                                model.equalityExplanations[fromId] = new TransitiveEqualityExplanation(fromTerm, toTerm, emptyEqualityExplanation);
                                break;
                        }
                    }
                    break;

                case "[new-match]":
                    {
                        if (!interestedInCurrentCheck) break;
                        if (words.Length < 3) break;
                        var separationIndex = Array.FindIndex(words, el => el == ";");
                        Term[] args = GetArgs(words, 4, separationIndex);
                        var quant = model.quantifiers[words[2]];
                        var pattern = GetTerm(words[3]);
                        if (pattern.Name != "pattern") throw new InvalidOperationException($"Expected pattern but found {pattern}.");
                        var bindingInfo = GetBindingInfoFromMatch(words, separationIndex + 1, pattern, args);
                        Instantiation inst = new Instantiation(bindingInfo)
                        {
                            Quant = quant
                        };
                        model.fingerprints[words[1]] = inst;
                    }
                    break;

                case "[instance]":
                    {
                        if (!interestedInCurrentCheck) break;
                        Instantiation inst;
                        if (!model.fingerprints.TryGetValue(words[1], out inst))
                        {
                            Console.WriteLine("fingerprint not found {0} {1}", words[0], words[1]);
                            break;
                        }
                        if (inst.LineNo != 0)
                        {
                            inst = inst.Copy();
                        }
                        int pos = 2;
                        if (words.Length > pos && words[pos] != ";")
                        {
                            long id = GetId(words[pos]);
                            var t = model.terms[(int)id];
                            Term quantImpliesBody = GetOrId(proofStepClosures, model.terms[(int) id]);

                            if (quantImpliesBody.Name == "or")
                            {
                                var bodyChildren = new List<Term>();
                                foreach (var child in quantImpliesBody.Args)
                                {
                                    if (child.Name != "not" || child.Args.First().Name != "FORALL")
                                    {
                                        bodyChildren.Add(child);
                                    }
                                }
                                if (bodyChildren.Count == 0)
                                {
                                    throw new Exception("Body couldn't be found");
                                }
                                else if (bodyChildren.Count == 1)
                                {
                                    inst.concreteBody = bodyChildren.First();
                                }
                                else
                                {
                                    inst.concreteBody = new Term("or", bodyChildren.ToArray())
                                    {
                                        id = quantImpliesBody.id
                                    };
                                }
                            }
                            else
                            {
                                inst.concreteBody = quantImpliesBody;
                            }
                            pos++;
                        }
                        else
                        {
                            throw new OldLogFormatException();
                        }
                        if (words.Length - 1 > pos && words[pos] == ";")
                        {
                            ++pos;
                            inst.Z3Generation = int.Parse(words[pos]);
                            ++pos;
                        }
                        else
                        {
                            inst.Z3Generation = -1;
                        }
                        AddInstance(inst);
                    }
                    break;
                case "[end-of-instance]": lastInst = null; break;

                case "[decide-and-or]":
                    if (!interestedInCurrentCheck) break;
                    if (words.Length >= 2)
                        decideClause = GetTerm(words[1]);
                    break;

                // we're getting [assign] anyhow
                case "[decide]": break;

                case "[assign]":
                    {
                        if (!interestedInCurrentCheck) break;
                        if (skipDecisions || words.Length < 2) break;
                        ScopeDesc d = model.scopes[model.scopes.Count - 1];
                        Literal l = GetLiteral(words[1], false);
                        if (d.Literal == null)
                            d.Literal = l;
                        else
                            d.Implied.Add(l);
                        int pos = 2;
                        if (pos < words.Length && words[pos] == "decision")
                            pos++;
                        if (pos < words.Length)
                        {
                            string kw = words[pos++];
                            switch (kw)
                            {
                                case "clause":
                                case "bin-clause":
                                    Term[] expl = new Term[words.Length - pos];
                                    for (int i = 0; i < expl.Length; ++i)
                                        expl[i] = GetLiteralTerm(words[pos++]);
                                    l.Explanation = expl;
                                    break;
                                case "justification": break;
                                default:
                                    if (kw != "axiom")
                                        l.Explanation = new Term[] { GetTerm(kw) };
                                    break;
                            }
                        }
                    }
                    break;

                case "[push]":
                    if (!interestedInCurrentCheck) break;
                    if (!skipDecisions)
                        model.PushScope(beginCheckSeen);
                    break;

                case "[pop]":
                    if (!interestedInCurrentCheck) break;
                    if (skipDecisions || words.Length < 2) break;
                    model.PopScopes(int.Parse(words[1]), curConfl, beginCheckSeen);
                    curConfl = null;
                    break;

                case "[begin-check]":
                    beginCheckSeen++;
                    interestedInCurrentCheck = checkToConsider == 0 || checkToConsider == beginCheckSeen;
                    break;

                case "[query-done]":
                    if (interestedInCurrentCheck && checkToConsider > 0) eofSeen++;
                    break;

                case "[eof]":
                    eofSeen++;
                    break;

                case "[resolve-process]":
                    if (!interestedInCurrentCheck) break;
                    if (skipDecisions || words.Length < 2) break;
                    currResNode = new ResolutionLiteral();
                    currResNode.Term = GetLiteralTerm(words[1], out currResNode.Negated, out currResNode.Id);
                    if (currResRoot == null)
                    {
                        currResRoot = currResNode;
                    }
                    else
                    {
                        var t = currResRoot.Find(currResNode.Id);
                        if (t != null)
                            currResNode = t;
                        else
                            Console.WriteLine("cannot attach to conflict {0}", words[1]);
                    }
                    break;

                case "[resolve-lit]":
                    if (!interestedInCurrentCheck) break;
                    if (skipDecisions || words.Length < 3 || currResNode == null) break;
                    {
                        var l = new ResolutionLiteral();
                        l.Term = GetLiteralTerm(words[2], out l.Negated, out l.Id);
                        l.LevelDifference = int.Parse(words[1]);
                        currResNode.Results.Add(l);
                    }
                    break;

                case "[conflict]":
                    if (!interestedInCurrentCheck) break;
                    if (skipDecisions) break;
                    curConfl = new Conflict();
                    curConfl.Id = cnflCount++;
                    curConfl.ResolutionLits = cnflResolveLits.ToArray();
                    cnflResolveLits.Clear();
                    curConfl.LineNo = curlineNo;
                    curConfl.Cost = curlineNo;
                    if (model.conflicts.Count > 0)
                        curConfl.Cost -= model.conflicts[model.conflicts.Count - 1].LineNo;
                    model.conflicts.Add(curConfl);
                    curConfl.ResolutionRoot = currResRoot;
                    currResRoot = null;
                    currResNode = null;
                    for (int i = 1; i < words.Length; ++i)
                    {
                        string w = words[i];
                        Literal lit = GetLiteral(w, false);
                        if (lit != null)
                            curConfl.Literals.Add(lit);
                    }
                    break;

                // obsolete
                case "[mk-enode]":
                case "[mk-bool-var]":
                    {
                        int generation;
                        if (words.Length < 3)
                        {
                            break;
                        }
                        words = StripGeneration(words, out generation);
                        Term[] args = GetArgs(words, 3);
                        Term t;
                        if (lastInst == null &&
                            model.terms.TryGetValue(parseIdentifier(words[1]), out t) &&
                            t.Name == words[2] && t.Args.Length == args.Length &&
                            ForAll2(t.Args, args, delegate (Term x, Term s) { return x == s; }))
                        {
                            // nothing
                        }
                        else
                        {
                            t = new Term(words[2], args)
                            {
                                Responsible = lastInst,
                                id = parseIdentifier(words[1])
                            };
                            lastInst?.dependentTerms.Add(t);
                            model.terms[parseIdentifier(words[1])] = t;
                        }
                    }
                    break;

                // V1 stuff
                case "[create]":
                    {
                        if (words.Length < 4) break;
                        Term[] args = GetArgs(words, 4);
                        Term t = new Term(args.Length == 0 ? words[3] : words[3].Substring(1), args);
                        t.Responsible = lastInst;
                        lastInst?.dependentTerms.Add(t);
                        model.terms[parseIdentifier(words[1])] = t;
                        t.id = parseIdentifier(words[1]);
                    }
                    break;
                case "[mk_const]": if (words.Length < 2) break; model.terms.Remove(parseIdentifier(words[1])); break;
                case "[create_ite]": if (words.Length < 2) break; model.terms.Remove(parseIdentifier(words[1])); break;

                case "[done-instantiate-fp]": lastInst = null; break;
                case "[conflict-resolve]":
                    if (!interestedInCurrentCheck) break;
                    if (skipDecisions) break;
                    cnflResolveLits.Add(GetLiteral(words[1], true));
                    break;
                case "[conflict-lit]":
                    if (curConfl != null && words.Length >= 2)
                    {
                        Literal lit = new Literal();
                        curConfl.Literals.Add(lit);
                        int pos = 1;
                        if (words[pos] == "(not")
                        {
                            lit.Negated = true;
                            pos++;
                        }
                        if (words.Length <= pos) break;
                        string no = words[pos].Substring(1).Replace(":", "").Replace(")", "");
                        if (!int.TryParse(no, out lit.Id))
                            Console.WriteLine("cannot get literal number of {0}", no);
                        pos++;
                        if (words.Length <= pos) break;
                        if (words[pos] == "not") pos++;
                        if (words.Length <= pos) break;
                        string sym = words[pos].Replace("(", "");
                        pos++;
                        if (sym != "FORALL")
                        {
                            for (int i = pos; i < words.Length; i++)
                            {
                                int idx = words[i].IndexOf(":");
                                if (idx > 0) words[i] = words[i].Substring(0, idx);
                            }
                            lit.Term = new Term(sym, GetArgs(words, pos));
                        }
                    }
                    break;
                case "[end-conflict]": break;


                case "[used]": break;
                case "WARNING:": break;
                default:
                    Console.WriteLine("wrong line: '{0}'", line);
                    break;
            }
        }

        private static T GetOrId<T>(Dictionary<T, T> dict, T key)
        {
            if (!dict.TryGetValue(key, out var returnValue))
            {
                returnValue = key;
            }
            return returnValue;
        }

        public void Finish()
        {
            //add reverse rewrites to terms
            foreach (var reverseRewrite in reverseRewriteClosure)
            {
                reverseRewrite.Key.reverseRewrite = reverseRewrite.Value;
            }

            //unify quantifiers with the same name and body
            var unifiedQuantifiers = model.quantifiers.Values.GroupBy(quant => quant.Qid).SelectMany(group => {
                var patternsForBodies = new Dictionary<Term, Tuple<Quantifier, HashSet<Term>>>();
                foreach (var quant in group)
                {
                    var separation = quant.BodyTerm.Args.ToLookup(arg => arg.Name == "pattern");
                    if (separation[false].Count() != 1) throw new Exception();
                    if (!patternsForBodies.TryGetValue(separation[false].First(), out var collectedPatterns))
                    {
                        collectedPatterns = Tuple.Create(quant, new HashSet<Term>());
                        patternsForBodies[separation[false].First()] = collectedPatterns;
                    }
                    foreach (var inst in quant.Instances)
                    {
                        inst.Quant = collectedPatterns.Item1;
                    }
                    collectedPatterns.Item2.UnionWith(separation[true]);
                }
                foreach (var found in patternsForBodies)
                {
                    var quant = found.Value.Item1;
                    quant.BodyTerm = new Term(quant.BodyTerm.Name, found.Value.Item2.Concat(new Term[] { found.Key }).ToArray())
                    {
                        id = quant.BodyTerm.id
                    };
                }
                return patternsForBodies.Select(kv => kv.Value.Item1);
            }).ToList();
            model.quantifiers.Clear();
            foreach (var quant in unifiedQuantifiers)
            {
                model.quantifiers.Add("#" + quant.BodyTerm.id, quant);
            }

            //code used to test several heuristics for choosing a path to try to find a matching loop on
            /*var random = new Random();
            Console.Out.WriteLine("start");
            foreach (var i in new int[] { 30715 })//Enumerable.Range(0, 100))
            {
                var randomIndex = i;// random.Next(model.instances.Count);
                var randomInstantiation = model.instances[randomIndex];
                var graph = new HashSet<Instantiation> { randomInstantiation };
                AddUpNodes(1000, graph);
                AddDownNodes(1000, graph);
                /*var p0 = PathSelect(graph, randomInstantiation, (path, pre, post) => InstantiationPathScoreFunction1(path, pre, post, 1));
                var p = PathSelect(graph, randomInstantiation, (path, pre, post) => InstantiationPathScoreFunction2(path, pre, post, 0.05));
                if (!p.getInstantiations().SequenceEqual(p0.getInstantiations())) {
                    Console.Out.WriteLine($"1: {randomIndex}, {p0.TryGetCyclePath(out var x)}: {p0.Length()}, {p.TryGetCyclePath(out var y)}: {p.Length()}");
                    if (p0.TryGetLoop(out var cycle0) && p.TryGetLoop(out var cycle1))
                    {
                        Console.Out.WriteLine(cycle0.SequenceEqual(cycle1));
                        Console.Out.WriteLine($"{p0.GetNumRepetitions()}, {p.GetNumRepetitions()}");
                    }
                    continue;
                }*/
                //var p0 = PathSelect(graph, randomInstantiation, (path, pre, post) => InstantiationPathScoreFunction3(path, pre, post, 0.2));
                /*var p0 = PathSelect(graph, randomInstantiation, (path, pre, post) => InstantiationPathScoreFunction3(path, pre, post, 0.3));
                var p = PathSelect(graph, randomInstantiation, (path, pre, post) => InstantiationPathScoreFunction3_(path, pre, post, 0.3));
                if (!p.getInstantiations().SequenceEqual(p0.getInstantiations()))
                {
                    Console.Out.WriteLine($"2: {randomIndex}, {p0.TryGetCyclePath(out var x)}: {p0.Length()}, {p.TryGetCyclePath(out var y)}: {p.Length()}");
                    Console.Out.WriteLine($"{InstantiationPathScoreFunction3(p0, true, false, 0.3)}, {InstantiationPathScoreFunction3_(p0, true, false, 0.3)}");
                    Console.Out.WriteLine($"{InstantiationPathScoreFunction3(p, true, false, 0.3)}, {InstantiationPathScoreFunction3_(p, true, false, 0.3)}");
                    if (p0.TryGetLoop(out var cycle0) && p.TryGetLoop(out var cycle1))
                    {
                        var selected = Tuple.Create(randomInstantiation.Quant, randomInstantiation.bindingInfo.fullPattern);
                        Console.Out.WriteLine($"{cycle0.Contains(selected)}, {cycle1.Contains(selected)}");
                        Console.Out.WriteLine(cycle0.SequenceEqual(cycle1));
                        Console.Out.WriteLine($"{p0.GetNumRepetitions()}, {p.GetNumRepetitions()}");
                    }
                    continue;
                }
                /*p = PathSelect(graph, randomInstantiation, (path, pre, post) => InstantiationPathScoreFunction4(path, pre, post, 5));
                if (!p.getInstantiations().SequenceEqual(p0.getInstantiations()))
                {
                    Console.Out.WriteLine($"3: {randomIndex}, {p0.TryGetCyclePath(out var x)}: {p0.Length()}, {p.TryGetCyclePath(out var y)}: {p.Length()}");
                    if (p0.TryGetLoop(out var cycle0) && p.TryGetLoop(out var cycle1))
                    {
                        Console.Out.WriteLine(cycle0.SequenceEqual(cycle1));
                        Console.Out.WriteLine($"{p0.GetNumRepetitions()}, {p.GetNumRepetitions()}");
                    }
                    continue;
                }*/
                //Console.Out.WriteLine("iteration");
            //}
        }

        /*private static double InstantiationPathScoreFunction1(InstantiationPath instantiationPath, bool eliminatePrefix, bool eliminatePostfix, double factor)
        {
            var statistics = instantiationPath.Statistics();
            var expectation = statistics.Average(dataPoint => dataPoint.Item2);
            var stddev = Math.Sqrt(statistics.Sum(dataPoint => Math.Pow(dataPoint.Item2 - expectation, 2)) / statistics.Count());
            var eliminationTreshhold = Math.Max(expectation - factor * stddev, 2);
            var outliers = (eliminatePrefix ? statistics.TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            outliers = outliers.Concat(eliminatePostfix ? statistics.Skip(outliers.Count()).Reverse().TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            return 1.0 * (instantiationPath.Length() - outliers.Sum(dataPoint => dataPoint.Item2)) / (statistics.Count() - outliers.Count());
        }

        private static double InstantiationPathScoreFunction2(InstantiationPath instantiationPath, bool eliminatePrefix, bool eliminatePostfix, double percentile)
        {
            var statistics = instantiationPath.Statistics();
            var eliminationTreshhold = Math.Max(instantiationPath.Length() * percentile, 2);
            var outliers = (eliminatePrefix ? statistics.TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            outliers = outliers.Concat(eliminatePostfix ? statistics.Skip(outliers.Count()).Reverse().TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            return 1.0 * (instantiationPath.Length() - outliers.Sum(dataPoint => dataPoint.Item2)) / (statistics.Count() - outliers.Count());
        }

        private static double InstantiationPathScoreFunction3(InstantiationPath instantiationPath, bool eliminatePrefix, bool eliminatePostfix, double percentile)
        {
            var statistics = instantiationPath.Statistics();
            var eliminationTreshhold = Math.Max(statistics.Max(dp => dp.Item2) * percentile, 0);
            var outliers = (eliminatePrefix ? statistics.TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            outliers = outliers.Concat(eliminatePostfix ? statistics.Skip(outliers.Count()).Reverse().TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            return 1.0 * (instantiationPath.Length() - outliers.Sum(dataPoint => dataPoint.Item2)) / (statistics.Count() - outliers.Count());
        }

        private static double InstantiationPathScoreFunction3_(InstantiationPath instantiationPath, bool eliminatePrefix, bool eliminatePostfix, double percentile)
        {
            var statistics = instantiationPath.Statistics();
            var eliminationTreshhold = Math.Max(statistics.Max(dp => dp.Item2) * percentile, 0);
            //var eliminatableQuantifiers = statistics.Where(dp => dp.Item2 <= eliminationTreshhold).Select(dp => dp.Item1);
            var eliminatableQuantifiers = new HashSet<Tuple<Quantifier, Term>>();
            foreach (var quant in statistics.Where(dp => dp.Item2 <= eliminationTreshhold).Select(dp => dp.Item1))
            {
                eliminatableQuantifiers.Add(quant);
            }

            var clearSpace = (int) Math.Floor(eliminationTreshhold);
            var remainingInstantiations = instantiationPath.getInstantiations();
            if (eliminatePostfix) remainingInstantiations = remainingInstantiations.Reverse();
            while (remainingInstantiations.Take(clearSpace).Any(inst => eliminatableQuantifiers.Contains(Tuple.Create(inst.Quant, inst.bindingInfo.fullPattern))))
            {
                remainingInstantiations = remainingInstantiations.Skip(1);
            }

            if (remainingInstantiations.Count() == 0) return -1;

            var remainingPath = new InstantiationPath();
            foreach (var inst in remainingInstantiations)
            {
                remainingPath.append(inst);
            }

            return 1.0 * remainingPath.Length() / remainingPath.Statistics().Count();
        }

        private static double InstantiationPathScoreFunction4(InstantiationPath instantiationPath, bool eliminatePrefix, bool eliminatePostfix, double diff)
        {
            var statistics = instantiationPath.Statistics();
            var eliminationTreshhold = Math.Max(statistics.Max(dp => dp.Item2) - diff, 2);
            var outliers = (eliminatePrefix ? statistics.TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            outliers = outliers.Concat(eliminatePostfix ? statistics.Skip(outliers.Count()).Reverse().TakeWhile(dataPoint => dataPoint.Item2 <= eliminationTreshhold) : Enumerable.Empty<Tuple<Tuple<Quantifier, Term>, int>>());
            return 1.0 * (instantiationPath.Length() - outliers.Sum(dataPoint => dataPoint.Item2)) / (statistics.Count() - outliers.Count());
        }

        private static void AddUpNodes(int limit, ISet<Instantiation> graph)
        {
            List<Instantiation> generation = graph.ToList();
            var added = 0;
            while (added < limit)
            {
                generation = generation.SelectMany(i => i.ResponsibleInstantiations).ToList();
                graph.UnionWith(generation.Take(limit-added));
                added += generation.Count;
                if (generation.Count == 0) break;
            }
        }

        private static void AddDownNodes(int limit, ISet<Instantiation> graph)
        {
            List<Instantiation> generation = graph.ToList();
            var added = 0;
            while (added < limit)
            {
                generation = generation.SelectMany(i => i.DependantInstantiations).ToList();
                graph.UnionWith(generation.Take(limit - added));
                added += generation.Count;
                if (generation.Count == 0) break;
            }
        }

        private static InstantiationPath PathSelect(ISet<Instantiation> nodes, Instantiation startNode, Func<InstantiationPath, bool, bool, double> scoreFunction)
        {
            // building path downwards:
            var bestDownPath = BestDownPath(nodes, startNode, scoreFunction);
            InstantiationPath bestUpPath;
            if (bestDownPath.TryGetLoop(out var loop))
            {
                bestUpPath = ExtendPathUpwardsWithLoop(loop, nodes, startNode);
                if (bestUpPath == null)
                {
                    bestUpPath = BestUpPath(nodes, startNode, scoreFunction);
                }
            }
            else
            {
                bestUpPath = BestUpPath(nodes, startNode, scoreFunction);
            }

            bestUpPath.appendWithOverlap(bestDownPath);

            return bestUpPath;
        }

        private static InstantiationPath BestDownPath(ISet<Instantiation> nodes, Instantiation node, Func<InstantiationPath, bool, bool, double> scoreFunction)
        {
            var paths = AllDownPaths(new InstantiationPath(), nodes, node);
            return paths.OrderByDescending(p => scoreFunction(p, false, true)).First();
        }

        private static InstantiationPath BestUpPath(ISet<Instantiation> nodes, Instantiation node, Func<InstantiationPath, bool, bool, double> scoreFunction)
        {
            return AllUpPaths(new InstantiationPath(), nodes, node).OrderByDescending(p => scoreFunction(p, true, false)).First();
        }

        private static IEnumerable<InstantiationPath> AllDownPaths(InstantiationPath basePath, ISet<Instantiation> nodes, Instantiation node)
        {
            basePath = new InstantiationPath(basePath);
            basePath.append(node);
            var outNodes = node.DependantInstantiations.Where(i => nodes.Contains(i));
            if (outNodes.Any()) return outNodes.SelectMany(n => AllDownPaths(basePath, nodes, n));
            else return Enumerable.Repeat(basePath, 1);
        }

        private static IEnumerable<InstantiationPath> AllUpPaths(InstantiationPath basePath, ISet<Instantiation> nodes, Instantiation node)
        {
            basePath = new InstantiationPath(basePath);
            basePath.prepend(node);
            var inNodes = node.ResponsibleInstantiations.Where(i => nodes.Contains(i));
            if (inNodes.Any()) return inNodes.SelectMany(n => AllUpPaths(basePath, nodes, n));
            else return Enumerable.Repeat(basePath, 1);
        }

        private static InstantiationPath ExtendPathUpwardsWithLoop(IEnumerable<Tuple<Quantifier, Term>> loop, ISet<Instantiation> nodes, Instantiation node)
        {
            if (!loop.Any(inst => inst.Item1 == node.Quant && inst.Item2 == node.bindingInfo.fullPattern)) return null;
            loop = loop.Reverse().RepeatIndefinietly();
            loop = loop.SkipWhile(inst => inst.Item1 != node.Quant || inst.Item2 != node.bindingInfo.fullPattern);
            return ExtendPathUpwardsWithInstantiations(new InstantiationPath(), loop, nodes, node);
        }

        private static InstantiationPath ExtendPathUpwardsWithInstantiations(InstantiationPath path, IEnumerable<Tuple<Quantifier, Term>> instantiations, ISet<Instantiation> nodes, Instantiation node)
        {
            if (!instantiations.Any()) return path;
            var instantiation = instantiations.First();
            if (instantiation.Item1 != node.Quant || instantiation.Item2 != node.bindingInfo.fullPattern) return path;
            var extendedPath = new InstantiationPath(path);
            extendedPath.prepend(node);
            var bestPath = extendedPath;
            var remainingInstantiations = instantiations.Skip(1);
            var inNodes = node.ResponsibleInstantiations.Where(i => nodes.Contains(i));
            foreach (var predecessor in inNodes)
            {
                var candidatePath = ExtendPathUpwardsWithInstantiations(extendedPath, remainingInstantiations, nodes, predecessor);
                if (candidatePath.Length() > bestPath.Length())
                {
                    bestPath = candidatePath;
                }
            }
            return bestPath;
        }*/

        //Proof steps are now being parsed as by ParseTraceLine()
        /*private bool ParseProofStep(string[] words)
        {
            if (words.Length >= 3 && words[0].Length >= 2 && words[0][0] == '#' && words[1] == ":=")
            {
                long id = GetId(words[0]);
                if (words[2][0] == '(')
                {
                    if (words.Length == 3 && words[2].StartsWith("(not_"))
                        words = new string[] { words[0], words[1], "(not", words[2].Substring(5) };
                    Term[] args = new Term[words.Length - 3];
                    for (int i = 0; i < args.Length; ++i)
                        args[i] = GetProofTerm(words[i + 3]);
                    model.proofSteps[id] = new Term(words[2].Substring(1), args);
                }
                else if (words[2][0] == '[')
                {
                    string name = words[2].Substring(1);
                    List<Common> proofArgs = new List<Common>();
                    int pos = 3;

                    Common prevInst;
                    if (name == "quant-inst]:" && model.proofSteps.TryGetValue(id, out prevInst))
                        proofArgs.Add(prevInst);

                    if (words[2].Contains("]:"))
                    {
                        name = name.Substring(0, name.Length - 2);
                    }
                    else
                    {
                        while (pos < words.Length)
                        {
                            string cur = words[pos];
                            if (cur.Contains("]:"))
                                cur = cur.Substring(0, cur.Length - 2);
                            Common tmp;
                            if (model.proofSteps.TryGetValue(GetId(cur), out tmp))
                                proofArgs.Add(tmp);
                            else
                            {
                                Console.WriteLine("missing proof step: " + cur);
                            }
                            if (words[pos++].Contains("]:")) break;
                        }
                    }
                    ProofRule res = new ProofRule();
                    res.Name = name;
                    res.Premises = proofArgs.ToArray();
                    if (pos < words.Length)
                        res.Consequent = GetProofTerm(words[pos]);
                    model.proofSteps[id] = res;
                }
                else
                {
                    model.proofSteps[id] = new Term(words[2], EmptyTerms);
                }
                return true;
            }
            return false;
        }*/

        private Term GetLiteralTerm(string w, out bool negated, out int id)
        {
            id = 0;
            negated = false;
            if (w.Length < 2) return null;

            switch (w[0])
            {
                case '-':
                    negated = true;
                    goto case '+';
                case '+':
                    w = w.Substring(1);
                    break;
                case '#':
                    break;
                case '(':
                    if (w.StartsWith("(not_#"))
                    {
                        w = w.Substring(5, w.Length - 6);
                        negated = true;
                    }
                    break;
            }
            if (w[0] == '#')
                id = int.Parse(w.Substring(1));
            return GetTerm(w);
        }

        private Literal GetLiteral(string w, bool reuse)
        {
            Literal lit = new Literal();
            lit.Term = GetLiteralTerm(w, out lit.Negated, out lit.Id);

            var id = lit.Id;
            if (lit.Negated) id = -id;

            if (reuse)
            {
                Literal tmp;
                if (literalById.TryGetValue(id, out tmp))
                    return tmp;
            }

            literalById.TryGetValue(-id, out lit.Inverse);
            if (lit.Inverse != null && lit.Inverse.Inverse == null) lit.Inverse.Inverse = lit;
            literalById[id] = lit;

            lit.Clause = decideClause;
            decideClause = null;
            return lit;
        }

        private Term GetLiteralTerm(string w)
        {
            Literal l = GetLiteral(w, true);
            return l.Term;
        }

        private void AddInstance(Instantiation inst)
        {
            Quantifier quant = inst.Quant;
            inst.LineNo = curlineNo;
            lastInst = inst;
            inst.Cost = 1.0;
            quant.Instances.Add(inst);
            model.AddInstance(inst);

            foreach (Term t in inst.Responsible)
            {
                if (t.Responsible == null) continue;

                // Link both ways in DAG of Instantiations.
                t.Responsible.DependantInstantiations.Add(inst);
                inst.ResponsibleInstantiations.Add(t.Responsible);
            }

            foreach (var term in inst.Responsible)
            {
                term.dependentInstantiationsBlame.Add(inst);
            }

            foreach (var term in inst.Bindings)
            {
                term.dependentInstantiationsBind.Add(inst);
            }
        }

        private Quantifier CreateQuantifier(string name, string qid)
        {
            var quant = new Quantifier
            {
                Qid = qid
            };
            model.quantifiers[name] = quant;
            loadBoogieToken(quant);
            return quant;
        }
        
        [Serializable]
        public class OldLogFormatException : Exception {}
    }


}
