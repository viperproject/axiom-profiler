//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Z3AxiomProfiler.QuantifierModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler
{
    public partial class Z3AxiomProfiler : Form
    {
        public string SearchText = "";
        SearchTree searchTree;
        readonly Dictionary<TreeNode, Common> expanded = new Dictionary<TreeNode, Common>();

        // Needed to expand nodes with many children without freezing the GUI.
        private readonly Timer uiUpdateTimer = new Timer();
        private readonly ConcurrentQueue<Tuple<TreeNode, List<TreeNode>, bool>> expandQueue = new ConcurrentQueue<Tuple<TreeNode, List<TreeNode>, bool>>();
        private readonly ConcurrentQueue<string[]> infoPanelQueue = new ConcurrentQueue<string[]>();
        private int workCounter;
        private IPrintable currentInfoPanelPrintable;
        private PrintRuleDictionary printRuleDict = new PrintRuleDictionary();
        private ParameterConfiguration parameterConfiguration;
        public Model model;

        private readonly TreeNode historyNode = new TreeNode
        {
            Text = "HISTORY"
        };

        public void addInstantiationToHistory(Instantiation inst)
        {
            // do not add a instantiation if it is already the most recent.
            if (isLastInHistory(inst)) return;
            historyNode.Nodes.Insert(0, makeNode(inst));
            if (historyNode.Nodes.Count > 100)
            {
                historyNode.Nodes.RemoveAt(100);
            }
        }

        public Z3AxiomProfiler()
        {
            InitializeComponent();
            uiUpdateTimer.Interval = 5;
            uiUpdateTimer.Tick += treeUiUpdateTimerTick;
            uiUpdateTimer.Tick += InfoPanelUpdateTick;
        }


        private void Z3AxiomProfiler_OnLoadEvent(object sender, EventArgs e)
        {
            if (parameterConfiguration == null) return;

            Loader.LoaderTask task = Loader.LoaderTask.LoaderTaskBoogie;

            if (!string.IsNullOrEmpty(parameterConfiguration.z3LogFile))
            {
                task = Loader.LoaderTask.LoaderTaskParse;
            }
            loadModel(parameterConfiguration, task);
            ParameterConfiguration.saveParameterConfigurationToSettings(parameterConfiguration);
        }

        private void LoadBoogie_Click(object sender, EventArgs e)
        {
            loadModelFromBoogie();
        }

        private void LoadZ3_Click(object sender, EventArgs e)
        {
            loadModelFromZ3();
        }

        private void LoadZ3Logfile_Click(object sender, EventArgs e)
        {
            loadModelFromZ3Logfile();
        }

        private void Exit_Click(object sender, EventArgs e)
        {
            Close();
        }

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

            config.boogieOptions = "/bv:z /trace";
            error = "";

            for (idx = 0; idx < args.Length; idx++)
            {
                args[idx] = stripCygdrive(args[idx]);
                if (args[idx].StartsWith("-")) args[idx] = "/" + args[idx].Substring(1);
                if (args[idx].StartsWith("/") && !File.Exists(args[idx]))
                {
                    // parse command line parameter switches
                    if (args[idx].StartsWith("/f:"))
                    {
                        config.functionName = args[idx].Substring(3);
                    }
                    else if (args[idx].StartsWith("/l:"))
                    {
                        config.z3LogFile = args[idx].Substring(3);
                        // minimum requirements have been fulfilled.
                        retval = true;
                    }
                    else if (args[idx].StartsWith("/t:"))
                    {
                        uint timeout;
                        if (!uint.TryParse(args[idx].Substring(3), out timeout))
                        {
                            error = $"Cannot parse timeout duration \"{args[idx].Substring(3)}\"";
                            return false;
                        }
                        config.timeout = (int)timeout;
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
                    else if (args[idx] == "/v2")
                    {
                        // Silently accept old command line argument
                    }
                    else if (args[idx] == "/v1")
                    {
                        error = "Z3 version 1 is no longer supported.";
                        return false;
                    }
                    else if (args[idx] == "/s")
                    {
                        config.skipDecisions = true;
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
                    else if (config.preludeBplFileInfo == null)
                    {
                        config.preludeBplFileInfo = new FileInfo(args[idx]);
                    }
                    else if (config.codeBplFileInfo == null)
                    {
                        config.codeBplFileInfo = new FileInfo(args[idx]);
                        // minimum requirements have been fulfilled.
                        retval = true;
                    }
                    else
                    {
                        error = "Multiple inputs files specified.";
                        return false;
                    }
                }
            }

            if (retval)
            {
                parameterConfiguration = config;
            }
            return true;
        }

        private void loadModelFromBoogie()
        {
            LoadBoogieForm loadform = new LoadBoogieForm();
            if (parameterConfiguration != null)
            {
                loadform.setParameterConfiguration(parameterConfiguration);
            }
            else
            {
                loadform.reloadParameterConfiguration();
            }

            var dialogResult = loadform.ShowDialog();
            if (dialogResult != DialogResult.OK)
                return;

            parameterConfiguration = loadform.GetParameterConfiguration();
            ParameterConfiguration.saveParameterConfigurationToSettings(parameterConfiguration);

            loadModel(parameterConfiguration, Loader.LoaderTask.LoaderTaskBoogie);
        }

        private void loadModelFromZ3()
        {
            LoadZ3Form loadform = new LoadZ3Form();
            if (parameterConfiguration != null)
            {
                loadform.setParameterConfiguration(parameterConfiguration);
            }
            else
            {
                loadform.reloadParameterConfiguration();
            }

            var dialogResult = loadform.ShowDialog();
            if (dialogResult != DialogResult.OK)
                return;

            parameterConfiguration = loadform.GetParameterConfiguration();
            ParameterConfiguration.saveParameterConfigurationToSettings(parameterConfiguration);

            loadModel(parameterConfiguration, Loader.LoaderTask.LoaderTaskZ3);
        }

        private void loadModelFromZ3Logfile()
        {
            LoadZ3LogForm loadform = new LoadZ3LogForm();
            if (parameterConfiguration != null)
            {
                loadform.setParameterConfiguration(parameterConfiguration);
            }
            else
            {
                loadform.reloadParameterConfiguration();
            }

            var dialogResult = loadform.ShowDialog();
            if (dialogResult != DialogResult.OK)
                return;

            parameterConfiguration = loadform.GetParameterConfiguration();
            ParameterConfiguration.saveParameterConfigurationToSettings(parameterConfiguration);

            loadModel(parameterConfiguration, Loader.LoaderTask.LoaderTaskParse);
        }

        private void loadModel(ParameterConfiguration config, Loader.LoaderTask task)
        {
            resetProfiler();

            // Create a new loader and LoadingProgressForm and execute the loading
            Loader loader = new Loader(config, task);
            LoadingProgressForm lprogf = new LoadingProgressForm(loader);
            lprogf.ShowDialog();

            model = loader.GetModel();
            loadTree();
        }

        private void resetProfiler()
        {
            // reset everything
            model = null;
            z3AxiomTree.Nodes.Clear();
            InstantiationPathView.Items.Clear();
            toolTipBox.Clear();
            printRuleDict = new PrintRuleDictionary();
            expanded.Clear();
            searchTree = null;
            currentInfoPanelPrintable = null;

            // clear history
            historyNode.Nodes.Clear();
        }

        private void loadTree()
        {
            Text = model.LogFileName + ": Z3 Axiom Profiler";

            z3AxiomTree.Nodes.Add(historyNode);
            if (model.conflicts.Count > 0)
            {
                AddTopNode(Common.Callback("CONFLICTS", () => model.conflicts));
                AddTopNode(Common.Callback("100 CONFLICTS", () => RandomConflicts(100)));
            }

            if (model.proofSteps.ContainsKey(0))
            {
                AddTopNode(model.proofSteps[0]);
                AddTopNode(model.SetupImportantInstantiations());
            }

            model.NewModel();
            foreach (var c in model.models)
                AddTopNode(c);

            model.PopScopes(model.scopes.Count - 1, null, 0);

            var rootSD = model.scopes[0];
            Scope root = rootSD.Scope;
            var l = new Literal
            {
                Id = -1,
                Term = new Term("root", new Term[0])
            };
            rootSD.Implied.Insert(0, rootSD.Literal);
            l.Implied = rootSD.Implied.ToArray();
            root.Literals.Add(l);

            root.PropagateImpliedByChildren();
            root.ComputeConflictCost(new List<Conflict>());
            root.AccountLastDecision(model);
            model.rootScope = root;

            List<Quantifier> quantByCnfls = model.quantifiers.Values.Where(q => q.GeneratedConflicts > 0).ToList();
            quantByCnfls.Sort((q1, q2) => q2.GeneratedConflicts.CompareTo(q1.GeneratedConflicts));
            if (quantByCnfls.Count > 0)
                AddTopNode(Common.Callback("Quant. by last conflict", () => quantByCnfls));

            AddTopNode(root);

            foreach (Quantifier q in model.GetQuantifiersSortedByInstantiations())
            {
                AddTopNode(q);
            }
        }


        private int AddTopNode(Common cfl)
        {
            return z3AxiomTree.Nodes.Add(makeNode(cfl));
        }



        void HandleExpand(object sender, TreeViewCancelEventArgs args)
        {
            if (args == null) return;

            TreeNode node = args.Node;
            if (expanded.ContainsKey(node)) return;
            expanded[node] = null;
            if (!(node.Tag is Common)) return;

            Common nodeTag = (Common)node.Tag;
            Task.Run(() => GenerateChildNodes(node, nodeTag));

            node.EnsureVisible();
            Interlocked.Increment(ref workCounter);
            uiUpdateTimer.Start();
        }

        private void GenerateChildNodes(TreeNode node, Common nodeTag)
        {
            List<TreeNode> childNodes = new List<TreeNode>();
            int i = 1;
            foreach (var common in nodeTag.Children())
            {
                childNodes.Add(makeNode(common));
                if (i == 1000)
                {
                    // 1000 is probably enough for a start.
                    // new sequence so that the parent is reevaluated again.
                    Interlocked.Increment(ref workCounter);

                    // add the cutoff node.
                    childNodes.Add(makeNode(new CallbackNode("... [Next 1000]", () => nodeTag.Children().Skip(1001))));
                    break;
                }
                i++;
            }

            // end of work batch
            expandQueue.Enqueue(new Tuple<TreeNode, List<TreeNode>, bool>(node, childNodes, true));
        }

        private static int batchSize = 20;
        private void treeUiUpdateTimerTick(object sender, EventArgs e)
        {
            Tuple<TreeNode, List<TreeNode>, bool> currTuple;
            if (!expandQueue.TryPeek(out currTuple)) return;

            var parentNode = currTuple.Item1;
            var nodes = currTuple.Item2;
            var hasProcessingNode = currTuple.Item3;
            z3AxiomTree.BeginUpdate();
            parentNode.Nodes.AddRange(nodes.Take(batchSize).ToArray());

            if (nodes.Count <= batchSize)
            {
                // finished, remove from work queue.
                expandQueue.TryDequeue(out currTuple);

                if (hasProcessingNode)
                {
                    // Remove the "Processing..." dummy node.
                    parentNode.Nodes.RemoveAt(0);
                }

                // done with a complete expand, check whether updating can be disabled.
                checkStopCounter();
            }
            else
            {
                // otherwise remove just the first batch and leave the rest for next tick.
                nodes.RemoveRange(0, batchSize);
                if (hasProcessingNode)
                {
                    parentNode.Nodes[0].Text = $"Processing... [{parentNode.Nodes.Count}]";
                }
            }
            z3AxiomTree.EndUpdate();
        }

        private void checkStopCounter()
        {
            int current_counter = Interlocked.Decrement(ref workCounter);
            if (current_counter == 0)
            {
                uiUpdateTimer.Stop();
            }
        }

        public TreeNode ExpandScope(Scope s)
        {
            var coll = z3AxiomTree.Nodes;
            if (s.parentScope != null)
            {
                var p = ExpandScope(s.parentScope);
                if (p == null) return null;
                coll = p.Nodes;
            }

            foreach (TreeNode n in coll)
            {
                if (n.Tag == s)
                {
                    n.Expand();
                    z3AxiomTree.SelectedNode = n;
                    return n;
                }
            }

            return null;
        }

        private static TreeNode makeNode(Common common)
        {
            TreeNode cNode = new TreeNode
            {
                Text = common.ToString(),
                Tag = common,
                ForeColor = common.ForeColor()
            };
            if (common.HasChildren())
                cNode.Nodes.Add(new TreeNode("Processing..."));
            if (common.AutoExpand())
                cNode.Expand();
            return cNode;
        }

        private void colorVisualizationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (model == null)
                return;
            ColorVisalizationForm colorForm = new ColorVisalizationForm();
            colorForm.quantifierLinkedText.Click += nodeSelector;
            colorForm.setQuantifiers(model.GetQuantifiersSortedByOccurence(), model.GetQuantifiersSortedByInstantiations());
            colorForm.Show();
        }

        private void nodeSelector(object sender, EventArgs args)
        {
            var ll = sender as LinkLabel;
            if (ll == null) return;

            foreach (TreeNode node in z3AxiomTree.Nodes.Cast<TreeNode>().Where(node => node != null && ll.Text == node.Text))
            {
                z3AxiomTree.SelectedNode = node;
            }
        }

        private void helpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            HelpWindow h = new HelpWindow();
            h.Show();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox().Show();
        }

        private void allConflictsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (model.conflicts == null) return;
            ConflictsToCsv(model.conflicts);
        }

        private List<Conflict> RandomConflicts(int n)
        {
            List<Conflict> res = new List<Conflict>();

            if (model.conflicts == null) return res;

            double sum = model.conflicts.Sum(c => c.InstCost);

            int id = 0;
            Random r = new Random(0);
            while (id++ < n)
            {
                int line = r.Next((int)sum);
                double s = 0;
                foreach (var c in model.conflicts)
                {
                    s += c.InstCost;
                    if (s > line)
                    {
                        res.Add(c);
                        break;
                    }
                }
            }
            return res;
        }

        private void randomConflictsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (model.conflicts == null) return;
            ConflictsToCsv(RandomConflicts(1000));
        }

        private static void ConflictsToCsv(List<Conflict> cc)
        {
            StringBuilder sb = Conflict.CsvHeader();
            int id = 0;
            foreach (var c in cc)
            {
                c.PrintAsCsv(sb, ++id);
            }
            Clipboard.SetText(sb.ToString());
        }

        private void HandleTreeNodeSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = z3AxiomTree.SelectedNode;
            Common c = t.Tag as Common;
            SetInfoPanel(c);

            Scope scope = c as Scope;
            if (scope != null)
            {
                searchTree?.SelectScope(scope);
            }

            Instantiation inst = c as Instantiation;
            if (inst != null)
            {
                SetInstantiationPath(inst);
            }
        }

        public void SetInfoPanel(IPrintable c)
        {
            if (c == null) return;

            currentInfoPanelPrintable = c;
            Interlocked.Increment(ref workCounter);
            uiUpdateTimer.Start();
            Task.Run(() => infoPanelQueue.Enqueue(c.InfoPanelText(getFormatFromGUI()).Split('\n')));
        }

        private PrettyPrintFormat getFormatFromGUI()
        {
            return new PrettyPrintFormat
            {
                showType = showTypesCB.Checked,
                showTermId = showTermIdCB.Checked,
                maxWidth = (int)maxTermWidthUD.Value,
                maxDepth = (int)maxTermDepthUD.Value,
                rewritingEnabled = enableRewritingCB.Checked,
                printRuleDict = printRuleDict
            };
        }

        private int infoPanelLineIdx;
        private void InfoPanelUpdateTick(object sender, EventArgs e)
        {
            string[] lines;

            // dequeue outdated tooltips
            while (infoPanelQueue.Count > 1)
            {
                infoPanelQueue.TryDequeue(out lines);
                infoPanelLineIdx = 0;
            }

            // read the first entry if available
            if (!infoPanelQueue.TryPeek(out lines)) return;

            // clear toolTipBox if this is a new toolTip.
            if (infoPanelLineIdx == 0)
            {
                toolTipBox.Clear();
            }

            // work a batch of lines
            for (int i = 0; i < batchSize && infoPanelLineIdx < lines.Length; i++)
            {
                var line = lines[infoPanelLineIdx] + '\n';
                const FontStyle markerStyle = FontStyle.Bold | FontStyle.Underline;
                if (line.Contains("Term"))
                {
                    AppendInfoTextInColor(line, Color.DarkMagenta, markerStyle);
                }
                else if (line.Contains("term"))
                {
                    AppendInfoTextInColor(line, Color.DarkRed, markerStyle);
                }
                else if (line.Contains("Instantiation ") || line.Contains("uantifier"))
                {
                    AppendInfoTextInColor(line, Color.DarkBlue, markerStyle);
                }
                else
                {
                    toolTipBox.AppendText(line);
                }

                infoPanelLineIdx++;
            }

            // check if finished
            if (infoPanelLineIdx == lines.Length)
            {
                infoPanelQueue.TryDequeue(out lines);
                infoPanelLineIdx = 0;
                checkStopCounter();
            }
        }

        private void AppendInfoTextInColor(string text, Color color, FontStyle fontStyle)
        {
            toolTipBox.SelectionStart = toolTipBox.TextLength;
            toolTipBox.SelectionLength = 0;

            toolTipBox.SelectionFont = new Font(toolTipBox.Font, fontStyle);
            toolTipBox.SelectionColor = color;
            toolTipBox.AppendText(text);
            toolTipBox.SelectionColor = toolTipBox.ForeColor;
            toolTipBox.SelectionFont = new Font(toolTipBox.Font, FontStyle.Regular);
        }

        private void SetInstantiationPath(Instantiation inst)
        {
            // delete old content
            InstantiationPathView.BeginUpdate();
            InstantiationPathView.Items.Clear();
            List<Instantiation> instantiationPath = model.LongestPathWithInstantiation(inst);
            foreach (Instantiation i in instantiationPath)
            {
                ListViewItem item = new ListViewItem
                {
                    Text = i.Depth.ToString(),
                    Name = $"Quantifier Instantiation @{i.LineNo}",
                    Tag = i
                };
                item.SubItems.Add(i.Quant.PrintName);
                item.SubItems.Add(i.Quant.Qid);
                item.SubItems.Add(i.Quant.Instances.Count.ToString());

                InstantiationPathView.Items.Add(item);


                if (i != inst) continue;

                item.BackColor = Color.GreenYellow;
                item.Focused = true;
                item.EnsureVisible();
            }
            InstantiationPathView.EndUpdate();
        }

        void Search()
        {
            var searchBox = new SearchBox(this);
            searchBox.Populate(z3AxiomTree.Nodes);
            searchBox.Show();
        }

        internal void Activate(TreeNode n)
        {
            z3AxiomTree.SelectedNode = n;
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Search();
        }

        private void z3AxiomTree_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '/':
                    e.Handled = true;
                    Search();
                    break;
                case (char)27:
                    e.Handled = true;
                    Close();
                    break;
                case 'v':
                    e.Handled = true;
                    ShowTree();
                    break;
                case '\r':
                    z3AxiomTree.SelectedNode?.Expand();
                    e.Handled = true;
                    break;
            }
        }

        private void searchTreeVisualizationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowTree();
        }

        private void ShowTree()
        {
            if (searchTree == null)
                searchTree = new SearchTree(model, this);
            searchTree.Show();
        }

        private void PathItemClick(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            Common c = e.Item.Tag as Common;
            if (c != null)
            {
                SetInfoPanel(c);
            }
            var inst = c as Instantiation;
            if (inst != null)
            {
                addInstantiationToHistory(inst);
            }
        }

        private bool isLastInHistory(Instantiation inst)
        {
            if (historyNode.Nodes.Count == 0) return false;
            return historyNode.Nodes[0].Tag == inst;
        }

        private void z3AxiomTree_Enter(object sender, EventArgs e)
        {
            TreeNode t = z3AxiomTree.SelectedNode;
            if (t == null) return;
            Common c = t.Tag as Common;
            SetInfoPanel(c);
        }

        private void InstantiationPathView_Enter(object sender, EventArgs e)
        {
            if (InstantiationPathView.SelectedItems.Count <= 0) return;
            var c = InstantiationPathView.SelectedItems[0].Tag as Common;
            SetInfoPanel(c);
        }

        private void quantifierBlameVisualizationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (model != null)
            {
                var fInfo = parameterConfiguration?.preludeBplFileInfo;
                GraphVizualization.DumpGraph(model, fInfo?.FullName ?? "<unknown>");
            }
        }

        private void instantiationGraphToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (model == null) return;
            var dagView = new DAGView(this);
            dagView.Show();
        }

        private void showTypesCB_CheckedChanged(object sender, EventArgs e)
        {
            SetInfoPanel(currentInfoPanelPrintable);
        }

        private void showTermIdCB_CheckedChanged(object sender, EventArgs e)
        {
            SetInfoPanel(currentInfoPanelPrintable);
        }

        private void maxTermWidthUD_ValueChanged(object sender, EventArgs e)
        {
            SetInfoPanel(currentInfoPanelPrintable);
        }

        private void maxTermDepthUD_ValueChanged(object sender, EventArgs e)
        {
            SetInfoPanel(currentInfoPanelPrintable);
        }

        private PrintRuleViewer printRuleViewer = null;
        private void rewritingRulesButton_Click(object sender, EventArgs e)
        {
            if (printRuleViewer != null && printRuleViewer.IsDisposed)
            {
                printRuleViewer = null;
            }
            if (printRuleViewer != null)
            {
                printRuleViewer.BringToFront();
                return;
            }
            printRuleViewer = new PrintRuleViewer(this, printRuleDict);
            printRuleViewer.Show();
        }

        private void enableRewritingCB_CheckedChanged(object sender, EventArgs e)
        {
            SetInfoPanel(currentInfoPanelPrintable);
        }

        public void updateInfoPanel()
        {
            SetInfoPanel(currentInfoPanelPrintable);
        }
    }
}
