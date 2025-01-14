//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.IO;
using AxiomProfiler.QuantifierModel;

namespace AxiomProfiler
{
    public class AxiomProfilerCore
    {
        public ParameterConfiguration parameterConfiguration;
        private ScriptingTasks scriptingTasks = new ScriptingTasks();
        public Model model;

        static string stripCygdrive(string s)
        {
            if (s.Length > 12 && s.StartsWith("/cygdrive/"))
            {
                s = s.Substring(10);
                return s.Substring(0, 1) + ":" + s.Substring(1);
            }
            return s;
        }

        public bool parseCommandLineArguments(string[] args, out string error)
        {
            bool retval = false;
            int idx;

            ParameterConfiguration config = new ParameterConfiguration();

            error = "";

            for (idx = 0; idx < args.Length; idx++)
            {
                args[idx] = stripCygdrive(args[idx]);
                if (args[idx].StartsWith("-")) args[idx] = "/" + args[idx].Substring(1);
                if (args[idx].StartsWith("/") && !File.Exists(args[idx]))
                {
                    // parse command line parameter switches
                    if (args[idx].StartsWith("/l:"))
                    {
                        config.z3LogFile = args[idx].Substring(3);
                        // minimum requirements have been fulfilled.
                        retval = true;
                    }
                    else if (args[idx].StartsWith("/c:"))
                    {
                        uint ch;
                        if (!uint.TryParse(args[idx].Substring(3), out ch))
                        {
                            error = $"Cannot parse check number \"{args[idx].Substring(3)}\"";
                            return false;
                        }
                        config.checkToConsider = (int)ch;
                    }
                    else if (args[idx] == "/s")
                    {
                        config.skipDecisions = true;
                    }
                    else if (args[idx].StartsWith("/loops:"))
                    {
                        if (Int32.TryParse(args[idx].Substring(7), out var numPaths))
                        {
                            if (numPaths <= 0)
                            {
                                error = "Invalid command line argument: number of paths to check for matching loops must be >= 1.";
                                return false;
                            }
                            scriptingTasks.NumPathsToExplore = numPaths;
                        }
                        else
                        {
                            error = "Invalid command line argument: specified number of paths to check for matching loops was not a number.";
                            return false;
                        }
                    }
                    else if (args[idx].StartsWith("/loopsMs:"))
                    {
                        if (Int32.TryParse(args[idx].Substring(9), out var timeoutMs))
                        {
                            if (timeoutMs <= 0)
                            {
                                error = "Invalid command line argument: timeout for checking loops must be >= 1.";
                                return false;
                            }
                            scriptingTasks.SearchTimeoutMs = timeoutMs;
                        }
                        else
                        {
                            error = "Invalid command line argument: specified timeout for checking loops was not a number.";
                            return false;
                        }
                    }
                    else if (args[idx] == "/showNumChecks")
                    {
                        scriptingTasks.ShowNumChecks = true;
                    }
                    else if (args[idx] == "/showQuantStatistics")
                    {
                        scriptingTasks.ShowQuantStatistics = true;
                    }
                    else if (args[idx].StartsWith("/findHighBranching:"))
                    {
                        if (Int32.TryParse(args[idx].Substring(19), out var threshold))
                        {
                            if (threshold < 0)
                            {
                                error = "Invalid command line argument: high branching threshold must be non-negative.";
                                return false;
                            }
                            scriptingTasks.FindHighBranchingThreshold = threshold;
                        }
                        else
                        {
                            error = "Invalid command line argument: specified high branching threshold was not a number.";
                            return false;
                        }
                    }
                    else if (args[idx].StartsWith("/outPrefix:"))
                    {
                        scriptingTasks.OutputFilePrefix = args[idx].Substring(11);
                    }
                    else if (args[idx] == "/autoQuit")
                    {
                        scriptingTasks.QuitOnCompletion = true;
                    }
                    else if (args[idx] == "/headless") {
                        config.headless = true;
                    }
                    else if (args[idx] == "/timing")
                    {
                        config.timing = true;
                    }
                    else
                    {
                        error = $"Unknown command line argument \"{args[idx]}\".";
                        return false;
                    }
                }
                else
                {
                    bool isLogFile = false;
                    try
                    {
                        using (var s = File.OpenText(args[idx]))
                        {
                            var l = s.ReadLine();
                            if (l.StartsWith("[mk-app]") || l.StartsWith("Z3 error model") || l.StartsWith("partitions:") || l.StartsWith("*** MODEL"))
                                isLogFile = true;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    if (isLogFile)
                    {
                        config.z3LogFile = args[idx];
                        retval = true;
                    }
                    else
                    {
                        error = "Incorrect file format.";
                        return false;
                    }
                }
            }

            if (retval)
            {
                parameterConfiguration = config;
            }
            return retval;
        }

        public void Run() {
            loadModel(parameterConfiguration);
            RunScriptingTasks();
        }

        public void RunScriptingTasks() {
            if (ScriptingSupport.RunScriptingTasks(model, scriptingTasks, parameterConfiguration.timing))
            {
                Environment.Exit(0);
            }
        }

        public void loadModel(ParameterConfiguration config)
        {
            resetProfiler();

            // Create a new loader and LoadingProgressForm and execute the loading
            Loader loader = new Loader(config);
            if (!parameterConfiguration.headless) {
                LoadingProgressForm lprogf = new LoadingProgressForm(loader);
                lprogf.ShowDialog();
            } else {
                loader.Load();
            }

            model = loader.GetModel();
        }

        private void resetProfiler()
        {
            // reset everything
            model = null;
            Model.MarkerLiteral.Cause = null; //The cause may be a term in the old model, preventing the GC from freeing some resources untill a new cause is set in the new model

            /* The entire model can be garbage collected now. Most of it will have aged into generation 2 of the garbage collection algorithm by now
             * which might take a while (~10s) until it is executed regularly. Giving the hint is, therefore, a good idea.
             */
            GC.Collect(2, GCCollectionMode.Optimized);
        }
    }
}
