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
        public EqualityExplanation[] EqualityExplanations;

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

        /// <summary>
        /// Constructs a concrete term exacly matching the pattern by applying equality substituations.
        /// </summary>
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

        public int GetTermNumber(Term term)
        {
            var index = getDistinctBlameTerms().IndexOf(term);
            if (index == -1)
            {
                throw new ArgumentException("The specified Term was not a top level blame term for this instantiation.");
            }
            return index;
        }

        public int GetEqualityNumber(Term source, Term target)
        {
            var index = Array.FindIndex(EqualityExplanations, ee => ee.source.id == source.id && ee.target.id == target.id);
            if (index == -1)
            {
                throw new ArgumentException("The argument was not an equality used by this instantiation.");
            }
            return index;
        }

        public int GetNumberOfTermAndEqualityNumberingsUsed()
        {
            return getDistinctBlameTerms().Count + EqualityExplanations.Length;
        }

        /// <summary>
        /// Prints a section explaining the equality substitutions necessary to obtain a term matching the trigger.
        /// </summary>
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

        public BindingInfo(Term pattern, ICollection<Term> bindings, ICollection<Term> topLevelTerms, IEnumerable<EqualityExplanation> equalityExplanations)
        {
            fullPattern = pattern;
            BoundTerms = bindings.Distinct().ToArray();
            TopLevelTerms = topLevelTerms.ToArray();
            EqualityExplanations = equalityExplanations.ToArray();
            numEq = EqualityExplanations.Length;
        }

        private BindingInfo(BindingInfo other)
        {
            processed = other.processed;
            _bindings = new Dictionary<Term, Term>(other._bindings);
            BoundTerms = other.BoundTerms;
            TopLevelTerms = other.TopLevelTerms;
            EqualityExplanations = other.EqualityExplanations;
            fullPattern = other.fullPattern;
            numEq = other.numEq;

            // 'deeper' copy
            _matchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other._matchContext)
            {
                _matchContext[context.Key] = context.Value.Select(l => new List<Term>(l)).ToList();
            }

            // 'deeper' copy
            _patternMatchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other._patternMatchContext)
            {
                _patternMatchContext[context.Key] = context.Value.Select(l => new List<Term>(l)).ToList();
            }

            // 'deeper' copy
            _equalities = new Dictionary<Term, List<Term>>();
            foreach (var equality in other._equalities)
            {
                _equalities[equality.Key] = new List<Term>(equality.Value);
            }
        }

        public BindingInfo Clone()
        {
            return new BindingInfo(this);
        }

        private void addPatternMatchContext(Term term, List<List<Term>> context)
        {
            if (!patternMatchContext.ContainsKey(term.id)) _patternMatchContext[term.id] = new List<List<Term>>();
            _patternMatchContext[term.id].AddRange(context);
        }

        private bool AddPatternMatch(Term pattern, Term match, Term usedEquality, IEnumerable<Term> context, IEnumerable<Tuple<Term, Term, IEnumerable<Term>>> toMatch, int multipatternArg)
        {
            var origBindings = new Dictionary<Term, Term>(_bindings);
            var origEqualities = new Dictionary<Term, List<Term>>(_equalities);
            var origMatchContext = new Dictionary<int, List<List<Term>>>(_matchContext);

            if (usedEquality != null)
            {
                if (!_equalities.TryGetValue(pattern, out var eqs))
                {
                    eqs = new List<Term>();
                    _equalities[pattern] = eqs;
                }
                eqs.Add(usedEquality);
                if (!_matchContext.TryGetValue(usedEquality.id, out var eqContexts))
                {
                    eqContexts = new List<List<Term>>();
                    _matchContext[usedEquality.id] = eqContexts;
                }
                eqContexts.Add(context.ToList());
                context = Enumerable.Empty<Term>();
            }

            _bindings[pattern] = match;
            if (!_matchContext.TryGetValue(match.id, out var contexts))
            {
                contexts = new List<List<Term>>();
                _matchContext[match.id] = contexts;
            }
            contexts.Add(context.ToList());

            var nextContext = context.Concat(Enumerable.Repeat(match, 1));
            var nextMatches = pattern.Args.Zip(match.Args, (p, m) => Tuple.Create(p, m, nextContext)).Concat(toMatch);
            if (DoNextMatch(nextMatches, multipatternArg))
            {
                return true;
            }
            else
            {
                _bindings = origBindings;
                _equalities = origEqualities;
                _matchContext = origMatchContext;
                return false;
            }
        }

        private bool DoNextMatch(IEnumerable<Tuple<Term, Term, IEnumerable<Term>>> toMatch, int multipatternArg)
        {
            if (!toMatch.Any())
            {
                return ProcessMultipatternArg(multipatternArg + 1);
            }

            var match = toMatch.First();
            var nextMatches = toMatch.Skip(1);
            var patternTerm = match.Item1;
            var matchedTerm = match.Item2;
            var context = match.Item3;

            if (patternTerm.id == -1)
            {
                if (_bindings.TryGetValue(patternTerm, out var existingBinding))
                {
                    Term equality = null;
                    if (existingBinding.id != matchedTerm.id)
                    {
                        if (EqualityExplanations.Any(ee => ee.source.id == matchedTerm.id && ee.target.id == existingBinding.id))
                        {
                            equality = matchedTerm;
                            matchedTerm = existingBinding;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return AddPatternMatch(patternTerm, matchedTerm, equality, context, nextMatches, multipatternArg);
                }
                else
                {
                    if (AddPatternMatch(patternTerm, matchedTerm, null, context, nextMatches, multipatternArg))
                    {
                        return true;
                    }
                    var feasibleEqualities = EqualityExplanations.Where(ee => ee.source.id == matchedTerm.id);
                    foreach (var equalityExplanation in feasibleEqualities)
                    {
                        if (AddPatternMatch(patternTerm, equalityExplanation.target, matchedTerm, context, nextMatches, multipatternArg))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
            else
            {
                if (patternTerm.Name == matchedTerm.Name && patternTerm.Args.Count() == matchedTerm.Args.Count() && AddPatternMatch(patternTerm, matchedTerm, null, context, nextMatches, multipatternArg))
                {
                    return true;
                }
                var feasibleEqualities = EqualityExplanations.Where(ee => ee.source.id == matchedTerm.id && ee.target.Name == patternTerm.Name && ee.target.Args.Count() == patternTerm.Args.Count());
                foreach (var equalityExplanation in feasibleEqualities)
                {
                    if (AddPatternMatch(patternTerm, equalityExplanation.target, matchedTerm, context, nextMatches, multipatternArg))
                    {
                        return true;
                    }
                }
                return false;
            }
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

        private bool ProcessMultipatternArg(int index)
        {
            if (index >= fullPattern.Args.Length)
            {
                return IsValid();
            }
            else
            {
                var pattern = fullPattern.Args[index];
                var topLevelMatches = TopLevelTerms.Where(t => t.Name == pattern.Name && t.Args.Count() == pattern.Args.Count());
                foreach (var match in topLevelMatches)
                {
                    if (AddPatternMatch(pattern, match, null, Enumerable.Empty<Term>(), Enumerable.Empty<Tuple<Term, Term, IEnumerable<Term>>>(), index))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void Process()
        {
            if (processed) return;
            processed = true;
            var successful = ProcessMultipatternArg(0);
            if (!successful) throw new Exception("Couldn't calculate pattern match from log information.");
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
                .Select(bnd => bnd.Value).Distinct();
            return blameTerms
                .Where(t1 => matchContext[t1.id].Any(l => l.Count == 0))
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