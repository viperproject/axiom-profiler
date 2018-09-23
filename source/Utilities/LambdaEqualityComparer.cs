using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxiomProfiler.Utilities
{
    /// <summary>
    /// Allows a lambda expression to be used to compare two objects.
    /// </summary>
    /// <example>
    /// <code>
    /// enumerable.Distinct(new LambdaEqualityComparer((a, b) => sign(a) == sign(b)))
    /// </code>
    /// </example>
    class LambdaEqualityComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> lambda;

        public LambdaEqualityComparer(Func<T, T, bool> lambda)
        {
            this.lambda = lambda;
        }

        public bool Equals(T first, T second)
        {
            return lambda(first, second);
        }

        public int GetHashCode(T obj)
        {
            return 0;
        }
    }
}
