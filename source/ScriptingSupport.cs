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
        public static void RunScriptingTasks(Model model, ScriptingTasks tasks)
        {
            var basePath = Path.Combine(new string[] { Directory.GetCurrentDirectory(), tasks.OutputFilePrefix });
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
