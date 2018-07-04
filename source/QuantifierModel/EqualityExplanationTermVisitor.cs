using System;

namespace AxiomProfiler.QuantifierModel
{
    class EqualityExplanationTermVisitor : EqualityExplanationVisitor<object, Action<Term>>
    {
        public static readonly EqualityExplanationTermVisitor singleton = new EqualityExplanationTermVisitor();

        public override object Direct(DirectEqualityExplanation target, Action<Term> arg)
        {
            arg(target.source);
            arg(target.target);
            arg(target.equality);
            return null;
        }

        public override object RecursiveReference(RecursiveReferenceEqualityExplanation target, Action<Term> arg)
        {
            arg(target.source);
            arg(target.target);
            return null;
        }

        public override object Transitive(TransitiveEqualityExplanation target, Action<Term> arg)
        {
            arg(target.source);
            arg(target.target);
            foreach (var eq in target.equalities)
            {
                visit(eq, arg);
            }
            return null;
        }

        public override object Congruence(CongruenceExplanation target, Action<Term> arg)
        {
            arg(target.source);
            arg(target.target);
            foreach (var eq in target.sourceArgumentEqualities)
            {
                visit(eq, arg);
            }
            return null;
        }

        public override object Theory(TheoryEqualityExplanation target, Action<Term> arg)
        {
            arg(target.source);
            arg(target.target);
            return null;
        }
    }
}
