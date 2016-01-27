using System.Collections.Generic;
using System.Linq;
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

        public void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            content.Append("Path explanation:");
            content.Append("\n------------------------\n");
            content.Append("Length: " + Length()).Append('\n');
            content.Append("Cost: " + Cost()).Append('\n');

            Instantiation previous = null;
            foreach (var instantiation in pathInstantiations)
            {
                if (previous != null)
                {
                    content.Append("\nLink term: \n\n");
                    var term = findOverlap(previous, instantiation);
                    content.Append(term.PrettyPrint(format));
                    content.Append("\n\n");
                }

                instantiation.SummaryInfo(content);

                var biggestTerm = instantiation.dependentTerms[instantiation.dependentTerms.Count - 1];
                content.Append("\nThis instantiation yields:\n\n");
                content.Append(biggestTerm.PrettyPrint(format));
                content.Append("\n------------------------\n");

                previous = instantiation;
            }
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
