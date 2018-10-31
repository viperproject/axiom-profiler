# Axiom Profiler

An upgrade of the [Z3 Axiom Profiler](http://vcc.codeplex.com/SourceControl/latest#vcc/Tools/Z3Visualizer/) providing navigable visualisations of instantiation graphs, some preliminary analysis of potential matching loops, and many stability and bug-fixes. This version is based on [Frederik Rothenberger's MSc project: Integration and Analysis of Alternative SMT Solvers for Software Verification](http://www.pm.inf.ethz.ch/education/student-projects/completedprojects.html), supervised by [Alexander J. Summers](http://people.inf.ethz.ch/summersa/), who can also be contacted with questions about the current version of the tool.

## Using on Windows

1.  Clone repository:

        hg clone https://bitbucket.org/viperproject/axiom-profiler
        
2.  Build from Visual Studio (also possible on the command-line): open source/AxiomProfiler.sln solution, and run the release build. Requires C# 6.0 features, .Net >= 4.5 (and a version of Visual Studio which supports this, e.g. >= 2017).
        
3.  Run the tool (either via Visual Studio, or by executing bin/Release/AxiomProfiler.exe)

## Using on Ubuntu

1.  Clone repository:

        hg clone https://bitbucket.org/viperproject/axiom-profiler
        cd axiom-profiler

2.  Install mono.
3.  Download NuGet:

        wget https://nuget.org/nuget.exe

4.  Install C# 6.0 compiler:

        mono ./nuget.exe install Microsoft.Net.Compilers

5.  Compile project:

        xbuild /p:Configuration=Release source/AxiomProfiler.sln

6.  Run Axiom Profiler:

        mono bin/Release/AxiomProfiler.exe

## Obtaining Z3 logs from various verification back-ends

To obtain a Z3 log with Boogie, use e.g:

    boogie /z3opt:TRACE=true /z3opt:PROOF=true ./file.bpl

To obtain a Z3 log with the Viper symbolic execution verifier (Silicon), use e.g:

    silicon ./file.sil --z3Args "TRACE=true PROOF=true"

If it complains about unrecognized argument, try this:

    silicon ./file.sil --z3Args '"TRACE=true"'

To obtain a Z3 log with the Viper verification condition generation verifier (Carbon), use e.g:

    carbon ./file.sil --print ./file.bpl
    boogie /z3opt:TRACE=true /z3opt:PROOF=true ./file.bpl

In all cases, the Z3 log should be stored in `./z3.log`. It is also possible to change this filename (see Z3 -pd for more options).
