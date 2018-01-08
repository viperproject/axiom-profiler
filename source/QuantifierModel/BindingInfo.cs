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

        // unmatched blame terms
        private readonly List<Term> unusedBlameTerms;

        //additional terms that didn't exist in the log but were used for matchings (parts of the pattern)
        private readonly List<Term> additionalBlameTerms = new List<Term>();

        // outstanding check candidates
        // parent term -> subpattern, subterm
        // if parent is evicted the associated entry will be cleared
        private readonly Dictionary<Term, List<Tuple<Term, Term>>> outstandingCandidates = new Dictionary<Term, List<Tuple<Term, Term>>>();

        // outstanding checks
        private readonly Dictionary<Term, List<Term>> outstandingMatches = new Dictionary<Term, List<Term>>();

        // bindings: freeVariable --> Term
        public readonly Dictionary<Term, Term> bindings = new Dictionary<Term, Term>();

        // highlighting info: Term --> List of path constraints
        public readonly Dictionary<int, List<List<Term>>> matchContext = new Dictionary<int, List<List<Term>>>();

        // highlighting info for pattern: Term --> List of path constraints
        public readonly Dictionary<int, List<List<Term>>> patternMatchContext = new Dictionary<int, List<List<Term>>>();

        // equalities inferred from pattern matching
        public readonly Dictionary<Term, List<Term>> equalities = new Dictionary<Term, List<Term>>();

        // number of equalities
        public int numEq;

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

            foreach (var effectiveTerm in EffectiveBlameTerms)
            {
                effectiveTerm.PrettyPrint(content, format);
                content.Append("\n\n");
            }
        }

        public BindingInfo(Term pattern, ICollection<Term> blameTerms)
        {
            fullPattern = pattern;
            unusedBlameTerms = new List<Term>(blameTerms);
        }

        private BindingInfo(BindingInfo other)
        {
            bindings = new Dictionary<Term, Term>(other.bindings);
            unusedBlameTerms = new List<Term>(other.unusedBlameTerms);
            additionalBlameTerms = new List<Term>(other.additionalBlameTerms);
            fullPattern = other.fullPattern;
            numEq = other.numEq;

            // 'deeper' copy
            matchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other.matchContext)
            {
                matchContext[context.Key] = new List<List<Term>>(context.Value);
            }

            // 'deeper' copy
            patternMatchContext = new Dictionary<int, List<List<Term>>>();
            foreach (var context in other.patternMatchContext)
            {
                patternMatchContext[context.Key] = new List<List<Term>>(context.Value);
            }

            // 'deeper' copy
            equalities = new Dictionary<Term, List<Term>>();
            foreach (var equality in other.equalities)
            {
                equalities[equality.Key] = new List<Term>(equality.Value);
            }

            // 'deeper' copy
            outstandingMatches = new Dictionary<Term, List<Term>>();
            foreach (var outstandingMatch in other.outstandingMatches)
            {
                outstandingMatches[outstandingMatch.Key] = new List<Term>(outstandingMatch.Value);
            }
        }

        public BindingInfo clone()
        {
            return new BindingInfo(this);
        }

        public List<BindingInfo> allNextMatches(Term pattern)
        {
            if (pattern.id == -1)
            {
                // free var, do not expect to find a blameterm
                var copy = clone();
                copy.handleOutstandingMatches(pattern);
                return new List<BindingInfo> { copy };
            }

            IEnumerable<Term> possibleMatches = pattern.ContainsFreeVar() ? unusedBlameTerms : unusedBlameTerms.Concat(new Term[] { pattern });
            return (from blameTerm in possibleMatches
                    let copy = clone()
                    where copy.matchBlameTerm(pattern, blameTerm)
                    select copy)
                    .ToList();
        }

        private bool matchBlameTerm(Term pattern, Term matchTerm)
        {
            if (!matchCondition(pattern, matchTerm)) return false;
            if (unusedBlameTerms.Contains(matchTerm))
            {
                unusedBlameTerms.Remove(matchTerm);
            }
            else
            {
                additionalBlameTerms.Add(matchTerm);
            }

            // add blame term without context
            // context is provided by previous matches
            addOutstandingMatch(pattern, matchTerm);
            handleOutstandingMatches(pattern);
            collectOutstandingCandidates();
            return true;
        }

        private void collectOutstandingCandidates()
        {
            foreach (var outstandingMatch in outstandingCandidates
                .SelectMany(outstandingCandidate => outstandingCandidate.Value))
            {
                addOutstandingMatch(outstandingMatch.Item1, outstandingMatch.Item2);
            }
            outstandingCandidates.Clear();
        }

        private void addOutstandingMatch(Term subPattern, Term outstandingCandidate)
        {
            if (!outstandingMatches.ContainsKey(subPattern))
            {
                outstandingMatches[subPattern] = new List<Term>();
            }
            outstandingMatches[subPattern].Add(outstandingCandidate);
        }

        private void handleOutstandingMatches(Term pattern)
        {
            // nothing outstanding
            if (!outstandingMatches.ContainsKey(pattern)) return;

            foreach (var term in outstandingMatches[pattern])
            { 
                handleMatch(pattern, term);
            }
            outstandingMatches.Remove(pattern);
        }

        private void addMatchContext(Term term, List<List<Term>> context)
        {
            if (!matchContext.ContainsKey(term.id)) matchContext[term.id] = new List<List<Term>>();
            matchContext[term.id].AddRange(context);
        }

        private void addPatternMatchContext(Term term, List<List<Term>> context)
        {
            if (!patternMatchContext.ContainsKey(term.id)) patternMatchContext[term.id] = new List<List<Term>>();
            patternMatchContext[term.id].AddRange(context);
        }

        private List<List<Term>> getContext(Term term)
        {
            if (!matchContext.ContainsKey(term.id)) matchContext[term.id] = new List<List<Term>>();
            return matchContext[term.id];
        }

        private void handleMatch(Term pattern, Term term)
        {
            if (bindings.ContainsKey(pattern) && bindings[pattern].id != term.id)
            {
                // already bound to something different!
                var currBinding = bindings[pattern];
                evictBinding(currBinding);

                // add equality
                addEquality(pattern, currBinding);
            }

            bindings[pattern] = term;
            foreach (var subPatternWithSubTerm in pattern.Args.Zip(term.Args, Tuple.Create))
            {
                var subPattern = subPatternWithSubTerm.Item1;
                var subTerm = subPatternWithSubTerm.Item2;
                var subTermContext = new List<List<Term>>();

                // build context for subterms
                if (getContext(term).Count == 0)
                {
                    subTermContext.Add(new List<Term> { term });
                }
                else
                {
                    subTermContext.AddRange(getContext(term).Select(history => new List<Term>(history) {term}));
                }
                addOutstandingCandidate(term, subPattern, subTerm);
                addMatchContext(subTerm, subTermContext);
            }
        }

        private void evictBinding(Term currBinding)
        {
            if (outstandingCandidates.ContainsKey(currBinding))
            {
                foreach (var childTerm in outstandingCandidates[currBinding].Select(tpl => tpl.Item2))
                {
                    // context of evicted childterms is irrelevant
                    matchContext.Remove(childTerm.id);
                }
                outstandingCandidates.Remove(currBinding);
            }
        }

        private void addOutstandingCandidate(Term term, Term subPattern, Term subTerm)
        {
            if (!outstandingCandidates.ContainsKey(term)) outstandingCandidates[term] = new List<Tuple<Term, Term>>();
            outstandingCandidates[term].Add(new Tuple<Term, Term>(subPattern, subTerm));
        }

        private void addEquality(Term pattern, Term currBinding)
        {
            if (!equalities.ContainsKey(pattern)) equalities[pattern] = new List<Term>();
            equalities[pattern].Add(currBinding);
            numEq++;
        }

        private static bool matchCondition(Term pattern, Term term)
        {
            // id -1 signifies free variable
            // every term matches the free variable pattern
            if (pattern.id == -1) return true;
            return pattern.Name == term.Name &&
                   pattern.GenericType == term.GenericType &&
                   pattern.Args.Length == term.Args.Length;
        }

        public bool finalize(List<Term> blameTerms, List<Term> boundTerms)
        {
            if (unusedBlameTerms.Count != 0 ||
                bindings.Count != boundTerms.Count + blameTerms.Count + additionalBlameTerms.Count) return false;

            // decouple collections
            var freeVarsToRebind = bindings.Where(kvPair => kvPair.Key.id == -1).Select(kvPair => kvPair.Key).ToList();
            if ((from freeVar in freeVarsToRebind
                let term = bindings[freeVar]
                where boundTerms.All(bndTerm => bndTerm.id != term.id)
                where !fixBindingWithEqLookUp(boundTerms, term, freeVar)
                select freeVar)
                .Any())
                return false;

            // declutter equalities
            // clutter happens if an evictor is evicted by the victim
            foreach (var eqPatterns in equalities.Keys.ToList())
            {
                var matched = bindings[eqPatterns];
                numEq -= equalities[eqPatterns].RemoveAll(eq => eq.id == matched.id);
            }

            // only keep top level terms
            additionalBlameTerms.RemoveAll(t1 => additionalBlameTerms.Any(t2 => t1 != t2 && t2.isSubterm(t1)));

            addPatternPathconditions();
            return true;
        }

        private void addPatternPathconditions()
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

        private bool fixBindingWithEqLookUp(List<Term> boundTerms, Term term, Term freeVar)
        {
            var eqFound = false;
            foreach (var bndTerm in boundTerms.Where(bndTerm => SameEqClass(term, bndTerm)))
            {
                addEquality(freeVar, term);
                bindings[freeVar] = bndTerm;
                eqFound = true;
                break;
            }
            return eqFound;
        }

        private static bool SameEqClass(Term t1, Term t2, ISet<Term> alreadyVisited = null)
        {
            if (t1.id == -1 || t2.id == -1) return false;
            if (t1.id == t2.id) return true;

            if (alreadyVisited == null)
            {
                alreadyVisited = new HashSet<Term>();
            }
            foreach (var equality in t1.dependentTerms
                .Where(dependentTerm => dependentTerm.Name == "=" && !alreadyVisited.Contains(dependentTerm))) {
                alreadyVisited.Add(equality);
                foreach (var term in equality.Args)
                {
                    if (alreadyVisited.Count > 1000) return false;
                    if (term != t1 && SameEqClass(term, t2, alreadyVisited))
                    {
                        return true;
                    }
                }
            }

            return t1.Name == t2.Name && t1.GenericType == t2.GenericType && t1.Args.Length == t2.Args.Length &&
                t1.Args.Zip(t2.Args, Tuple.Create).All(argEquality => SameEqClass(argEquality.Item1, argEquality.Item2));
        }

        public List<Term> getDistinctBlameTerms()
        {
            return bindings
                .Where(bnd => bnd.Key.id != -1)
                .Select(bnd => bnd.Value)
                .Where(term => getContext(term).Count == 0)
                .ToList();
        }

        public List<KeyValuePair<Term, Term>> getBindingsToFreeVars()
        {
            return bindings
                .Where(bnd => bnd.Key.id == -1)
                .ToList();
        }

        public bool validate()
        {
            return !(from equality in equalities
                     let eqBaseTerm = bindings[equality.Key]
                     where equality.Value.Any(otherTerm => !SameEqClass(eqBaseTerm, otherTerm))
                     select equality)
                     .Any();
        }
    }
}