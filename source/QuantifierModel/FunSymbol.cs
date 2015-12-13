using System;
using System.Collections.Generic;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class FunSymbol : Common
    {
        public string Name;
        public List<FunApp> Apps = new List<FunApp>();
        public List<FunSymbol> AllSymbols;

        static bool appsByPartition = false;

        public override IEnumerable<Common> Children()
        {
            if (Apps.Count == 1)
                return Apps[0].Children();

            if (appsByPartition)
            {
                // group by partition
                Dictionary<string, List<Common>> byPart = new Dictionary<string, List<Common>>();
                foreach (var f in Apps)
                {
                    List<Common> tmp;
                    if (!byPart.TryGetValue(f.Value.Id, out tmp))
                    {
                        tmp = new List<Common>();
                        byPart.Add(f.Value.Id, tmp);
                    }
                    tmp.Add(f);
                }
                // sort by partition size
                List<List<Common>> lists = new List<List<Common>>(byPart.Values);
                lists.Sort(delegate (List<Common> x, List<Common> y) { return y.Count - x.Count; });
                List<Common> res = new List<Common>();
                foreach (var l in lists)
                    res.AddRange(l);
                return res;
            }
            Apps.Sort(delegate (FunApp a1, FunApp a2)
            {
                for (int i = 0; i < a1.Args.Length; ++i)
                {
                    int tmp = string.CompareOrdinal(a1.Args[i].ShortName(), a2.Args[i].ShortName());
                    if (tmp != 0) return tmp;
                }
                return 0;
            });
            return ConvertIEnumerable<Common, FunApp>(Apps);
        }

        static long[] maxValues = { short.MaxValue, int.MaxValue, long.MaxValue };
        static string[] maxValueNames = { "INT16", "INT32", "INT64" };

        string cachedDisplayName;
        public string DisplayName
        {
            get
            {
                if (cachedDisplayName == null)
                {
                    cachedDisplayName = Name;

                    int idx = Name.LastIndexOf("@@");
                    if (idx > 0)
                    {
                        int end = idx + 2;
                        while (end < Name.Length && Char.IsDigit(Name[end]))
                            end++;
                        if (end == Name.Length)
                            cachedDisplayName = Name.Substring(0, idx);
                    }

                    long v;
                    if (long.TryParse(cachedDisplayName, out v))
                    {
                        for (int i = 0; i < maxValues.Length; ++i)
                        {
                            long diff = v - maxValues[i];
                            if (Math.Abs((double)diff) < maxValues[i] / 100)
                            {
                                cachedDisplayName = MaxValueName("MAX", i, diff);
                                break;
                            }
                        }

                        for (int i = 0; i < maxValues.Length; ++i)
                        {
                            long diff = v - (-maxValues[i] - 1);
                            if (Math.Abs((double)diff) < maxValues[i] / 100)
                            {
                                cachedDisplayName = MaxValueName("MIN", i, diff);
                                break;
                            }
                        }

                        // TODO: should use bignums to handle MAXUINT64+/-
                        for (int i = 0; i < maxValues.Length - 1; ++i)
                        {
                            long diff = v - (maxValues[i] + maxValues[i]);
                            if (Math.Abs((double)diff) < maxValues[i] / 100)
                            {
                                cachedDisplayName = MaxValueName("MAXU", i, diff);
                                break;
                            }
                        }
                    }
                }

                return cachedDisplayName;
            }
        }

        private string MaxValueName(string pref, int i, long diff)
        {
            pref += maxValueNames[i];
            if (diff == 0) return pref;
            return string.Format("{0}{1}{2}", pref, diff >= 0 ? "+" : "", diff);
        }

        public override string ToString()
        {
            if (Apps.Count == 1)
                return Apps[0].ToString();
            else
                return DisplayName + " [" + Apps.Count + "]";
        }

    }
}