using System.Collections.Generic;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class Partition
    {
        public List<FunApp> Values = new List<FunApp>();
        public string Id;
        internal Model model;
        string shortName;
        FunApp bestApp;

        public FunApp BestApp()
        {
            if (bestApp != null)
                return bestApp;

            int cntMin = 0;
            int minBadness = 0;

            foreach (var f in Values)
            {
                int cur = model.FunAppBadness(f);
                if (bestApp == null || cur <= minBadness)
                {
                    if (minBadness < cur) cntMin = 0;
                    cntMin++;
                    minBadness = cur;
                    bestApp = f;
                }
            }

            if (cntMin > 1)
            {
                string prevName = bestApp.ShortName();
                foreach (var f in Values)
                {
                    int cur = model.FunAppBadness(f);
                    if (cur == minBadness)
                    {
                        string curName = f.ShortName();
                        if (curName.Length < prevName.Length ||
                            (curName.Length == prevName.Length && string.CompareOrdinal(curName, prevName) < 0))
                        {
                            prevName = curName;
                            bestApp = f;
                        }
                    }
                }
            }

            return bestApp;
        }

        public string ShortName()
        {
            if (shortName != null)
                return shortName;

            FunApp best = BestApp();
            shortName = Id;
            shortName = best.ShortName();

            return shortName;
        }
    }
}