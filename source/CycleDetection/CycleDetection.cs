using System.Collections.Generic;
using System.Linq;
using Z3AxiomProfiler.QuantifierModel;

namespace Z3AxiomProfiler.CycleDetection
{
    public class CycleDetection
    {
        private IEnumerable<Instantiation> path;
        private bool processed;
        private string cycle;
        private readonly int minRepetitions;
        private const char endChar = char.MaxValue;
        private char currMap = char.MinValue;
        private readonly Dictionary<string, char> mapping = new Dictionary<string, char>();
        private readonly Dictionary<char, List<Instantiation>> reverseMapping = new Dictionary<char, List<Instantiation>>();

        public CycleDetection(IEnumerable<Instantiation> pathToCheck, int minRep)
        {
            path = pathToCheck;
            minRepetitions = minRep;
        }

        public bool hasCycle()
        {
            if (!processed) findCycle();
            return !string.IsNullOrEmpty(cycle);
        }

        public List<Quantifier> getCycleQuantifiers()
        {
            var result = new List<Quantifier>();
            if (!hasCycle()) return result;
            result.AddRange(cycle.Where(c => c != endChar).Select(c => reverseMapping[c].First().Quant));
            return result;
        }

        private void findCycle()
        {
            var chars = new List<char>();

            // map the instantiations
            foreach (var instantiation in path)
            {
                var key = instantiation.Quant.BodyTerm.id + "" +
                    instantiation.bindingInfo.fullPattern.id + "" +
                    instantiation.bindingInfo.numEq;
                if (!mapping.ContainsKey(key))
                {
                    mapping[key] = currMap;
                    currMap++;
                }
                var charValue = mapping[key];
                chars.Add(charValue);
                if (!reverseMapping.ContainsKey(charValue))
                {
                    reverseMapping[charValue] = new List<Instantiation>();
                }
                reverseMapping[charValue].Add(instantiation);
            }
            chars.Add(endChar);

            // search for cycles
            var suffixTree = new SuffixTree.SuffixTree(chars.Count);
            foreach (var c in chars)
            {
                suffixTree.addChar(c);
            }
            suffixTree.finalize();

            cycle = suffixTree.getLongestCycle(minRepetitions);
            processed = true;
        }
    }
}
