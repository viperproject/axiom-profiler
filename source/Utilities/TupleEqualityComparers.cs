using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxiomProfiler.Utilities.TupleEqulityComparers
{
    class FirstComparer<T1, T2> : IEqualityComparer<Tuple<T1, T2>>
    {
        private IEqualityComparer<T1> deleg = EqualityComparer<T1>.Default;

        public bool Equals(Tuple<T1, T2> first, Tuple<T1, T2> second)
        {
            return deleg.Equals(first.Item1, second.Item1);
        }

        public int GetHashCode(Tuple<T1, T2> obj)
        {
            return deleg.GetHashCode(obj.Item1);
        }
    }
}
