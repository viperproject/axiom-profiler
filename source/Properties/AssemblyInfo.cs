using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

[assembly: AssemblyTitle("AxiomProfiler")]
[assembly: AssemblyVersion("2.0.0.0")] // Note: even if we write fewer .0s they get appended on..
[assembly: AssemblyDescription("e-matching profiling tool for exploring and visualing quantifier instantiations")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyTrademark("Axiom Profiler Contributors")]
[assembly: AssemblyProduct("Axiom Profiler")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]

