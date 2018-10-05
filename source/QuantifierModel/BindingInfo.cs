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
        // bindings: trigger subterm --> (path constraints, Term)
        private Dictionary<Term, Tuple<List<List<Term>>, Term>> _bindings = new Dictionary<Term, Tuple<List<List<Term>>, Term>>();
        public Dictionary<Term, Tuple<List<List<Term>>, Term>> bindings
        {
            get
            {
                if (!processed) Process();
                return _bindings;
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
        private Dictionary<Term, List<Tuple<List<Term>, Term>>> _equalities = new Dictionary<Term, List<Tuple<List<Term>, Term>>>();
        public Dictionary<Term, List<Tuple<List<Term>, Term>>> equalities
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
            var boundTerm = bindings[patternTerm].Item2;
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

        /// <summary>
        /// Prints a section explaining the equality substitutions necessary to obtain a term matching the trigger.
        /// </summary>
        public void PrintEqualitySubstitution(InfoPanelContent content, PrettyPrintFormat format)
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
                var topLevelTerm = format.termNumbers.First(kv => kv.Key.isSubterm(effectiveTerm.id));
                var usedEqualityNumbers = equalities.Keys.Where(k => usedPattern.isSubterm(k)).Select(k => bindings[k])
                    .Select(b => format.equalityNumbers.First(kv => Term.semanticTermComparer.Equals(kv.Key.target, b.Item2)).Value).Distinct();
                content.Append($"Substituting ({String.Join("), (", usedEqualityNumbers)}) in ({topLevelTerm.Value}):\n");
                effectiveTerm.PrettyPrint(content, format);
                content.Append("\n\n");
                content.switchToDefaultFormat();
            }
        }

        private static Dictionary<Term, Tuple<List<List<Term>>, Term>> copyBindings(Dictionary<Term, Tuple<List<List<Term>>, Term>> bindings)
        {
            var copy = new Dictionary<Term, Tuple<List<List<Term>>, Term>>();

            // 'deeper' copy of path constraints
            foreach (var kv in bindings)
            {
                copy[kv.Key] = Tuple.Create(kv.Value.Item1.Select(l => new List<Term>(l)).ToList(), kv.Value.Item2);
            }

            return copy;
        }

        private static Dictionary<Term, List<Tuple<List<Term>, Term>>> copyEqualities(Dictionary<Term, List<Tuple<List<Term>, Term>>> bindings)
        {
            var copy = new Dictionary<Term, List<Tuple<List<Term>, Term>>>();

            // 'deeper' copy of path constraints and equality lhs
            foreach (var kv in bindings)
            {
                copy[kv.Key] = kv.Value.Select(t => Tuple.Create(new List<Term>(t.Item1), t.Item2)).ToList();
            }

            return copy;
        }

        public BindingInfo(Term pattern, ICollection<Term> bindings, ICollection<Term> topLevelTerms, IEnumerable<EqualityExplanation> equalityExplanations)
        {
            fullPattern = pattern;
            BoundTerms = bindings.Distinct().ToArray();
            TopLevelTerms = topLevelTerms.ToArray();
            EqualityExplanations = equalityExplanations.Distinct().ToArray();
            numEq = EqualityExplanations.Length;
        }

        private BindingInfo(BindingInfo other)
        {
            processed = other.processed;
            _bindings = copyBindings(other._bindings);
            BoundTerms = other.BoundTerms;
            TopLevelTerms = other.TopLevelTerms;
            EqualityExplanations = other.EqualityExplanations;
            fullPattern = other.fullPattern;
            numEq = other.numEq;

            // 'deeper' copy
            _patternMatchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other._patternMatchContext)
            {
                _patternMatchContext[context.Key] = context.Value.Select(l => new List<Term>(l)).ToList();
            }
            
            _equalities = copyEqualities(other._equalities);
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

        /// <summary>
        /// Tries to match the specified term against the specified subpattern.
        /// </summary>
        /// <param name="pattern"> The subpattern. </param>
        /// <param name="match"> A term that should be matched against that subpattern. </param>
        /// <param name="usedEquality"> The lhs (term that occured in the parent term) of the equality that was used or null. </param>
        /// <param name="context"> Match context under which this term was reached. </param>
        /// <param name="toMatch"> Other matches that still need to be processed. </param>
        /// <param name="multipatternArg"> The index of the term of a multi-pattern that is currently being matched. </param>
        /// <returns> Boolean indicating wether the match and all subsequent matches were successful or not. </returns>
        private bool AddPatternMatch(Term pattern, Term match, Term usedEquality, IEnumerable<Term> context, IEnumerable<Tuple<Term, Term, IEnumerable<Term>>> toMatch, int multipatternArg)
        {
            // Backup so we can backtrack later.
            var origBindings = copyBindings(_bindings);
            var origEqualities = copyEqualities(_equalities);

            // Update binding info.
            if (usedEquality != null)
            {
                if (!_equalities.TryGetValue(pattern, out var eqs))
                {
                    eqs = new List<Tuple<List<Term>, Term>>();
                    _equalities[pattern] = eqs;
                }
                eqs.Add(Tuple.Create(context.ToList(), usedEquality));
                context = Enumerable.Empty<Term>();
            }

            if (!_bindings.TryGetValue(pattern, out var existingBinding))
            {
                existingBinding = Tuple.Create(new List<List<Term>>(), match);
                _bindings[pattern] = existingBinding;
            }
            existingBinding.Item1.Add(context.ToList());
            
            // Continue with arguments.
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
                return false;
            }
        }

        private bool DoNextMatch(IEnumerable<Tuple<Term, Term, IEnumerable<Term>>> toMatch, int multipatternArg)
        {
            // Done with one term in multi-pattern.
            if (!toMatch.Any())
            {
                return ProcessMultipatternArg(multipatternArg + 1);
            }

            var match = toMatch.First();
            var nextMatches = toMatch.Skip(1);
            var patternTerm = match.Item1;
            var matchedTerm = match.Item2;
            var context = match.Item3;

            // Quantified variable?
            if (patternTerm.id == -1)
            {
                if (_bindings.TryGetValue(patternTerm, out var existingBinding))
                {
                    // Make sure same term was bound to quantified variable in both cases.
                    Term equality = null;
                    if (existingBinding.Item2.id != matchedTerm.id)
                    {
                        if (EqualityExplanations.Any(ee => ee.source.id == matchedTerm.id && ee.target.id == existingBinding.Item2.id))
                        {
                            equality = matchedTerm;
                            matchedTerm = existingBinding.Item2;
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
                    // Try without equality first.
                    if (AddPatternMatch(patternTerm, matchedTerm, null, context, nextMatches, multipatternArg))
                    {
                        return true;
                    }

                    // If that fails try equalities.
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
                // Try without equality first.
                if (patternTerm.Name == matchedTerm.Name && patternTerm.Args.Count() == matchedTerm.Args.Count() &&
                    AddPatternMatch(patternTerm, matchedTerm, null, context, nextMatches, multipatternArg))
                {
                    return true;
                }

                // If that fails try equalities.
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

        /// <summary>
        /// There are some properties we require that we can only check once a pattern match is complete (e.g. wheter
        /// all equalities were used). This method checks these properties.
        /// </summary>
        private bool IsValid()
        {
            // Bindings match with those reported by z3.
            var qVarBindings = _bindings.Where(kv => kv.Key.id == -1).Select(kv => kv.Value);
            if (!qVarBindings.All(binding => BoundTerms.Any(o => o.id == binding.Item2.id)))
            {
                return false;
            }
            if (!BoundTerms.All(binding => qVarBindings.Any(o => o.Item2.id == binding.id)))
            {
                return false;
            }


            // All equalities reported by z3 used.
            var resolvedEqualities = new Dictionary<int, ISet<int>>();
            foreach (var kv in _equalities)
            {
                var key = bindings[kv.Key].Item2.id;
                if (!resolvedEqualities.TryGetValue(key, out var lhs))
                {
                    lhs = new HashSet<int>();
                    resolvedEqualities[key] = lhs;
                }
                lhs.UnionWith(kv.Value.Select(v => v.Item2.id));
            }

            return EqualityExplanations.Where(ee => ee.source.id != ee.target.id).All(ee => {
                if (!resolvedEqualities.TryGetValue(ee.target.id, out var lhs))
                {
                    return false;
                }
                return lhs.Contains(ee.source.id);
            });
        }

        /// <summary>
        /// Process the next term in a multi-pattern or validate if we have processed the entire trigger.
        /// </summary>
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

        /// <summary>
        /// Find the pattern match.
        /// </summary>
        private void Process()
        {
            if (processed) return;
            processed = true;
            var successful = ProcessMultipatternArg(0);
            if (!successful) throw new Exception("Couldn't calculate pattern match from log information.");
            AddPatternPathconditions();
        }

        /// <summary>
        /// Adds the path conditions for highlighting the pattern term itself.
        /// </summary>
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

        /// <summary>
        /// Terms needed for the pattern match that have no parent, i.e. are not subterms of other blame terms.
        /// </summary>
        /// <remarks>
        /// These are either terms that were matched against the top level structure of a trigger or rhs (a term
        /// substitued for the original subterm of some higher level structure) of equalities.
        /// </remarks>
        public List<Term> getDistinctBlameTerms()
        {
            var blameTerms = bindings
                .Select(bnd => bnd.Value).Distinct();
            return blameTerms
                .Where(t1 => t1.Item1.Any(l => l.Count == 0))
                .Select(t => t.Item2)
                .Distinct(Term.semanticTermComparer)
                .ToList();
        }

        public List<KeyValuePair<Term, Term>> getBindingsToFreeVars()
        {
            return bindings
                .Where(bnd => bnd.Key.id == -1)
                .Select(kv => new KeyValuePair<Term, Term>(kv.Key, kv.Value.Item2))
                .ToList();
        }
    }
}