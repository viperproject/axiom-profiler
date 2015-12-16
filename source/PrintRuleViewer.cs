using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler
{
    public partial class PrintRuleViewer : Form
    {
        private readonly PrintRuleDictionary printRuleDict;
        private readonly Z3AxiomProfiler profiler;
        public PrintRuleViewer(Z3AxiomProfiler profiler, PrintRuleDictionary rulesDictionary)
        {
            this.profiler = profiler;
            printRuleDict = rulesDictionary;
            InitializeComponent();
            updateRulesList();
        }

        private void updateRulesList()
        {
            rulesView.BeginUpdate();
            rulesView.Items.Clear();
            foreach (var item in printRuleDict.getAllRules().Select(getRuleItem))
            {
                rulesView.Items.Add(item);
            }
            rulesView.EndUpdate();
            profiler.updateInfoPanel();
        }

        private static ListViewItem getRuleItem(KeyValuePair<string, PrintRule> keyValPair)
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
            item.SubItems.Add(PrintRule.lineBreakSettingToString(rule.prefixLineBreak));
            item.SubItems.Add(PrintRule.lineBreakSettingToString(rule.infixLineBreak));
            item.SubItems.Add(PrintRule.lineBreakSettingToString(rule.suffixLineBreak));
            item.SubItems.Add(rule.associative + "");
            item.SubItems.Add(PrintRule.parenthesesSettingsToString(rule.parentheses));
            item.SubItems.Add(rule.precedence + "");
            return item;
        }

        private void addRuleButton_Click(object sender, EventArgs e)
        {
            var addRuleDialog = new EditPrintRuleDialog(printRuleDict);
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
                printRuleDict.removeRule(rule.Text);
            }
            updateRulesList();
        }

        private void rulesView_KeyDown(object sender, KeyEventArgs e)
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
            foreach (var rulePair in printRuleDict.getAllRules())
            {
                var rule = rulePair.Value;
                outStream.WriteLineAsync(
                    $"{rulePair.Key};" +
                    $"{rule.prefix};" +
                    $"{rule.infix};" +
                    $"{rule.suffix};" +
                    $"{rule.printChildren};" +
                    $"{PrintRule.lineBreakSettingToString(rule.prefixLineBreak)};" +
                    $"{PrintRule.lineBreakSettingToString(rule.infixLineBreak)};" +
                    $"{PrintRule.lineBreakSettingToString(rule.suffixLineBreak)};" +
                    $"{rule.associative};" +
                    $"{PrintRule.parenthesesSettingsToString(rule.parentheses)};" +
                    $"{rule.precedence}");
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

            var inStream = new StreamReader(dialog.OpenFile());
            var invalidLines = false;
            while (!inStream.EndOfStream)
            {
                var line = inStream.ReadLine();

                // skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                var lines = line.Split(';');
                // validate
                if (lines.Length != 11)
                {
                    invalidLines = true;
                    continue;
                }

                // parse bools & ints
                bool printChildren;
                bool associative;
                int precedence;
                if (!bool.TryParse(lines[4], out printChildren) ||
                    !bool.TryParse(lines[8], out associative) ||
                    !int.TryParse(lines[10], out precedence))
                {
                    invalidLines = true;
                    continue;
                }

                // parse enums
                PrintRule.LineBreakSetting prefixLinebreaks;
                PrintRule.LineBreakSetting infixLinebreaks;
                PrintRule.LineBreakSetting suffixLinebreaks;
                PrintRule.ParenthesesSetting parenthesesSettings;
                try
                {
                    prefixLinebreaks = PrintRule.lineBreakSettingFromString(lines[5]);
                    infixLinebreaks = PrintRule.lineBreakSettingFromString(lines[6]);
                    suffixLinebreaks = PrintRule.lineBreakSettingFromString(lines[7]);
                    parenthesesSettings = PrintRule.parenthesesSettingsFromString(lines[9]);
                }
                catch (ArgumentException)
                {
                    invalidLines = true;
                    continue;
                }

                printRuleDict.addRule(lines[0], new PrintRule
                {
                    prefix = lines[1],
                    infix = lines[2],
                    suffix = lines[3],
                    printChildren = printChildren,
                    prefixLineBreak = prefixLinebreaks,
                    infixLineBreak = infixLinebreaks,
                    suffixLineBreak = suffixLinebreaks,
                    associative = associative,
                    parentheses = parenthesesSettings,
                    precedence = precedence,
                });
            }

            if (invalidLines)
            {
                MessageBox.Show("Some unparseable lines were skipped!",
                                "There were unparseable lines!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            updateRulesList();
        }

        private void editButton_Click(object sender, EventArgs e)
        {
            if (rulesView.SelectedItems.Count == 0) return;
            var selectedRule = rulesView.SelectedItems[0].Tag as PrintRule;
            if (selectedRule == null) return;
            var addRuleDialog = new EditPrintRuleDialog(printRuleDict, rulesView.SelectedItems[0].Text, selectedRule);
            var result = addRuleDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                updateRulesList();
            }
        }
    }
}
