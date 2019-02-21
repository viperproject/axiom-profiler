using System;
using System.Collections.Generic;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler.QuantifierModel
{
    // Class to represent a push statement.
    public class ScopeDesc
    {
        // The underlying scope.
        public Scope Scope;

        // The first [assigne] literal.
        // This is stored as the cause of all implied literals.
        public Literal Literal;

        // All literals implied by this scope.
        public readonly List<Literal> Implied = new List<Literal>();

        // Number of quantifier instantiations in this scope.
        public int InstanceCount;

        public readonly int checkNumber;
        public ScopeDesc(int checkNo)
        {
            checkNumber = checkNo;
        }
    }
    public class Scope : Common
    {
        // Literals implied in this scope.
        public readonly List<Literal> Literals = new List<Literal>();

        // Literals implied at parent.
        // Will be assigned after parsing for the tree structure.
        private Literal[] ImpliedAtParent;

        // Conflict that was found in this proof branch.
        public Conflict Conflict;

        // All children scopes.
        public readonly List<Scope> ChildrenScopes = new List<Scope>();

        // Depth of this scope.
        public int level;

        public Scope parentScope;

        // Recursively calculated information about this scope.
        int recursiveConflictCount = -1;
        int recursiveInstanceCount = -1;
        int recurisveInstanceDepth = -1;

        // Largest conflict (measured in the number of Literals).
        int maxConflictSize = 0;

        bool recursiveInstanceCountComputed;

        // Identifier based on the conflict in this scope or below.
        int id = -1;
        int subid;

        public readonly int checkNumber;

        public Scope(int checkNo)
        {
            checkNumber = checkNo;
        }

        // Number of quantifier instantiations in this level.
        public int OwnInstanceCount;

        public int InstanceCount
        {
            get
            {
                if (recursiveInstanceCountComputed) return recursiveInstanceCount;
                recursiveInstanceCountComputed = true;
                recursiveInstanceCount = OwnInstanceCount;
                foreach (var c in ChildrenScopes)
                    recursiveInstanceCount += c.InstanceCount;
                return recursiveInstanceCount;
            }
        }

        public string LastDecisionQID()
        {
            if (Conflict == null || Literals.Count == 0) return null;

            Literal l = Literals[Literals.Count - 1];
            if (l.Clause == null || l.Clause.Name != "or" || l.Clause.Args[0].Name != "not") return null;
            string qid = l.Clause.Args[0].Args[0].Name;
            return qid;
        }

        internal void AddChildScope(Scope s)
        {
            ChildrenScopes.Add(s);
            s.parentScope = this;
        }

        public int RecurisveInstanceDepth
        {
            get
            {
                var dummy = RecursiveConflictCount;
                return recurisveInstanceDepth;
            }
        }

        public int RecursiveConflictCount
        {
            get
            {
                if (recursiveConflictCount >= 0) return recursiveConflictCount;
                recursiveConflictCount = 0;
                if (Conflict != null)
                    maxConflictSize = Conflict.Literals.Count;
                foreach (var c in ChildrenScopes)
                {
                    recursiveConflictCount += c.RecursiveConflictCount;
                    if (c.maxConflictSize > maxConflictSize)
                        maxConflictSize = c.maxConflictSize;
                    if (c.recurisveInstanceDepth > recurisveInstanceDepth)
                        recurisveInstanceDepth = c.recurisveInstanceDepth;
                }
                recurisveInstanceDepth += OwnInstanceCount;
                if (Conflict != null) recursiveConflictCount++;
                return recursiveConflictCount;
            }
        }

        public override string ToString()
        {
            if (id == -1)
            {
                if (Conflict == null)
                {
                    if (ChildrenScopes != null && ChildrenScopes.Count > 0)
                    {
                        ChildrenScopes[0].ToString();
                        id = ChildrenScopes[0].id;
                        subid = ChildrenScopes[0].subid + 1;
                    }
                    else
                    {
                        id = 0;
                    }
                }
                else
                {
                    id = Conflict.Id;
                }
            }

            string res = string.Format("Scope#{5}: {0} / {2} inst, {1} lits [{3}level:{4}]",
                InstanceCount, Literals.Count, OwnInstanceCount, level < 0 ? "f" : "", level < 0 ? -level : level,
                string.Format("{0}.{1}", id, subid));
            if (ChildrenScopes.Count > 0)
                res += string.Format(", {0} children [rec: {1}, {2} inst/cnfl]",
                    ChildrenScopes.Count, RecursiveConflictCount,
                    (RecursiveConflictCount == 0) ? "inf" :
                        (InstanceCount / RecursiveConflictCount).ToString());
            var dummy = RecursiveConflictCount;
            if (maxConflictSize > 0)
                res += string.Format(" maxCnflSz: {0}", maxConflictSize);
            return res;
        }

        public override void InfoPanelText(InfoPanelContent content, PrettyPrintFormat format)
        {
            if (Conflict != null)
            {
                Conflict.InfoPanelText(content, format);
            }
            else
            {
                content.Append("No conflict");
            }
        }

        public void PropagateImpliedByChildren()
        {
            if (Literals.Count > 0)
            {
                int scopeNo = 0;
                int firstMarker = -1;
                int prevMarker = -1;
                var last = Literals[0];
                var lastImpl = last.Implied;

                for (int idx = 0; idx <= lastImpl.Length; ++idx)
                {
                    if (idx == lastImpl.Length || lastImpl[idx] == Model.MarkerLiteral)
                    {
                        if (firstMarker < 0) firstMarker = idx;
                        if (prevMarker >= 0)
                        {
                            var arr = new Literal[idx - prevMarker - 1];
                            Array.Copy(lastImpl, prevMarker + 1, arr, 0, arr.Length);
                            var scope = ChildrenScopes[scopeNo++];
                            scope.ImpliedAtParent = arr;
                            foreach (var l in arr)
                                l.Cause = scope;
                        }
                        prevMarker = idx;
                    }
                }
                if (!(scopeNo == ChildrenScopes.Count || scopeNo == ChildrenScopes.Count - 1))
                    Console.WriteLine("Scopes are inconsistent. Is this log incomplete?");

                if (scopeNo > 0)
                {
                    last.Implied = new Literal[firstMarker];
                    Array.Copy(lastImpl, last.Implied, firstMarker);
                }
            }

            foreach (var v in ChildrenScopes)
                v.PropagateImpliedByChildren();
        }

        public void AccountLastDecision(Model m)
        {
            /*string qid = this.LastDecisionQID();
            Quantifier q;
            if (qid != null && m.quantifiers.TryGetValue(qid, out q))
            {
                q.GeneratedConflicts++;
            }
            foreach (var c in ChildrenScopes)
                c.AccountLastDecision(m);*/
        }

        public void ComputeConflictCost(List<Conflict> acc)
        {
            Literals.Reverse();

            int pos = acc.Count;
            foreach (var c in ChildrenScopes)
                c.ComputeConflictCost(acc);
            if (Conflict != null) acc.Add(Conflict);
            if (acc.Count == pos) return;
            double off = (double)OwnInstanceCount / (acc.Count - pos);
            if (off > 0)
                while (pos < acc.Count)
                {
                    acc[pos].InstCost += off;
                    pos++;
                }
        }

        public override IEnumerable<Common> Children()
        {
            yield return Callback("LITERALS [" + Literals.Count + "]", () => Literals);
            if (Conflict != null)
                yield return Conflict;
            if (ImpliedAtParent != null)
                yield return Callback("AT PARENT [" + ImpliedAtParent.Length + "]", () => ImpliedAtParent);
            foreach (var c in ChildrenScopes)
            {
                yield return c;
            }
        }
    }
}