using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler.QuantifierModel
{
    public class InstantiationPath : IPrintable
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

        public string InfoPanelText(PrettyPrintFormat format)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Path explanation:");
            builder.Append("\n------------------------\n");
            builder.Append("Length: ").Append(Length()).Append('\n');
            builder.Append("Cost: ").Append(Cost()).Append('\n');

            Instantiation previous = null;
            foreach (var instantiation in pathInstantiations)
            {
                if (previous != null)
                {
                    builder.Append("Link term: \n\n");
                    var term = findOverlap(previous, instantiation);
                    if (term == null)
                    {
                        builder.Append("No overlap found!!!\n");
                    }
                    else
                    {
                        builder.Append(term.PrettyPrint(format));
                    }
                }

                builder.Append("\n------------------------\n");
                builder.Append(instantiation.SummaryInfo());

                previous = instantiation;
            }

            return builder.ToString();
        }

        public IEnumerable<Instantiation> getInstantiations()
        {
            return pathInstantiations;
        }

        private Term findOverlap(Instantiation parent, Instantiation child)
        {
            var termsToCheck = new List<Term>();
            termsToCheck.AddRange(child.Responsible);
            termsToCheck.AddRange(child.Bindings);
            return termsToCheck.FirstOrDefault(term => parent.dependentTerms.Contains(term));
        }
    }
}
