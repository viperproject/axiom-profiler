//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AxiomProfiler.QuantifierModel
{
    /// <summary>
    ///  Abstraction of the Quantifier Model.
    /// </summary>
    public class Model
    {
        private class Namespace
        {
            // dict with all terms seen so far.
            public readonly Dictionary<int, Term> terms = new Dictionary<int, Term>();

            // dict with all quanitfiers seen so far.
            public readonly Dictionary<int, Quantifier> quantifiers = new Dictionary<int, Quantifier>();
        }

        // terms and quantifiers for each namespace
        private readonly Dictionary<string, Namespace> namespaces = new Dictionary<string, Namespace>() { [""] = new Namespace() };

        // dict with equality explanations to a term's equivalence class' root (in z3)
        public readonly Dictionary<int, EqualityExplanation> equalityExplanations = new Dictionary<int, EqualityExplanation>();

        // Fingerprint (pointer) of specific instance to instantiation dict.
        public readonly Dictionary<string, Instantiation> fingerprints = new Dictionary<string, Instantiation>();

        // Specific quantifier instantiations.
        public List<Instantiation> instances = new List<Instantiation>();

        // list of conflicts.
        public List<Conflict> conflicts = new List<Conflict>();

        // TODO: list of function symbols(?) 
        public List<FunSymbol> modelFuns = new List<FunSymbol>();

        // TODO: dict with functions(?)
        public Dictionary<string, FunSymbol> modelFunsByName = new Dictionary<string, FunSymbol>();

        // TODO: dict with partitions(?)
        public Dictionary<string, Partition> modelPartsByName = new Dictionary<string, Partition>();

        // Representation of the internal prover model.
        public List<Common> models = new List<Common>();

        // TODO: some structure for proof mode(?)
        public Dictionary<long, Common> proofSteps = new Dictionary<long, Common>();

        // List of (push / pop) scope descriptors.
        public List<ScopeDesc> scopes = new List<ScopeDesc>();

        // Root (push / pop) scope.
        public Scope rootScope;

        // Source of the model.
        public string LogFileName;

        // Number of checks in the log file
        public int NumChecks;

        // Literal to mark a scope as done.
        internal static readonly Literal MarkerLiteral = new Literal
        {
            Id = -13,
            Term = new Term("marker", new Term[] { })
        };

        private void EnsureNamespaceExists(string ns)
        {
            if (!namespaces.ContainsKey(ns))
            {
                var newNamespace = new Namespace();
                newNamespace.quantifiers[-1] = new Quantifier
                {
                    Qid = ns,
                    PrintName = $"{ns}-axiom",
                    Namespace = ns
                };
                namespaces[ns] = newNamespace;
            }
        }

        public Term GetTerm(string ns, int id)
        {
            return namespaces[ns].terms[id];
        }

        public Term GetTerm(int id)
        {
            return GetTerm("", id);
        }

        public void SetTerm(string ns, int id, Term t)
        {
            EnsureNamespaceExists(ns);
            namespaces[ns].terms[id] = t;
        }

        public Quantifier GetQuantifier(string ns, int id)
        {
            if (id == -1)
            {
                EnsureNamespaceExists(ns);
            }
            return namespaces[ns].quantifiers[id];
        }

        public Quantifier GetQuantifier(int id)
        {
            return GetQuantifier("", id);
        }

        public void SetQuantifier(string ns, int id, Quantifier q)
        {
            EnsureNamespaceExists(ns);
            namespaces[ns].quantifiers[id] = q;
        }

        public Dictionary<int, Quantifier> GetRootNamespaceQuantifiers()
        {
            return namespaces[""].quantifiers;
        }

        // TODO: Find out, what these do!
        private readonly Dictionary<Instantiation, ImportantInstantiation> importants = new Dictionary<Instantiation, ImportantInstantiation>();
        private readonly Dictionary<ProofRule, bool> visitedRules = new Dictionary<ProofRule, bool>();

        public Model()
        {
            PushScope(0);
        }

        public void BuildInstantiationDAG()
        {
            if (instances.Count == 0)
            {
                return;
            }

            Queue<Instantiation> todo = new Queue<Instantiation>(instances
                .Where(inst => inst.DependantInstantiations.Count == 0));
            while (todo.Count > 0)
            {
                Instantiation current = todo.Dequeue();
                foreach (Instantiation inst in current.ResponsibleInstantiations
                    .Where(inst => current.DeepestSubpathDepth >= inst.DeepestSubpathDepth))
                {
                    inst.DeepestSubpathDepth = current.DeepestSubpathDepth + 1;
                    todo.Enqueue(inst);
                }
            }
        }

        public List<Instantiation> LongestPathWithInstantiation(Instantiation inst)
        {
            List<Instantiation> path = new List<Instantiation>();

            Instantiation current = inst;
            // reconstruct path to source
            path.Add(current);
            while (current.ResponsibleInstantiations.Count > 0)
            {
                // follow the longest path
                current = current.ResponsibleInstantiations
                    .Aggregate((i1, i2) => i1.Depth > i2.Depth ? i1 : i2);
                path.Add(current);
            }
            path.Reverse();

            // other direction
            current = inst;
            while (current.DependantInstantiations.Count > 0)
            {
                // follow the longest path
                current = current.DependantInstantiations
                    .Aggregate((i1, i2) => i1.DeepestSubpathDepth > i2.DeepestSubpathDepth ? i1 : i2);

                path.Add(current);
            }

            Debug.Assert(path.Count == inst.DeepestSubpathDepth + inst.Depth);
            return path;
        }

        public List<Quantifier> GetQuantifiersSortedByInstantiations()
        {
            List<Quantifier> qList = GetRootNamespaceQuantifiers().Values.ToList();
            qList.Sort((q1, q2) => q2.Cost.CompareTo(q1.Cost));
            return qList;
        }

        public List<Quantifier> GetQuantifiersSortedByOccurence()
        {
            return instances.Select(inst => inst.Quant).ToList();
        }

        public void PushScope(int checkNo)
        {
            ScopeDesc d = new ScopeDesc(checkNo);
            scopes.Add(d);
        }

        public void AddInstance(Instantiation inst)
        {
            instances.Add(inst);
            scopes[scopes.Count - 1].InstanceCount++;
        }

        public void PopScopes(int n, Conflict cnfl, int checkNumber)
        {
            Debug.Assert(n <= scopes.Count - 1);

            // Instantiate scope from scope descriptor.
            // cur represents the root of the scope tree that is popped away.
            Scope cur = new Scope(checkNumber)
            {
                level = -(scopes.Count - n - 1),
                Conflict = cnfl
            };

            // traverse backwards
            for (int i = scopes.Count - 1; i >= scopes.Count - n; --i)
            {
                // add the new scope as a child of the first non null scope that is popped away as well.
                // -> preserve the scope tree being popped.
                if (scopes[i].Scope != null)
                {
                    scopes[i].Scope.AddChildScope(cur);
                    cur = scopes[i].Scope;
                }

                cur.OwnInstanceCount += scopes[i].InstanceCount;
                Literal l = scopes[i].Literal;
                if (l != null)
                {
                    l.Implied = new Literal[scopes[i].Implied.Count];
                    scopes[i].Implied.CopyTo(l.Implied);
                    cur.Literals.Add(l);
                    foreach (var x in l.Implied)
                        x.Cause = l;
                }
            }
            scopes.RemoveRange(scopes.Count - n, n);

            int end = scopes.Count - 1;
            if (scopes[end].Scope == null)
            {
                scopes[end].Scope = new Scope(checkNumber);
                scopes[end].Scope.level = end;
            }
            scopes[end].Scope.AddChildScope(cur);
            scopes[end].Implied.Add(MarkerLiteral);
        }

        public Common SetupImportantInstantiations()
        {
            List<Common> res = new List<Common>();

            if (!proofSteps.ContainsKey(0))
            {
                return Common.Callback("PROOF-INST", () => res);
            }

            List<ImportantInstantiation> roots = new List<ImportantInstantiation>();
            List<ImportantInstantiation> allUsed = new List<ImportantInstantiation>();
            List<Common> quantLabels = new List<Common>();

            res.Add(Common.Callback("QUANTS BY MAX USEFUL DEPTH", () => quantLabels));
            res.Add(Common.Callback("ALL PROOF INSTS", () => allUsed));

            CollectInsts((ProofRule)proofSteps[0]);
            foreach (var imp in importants.Values)
            {
                if (imp.DepCount == 0)
                {
                    roots.Add(imp);
                }

                imp.Quant.UsefulInstances++;

                if (imp.UseCount > 0)
                {
                    allUsed.Add(imp);
                }
            }

            roots.Sort((i1, i2) => i2.WDepth.CompareTo(i1.WDepth));
            foreach (var r in roots)
            {
                ComputeMaxDepth(r);
            }

            List<Quantifier> quantsByMaxDepth = GetRootNamespaceQuantifiers().Values
                .Where(q => q.MaxDepth > 0).ToList();

            quantsByMaxDepth.Sort((q1, q2) =>
                q1.MaxDepth == q2.MaxDepth
                    ? q2.UsefulInstances.CompareTo(q1.UsefulInstances)
                    : q2.MaxDepth.CompareTo(q1.MaxDepth));

            quantLabels.AddRange(quantsByMaxDepth
                .Select(q => new ForwardingNode(q.MaxDepth + "   " + q.UsefulInstances + "    " + q.ToString(), q)));

            importants.Clear();
            visitedRules.Clear();
            res.AddRange(roots);
            return Common.Callback("PROOF-INST", () => res);
        }

        private void ComputeMaxDepth(ImportantInstantiation imp)
        {
            Quantifier q = imp.Quant;
            q.CurDepth++;
            if (q.CurDepth > q.MaxDepth)
                q.MaxDepth = q.CurDepth;
            foreach (var ch in imp.ResponsibleInsts)
                ComputeMaxDepth(ch);
            q.CurDepth--;
        }

        private void CollectInsts(ProofRule prf)
        {
            if (visitedRules.ContainsKey(prf)) return;
            visitedRules.Add(prf, true);
            if (prf.Name == "quant-inst")
            {
                ImportantInstantiation imp = AddInst((Instantiation)prf.Premises[0]);
                imp.UseCount++;
            }
            foreach (var p in prf.Premises)
            {
                ProofRule pr = p as ProofRule;
                if (pr != null) CollectInsts(pr);
            }
        }

        private ImportantInstantiation AddInst(Instantiation instantiation)
        {
            ImportantInstantiation imp;
            if (importants.TryGetValue(instantiation, out imp)) return imp;

            imp = new ImportantInstantiation(instantiation);
            importants.Add(instantiation, imp);
            foreach (var t in instantiation.Responsible)
            {
                if (t.Responsible != null)
                {
                    ImportantInstantiation x = AddInst(t.Responsible);
                    imp.ResponsibleInsts.Add(x);
                    x.DepCount++;
                }
            }
            return imp;
        }

        public bool IsV1Part(string fn)
        {
            int dummy;
            return fn.StartsWith("*") && int.TryParse(fn.Substring(1), out dummy);
        }

        public virtual int FunAppBadness(FunApp f)
        {
            // TODO read rules from file

            string fn = f.Fun.Name;
            switch (fn)
            {
                case "$ghost_emb":
                case "$int_to_ptrset":
                case "$int_to_ptr":
                    return 100;

                case "$array":
                case "$ptr":
                    return 40;
                case "$phys_ptr_cast":
                case "$spec_ptr_cast":
                case "$field_plus":
                    return 35;
                case "$idx_prim":
                case "$dot":
                    return 30;
                case "$read_ptr_m":
                    return 35;

                default:
                    if (fn.StartsWith("#distTp"))
                        return 100;
                    if (fn.StartsWith("$st_"))
                        return 60;

                    // general
                    if (fn.StartsWith("unique-value!"))
                        return 100;
                    if (fn.StartsWith("val!") || IsV1Part(fn))
                        return 200;
                    if (fn.StartsWith("call") && (fn.Contains("formal@") || fn.Contains("formal_")))
                        return 100;

                    if (f.Args.Length == 0) return 20;

                    return f.Args.Length + 50;
            }
        }

        bool IsModelConstant(string s)
        {
            if (s == "true" || s == "false") return true;

            if (s.Length == 0) return false;
            if (!Char.IsDigit(s[0]) && s[0] != '-') return false;
            for (int i = 1; i < s.Length; ++i)
                if (!Char.IsDigit(s[i])) return false;
            return true;
        }

        public void NewModel()
        {
            var copy = modelFuns;
            if (copy.Count > 0)
                models.Add(new CallbackNode("MODEL #" + models.Count, () => this.ModelKids(copy)));
            modelFuns = new List<FunSymbol>();
            modelFunsByName.Clear();
            modelPartsByName.Clear();
        }

        public FunSymbol FunSymbolByName(string name)
        {
            FunSymbol fn;
            if (modelFunsByName.TryGetValue(name, out fn)) return fn;
            fn = new FunSymbol();
            fn.Name = name;
            fn.AllSymbols = this.modelFuns;
            this.modelFuns.Add(fn);
            modelFunsByName.Add(name, fn);
            return fn;
        }


        public Partition PartitionByName(string name)
        {
            Partition part;
            if (modelPartsByName.TryGetValue(name, out part))
                return part;
            part = new Partition();
            part.Id = name;
            part.model = this;
            modelPartsByName.Add(name, part);

            FunSymbol consts = FunSymbolByName(name);
            FunApp fapp = new FunApp();
            fapp.Fun = consts;
            fapp.Args = new Partition[0];
            fapp.Value = part;
            consts.Apps.Add(fapp);
            part.Values.Add(fapp);

            return part;
        }

        IEnumerable<Common> ModelKids(List<FunSymbol> allFuns)
        {
            List<FunSymbol> realFuns = new List<FunSymbol>();
            List<FunApp> literals = new List<FunApp>();
            List<FunApp> consts = new List<FunApp>();

            allFuns.Sort(delegate (FunSymbol f1, FunSymbol f2) { return string.CompareOrdinal(f1.Name, f2.Name); });
            foreach (var fs in allFuns)
            {
                if (fs.Apps.Count == 1 && fs.Apps[0].Args.Length == 0)
                {
                    if (IsModelConstant(fs.Name))
                        literals.Add(fs.Apps[0]);
                    else if (FunAppBadness(fs.Apps[0]) < 100)
                        consts.Add(fs.Apps[0]);
                }
                else
                {
                    realFuns.Add(fs);
                }
            }
            yield return Common.Callback("LITERALS", delegate () { return literals; });
            yield return Common.Callback("CONSTS", delegate () { return consts; });
            foreach (var c in realFuns) yield return c;
        }

    }


    public class ProofRule : Common
    {
        public string Name;
        public Common[] Premises;
        public Term Consequent;

        public override string ToString()
        {
            return Name;
        }

        public override IEnumerable<Common> Children()
        {
            yield return Consequent;
            foreach (var p in Premises)
                yield return p;
        }

    }
}