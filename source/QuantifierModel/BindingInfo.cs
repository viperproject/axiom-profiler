using AxiomProfiler.PrettyPrinting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AxiomProfiler.QuantifierModel
{
    public class BindingInfo
    {
        // Pattern used for this binding
        public readonly Term fullPattern;

        public readonly Term[] BoundTerms;
        public readonly Term[] TopLevelTerms;
        public readonly EqualityExplanation[] EqualityExplanations;

        private bool processed = false;
        // bindings: freeVariable --> Term
        private Dictionary<Term, Term> _bindings = new Dictionary<Term, Term>();
        public Dictionary<Term, Term> bindings
        {
            get
            {
                if (!processed) Process();
                return _bindings;
            }
        }

        // highlighting info: Term --> List of path constraints
        private Dictionary<int, List<List<Term>>> _matchContext = new Dictionary<int, List<List<Term>>>();
        public Dictionary<int, List<List<Term>>> matchContext
        {
            get
            {
                if (!processed) Process();
                return _matchContext;
            }
        }

        // highlighting info for pattern: Term --> List of path constraints
        private readonly Dictionary<int, List<List<Term>>> _patternMatchContext = new Dictionary<int, List<List<Term>>>();
        public Dictionary<int, List<List<Term>>> patternMatchContext
        {
            get {
                if (!processed) Process();
                return _patternMatchContext;
            }
        }

        // equalities inferred from pattern matching
        private Dictionary<Term, List<Term>> _equalities = new Dictionary<Term, List<Term>>();
        public Dictionary<Term, List<Term>> equalities
        {
            get
            {
                if (!processed) Process();
                return _equalities;
            }
        }


        // number of equalities
        public readonly int numEq;

        private List<Term> _BlamedEffectiveTerms = new List<Term>();
        private List<Term> _BoundEffectiveTerms = new List<Term>();

        public List<Term> BlamedEffectiveTerms
        {
            get
            {
                if (_EffectiveBlameTerms == null)
                {
                    _EffectiveBlameTerms = fullPattern.Args.Select(p => EffectiveBlameTermForPatternTerm(p)).ToList();
                }
                return _BlamedEffectiveTerms;
            }
        }

        public List<Term> BoundEffectiveTerms
        {
            get
            {
                if (_EffectiveBlameTerms == null)
                {
                    _EffectiveBlameTerms = fullPattern.Args.Select(p => EffectiveBlameTermForPatternTerm(p)).ToList();
                }
                return _BoundEffectiveTerms;
            }
        }

        private List<Term> _EffectiveBlameTerms = null;
        public List<Term> EffectiveBlameTerms {
            get {
                if (_EffectiveBlameTerms == null)
                {
                    _EffectiveBlameTerms = fullPattern.Args.Select(p => EffectiveBlameTermForPatternTerm(p)).ToList();
                }
                return _EffectiveBlameTerms;
            }
        }

        private Term EffectiveBlameTermForPatternTerm(Term patternTerm)
        {
            var boundTerm = bindings[patternTerm];
            var newArgs = patternTerm.Args.Count() == 0 ? boundTerm.Args : patternTerm.Args.Select(p => EffectiveBlameTermForPatternTerm(p)).ToArray();
            var effectiveTerm = new Term(boundTerm.Name, newArgs, boundTerm.generalizationCounter) { id = boundTerm.id };
            if (patternTerm.id == -1)
            {
                _BoundEffectiveTerms.Add(effectiveTerm);
            }
            else
            {
                _BlamedEffectiveTerms.Add(effectiveTerm);
            }
            return effectiveTerm;
        }

        public void PrintEqualityExplanations(InfoPanelContent content, PrettyPrintFormat format, List<Tuple<IEnumerable<Term>, int>> equalityNumberings)
        {
            var wasContextSensitive = format.printContextSensitive;
            format.printContextSensitive = false;

            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nEquality explanations:\n\n");
            content.switchToDefaultFormat();

            foreach (var numbering in equalityNumberings)
            {
                var explanations = EqualityExplanations.Where(ee => numbering.Item1.Contains(ee.source) && numbering.Item1.Contains(ee.target));
                foreach (var explanation in explanations)
                {
                    explanation.PrettyPrint(content, format, numbering.Item2);
                    content.Append("\n\n");
                }
            }

            format.printContextSensitive = wasContextSensitive;
        }

        public void PrintEqualitySubstitution(InfoPanelContent content, PrettyPrintFormat format, IEnumerable<Tuple<Term, int>> termNumberings, IEnumerable<Tuple<IEnumerable<Term>, int>> equalityNumberings)
        {
            content.switchFormat(PrintConstants.SubtitleFont, PrintConstants.sectionTitleColor);
            content.Append("\nSubstituting equalities yields:\n\n");
            content.switchToDefaultFormat();

            foreach (var blamedTerm in BlamedEffectiveTerms)
            {
                blamedTerm.highlightTemporarily(format, PrintConstants.blameColor);
            }

            foreach (var boundTerm in BoundEffectiveTerms)
            {
                boundTerm.highlightTemporarily(format, PrintConstants.bindColor);
            }

            foreach (var pair in EffectiveBlameTerms.Zip(fullPattern.Args, Tuple.Create))
            {
                var effectiveTerm = pair.Item1;
                var usedPattern = pair.Item2;
                var topLevelTerm = termNumberings.First(numbering => numbering.Item1.isSubterm(effectiveTerm.id));
                var usedEqualityNumbers = equalities.Keys.Where(k => usedPattern.isSubterm(k)).Select(k => bindings[k])
                    .Select(b => equalityNumberings.First(numbering => numbering.Item1.Contains(b)).Item2).Distinct();
                content.Append($"Substituting ({String.Join("), (", usedEqualityNumbers)}) in ({topLevelTerm.Item2}):\n");
                effectiveTerm.PrettyPrint(content, format);
                content.Append("\n\n");
                content.switchToDefaultFormat();
            }
        }

        public BindingInfo(Term pattern, ICollection<Term> bindings, ICollection<Term> topLevelTerms, ICollection<EqualityExplanation> equalityExplanations)
        {
            fullPattern = pattern;
            BoundTerms = bindings.ToArray();
            TopLevelTerms = topLevelTerms.ToArray();
            EqualityExplanations = equalityExplanations.ToArray();
            numEq = EqualityExplanations.Length;
        }

        private BindingInfo(BindingInfo other)
        {
            processed = other.processed;
            _bindings = new Dictionary<Term, Term>(other.bindings);
            BoundTerms = other.BoundTerms;
            TopLevelTerms = other.TopLevelTerms;
            EqualityExplanations = other.EqualityExplanations;
            fullPattern = other.fullPattern;
            numEq = other.numEq;

            // 'deeper' copy
            _matchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other._matchContext)
            {
                _matchContext[context.Key] = new List<List<Term>>(context.Value);
            }

            // 'deeper' copy
            _patternMatchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other._patternMatchContext)
            {
                _patternMatchContext[context.Key] = new List<List<Term>>(context.Value);
            }

            // 'deeper' copy
            _equalities = new Dictionary<Term, List<Term>>();
            foreach (var equality in other._equalities)
            {
                _equalities[equality.Key] = new List<Term>(equality.Value);
            }
        }

        public BindingInfo clone()
        {
            return new BindingInfo(this);
        }

        private void addPatternMatchContext(Term term, List<List<Term>> context)
        {
            if (!patternMatchContext.ContainsKey(term.id)) _patternMatchContext[term.id] = new List<List<Term>>();
            _patternMatchContext[term.id].AddRange(context);
        }

        private bool AddPatternMatch(Term pattern, Term match, IEnumerable<Term> context)
        {
            _bindings[pattern] = match;
            if (!_matchContext.TryGetValue(match.id, out var contexts))
            {
                contexts = new List<List<Term>>();
                _matchContext[match.id] = contexts;
            }
            contexts.Add(context.ToList());

            foreach (var submatch in pattern.Args.Zip(match.Args, Tuple.Create))
            {
                var origBindings = new Dictionary<Term, Term>(_bindings);
                var origEqualities = new Dictionary<Term, List<Term>>(_equalities);
                var origMatchContext = new Dictionary<int, List<List<Term>>>(_matchContext);

                var subpattern = submatch.Item1;
                var subterm = submatch.Item2;
                var nextContext = context.Concat(Enumerable.Repeat(match, 1));
                if (subpattern.id == -1 || (subpattern.Name == subterm.Name && subpattern.Args.Count() == subterm.Args.Count()))
                {
                    if (!AddPatternMatch(subpattern, subterm, nextContext)) return false;
                }
                else
                {
                    var plausibleEqualities = EqualityExplanations.Where(e => e.source.id == subterm.id && e.target.Name == subpattern.Name && e.target.Args.Count() == subpattern.Args.Count());
                    foreach (var eq in plausibleEqualities)
                    {
                        if (!_equalities.TryGetValue(subpattern, out var eqs))
                        {
                            eqs = new List<Term>();
                            _equalities[subpattern] = eqs;
                        }
                        eqs.Add(subterm);
                        if (!_matchContext.TryGetValue(subterm.id, out contexts))
                        {
                            contexts = new List<List<Term>>();
                            _matchContext[subterm.id] = contexts;
                        }
                        contexts.Add(nextContext.ToList());
                        if (AddPatternMatch(subpattern, eq.target, Enumerable.Empty<Term>())) goto foundMatch;
                    }
                    _bindings = origBindings;
                    _equalities = origEqualities;
                    _matchContext = origMatchContext;
                    return false;
                    foundMatch: { }
                }
            }
            return true;
        }

        private bool IsValid()
        {
            var qVarBindings = _bindings.Where(kv => kv.Key.id == -1).Select(kv => kv.Value);
            if (!qVarBindings.All(binding => BoundTerms.Any(o => o.id == binding.id)))
            {
                return false;
            }
            if (!BoundTerms.All(binding => qVarBindings.Any(o => o.id == binding.id)))
            {
                return false;
            }

            var resolvedEqualities = new Dictionary<int, ISet<int>>();
            foreach (var kv in _equalities)
            {
                var key = bindings[kv.Key].id;
                if (!resolvedEqualities.TryGetValue(key, out var lhs))
                {
                    lhs = new HashSet<int>();
                    resolvedEqualities[key] = lhs;
                }
                lhs.UnionWith(kv.Value.Select(v => v.id));
            }

            return EqualityExplanations.Where(ee => ee.source.id != ee.target.id).All(ee => {
                if (!resolvedEqualities.TryGetValue(ee.target.id, out var lhs))
                {
                    return false;
                }
                return lhs.Contains(ee.source.id);
            });
        }

        private void Process()
        {
            if (processed) return;
            processed = true;
            foreach (var pattern in fullPattern.Args)
            {
                var topLevelMatches = TopLevelTerms.Where(t => t.Name == pattern.Name && t.Args.Count() == pattern.Args.Count());
                foreach (var match in topLevelMatches)
                {
                    if (AddPatternMatch(pattern, match, Enumerable.Empty<Term>()))
                    {
                        if (IsValid()) break;
                        else
                        {
                            _bindings.Clear();
                            _equalities.Clear();
                            _matchContext.Clear();
                        }
                    }
                }
            }
            AddPatternPathconditions();
        }

        private void AddPatternPathconditions()
        {
            var patternStack = new Stack<Term>();
            patternStack.Push(fullPattern);
            var history = new Stack<Term>();

            while (patternStack.Count > 0)
            {
                var currentPattern = patternStack.Peek();
                if (history.Count > 0 && history.Peek() == currentPattern)
                {
                    patternStack.Pop();
                    history.Pop();
                    continue;
                }

                var pathConstraint = history.ToList();
                pathConstraint.Reverse();
                addPatternMatchContext(currentPattern, new List<List<Term>> { pathConstraint });
                
                history.Push(currentPattern);
                foreach (var subPattern in currentPattern.Args)
                {
                    patternStack.Push(subPattern);
                }

            }
        }

        public List<Term> getDistinctBlameTerms()
        {
            var blameTerms = bindings
                .Where(bnd => bnd.Key.id != -1)
                .Select(bnd => bnd.Value);
            return blameTerms
                .Where(t1 => blameTerms.All(t2 => t2 == t1 || !t2.isSubterm(t1)))
                .ToList();
        }

        public List<KeyValuePair<Term, Term>> getBindingsToFreeVars()
        {
            return bindings
                .Where(bnd => bnd.Key.id == -1)
                .ToList();
        }
    }
}