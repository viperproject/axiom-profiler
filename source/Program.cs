//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Windows.Forms;

namespace AxiomProfiler
{
    static class Program
    {
        static void parseErrorCommandLineArguments(string err)
        {
            Console.WriteLine("Aborting parsing command line arguments:\n" + err);
            printUsage();
            Environment.Exit(1);
        }

        static void printUsage()
        {
            Console.WriteLine("Usage: AxiomProfiler [options]");
            Console.WriteLine("     options ");
            Console.WriteLine("         /headless                      : Run in headless mode (no GUI)");
            Console.WriteLine("         /timing                        : Print performance statistics");
            Console.WriteLine("         /l:<file>                      : Log file to process");
            Console.WriteLine("         /c:<check>                     : Only load instantiations from <check>-th check-sat command");
            Console.WriteLine("         /s                             : Skip conflicts/decisions (conserves memory)");
            Console.WriteLine("         /loops:<num paths>             : Checks heaviest paths for potential matching loops (printed to CSV)");
            Console.WriteLine("         /loopsMs:<ms>                  : Timeout for matching loops analysis");
            Console.WriteLine("         /showNumChecks                 : Get total number of checks-sat commands executed (printed to CSV)");
            Console.WriteLine("         /showQuantStatistics           : Get statistics about quantifiers (printed to CSV)");
            Console.WriteLine("         /findHighBranching:<threshold> : Find instantiations with at least <threshold> children (printed to CSV)");
            Console.WriteLine("         /outPrefix:<prefix>            : Prefix for generated CSV files");
            Console.WriteLine("         /autoQuit                      : Automatically quit after completing automated analysis (Deprecated, use /headless)");
            Environment.Exit(0);
        }

        private static void InitConfig(AxiomProfilerCore z3vis, string[] args)
        {
            string error_msg;
            if (!z3vis.parseCommandLineArguments(args, out error_msg))
            {
                parseErrorCommandLineArguments(error_msg);
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (Array.Exists(args, element => element == "/headless")) {
                AxiomProfilerCore core = new AxiomProfilerCore();
                InitConfig(core, args);
                core.Run();
                Environment.Exit(0);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AxiomProfiler f = new AxiomProfiler();
            InitConfig(f.core, args);
            f.Show();
            Application.Run(f);
        }

    }
}
