using System;
using System.Collections.Generic;
using System.Linq;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class BindingInfo
    {
        // Pattern used for this binding
        public Term fullPattern;
        public readonly List<Term> boundTerms;

        // unmatched blame terms
        private readonly List<Term> unusedBlameTerms;

        // outstanding checks
        private readonly Dictionary<Term, List<Tuple<Term, List<List<Term>>>>> outstandingChecks;

        // bindings: freeVariable --> Term
        public readonly Dictionary<Term, Term> bindings = new Dictionary<Term, Term>();

        // highlighting info: Term --> List of path constraints
        public readonly Dictionary<Term, List<List<Term>>> matchContext = new Dictionary<Term, List<List<Term>>>();

        // equalities inferred from pattern matching
        // lower id is item1!
        public readonly List<Tuple<Term, Term>> equalities = new List<Tuple<Term, Term>>();

        // Blame terms to build the validation equalities with
        public readonly HashSet<int> matchedTerms = new HashSet<int>();


        public bool merge(BindingInfo other, ICollection<Term> boundTerms)
        {
            if (other == null) return true; // allows unchecked aggregation
            List<KeyValuePair<Term, Term>> toAdd;
            if (!consistentBindings(other, boundTerms, out toAdd)) return false;

            // add missing bindings
            foreach (var keyValuePair in toAdd)
            {
                bindings[keyValuePair.Key] = keyValuePair.Value;
            }
            mergeHighlightInfo(other);
            equalities.AddRange(other.equalities.FindAll(eq => !equalities.Contains(eq)));
            matchedTerms.UnionWith(other.matchedTerms);
            return true;
        }

        public BindingInfo(ICollection<Term> blameTerms, ICollection<Term> boundTerms)
        {
            unusedBlameTerms = new List<Term>(blameTerms);
            boundTerms = new List<Term>(boundTerms);
        }

        private BindingInfo(BindingInfo other)
        {
            bindings = new Dictionary<Term, Term>(other.bindings);
            matchContext = new Dictionary<Term, List<List<Term>>>(other.matchContext);
            equalities = new List<Tuple<Term, Term>>(equalities);
            matchedTerms = new HashSet<int>(matchedTerms);
            unusedBlameTerms = new List<Term>(other.unusedBlameTerms);
        }

        public BindingInfo clone()
        {
            return new BindingInfo(this);
        }

        private void mergeHighlightInfo(BindingInfo other)
        {
            foreach (var highlight in other.matchContext)
            {
                if (matchContext.ContainsKey(highlight.Key))
                {
                    var pathConstraints = matchContext[highlight.Key];
                    pathConstraints.AddRange(highlight.Value.FindAll(constraint => !pathConstraints.Contains(constraint)));
                }
                else
                {
                    matchContext[highlight.Key] = highlight.Value;
                }
            }
        }

        public bool addBinding(Term freeVar, Term boundTo)
        {
            if (bindings.ContainsKey(freeVar))
            {
                return bindings[freeVar].id == boundTo.id;
            }
            bindings[freeVar] = boundTo;
            return true;
        }

        public void addHistoryConstraint(Term term, List<Term> constraint)
        {
            if (!matchContext.ContainsKey(term))
            {
                matchContext[term] = new List<List<Term>>();
            }

            if (!matchContext[term].Contains(constraint))
            {
                matchContext[term].Add(constraint);
            }
        }

        private bool consistentBindings(BindingInfo other, ICollection<Term> boundTerms,
            out List<KeyValuePair<Term, Term>> missingBindings)
        {
            missingBindings = new List<KeyValuePair<Term, Term>>();
            foreach (var binding in other.bindings)
            {

                if (bindings.ContainsKey(binding.Key)
                    && bindings[binding.Key].id != binding.Value.id)
                {
                    var thisBinding = bindings[binding.Key];
                    var otherBinding = binding.Value;

                    var thisBound = boundTerms.Any(term => term.id == thisBinding.id);
                    var otherBound = boundTerms.Any(term => term.id == otherBinding.id);

                    if (thisBound && otherBound)
                    {
                        return false;
                    }

                    if (thisBound || (!otherBound && thisBinding.id < otherBinding.id))
                    {
                        equalities.Add(new Tuple<Term, Term>(thisBinding, otherBinding));
                    }
                    else
                    {
                        bindings[binding.Key] = otherBinding;
                        equalities.Add(new Tuple<Term, Term>(otherBinding, thisBinding));
                    }

                }
                if (!bindings.ContainsKey(binding.Key))
                {
                    missingBindings.Add(binding);
                }
            }
            return true;
        }

        internal List<Term> FindCandidates(Term pattern)
        {
            return unusedBlameTerms.Where(term => matchCondition(pattern, term)).ToList();
        }

        private bool matchTerm(Term pattern, Term matchTerm)
        {
            unusedBlameTerms.Remove(matchTerm);
            var matchTermContext = new List<List<Term>>();

            if (outstandingChecks.ContainsKey(pattern))
            {
                if (!handleOutstandingMatches(pattern, matchTerm, matchTermContext)) return false;
            }

            return handleMatch(pattern, matchTerm, matchTermContext);
        }

        private bool handleOutstandingMatches(Term pattern, Term matchTerm, List<List<Term>> matchTermContext)
        {
            foreach (var termWithContext in outstandingChecks[pattern])
            {
                // outstanding term with its context
                var term = termWithContext.Item1;
                var context = termWithContext.Item2;

                // ensure the key exists
                if (!matchContext.ContainsKey(term)) matchContext[term] = new List<List<Term>>();

                matchContext[term].AddRange(context);
                if (matchCondition(pattern, term))
                {
                    if (!handleMatch(pattern, term, context)) return false;
                    if (term.id == matchTerm.id)
                    {
                        matchTermContext.AddRange(context); // carry on the history in nested blame terms
                    }
                }
                else
                {
                    // add equality requirement with current candidate
                    // as the candidate is matched
                    equalities.Add(new Tuple<Term, Term>(term, matchTerm));
                }
            }
            return true;
        }

        private bool handleMatch(Term pattern, Term term, List<List<Term>> context)
        {
            if (pattern.id == -1)
            {
                if (bindings[pattern] == null)
                {
                    // no binding yet, add one
                    bindings[pattern] = term;
                }
                else if (bindings[pattern].id != term.id)
                {
                    // already bound to something different!
                    var currBinding = bindings[pattern];
                    var currBindingIsBound = boundTerms.Any(bt => bt.id == currBinding.id);
                    var termIsBound = boundTerms.Any(bt => bt.id == term.id);

                    if (!termIsBound)
                    {
                        equalities.Add(new Tuple<Term, Term>(currBinding, term));
                    }
                    else if (!currBindingIsBound)
                    {
                        bindings[pattern] = term;
                        equalities.Add(new Tuple<Term, Term>(term, currBinding));
                    }
                    else
                    {
                        // inconsistent!
                        return false;
                    }
                }

                // binding added or already bound to the exact same thing --> consistent
                return true;
            }

            foreach (var subPatternWithSubTerm in pattern.Args.Zip(term.Args, Tuple.Create))
            {
                var subPattern = subPatternWithSubTerm.Item1;
                var subTerm = subPatternWithSubTerm.Item2;
                var outstandingItem = new Tuple<Term, List<List<Term>>>(subTerm, new List<List<Term>>());
                foreach (var history in context)
                {
                    var copy = new List<Term>(history);
                    copy.Add(term);
                    outstandingItem.Item2.Add(copy);
                }
                outstandingChecks[subPattern].Add(outstandingItem);
            }
            return true;
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
    }
}
