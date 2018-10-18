using AxiomProfiler.QuantifierModel;
using System.IO;
using System.Linq;

namespace AxiomProfiler
{
    public class ScriptingTasks
    {
        public int NumPathsToExplore = 0;
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
        public static bool RunScriptingTasks(Model model, ScriptingTasks tasks)
        {
            var basePath = Path.Combine(new string[] { Directory.GetCurrentDirectory(), tasks.OutputFilePrefix });

            // Output basic information
            var basicFileExists = false;
            if (tasks.ShowNumChecks)
            {
                basicFileExists = true;
                using (var writer = new StreamWriter(basePath + ".basic", false))
                {
                    writer.WriteLine("checks, " + model.NumChecks);
                }
            }
            if (tasks.ShowQuantStatistics)
            {
                using (var writer = new StreamWriter(basePath + ".basic", basicFileExists))
                {
                    writer.WriteLine("num quantifiers, " + model.quantifiers.Count());
                    writer.WriteLine("tot number instantiations, " + model.instances.Count());
                    foreach (var quant in model.quantifiers.Values)
                    {
                        writer.WriteLine(quant.PrintName + ", " + quant.Instances.Count());
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
                    writer.WriteLine("# repetitions, repeating pattern");
                    foreach (var path in pathsToCheck)
                    {
                        var cycleDetection = new CycleDetection.CycleDetection(path.getInstantiations(), 3);
                        if (cycleDetection.hasCycle())
                        {
                            writer.WriteLine(cycleDetection.GetNumRepetitions() + ", " + string.Join(" -> ", cycleDetection.getCycleQuantifiers().Select(quant => quant.PrintName)));
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
                    writer.WriteLine($"Quantifier, # instances with ≥ {tasks.FindHighBranchingThreshold} direct children");
                    foreach (var quant in highBranchingInsts.GroupBy(inst => inst.Quant))
                    {
                        writer.WriteLine(quant.Key.PrintName + ", " + quant.Count());
                    }
                }
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
