using AxiomProfiler.PrettyPrinting;
using System;

namespace AxiomProfiler.QuantifierModel
{
    public abstract class EqualityExplanation
    {
        public readonly Term source, target;

        protected EqualityExplanation(Term source, Term target)
        {
            this.source = source;
            this.target = target;
        }

        abstract internal protected R Accept<R, A>(EqualityExplanationVisitor<R, A> visitor, A arg);

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (EqualityExplanation) obj;
            return source.id == other.source.id && target.id == other.target.id;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return source.GetHashCode();
        }

        public void PrettyPrint(InfoPanelContent content, PrettyPrintFormat format, int eqNumber)
        {
            var numberingString = $"({eqNumber}) ";
            content.switchToDefaultFormat();
            content.Append(numberingString);
            source.PrettyPrint(content, format, numberingString.Length);
            EqualityExplanationPrinter.singleton.visit(this, Tuple.Create(content, format, false, numberingString.Length));
        }
    }

    public class DirectEqualityExplanation : EqualityExplanation
    {
        public readonly Term equality;

        public DirectEqualityExplanation(Term source, Term target, Term equality) : base(source, target)
        {
            this.equality = equality;
        }
        
        override internal protected R Accept<R, A>(EqualityExplanationVisitor<R, A> visitor, A arg)
        {
            return visitor.Direct(this, arg);
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType() || !base.Equals(obj))
            {
                return false;
            }

            var other = (DirectEqualityExplanation) obj;
            return equality.id == other.equality.id;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return source.GetHashCode();
        }
    }

    public class TransitiveEqualityExplanation : EqualityExplanation
    {
        public readonly EqualityExplanation[] equalities;

        public TransitiveEqualityExplanation(Term source, Term target, EqualityExplanation[] equalities) : base(source, target)
        {
            this.equalities = equalities;
        }

        override internal protected R Accept<R, A>(EqualityExplanationVisitor<R, A> visitor, A arg)
        {
            return visitor.Transitive(this, arg);
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (ReferenceEquals(this, obj)) return true;

            if (obj == null || GetType() != obj.GetType() || !base.Equals(obj))
            {
                return false;
            }

            var other = (TransitiveEqualityExplanation) obj;
            if (equalities.Length != other.equalities.Length) return false;
            for (var i = 0; i < equalities.Length; ++i)
            {
                if (!equalities[i].Equals(other.equalities[i])) return false;
            }
            return true;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return source.GetHashCode();
        }
    }

    public class CongruenceExplanation : EqualityExplanation
    {
        public readonly EqualityExplanation[] sourceArgumentEqualities;

        public CongruenceExplanation(Term source, Term target, EqualityExplanation[] sourceArgumentEqualities) : base(source, target)
        {
            this.sourceArgumentEqualities = sourceArgumentEqualities;
        }

        override internal protected R Accept<R, A>(EqualityExplanationVisitor<R, A> visitor, A arg)
        {
            return visitor.Congruence(this, arg);
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (ReferenceEquals(this, obj)) return true;

            if (obj == null || GetType() != obj.GetType() || !base.Equals(obj))
            {
                return false;
            }

            var other = (CongruenceExplanation) obj;
            foreach (var equality in sourceArgumentEqualities)
            {
                var found = false;
                foreach (var otherEquality in other.sourceArgumentEqualities)
                {
                    if (equality.Equals(otherEquality))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            foreach (var equality in other.sourceArgumentEqualities)
            {
                var found = false;
                foreach (var otherEquality in sourceArgumentEqualities)
                {
                    if (equality.Equals(otherEquality))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            return true;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return source.GetHashCode();
        }
    }

    public class RecursiveReferenceEqualityExplanation: EqualityExplanation
    {
        public readonly int EqualityNumber;
        public readonly int GenerationOffset;
        public readonly bool isPrime;

        public RecursiveReferenceEqualityExplanation(Term source, Term target, int EqualityNumber, int GenerationOffset, bool isPrime = false): base(source, target)
        {
            this.EqualityNumber = EqualityNumber;
            this.GenerationOffset = GenerationOffset;
            this.isPrime = isPrime;
        }

        protected internal override R Accept<R, A>(EqualityExplanationVisitor<R, A> visitor, A arg)
        {
            return visitor.RecursiveReference(this, arg);
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (RecursiveReferenceEqualityExplanation) obj;

            if (EqualityNumber != other.EqualityNumber || GenerationOffset != other.GenerationOffset) return false;

            return base.Equals(obj);
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return source.GetHashCode();
        }
    }

    public abstract class EqualityExplanationVisitor<R, A>
    {
        public R visit(EqualityExplanation target, A arg)
        {
            return target.Accept(this, arg);
        }

        public abstract R Direct(DirectEqualityExplanation target, A arg);

        public abstract R Transitive(TransitiveEqualityExplanation target, A arg);

        public abstract R Congruence(CongruenceExplanation target, A arg);

        public abstract R RecursiveReference(RecursiveReferenceEqualityExplanation target, A arg);
    }
}
