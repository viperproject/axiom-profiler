# Z3 Axiom Profiler

Fork with the goal to improve visualization and accessibility of this tool.

## Using on Windows

1.  Clone repository:

        hg clone https://bitbucket.org/viperproject/axiom-profiler
        
2.  Build from Visual Studio (also possible on the command-line):

  Open source/AxiomProfiler.sln solution, and run the default (Debug) Build. Requires C# 6.0 features, .Net >= 4.5 (and a version of Visual Studio which supports this, e.g. >= 2015).
        
3.  Run the tool (either via Visual Studio, or by executing bin/Debug/AxiomProfiler.exe)

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

        xbuild source/AxiomProfiler.sln

6.  Run Axiom Profiler:

        mono bin/Debug/AxiomProfiler.exe

## Obtaining Z3 logs from various verification back-ends

To obtain a Z3 log with Boogie, use e.g:

    boogie /z3opt:TRACE=true ./file.bpl

To obtain a Z3 log with the Viper symbolic execution verifier (Silicon), use e.g:

    silicon ./file.sil --z3Args TRACE=true

To obtain a Z3 log with the Viper verification condition generation verifier (Carbon), use e.g:

    carbon ./file.sil --print ./file.bpl
    boogie /z3opt:TRACE=true ./file.bpl

In all cases, the Z3 log should be stored in `./z3.log`. It is also possible to change this filename (see Z3 -pd for more options).
