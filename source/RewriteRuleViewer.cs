using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using Z3AxiomProfiler.Rewriting;

namespace Z3AxiomProfiler
{
    public partial class RewriteRuleViewer : Form
    {
        private RewriteDictionary rewriteRulesDict;
        private readonly Z3AxiomProfiler profiler;
        public RewriteRuleViewer(Z3AxiomProfiler profiler, RewriteDictionary rulesDictionary)
        {
            this.profiler = profiler;
            rewriteRulesDict = rulesDictionary;
            InitializeComponent();
            updateRulesList();
        }

        private void updateRulesList()
        {
            rulesView.BeginUpdate();
            rulesView.Items.Clear();
            foreach (var item in rewriteRulesDict.getAllRules().Select(getRuleItem))
            {
                rulesView.Items.Add(item);
            }
            rulesView.EndUpdate();
            profiler.updateInfoPanel();
        }

        private static ListViewItem getRuleItem(KeyValuePair<string, RewriteRule> keyValPair)
        {
            var rule = keyValPair.Value;
            var item = new ListViewItem
            {
                Text = keyValPair.Key,
                Name = $"Rule for {keyValPair.Key}",
                Tag = rule
            };
            item.SubItems.Add(rule.prefix);
            item.SubItems.Add(rule.infix);
            item.SubItems.Add(rule.suffix);
            item.SubItems.Add(rule.printChildren + "");
            return item;
        }

        private void addRuleButton_Click(object sender, EventArgs e)
        {
            var addRuleDialog = new AddRewriteRuleDialog(rewriteRulesDict);
            var result = addRuleDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                updateRulesList();
            }
        }

        private void deleteRuleButton_Click(object sender, EventArgs e)
        {
            deleteSelectedRule();
        }

        private void deleteSelectedRule()
        {
            foreach (ListViewItem rule in rulesView.SelectedItems)
            {
                rewriteRulesDict.removeRule(rule.Text);
            }
            updateRulesList();
        }

        private void rulesView_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                deleteSelectedRule();
            }
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                DefaultExt = ".csv",
                AddExtension = true,
                Filter = "Comma separated Values (*.csv)|*.csv"
            };
            var dialogResult = dialog.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                return;
            }

            var outStream = new StreamWriter(dialog.OpenFile());
            foreach (var rulePair in rewriteRulesDict.getAllRules())
            {
                var rule = rulePair.Value;
                outStream.WriteLineAsync(
                    $"{rulePair.Key};{rule.prefix};{rule.infix};{rule.suffix};{rule.printChildren}");
            }
            outStream.Close();
        }

        private void importButton_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                DefaultExt = ".csv",
                Filter = "Comma separated Values (*.csv)|*.csv"
            };
            var dialogResult = dialog.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                return;
            }

            rewriteRulesDict = new RewriteDictionary();
            var inStream = new StreamReader(dialog.OpenFile());
            var invalidLines = false;
            while (!inStream.EndOfStream)
            {
                var line = inStream.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                var lines = line.Split(';');
                if (lines.Length != 5)
                {
                    invalidLines = true;
                    continue;
                }
                var printChildren = true;
                if (!bool.TryParse(lines[4], out printChildren))
                {
                    invalidLines = true;
                }

                rewriteRulesDict.addRule(lines[0], new RewriteRule
                {
                    prefix = lines[1],
                    infix = lines[2],
                    suffix = lines[3],
                    printChildren = printChildren
                });
            }

            if (invalidLines)
            {
                MessageBox.Show("Some unparseable lines were skipped!",
                                "There were unparseable lines!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            updateRulesList();
        }
    }
}
