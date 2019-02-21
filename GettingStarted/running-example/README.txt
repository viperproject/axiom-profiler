This folder contains various versions of the running example we used in the "The Axiom Profiler: Understanding and Debugging SMT Quantifier Instantiations" paper as well as corresponding log files that can be loaded directly into the Axiom Profiler.

running-example-orig.smt2 contains the running example as shown in Fig. 1

running-example-fix-nxt.smt2 contains the fixes described in Sec. 5 (Simple Matching 
Loops), i.e. choosing a different trigger for the nxt axiom

running-example-fix-inj.smt2 additionally enables z3's automatic refinement of injectivity axioms as described in Sec. 5 (High Branching)
