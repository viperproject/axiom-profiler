using System;
using System.Collections.Generic;
using System.Linq;

namespace Z3AxiomProfiler.QuantifierModel
{
    class PatternMatchInfo
    {
        // bindings: freeVariable --> Term
        public readonly Dictionary<Term, Term> bindings = new Dictionary<Term, Term>();

        // highlighting info: Term --> List of path constraints
        public readonly Dictionary<Term, List<List<Term>>> highlightingInfo = new Dictionary<Term, List<List<Term>>>();

        // equalities inferred from pattern matching
        // lower id is item1!
        public readonly List<Tuple<Term, Term>> equalities = new List<Tuple<Term, Term>>();


        public bool merge(PatternMatchInfo other)
        {
            List<KeyValuePair<Term, Term>> toAdd;
            if (!consistentBindings(other, out toAdd)) return false;

            // add missing bindings
            foreach (var keyValuePair in toAdd)
            {
                bindings[keyValuePair.Key] = keyValuePair.Value;
            }
            mergeHighlightInfo(other);
            equalities.AddRange(other.equalities.FindAll(eq => !equalities.Contains(eq)));
            return true;
        }

        private void mergeHighlightInfo(PatternMatchInfo other)
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

        private bool consistentBindings(PatternMatchInfo other, out List<KeyValuePair<Term, Term>> missingBindings)
        {
            missingBindings = new List<KeyValuePair<Term, Term>>();
            foreach (var binding in other.bindings)
            {
                if (bindings.ContainsKey(binding.Key)
                    && bindings[binding.Key].id != binding.Value.id)
                {
                    return false;
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
