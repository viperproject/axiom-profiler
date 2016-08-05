using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using AxiomProfiler.QuantifierModel;
using System.Text.RegularExpressions;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler {
  class Utils {
    internal static string Serialize<T>(T obj) {
      using (var ms = new MemoryStream()) {
        var serializer = new DataContractJsonSerializer(typeof(T));
        serializer.WriteObject(ms, obj);
        return Encoding.UTF8.GetString(ms.ToArray());
      }
    }
  }

  class GraphVizualization {
    [Serializable]
    internal struct Edge {
      [DataMember]
      public string from;
      [DataMember]
      public string to;
      [DataMember]
      public int value;
    }

    [Serializable]
    internal struct Node {
      [DataMember]
      public string id;
      [DataMember]
      public string label;
      [DataMember]
      public int value;
      [DataMember]
      public string title;
      [DataMember]
      public string search;
      [DataMember]
      public int mergedCount;
    }

    [Serializable]
    internal struct Graph {
      [DataMember]
      internal List<Edge> edges;
      [DataMember]
      internal List<Node> nodes;
    }

    internal static IEnumerable<string> getTriggers(Quantifier quantifier, bool display) {
      var bodyTerms = quantifier.BodyTerm.Args;

      if (bodyTerms == null || bodyTerms.Length <= 1) {
        return Enumerable.Empty<string>();
      }

     return bodyTerms.Take(bodyTerms.Length - 1).Select(x => ExtractTrigger(x, display));
    }

    private static void FormatTerm(Term term, StringBuilder builder, int offset, bool multiLine, bool display) {
      var hasArgs = term.Args != null && term.Args.Length > 0;
      var isVar = !hasArgs && term.Name != null && term.Name.StartsWith("#");
      var isSpecial = !hasArgs && term.Name != null && term.Name.StartsWith("@");

      if (hasArgs) {
        offset += 1;
        builder.Append("(");
      }

      if (display) {
        if (hasArgs) {
          builder.Append("<i class=\"fun\">");
        } else if (isVar) {
          builder.Append("<i class=\"var\">");
        } else if (isSpecial) {
          builder.Append("<i class=\"special\">");
        }
      }

      offset += term.Name.Length;
      builder.Append(term.Name);
      
      if (display && hasArgs) {
        builder.Append("</i>");
      }

      if (hasArgs) {
        offset += 1;
        builder.Append(" ");

        var someArgHadArgs = false;
        for (int argId = 0; argId < term.Args.Length; argId++) {
          var arg = term.Args[argId];
          if (argId > 0) {
            someArgHadArgs |= arg.Args.Length > 0;
            if (someArgHadArgs && multiLine) {
              builder.Append(Environment.NewLine).Append(new string(' ', offset));
            } else {
              builder.Append(' ');
            }
          }
          FormatTerm(term.Args[argId], builder, offset, multiLine, display);
        }
      }
      
      if (hasArgs) {
        builder.Append(")");
      } else if (display && (isVar || isSpecial)) {
        builder.Append("</i>");
      }
    }

    private static string FormatTerm(Term term, bool multiLine, bool display) {
      var sb = new StringBuilder();
      FormatTerm(term, sb, 0, multiLine, display);
      return sb.ToString();
    }

    private static string FormatBody(Quantifier quantifier, bool display = true) {
      if (quantifier.BoogieBody != null) {
        return quantifier.BoogieBody;
      } else {
        if (quantifier.BodyTerm == null) {
          return "?";
        } else {
          var args = quantifier.BodyTerm.Args;
          if (args != null && args.Length > 0) {
            return FormatTerm(args[args.Length - 1], display, display);
          } else {
            return quantifier.BodyTerm.Name;
          }
        }
      }
    }

    private static string FormatBody(IEnumerable<Quantifier> quantifiers, bool display = true) {
      return String.Join(Environment.NewLine + Environment.NewLine, new HashSet<string>(quantifiers.Select(q => FormatBody(q, display))));
    }

    private static string ExtractTrigger(Term pattern, bool display) {
      System.Diagnostics.Debug.Assert(pattern.Name == "pattern");
      System.Diagnostics.Debug.Assert(pattern.Args != null);
      return string.Format("{{{0}}}", String.Join<string>(", ", pattern.Args.Select(t => FormatTerm(t, false, display))));
    }

    private static string FormatTriggers(Quantifier quantifier, bool display = true) {
      var triggers = string.Join(display ? Environment.NewLine + new string(' ', 11) : " ", getTriggers(quantifier, display));
      return string.IsNullOrEmpty(triggers) ? "none" : triggers;
    }

    private static string FormatConflicts(MergedQuantifierInfo quantifier) {
      return quantifier.Conflicts > 0 ? string.Format("found {0}", quantifier.Conflicts) : "none";
    }

    private static object FormatMerged(MergedQuantifierInfo quantifier) {
      var count = quantifier.AllQuantifiers.Count;
      return count > 1 ? string.Format("merged {0} ({1})", count > 10 ? "many" : "some", count) : "none";
    }

    private static string normalizeName(Quantifier q) {
      return Regex.Replace(q.PrintName, @"\[\#[0-9]+\]", "");
    }

    internal class MergedQuantifierInfo {
      internal string Name;
      internal double Cost;
      internal int Conflicts;
      internal List<Instantiation> Instances;
      internal List<Quantifier> AllQuantifiers;

      internal static void UpdateBinding(Quantifier quant, Dictionary<string, MergedQuantifierInfo> quantifierNameToInfo) {
        var qName = normalizeName(quant);
        MergedQuantifierInfo info;
        if (!quantifierNameToInfo.TryGetValue(qName, out info)) {
          info = new MergedQuantifierInfo { Name = qName, Cost = 0, Conflicts = 0, Instances = new List<Instantiation>(), AllQuantifiers = new List<Quantifier>() };
          quantifierNameToInfo.Add(qName, info);
        }
        info.Cost += quant.Cost;
        info.Conflicts += quant.GeneratedConflicts;
        if (quant.Instances.Any()) { // Virtually every non-nested quantifier has two or three copies, all of them but one with 0 instantiations, and these copies often have incorrect body text
          info.AllQuantifiers.Add(quant);
        }
        info.Instances.AddRange(quant.Instances);
      }
    }

    internal static Graph ComputeGraph(Model model) {
      const int MIN_COST = 0, MIN_COUNT = 0;

      var quantifierNameToInfo = new Dictionary<string, MergedQuantifierInfo>();
      foreach (var source in model.quantifiers.Values) {
        MergedQuantifierInfo.UpdateBinding(source, quantifierNameToInfo);
      }
      
      var edges = new List<Edge>();
      var nodes = new List<Node>();
      var quantifiers = new HashSet<MergedQuantifierInfo>();

      foreach (var source in quantifierNameToInfo.Values) {
        if (source.Cost > MIN_COST) {
          quantifiers.Add(source);

          var edgesTo = new Dictionary<string, int>();
          foreach (var instance in source.Instances) {
            foreach (var dependants in instance.DependantInstantiations) {
              var dName = normalizeName(dependants.Quant);
              int count; edgesTo.TryGetValue(dName, out count);
              edgesTo[dName] = count + 1;
            }
          }
          foreach (var other in edgesTo.Keys) {
            var count = edgesTo[other];
            if (quantifierNameToInfo[other].Cost > MIN_COST && count > MIN_COUNT) {
              quantifiers.Add(quantifierNameToInfo[other]);
              edges.Add(new Edge { from = source.Name, to = other, value = count });
            }
          }
        }
      }

      foreach (var quantifier in quantifiers) {
        const string titleFormat = "<b>Cost:</b>      {1:0.}{0}<b>Instances:</b> {2}{0}<b>Conflicts:</b> {3}{0}<b>Triggers:</b>  {4}{0}<b>Nested:</b>    {5}{0}{0}{6}";
        const string searchFormat = "Cost: {1:0.}{0}Instances: {2}{0}Conflicts: {3}{0}Triggers: {4}{0}Nested: {5}{0}{6}";

        var one = quantifier.AllQuantifiers.First();
        var title = string.Format(titleFormat, Environment.NewLine, quantifier.Cost, quantifier.Instances.Count, FormatConflicts(quantifier), FormatTriggers(one), FormatMerged(quantifier), FormatBody(quantifier.AllQuantifiers));
        var search = string.Format(searchFormat, Environment.NewLine, quantifier.Cost, quantifier.Instances.Count, FormatConflicts(quantifier), FormatTriggers(one, false), FormatMerged(quantifier), FormatBody(quantifier.AllQuantifiers, false));
        nodes.Add(new Node { id = quantifier.Name, value = (int)quantifier.Cost, label = quantifier.Name, title = title, search = search, mergedCount = quantifier.AllQuantifiers.Count });
      }

      return new Graph { edges = edges, nodes = nodes };
    }

    internal static string GetUri(string path) {
      return new Uri(path).AbsoluteUri;
    }

    internal static void DumpGraph(Model model, string fname = "<unknown>") { // FIXME add warning for multiple traces
      var graph = ComputeGraph(model);
      
      var binDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      var templatePath = Path.Combine(binDir, "axiom-profiler.template.html");
      var jsPath = Path.Combine(binDir, "axiom-profiler.js");
      var cssPath = Path.Combine(binDir, "axiom-profiler.css");
      var appPath = Path.GetFullPath("axiom-profiler.html");

      var app = File.ReadAllText(templatePath);
      app = app.Replace("__JS__", GetUri(jsPath));
      app = app.Replace("__CSS__", GetUri(cssPath));
      app = app.Replace("__TITLE__", string.Format("Axiom profile for {0}", System.Net.WebUtility.HtmlEncode(fname)));
      app = app.Replace("__DATA__", Utils.Serialize(graph));
      File.WriteAllText(appPath, app);

      System.Diagnostics.Process.Start(GetUri(appPath));
    }

    private void FindCycles(Model model) {
      List<Quantifier> qList = model.GetQuantifiersSortedByInstantiations();
      Dictionary<List<Quantifier>, int> cyclesCounter = new Dictionary<List<Quantifier>, int>(new SpineComparer<Quantifier>());

      foreach (var q in qList)
        FindCycles(q, cyclesCounter);

      List<KeyValuePair<List<Quantifier>, int>> results = cyclesCounter.Where(kv => kv.Key.Count > 1).OrderByDescending(kv => kv.Value).Take(3).ToList();

      foreach (var kv in results)
        PrintCycle(kv);
    }


    class SpineComparer<T> : IEqualityComparer<List<T>> {
      public bool Equals(List<T> seq1, List<T> seq2) {
        return seq1 != null && seq1.SequenceEqual(seq2);
      }

      public int GetHashCode(List<T> seq) {
        int hashcode = 0;

        foreach (T elem in seq)
          hashcode ^= elem.ToString().GetHashCode();

        return hashcode;
      }
    }

    private void PrintCycle(KeyValuePair<List<Quantifier>, int> cycle) {
      Console.WriteLine("Cycle found! {0} instances, length {1}", cycle.Value, cycle.Key.Count);
      foreach (Quantifier q in cycle.Key)
                Console.WriteLine(q);
      Console.WriteLine();
    }

    private void FindCycles(Quantifier q, Dictionary<List<Quantifier>, int> cyclesCounter) {
      Stack<Quantifier> spine = new Stack<Quantifier>();

      foreach (Instantiation inst in q.Instances)
        FindCycles(null, inst, cyclesCounter, spine);
    }

    private void FindCycles(Quantifier source, Instantiation inst, Dictionary<List<Quantifier>, int> cyclesCounter, Stack<Quantifier> spine) {
      Quantifier quant = inst.Quant;

      if (source == quant && spine != null) { // && spine.Count >= 2 && quant != spine.Peek()) {
        List<Quantifier> key = new List<Quantifier>(spine);
        int count; cyclesCounter.TryGetValue(key, out count);
        cyclesCounter[key] = count + 1;
        source = null;
      }

      if (source == null) {
        source = quant;
        spine = new Stack<Quantifier>();
      }

      spine.Push(quant);
      foreach (Instantiation child in inst.DependantInstantiations)
        FindCycles(source, child, cyclesCounter, spine);
      spine.Pop();
    }
  }
}
