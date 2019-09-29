//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using AxiomProfiler.QuantifierModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler
{
    public partial class AxiomProfiler : Form
    {
        public string SearchText = "";
        SearchTree searchTree;
        readonly Dictionary<TreeNode, Common> expanded = new Dictionary<TreeNode, Common>();

        // Needed to expand nodes with many children without freezing the GUI.
        private readonly Timer uiUpdateTimer = new Timer();
        private readonly ConcurrentQueue<Tuple<TreeNode, List<TreeNode>, bool>> expandQueue = new ConcurrentQueue<Tuple<TreeNode, List<TreeNode>, bool>>();
        private readonly ConcurrentQueue<InfoPanelContent> infoPanelQueue = new ConcurrentQueue<InfoPanelContent>();
        private int workCounter;
        private IPrintable currentInfoPanelPrintable;
        private PrintRuleDictionary printRuleDict = new PrintRuleDictionary();
        private ParameterConfiguration parameterConfiguration;
        private ScriptingTasks scriptingTasks = new ScriptingTasks();
        private DAGView dagView;
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

        public AxiomProfiler()
        {
            InitializeComponent();
            dagView = new DAGView(this) {Dock = DockStyle.Fill};
            splitContainer1.Panel2.Controls.Add(dagView);
            uiUpdateTimer.Interval = 5;
            uiUpdateTimer.Tick += treeUiUpdateTimerTick;
            uiUpdateTimer.Tick += InfoPanelUpdateTick;

            var largeTextMode = Properties.Settings.Default.LargeTextMode;
            largeTextToolStripMenuItem.Checked = largeTextMode;
            PrintConstants.LargeTextMode = largeTextMode;
            z3AxiomTree.Font = PrintConstants.DefaultFont;
        }


        private void AxiomProfiler_Load(object sender, EventArgs e)
        {
            if (parameterConfiguration == null) return;
            loadModel(parameterConfiguration);
            ParameterConfiguration.saveParameterConfigurationToSettings(parameterConfiguration);

            if (ScriptingSupport.RunScriptingTasks(model, scriptingTasks))
            {
                Environment.Exit(0);
            }
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
            return true;
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

            loadModel(parameterConfiguration);
        }

        private void loadModel(ParameterConfiguration config)
        {
            resetProfiler();

            // Create a new loader and LoadingProgressForm and execute the loading
            Loader loader = new Loader(config);
            LoadingProgressForm lprogf = new LoadingProgressForm(loader);
            lprogf.ShowDialog();

            model = loader.GetModel();
            loadTree();
            dagView.drawGraphNoFilterQuestion();
        }

        private void resetProfiler()
        {
            // Close printRuleViewer if it is still open
            printRuleViewer?.Close();

            // reset everything
            model = null;
            Model.MarkerLiteral.Cause = null; //The cause may be a term in the old model, preventing the GC from freeing some resources untill a new cause is set in the new model
            z3AxiomTree.Nodes.Clear();
            toolTipBox.Clear();
            printRuleDict = new PrintRuleDictionary();
            expanded.Clear();
            searchTree = null;
            currentInfoPanelPrintable = null;

            // clear history
            historyNode.Nodes.Clear();

            dagView.Clear(); //The dagView keeps references to the instances represented by visible nodes. Clearing it allows these resources to be freed.

            /* The entire model can be garbage collected now. Most of it will have aged into generation 2 of the garbage collection algorithm by now
             * which might take a while (~10s) until it is executed regularly. Giving the hint is, therefore, a good idea.
             */
            GC.Collect(2, GCCollectionMode.Optimized);
        }

        private void loadTree()
        {
            Text = model.LogFileName + ": Axiom Profiler";

            z3AxiomTree.Nodes.Add(historyNode);
            if (model.conflicts.Count > 0)
            {
                AddTopNode(Common.Callback("CONFLICTS", () => model.conflicts));
                AddTopNode(Common.Callback("100 CONFLICTS", () => RandomConflicts(100)));
            }

            model.NewModel();
            AddTopNodes(model.models);

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

            List<Quantifier> quantByCnfls = model.GetRootNamespaceQuantifiers().Values.Where(q => q.GeneratedConflicts > 0).ToList();
            quantByCnfls.Sort((q1, q2) => q2.GeneratedConflicts.CompareTo(q1.GeneratedConflicts));
            if (quantByCnfls.Count > 0)
                AddTopNode(Common.Callback("Quant. by last conflict", () => quantByCnfls));

            AddTopNode(root);

            AddTopNodes(model.GetQuantifiersSortedByInstantiations());
        }


        private int AddTopNode(Common cfl)
        {
            return z3AxiomTree.Nodes.Add(makeNode(cfl));
        }

        private void AddTopNodes(IEnumerable<Common> cfls)
        {
            z3AxiomTree.Nodes.AddRange(cfls.Select(makeNode).ToArray());
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
            if (model?.conflicts == null) return;
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
                addInstantiationToHistory(inst);
                dagView.selectInstantiation(inst);
            }
            else
            {
                dagView.unselectNode();
            }
        }

        /// <summary>
        /// Prints a message in the left panel.
        /// </summary>
        /// <param name="message"> The message to be displayed </param>
        public void DisplayMessage(string message)
        {
            // We have an additional UI update
            Interlocked.Increment(ref workCounter);

            // Must run on the main (UI) thread
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { uiUpdateTimer.Start(); });
            }
            else
            {
                uiUpdateTimer.Start();
            }

            // Enqueue the message
            var messageContent = new InfoPanelContent();
            messageContent.Append(message);
            messageContent.finalize();
            infoPanelQueue.Enqueue(messageContent);
        }

        /// <summary>
        /// Displays a printable.
        /// </summary>
        /// <param name="c"> The printable to be displayed. </param>
        /// <ramarks> Calls to this method are synchronized, i.e. it will finish displaying one printable before displaying the next. </remarks>
        public void UpdateSync(IPrintable c)
        {
            // We have an additional UI update
            Interlocked.Increment(ref workCounter);

            //Must run on the main (UI) thread
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { uiUpdateTimer.Start(); });
            }
            else
            {
                uiUpdateTimer.Start();
            }

            lock (this)
            {
                // During debugging we want VS to catch exceptions so we can inspect the program state at the point where the exception was thrown.
                // For release builds we catch the execption here and display a message so the user knows that that they shouldn't wait for the generalization.
#if !DEBUG
                try
                {
#endif
                    DisplayMessage("busy...");

                    // Update
                    var content = new InfoPanelContent();
                    c.InfoPanelText(content, getFormatFromGUI());
                    content.finalize();
                    currentInfoPanelPrintable = c;
                    infoPanelQueue.Enqueue(content);
#if !DEBUG
                }
                catch (Exception e)
                {
                    // Notify user
                    Interlocked.Decrement(ref workCounter);
                    DisplayMessage($"An exception was thrown. Please report this bug to viper@inf.ethz.ch.\nDescription of the exception: {e.Message}");
                }
#endif
            }
        }

        public void SetInfoPanel(IPrintable c)
        {
            if (c == null) return;

            Task.Run(() => UpdateSync(c));
        }

        private PrettyPrintFormat getFormatFromGUI()
        {
            return new PrettyPrintFormat
            {
                showType = showTypesCB.Checked,
                showTermId = showTermIdCB.Checked,
                maxWidth = (int)maxTermWidthUD.Value,
                MaxTermPrintingDepth = (int)maxTermDepthUD.Value,
                MaxEqualityExplanationPrintingDepth = (int)congruenceDepthUD.Value,
                ShowEqualityExplanations = showEqualityExplanationsCheckBox.Checked,
                rewritingEnabled = enableRewritingCB.Checked,
                printRuleDict = printRuleDict.clone()
            };
        }

        private void InfoPanelUpdateTick(object sender, EventArgs e)
        {
            InfoPanelContent content;

            // dequeue outdated tooltips
            while (infoPanelQueue.Count > 1)
            {
                infoPanelQueue.TryDequeue(out content);
            }

            // read the first entry if available
            if (!infoPanelQueue.TryPeek(out content)) return;

            // clear toolTipBox if this is a new toolTip.
            if (!content.inProgress())
            {
                toolTipBox.Enabled = false;
                toolTipBox.Clear();
            }

            
            content.writeToTextBox(toolTipBox, 500);

            // check if finished
            if (content.finished)
            {
                infoPanelQueue.TryDequeue(out content);
                checkStopCounter();
                toolTipBox.Enabled = true;
            }
            
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

        private void quantifierBlameVisualizationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (model != null)
            {
                GraphVizualization.DumpGraph(model, "<unknown>");
            }
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

        private PrintRuleViewer printRuleViewer;
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

        private void z3AxiomTree_Leave(object sender, EventArgs e)
        {
            z3AxiomTree.SelectedNode = null;
        }

        private void congruenceDepthUD_ValueChanged(object sender, EventArgs e)
        {
            // Print with new format
            SetInfoPanel(currentInfoPanelPrintable);
        }

        private void showEqualityExplanationsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // Print with new format
            SetInfoPanel(currentInfoPanelPrintable);
        }

        private void largeTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Set and save
            var largeTextMode = largeTextToolStripMenuItem.Checked;
            Properties.Settings.Default.LargeTextMode = largeTextMode;
            Properties.Settings.Default.Save();

            // Update UI
            PrintConstants.LargeTextMode = largeTextMode;
            SetInfoPanel(currentInfoPanelPrintable);
            z3AxiomTree.Font = PrintConstants.DefaultFont;
        }

        public void EnableTermIds()
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { showTermIdCB.Checked = true; });
            }
            else
            {
                showTermIdCB.Checked = true;
            }
        }
    }
}
