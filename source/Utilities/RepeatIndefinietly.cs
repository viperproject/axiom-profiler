using System.Collections.Generic;
using System.Linq;

namespace AxiomProfiler.Utilities
{
    public static class RepeatIndefinietely
    {
        public static IEnumerable<T> RepeatIndefinietly<T>(this IEnumerable<T> source)
        {
            var list = source.ToList();
            while (true)
            {
                foreach (var item in list)
                {
                    yield return item;
                }
            }
        }
    }
}
