using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Z3AxiomProfiler.Rewriting;

namespace Z3AxiomProfiler
{
    public partial class RewriteRuleViewer : Form
    {
        private readonly RewriteDictionary rewriteRulesDict;
        public RewriteRuleViewer(RewriteDictionary rulesDictionary)
        {
            rewriteRulesDict = rulesDictionary;
            InitializeComponent();
            updateRulesList();
        }

        private void updateRulesList()
        {
            rulesView.BeginUpdate();
            foreach (var item in rewriteRulesDict.termTranslations.OrderBy(keyValPair => keyValPair.Key).Select(keyValPair => getRuleItem(keyValPair)))
            {
                rulesView.Items.Add(item);
            }
            rulesView.EndUpdate();
        }

        private static ListViewItem getRuleItem(KeyValuePair<string, RewriteRule> keyValPair)
        {
            var rule = keyValPair.Value;
            ListViewItem item = new ListViewItem
            {
                Text = keyValPair.Key,
                Name = $"Rule for {keyValPair.Key}",
                Tag = rule
            };
            item.SubItems.Add(rule.prefix);
            item.SubItems.Add(rule.infix);
            item.SubItems.Add(rule.postfix);
            item.SubItems.Add(rule.printChildren + "");
            return item;
        }

        private void addRuleButton_Click(object sender, EventArgs e)
        {
            var addRuleDialog = new AddRewriteRuleDialog(rewriteRulesDict.termTranslations);
            var result = addRuleDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                updateRulesList();
            }
        }
    }
}
