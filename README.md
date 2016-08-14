# Z3 Axiom Profiler

Fork with the goal to improve visualization and accessibility of this tool.

## Using on Ubuntu

1.  Clone repository:

        hg clone https://vakaras@bitbucket.org/viperproject/axiom-profiler
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

To obtain Z3 log with Silicon, use:

    cd /tmp
    silicon --useNailgun /tmp/file.sil --z3Args TRACE=true

To obtain Z3 log with Carbon, use:

    cd /tmp
    carbon --useNailgun /tmp/file.sil --print /tmp/file.bpl
    boogie /z3opt:TRACE=true /tmp/file.bpl

In both cases Z3 log should be stored in `/tmp/z3.log`.
