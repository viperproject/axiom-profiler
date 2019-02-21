This folder contains the running example from our "The Axiom Profiler: Understanding and Debugging SMT Quantifier Instantiations" paper which can be used to start trying out the Axiom Profiler. Additionally we provide a number of scripts that may be useful for analyzing a large number of smt-files.

The following steps include instructions for generating log files from SMT-queries using Z3, then (as in our evaluation) analyzing them in an automated way using the Axiom Profiler and finally manually looking at the ones that may contain matching loops using the Axiom Profiler.

To build the Axiom Profiler open source/AxiomProfiler.sln in Visual Studio and make sure the target is set to "Release" before building. Once you have built the Axiom Profiler you can use the GUI to load a single log file or follow the steps below to automatically analyze multiple smt-files using the scripts we provide:

1. Using the Windows PowerShell navigate to the folder containing the SMT-files to investigate (e.g. GettingStarted\running-example). The scripts we provide do not explore directories recursively: they will only run on SMT-files in the immediate directory.

2. Generate log-files for these examples by executing "..\scripts\GetLogs.ps1" in the PowerShell. The logs will be collected in .\logs. If you want to generate them manually you can do so by executing "z3.exe trace=true proof=true trace_file_name=<log_file_name>.log -T:30 <smt_file_name>.smt2".

3. Run "..\scripts\AnalyzeLogs.ps1 .\logs" to analyze the generated logs using the Axiom Profiler. The Axiom Profiler will put the results of its analysis into .\out\ (these can be used for further analysis using the scripts we provide: see README in the scripts directory) and the script will then copy log files that contain at least 10 repetitions of some sequence of quantifier instantiations into .\looping\.

4. Use the Axiom Profiler (run ..\..\tools\axiom-profiler\bin\Release\AxiomProfiler.exe) to analyze these files manually as described in the paper. You may for example click on single instantiations in the graph to see information about them or click "Explain Path," which will cause the tool to try finding a matching loop and if it finds one give a generalized explanation for why this loop can repeat indefinitely.