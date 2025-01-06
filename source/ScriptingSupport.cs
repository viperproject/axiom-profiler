using AxiomProfiler.QuantifierModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AxiomProfiler
{
    public class ScriptingTasks
    {
        public int NumPathsToExplore = 0;
        public int SearchTimeoutMs = int.MaxValue;
        public bool ShowNumChecks = false;
        public bool ShowQuantStatistics = false;
        public int FindHighBranchingThreshold = int.MaxValue;
        public string OutputFilePrefix = "AxiomProfiler";
        public bool QuitOnCompletion = false;
    }

    public static class ScriptingSupport
    {

        /// <summary>
        /// Analizes the specified model and writes the results into corresponding (CSV) files.
        /// </summary>
        /// <param name="model"> The model to analyize. </param>
        /// <param name="tasks"> The analysis tasks to perform. </param>
        /// <returns>True if the tool should quit, false otherwise.</returns>
        public static bool RunScriptingTasks(Model model, ScriptingTasks tasks, bool timing)
        {
            var basePath = Path.Combine(new string[] { Directory.GetCurrentDirectory(), tasks.OutputFilePrefix });
            var tasksMs = long.MaxValue;
            var trueLoops = new List<List<Quantifier>>();
            var falseLoops = new List<List<Quantifier>>();
            var error = "";
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                // Output basic information
                var basicFileExists = false;
                if (tasks.ShowNumChecks)
                {
                    basicFileExists = true;
                    using (var writer = new StreamWriter(basePath + ".basic", false))
                    {
                        writer.WriteLine("checks," + model.NumChecks);
                    }
                }
                if (tasks.ShowQuantStatistics)
                {
                    using (var writer = new StreamWriter(basePath + ".basic", basicFileExists))
                    {
                        writer.WriteLine("num quantifiers," + model.GetRootNamespaceQuantifiers().Count());
                        writer.WriteLine("tot number instantiations," + model.instances.Count());
                        foreach (var quant in model.GetRootNamespaceQuantifiers().Values)
                        {
                            writer.WriteLine(quant.PrintName + "," + quant.Instances.Count());
                        }
                    }
                }

                // Check logest paths for potential loops
                var pathsToCheck = model.instances.Where(inst => inst.Depth == 1)
                    .OrderByDescending(inst => inst.DeepestSubpathDepth)
                    .Take(tasks.NumPathsToExplore)
                    .Select(inst => deepestPathStartingFrom(inst)).ToList();
                if (pathsToCheck.Any())
                {
                    using (var writer = new StreamWriter(basePath + ".loops", false))
                    {
                        writer.WriteLine("# repetitions,repeating pattern");
                        foreach (var path in pathsToCheck)
                        {
                            var cycleDetection = new CycleDetection.CycleDetection(path.getInstantiations(), 3);
                            var gen = cycleDetection.getGeneralization();
                            if (gen != null)
                            {
                                var quantifiers = cycleDetection.getCycleQuantifiers();
                                writer.WriteLine(cycleDetection.GetNumRepetitions() + "," + string.Join(" -> ", quantifiers.Select(quant => quant.PrintName)) + "," + gen.TrueLoop);
                                if (gen.TrueLoop) {
                                    if (!trueLoops.Contains(quantifiers))
                                        trueLoops.Add(quantifiers);
                                }
                                else {
                                    if (!falseLoops.Contains(quantifiers))
                                        falseLoops.Add(quantifiers);
                                }
                            }
                            if (watch.ElapsedMilliseconds >= tasks.SearchTimeoutMs)
                            {
                                break;
                            }
                        }
                    }
                }

                // High branching analysis
                var highBranchingInsts = model.instances.Where(inst => inst.DependantInstantiations.Count() >= tasks.FindHighBranchingThreshold).ToList();
                if (highBranchingInsts.Any())
                {
                    using (var writer = new StreamWriter(basePath + ".branching"))
                    {
                        writer.WriteLine($"Quantifier,# instances with ≥ {tasks.FindHighBranchingThreshold} direct children");
                        foreach (var quant in highBranchingInsts.GroupBy(inst => inst.Quant))
                        {
                            writer.WriteLine(quant.Key.PrintName + "," + quant.Count());
                        }
                    }
                }
                watch.Stop();
                tasksMs = watch.ElapsedMilliseconds;
            }
            catch (Exception e)
            {
                error = e.Message;
                const int ERROR_HANDLE_DISK_FULL = 0x27;
                const int ERROR_DISK_FULL = 0x70;
                int win32ErrorCode = e.HResult & 0xFFFF;
                if (win32ErrorCode != ERROR_HANDLE_DISK_FULL && win32ErrorCode != ERROR_DISK_FULL)
                {
                    using (var writer = new StreamWriter(basePath + ".error", false))
                    {
                        writer.WriteLine(e.Message);
                    }
                }
            }
            if (timing) {
                if (tasksMs == long.MaxValue)
                    Console.WriteLine("[Analysis] Err " + error);
                else
                    Console.WriteLine("[Analysis] " + tasksMs + "ms");
                Console.WriteLine("[Loops] " + trueLoops.Count + " true, " + falseLoops.Count + " false");
            }

            return tasks.QuitOnCompletion;
        }

        private static InstantiationPath deepestPathStartingFrom(Instantiation instantiation)
        {
            var current = instantiation;
            var path = new InstantiationPath(current);
            while (current.DependantInstantiations.Count != 0)
            {
                var next = current.DependantInstantiations.First(inst => inst.DeepestSubpathDepth == current.DeepestSubpathDepth - 1);
                path.append(next);
                current = next;
            }
            return path;
        }
    }
}
