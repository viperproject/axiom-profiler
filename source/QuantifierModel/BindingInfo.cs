﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class BindingInfo
    {
        // Pattern used for this binding
        public Term fullPattern;

        // Terms that are bound in the instantiation
        public readonly List<Term> boundTerms;

        // unmatched blame terms
        private readonly List<Term> unusedBlameTerms;

        // outstanding checks
        private readonly Dictionary<Term, List<Tuple<Term, List<List<Term>>>>> outstandingChecks = new Dictionary<Term, List<Tuple<Term, List<List<Term>>>>>();

        // bindings: freeVariable --> Term
        public readonly Dictionary<Term, Term> bindings = new Dictionary<Term, Term>();

        // highlighting info: Term --> List of path constraints
        public readonly Dictionary<Term, List<List<Term>>> matchContext = new Dictionary<Term, List<List<Term>>>();

        // equalities inferred from pattern matching
        // lower id is item1!
        public readonly List<Tuple<Term, Term>> equalities = new List<Tuple<Term, Term>>();


        public BindingInfo(Term pattern, ICollection<Term> blameTerms, ICollection<Term> bindings)
        {
            fullPattern = pattern;
            unusedBlameTerms = new List<Term>(blameTerms);
            boundTerms = new List<Term>(bindings);
        }

        private BindingInfo(BindingInfo other)
        {
            bindings = new Dictionary<Term, Term>(other.bindings);
            matchContext = new Dictionary<Term, List<List<Term>>>(other.matchContext);
            equalities = new List<Tuple<Term, Term>>(equalities);
            unusedBlameTerms = new List<Term>(other.unusedBlameTerms);
            fullPattern = other.fullPattern;
            outstandingChecks = new Dictionary<Term, List<Tuple<Term, List<List<Term>>>>>(other.outstandingChecks);
            boundTerms = other.boundTerms;
        }

        private BindingInfo clone()
        {
            return new BindingInfo(this);
        }

        public List<BindingInfo> allNextMatches(Term pattern)
        {
            if (pattern.id == -1)
            {
                var bindingMatches = new List<BindingInfo>();
                foreach (var boundTerm in boundTerms)
                {
                    var copy = clone();
                    if(copy.handleOutstandingMatches(pattern, boundTerm)) bindingMatches.Add(copy);
                }
                return bindingMatches;
            }
            return (from blameTerm in unusedBlameTerms
                    let copy = clone()
                    where copy.matchTerm(pattern, blameTerm)
                    select copy)
                    .ToList();
        }

        private bool matchTerm(Term pattern, Term matchTerm)
        {
            if (!matchCondition(pattern, matchTerm)) return false;

            unusedBlameTerms.Remove(matchTerm);
            return handleOutstandingMatches(pattern, matchTerm) &&
                handleMatch(pattern, matchTerm);
        }

        private bool handleOutstandingMatches(Term pattern, Term matchTerm)
        {
            if (outstandingChecks.ContainsKey(pattern))
            {
                foreach (var termWithContext in outstandingChecks[pattern])
                {
                    // outstanding term with its context
                    var term = termWithContext.Item1;
                    var context = termWithContext.Item2;

                    // ensure the key exists
                    addMatchContext(term, context);
                    if (matchCondition(pattern, term))
                    {
                        if (!handleMatch(pattern, term)) return false;
                        if (term.id == matchTerm.id)
                        {
                            addMatchContext(matchTerm, context); // carry on the history in nested blame terms
                        }
                    }
                    else
                    {
                        // add equality requirement with current candidate
                        // as the candidate is matched
                        equalities.Add(new Tuple<Term, Term>(term, matchTerm));
                    }
                }
                outstandingChecks.Remove(pattern);
            }
            return true;
        }

        private void addMatchContext(Term term, List<List<Term>> context)
        {
            if (!matchContext.ContainsKey(term)) matchContext[term] = new List<List<Term>>();
            matchContext[term].AddRange(context);
        }

        private List<List<Term>> getContext(Term term)
        {
            if (!matchContext.ContainsKey(term)) matchContext[term] = new List<List<Term>>();
            return matchContext[term];
        } 

        private bool handleMatch(Term pattern, Term term)
        {
            if (pattern.id == -1)
            {
                if (!bindings.ContainsKey(pattern))
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
                foreach (var copy in getContext(term).Select(history => new List<Term>(history) { term }))
                {
                    outstandingItem.Item2.Add(copy);
                }
                if (!outstandingChecks.ContainsKey(subPattern))
                {
                    outstandingChecks[subPattern] = new List<Tuple<Term, List<List<Term>>>>();
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

        public bool isFinishedPlausible()
        {
            return unusedBlameTerms.Count == 0 && bindings.Count == boundTerms.Count;
        }
    }
}
