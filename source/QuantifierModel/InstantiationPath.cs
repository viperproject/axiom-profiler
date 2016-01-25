using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class InstantiationPath
    {
        private readonly List<Instantiation> pathInstantiations;

        public InstantiationPath()
        {
            pathInstantiations = new List<Instantiation>();
        }

        public InstantiationPath(InstantiationPath other) : this()
        {
            pathInstantiations.AddRange(other.pathInstantiations);
        }

        public InstantiationPath(Instantiation inst) : this()
        {
            pathInstantiations.Add(inst);
        }

        public double Cost()
        {
            return pathInstantiations.Sum(instantiation => instantiation.Cost);
        }

        public int Length()
        {
            return pathInstantiations.Count;
        }

        public void prepend(Instantiation inst)
        {
            pathInstantiations.Insert(0, inst);
        }

        public void append(Instantiation inst)
        {
            pathInstantiations.Add(inst);
        }

        public void appendWithOverlap(InstantiationPath other)
        {
            var joinIdx = other.pathInstantiations.FindIndex(inst => !pathInstantiations.Contains(inst));
            if (other.Length() == 0 || joinIdx == -1)
            {
                return;
            }
            pathInstantiations.AddRange(other.pathInstantiations.GetRange(joinIdx, other.pathInstantiations.Count - joinIdx));
        }

        public string toString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Path explanation:");
            builder.Append("\n------------------------\n");
            builder.Append("Length: ").Append(Length()).Append('\n');
            builder.Append("Cost: ").Append(Cost()).Append('\n');
            foreach (var instantiation in pathInstantiations)
            {
                builder.Append("\n------------------------\n");
                builder.Append(instantiation.SummaryInfo());
            }

            return builder.ToString();
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }
    }
}
