using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Msagl.Core.DataStructures;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class BindingInfo
    {
        // bindings: freeVariable --> Term
        public readonly Dictionary<Term, Term> bindings = new Dictionary<Term, Term>();

        // highlighting info: Term --> List of path constraints
        public readonly Dictionary<Term, List<List<Term>>> highlightingInfo = new Dictionary<Term, List<List<Term>>>();

        // equalities inferred from pattern matching
        // lower id is item1!
        public readonly List<Tuple<Term, Term>> equalities = new List<Tuple<Term, Term>>();

        // Blame terms to build the validation equalities with
        public readonly HashSet<int> matchedTerms = new HashSet<int>();


        public bool merge(BindingInfo other, ICollection<Term> boundTerms)
        {
            if (other == null) return true; // allows unchecked aggregation
            List<KeyValuePair<Term, Term>> toAdd;
            if (!consistentBindings(other,boundTerms, out toAdd)) return false;

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

        public BindingInfo()
        {
        }

        private BindingInfo(BindingInfo other)
        {
            bindings = new Dictionary<Term, Term>(other.bindings);
            highlightingInfo = new Dictionary<Term, List<List<Term>>>(other.highlightingInfo);
            equalities = new List<Tuple<Term, Term>>(equalities);
            matchedTerms = new HashSet<int>(matchedTerms);
        }

        public BindingInfo clone()
        {
            return new BindingInfo(this);
        }

        private void mergeHighlightInfo(BindingInfo other)
        {
            foreach (var highlight in other.highlightingInfo)
            {
                if (highlightingInfo.ContainsKey(highlight.Key))
                {
                    var pathConstraints = highlightingInfo[highlight.Key];
                    pathConstraints.AddRange(highlight.Value.FindAll(constraint => !pathConstraints.Contains(constraint)));
                }
                else
                {
                    highlightingInfo[highlight.Key] = highlight.Value;
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
            if (!highlightingInfo.ContainsKey(term))
            {
                highlightingInfo[term] = new List<List<Term>>();
            }

            if (!highlightingInfo[term].Contains(constraint))
            {
                highlightingInfo[term].Add(constraint);
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
    }
}
