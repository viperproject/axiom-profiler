//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Windows.Forms;

namespace Z3AxiomProfiler
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
            Console.WriteLine("Usage: Z3AxiomProfiler [options] <prelude-file> <filename>");
            Console.WriteLine("       prelude-file       : VCC prelude file location");
            Console.WriteLine("       filename           : Boogie input file");
            Console.WriteLine("       options ");
            Console.WriteLine("          /f:<function>   : Function name");
            Console.WriteLine("          /t:<seconds>    : Verification timeout");
            Console.WriteLine("          /l:<file>       : Log file to process");
            Console.WriteLine("          /s              : Skip conflicts/decisions (conserves memory)");
            Console.WriteLine("          /v1             : Start Z3 v1 (default)");
            Console.WriteLine("          /v2             : Start Z3 v2");
            Environment.Exit(0);
        }

        private static void InitConfig(Z3AxiomProfiler z3vis, string[] args)
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Z3AxiomProfiler f = new Z3AxiomProfiler();
            InitConfig(f, args);
            f.Show();
            Application.Run(f);
        }

    }
}
