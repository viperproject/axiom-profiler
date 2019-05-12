using AxiomProfiler.PrettyPrinting;
using AxiomProfiler.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AxiomProfiler.QuantifierModel
{
    using ConstraintElementType = Tuple<Term, int>;
    using ConstraintType = List<Tuple<Term, int>>;

    public class BindingInfo
    {
        private static readonly Term[] noTerms = new Term[0];
        private static readonly EqualityExplanation[] noExplanations = new EqualityExplanation[0];

        // Pattern used for this binding
        public readonly Term fullPattern;

        public readonly Term[] BoundTerms;
        public readonly Term[] TopLevelTerms;
        public EqualityExplanation[] EqualityExplanations;
        public readonly Term[] explicitlyBlamedTerms = noTerms;

        private bool processed = false;
        private Exception cachedException = null;
        // bindings: trigger subterm --> (path constraints, Term)
        private Dictionary<Term, Tuple<List<ConstraintType>, Term>> _bindings = new Dictionary<Term, Tuple<List<ConstraintType>, Term>>(Term.semanticTermComparer);
        public Dictionary<Term, Tuple<List<ConstraintType>, Term>> bindings
        {
            get
            {
                Process();
                return _bindings;
            }
        }

        // highlighting info for pattern: Term --> List of path constraints
        private readonly Dictionary<int, List<ConstraintType>> _patternMatchContext = new Dictionary<int, List<ConstraintType>>();
        public Dictionary<int, List<ConstraintType>> patternMatchContext
        {
            get {
                Process();
                return _patternMatchContext;
            }
        }

        // equalities inferred from pattern matching
        private Dictionary<Term, List<Tuple<ConstraintType, Term>>> _equalities = new Dictionary<Term, List<Tuple<ConstraintType, Term>>>(Term.semanticTermComparer);
        public Dictionary<Term, List<Tuple<ConstraintType, Term>>> equalities
        {
            get
            {
                Process();
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
            var effectiveTerm = new Term(boundTerm, newArgs);
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
                var rootTerm = equalities.FirstOrDefault(eq => eq.Key == usedPattern).Value?.FirstOrDefault(eqSource => TopLevelTerms.Contains(eqSource.Item2))?.Item2 ?? effectiveTerm;
                var topLevelTermNumber = format.termNumbers.First(kv => kv.Key.isSubterm(rootTerm.id));
                var usedEqualityNumbers = equalities.Keys.Where(k => usedPattern.isSubterm(k)).Select(k => bindings[k])
                    .Select(b => {
#if !DEBUG
                        try
                        {
#endif
                            return format.equalityNumbers.First(kv => Term.semanticTermComparer.Equals(kv.Key.target, b.Item2)).Value;
#if !DEBUG
                        }
                        catch (Exception)
                        {
                            return 0;
                        }
#endif
                    }).Distinct();
                content.Append($"Substituting ({String.Join("), (", usedEqualityNumbers)}) in ({topLevelTermNumber.Value}):\n");
                effectiveTerm.PrettyPrint(content, format);
                content.Append("\n\n");
                content.switchToDefaultFormat();
            }
        }

        private static Dictionary<Term, Tuple<List<ConstraintType>, Term>> CopyBindings(Dictionary<Term, Tuple<List<ConstraintType>, Term>> bindings)
        {
            var copy = new Dictionary<Term, Tuple<List<ConstraintType>, Term>>();

            // 'deeper' copy of path constraints
            foreach (var kv in bindings)
            {
                copy[kv.Key] = Tuple.Create(kv.Value.Item1.Select(l => new ConstraintType(l)).ToList(), kv.Value.Item2);
            }

            return copy;
        }

        private static Dictionary<Term, List<Tuple<ConstraintType, Term>>> CopyEqualities(Dictionary<Term, List<Tuple<ConstraintType, Term>>> bindings)
        {
            var copy = new Dictionary<Term, List<Tuple<ConstraintType, Term>>>();

            // 'deeper' copy of path constraints and equality lhs
            foreach (var kv in bindings)
            {
                copy[kv.Key] = kv.Value.Select(t => Tuple.Create(new ConstraintType(t.Item1), t.Item2)).ToList();
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

        public BindingInfo(Quantifier quant, Term[] bindings, Term[] explicitlyBlamedTerms = null)
        {
            fullPattern = null;
            BoundTerms = bindings;
            if (quant.BodyTerm != null)
                foreach (var qv in quant.BodyTerm.QuantifiedVariables())
                {
                    //TODO: index out of bounds => wrong log (not enough bound terms provided). Provide user with line number so they can check themselves.
                    _bindings[qv] = Tuple.Create(new List<ConstraintType>(), bindings[qv.varIdx]);
                    _patternMatchContext[qv.id] = new List<ConstraintType>() { new ConstraintType() };
                }
            TopLevelTerms = noTerms;
            EqualityExplanations = noExplanations;
            numEq = 0;
            processed = true;
            this.explicitlyBlamedTerms = explicitlyBlamedTerms ?? noTerms;
        }

        private BindingInfo(BindingInfo other)
        {
            processed = other.processed;
            _bindings = CopyBindings(other._bindings);
            BoundTerms = other.BoundTerms;
            TopLevelTerms = other.TopLevelTerms;
            EqualityExplanations = other.EqualityExplanations;
            fullPattern = other.fullPattern;
            numEq = other.numEq;
            explicitlyBlamedTerms = new Term[other.explicitlyBlamedTerms.Length];
            Array.Copy(other.explicitlyBlamedTerms, explicitlyBlamedTerms, other.explicitlyBlamedTerms.Length);

            // 'deeper' copy
            _patternMatchContext = new Dictionary<int, List<ConstraintType>>();
            foreach (var context in other._patternMatchContext)
            {
                _patternMatchContext[context.Key] = context.Value.Select(l => new ConstraintType(l)).ToList();
            }
            
            _equalities = CopyEqualities(other._equalities);
        }

        public BindingInfo Clone()
        {
            return new BindingInfo(this);
        }

        private void addPatternMatchContext(Term term, List<ConstraintType> context)
        {
            if (!patternMatchContext.ContainsKey(term.id)) _patternMatchContext[term.id] = new List<ConstraintType>();
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
        private bool AddPatternMatch(Term pattern, Term match, Term usedEquality, IEnumerable<ConstraintElementType> context, IEnumerable<Tuple<Term, Term, IEnumerable<ConstraintElementType>>> toMatch, int multipatternArg)
        {
            // Backup so we can backtrack later.
            var origBindings = CopyBindings(_bindings);
            var origEqualities = CopyEqualities(_equalities);

            // Update binding info.
            if (usedEquality != null)
            {
                if (!_equalities.TryGetValue(pattern, out var eqs))
                {
                    eqs = new List<Tuple<ConstraintType, Term>>();
                    _equalities[pattern] = eqs;
                }
                eqs.Add(Tuple.Create(context.ToList(), usedEquality));
                context = Enumerable.Empty<ConstraintElementType>();
            }

            if (!_bindings.TryGetValue(pattern, out var existingBinding))
            {
                existingBinding = Tuple.Create(new List<ConstraintType>(), match);
                _bindings[pattern] = existingBinding;
            }
            existingBinding.Item1.Add(context.ToList());
            
            // Continue with arguments.
            var nextMatches = Enumerable.Range(0, pattern.Args.Length).Select(i => Tuple.Create(pattern.Args[i], match.Args[i], context.Concat(Enumerable.Repeat(Tuple.Create(match, i), 1)))).Concat(toMatch);
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

        private bool DoNextMatch(IEnumerable<Tuple<Term, Term, IEnumerable<ConstraintElementType>>> toMatch, int multipatternArg)
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
                //TODO: index out of bounds => wrong log (not enough bound terms provided). Provide user with line number so they can check themselves.
                var boundTerm = BoundTerms[patternTerm.varIdx];
                if (boundTerm.id == matchedTerm.id)
                {
                    return AddPatternMatch(patternTerm, boundTerm, null, context, nextMatches, multipatternArg);
                }
                else
                {
                    // Check that equality actually exists.
                    if (!EqualityExplanations.Any(ee => ee.source.id == matchedTerm.id && ee.target.id == boundTerm.id))
                    {
                        return false;
                    }
                    return AddPatternMatch(patternTerm, boundTerm, matchedTerm, context, nextMatches, multipatternArg);
                }
            }
            else
            {
                // Try without equality first.
                if (patternTerm.Name == matchedTerm.Name &&
                    patternTerm.GenericType == matchedTerm.GenericType &&
                    patternTerm.Args.Length == matchedTerm.Args.Length &&
                    AddPatternMatch(patternTerm, matchedTerm, null, context, nextMatches, multipatternArg))
                {
                    return true;
                }

                // If that fails try equalities.
                var feasibleEqualities = EqualityExplanations.Where(ee => ee.source.id == matchedTerm.id &&
                    ee.target.Name == patternTerm.Name && ee.target.GenericType == patternTerm.GenericType &&
                    ee.target.Args.Length == patternTerm.Args.Length);
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
            var qVarBindings = _bindings.Where(kv => kv.Key.id == -1).Select(kv => kv.Value.Item2);
            if (!qVarBindings.All(binding => BoundTerms.Any(o => o.id == binding.id)))
            {
                return false;
            }
            if (!BoundTerms.All(binding => qVarBindings.Any(o => o.id == binding.id)))
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
                var topLevelMatches = TopLevelTerms.Where(t => t.Name == pattern.Name &&
                    t.GenericType == pattern.GenericType &&
                    t.Args.Length == pattern.Args.Length);
                foreach (var match in topLevelMatches)
                {
                    if (AddPatternMatch(pattern, match, null, Enumerable.Empty<ConstraintElementType>(), Enumerable.Empty<Tuple<Term, Term, IEnumerable<ConstraintElementType>>>(), index))
                    {
                        return true;
                    }
                    else foreach (var eq in EqualityExplanations.Where(ee => Term.semanticTermComparer.Equals(ee.source, match)))
                    {
                        if (AddPatternMatch(pattern, eq.target, match, Enumerable.Empty<ConstraintElementType>(), Enumerable.Empty<Tuple<Term, Term, IEnumerable<ConstraintElementType>>>(), index))
                        {
                            return true;
                        }
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
            if (cachedException != null) throw cachedException;
            if (processed) return;
            processed = true;
            var successful = ProcessMultipatternArg(0);
            if (!successful)
            {
                cachedException = new Exception("Couldn't calculate pattern match from log information.");
                throw cachedException;
            }
            AddPatternPathconditions();
        }

        /// <summary>
        /// Adds the path conditions for highlighting the pattern term itself.
        /// </summary>
        private void AddPatternPathconditions()
        {
            var patternStack = new Stack<ConstraintElementType>();
            patternStack.Push(Tuple.Create(fullPattern, 0));
            var history = new Stack<ConstraintElementType>();

            while (patternStack.Any())
            {
                var currentPattern = patternStack.Peek();
                if (history.Any() && history.Peek().Item1 == currentPattern.Item1)
                {
                    patternStack.Pop();
                    history.Pop();
                    continue;
                }

                if (history.Any())
                {
                    var parent = history.Pop();
                    history.Push(Tuple.Create(parent.Item1, currentPattern.Item2));
                }

                var pathConstraint = history.Reverse().ToList();
                addPatternMatchContext(currentPattern.Item1, new List<ConstraintType> { pathConstraint });
                
                history.Push(currentPattern);
                for (var i = 0; i < currentPattern.Item1.Args.Length; ++i)
                {
                    var subPattern = currentPattern.Item1.Args[i];
                    patternStack.Push(Tuple.Create(subPattern, i));
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
                .Where(t1 => t1 == default(Tuple<List<ConstraintType>, Term>) ? false : t1.Item1.Any(l => l.Count == 0))
                .Select(t => t.Item2)
                .Concat(TopLevelTerms)
                .Concat(explicitlyBlamedTerms)
                .Distinct(Term.semanticTermComparer)
                .ToList();
        }

        public List<KeyValuePair<Term, Term>> getBindingsToFreeVars()
        {
            return bindings
                .Where(bnd => bnd.Key.id == -1)
                .Select(kv => new KeyValuePair<Term, Term>(kv.Key, kv.Value?.Item2))
                .ToList();
        }

        /// <summary>
        /// Indicates wheter this binding info represents a pattern match.
        /// </summary>
        /// <returns>true if this binding info represents a pattern match, false otherwise (e.g. if MBQI was used)</returns>
        public bool IsPatternMatch()
        {
            return fullPattern != null;
        }
    }
}