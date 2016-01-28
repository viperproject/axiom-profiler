using System;
using System.Windows.Forms;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler
{
    public partial class EditPrintRuleDialog : Form
    {
        private readonly PrintRuleDictionary printRuleDictionary;
        private readonly bool editMode;
        public EditPrintRuleDialog(PrintRuleDictionary printRuleDictionary)
        {
            InitializeComponent();
            this.printRuleDictionary = printRuleDictionary;
            prefixLinebreakCB.SelectedIndex = (int)PrintRule.LineBreakSetting.After;
            infixLinebreakCB.SelectedIndex = (int)PrintRule.LineBreakSetting.After;
            suffixLinebreakCB.SelectedIndex = (int)PrintRule.LineBreakSetting.Before;
            parenthesesCB.SelectedIndex = (int)PrintRule.ParenthesesSetting.Precedence;
        }

        public EditPrintRuleDialog(PrintRuleDictionary printRuleDictionary, string match, PrintRule editRule) : this(printRuleDictionary)
        {
            editMode = true;

            // text
            matchTextBox.Text = match;
            prefixTextBox.Text = editRule.prefix;
            infixTextBox.Text = editRule.infix;
            suffixTextBox.Text = editRule.suffix;

            // colors
            prefixColorButton.BackColor = editRule.prefixColor;
            infixColorButton.BackColor = editRule.infixColor;
            suffixColorButton.BackColor = editRule.suffixColor;

            // comboboxes
            // correct index for missing options
            prefixLinebreakCB.SelectedIndex = editRule.prefixLineBreak == PrintRule.LineBreakSetting.After ? 0 : 1; 
            infixLinebreakCB.SelectedIndex = (int)editRule.infixLineBreak;
            // correct index for missing options
            suffixLinebreakCB.SelectedIndex = editRule.suffixLineBreak == PrintRule.LineBreakSetting.Before ? 0 : 1;
            parenthesesCB.SelectedIndex = (int)editRule.parentheses;

            // checkboxes
            printChildrenCB.Checked = editRule.printChildren;
            associativeCB.Checked = editRule.associative;

            // nummeric updowns
            precedenceUD.Value = editRule.precedence;
        }

        private void printChildrenCB_CheckedChanged(object sender, EventArgs e)
        {
            if (printChildrenCB.Checked)
            {
                prefixLabel.Text = "Prefix:";
                infixTextBox.Enabled = true;
                suffixTextBox.Enabled = true;
                infixColorButton.Enabled = true;
                suffixColorButton.Enabled = true;
                associativeCB.Enabled = true;
                parenthesesCB.Enabled = true;
                parenthesesCB.SelectedIndex = (int)PrintRule.ParenthesesSetting.Precedence;
                indentCB.Enabled = true;
                prefixLinebreakCB.Enabled = true;
                infixLinebreakCB.Enabled = true;
                suffixLinebreakCB.Enabled = true;
                precedenceUD.Enabled = true;
            }
            else
            {
                prefixLabel.Text = "New value:";
                infixTextBox.Enabled = false;
                suffixTextBox.Enabled = false;
                infixColorButton.Enabled = false;
                suffixColorButton.Enabled = false;
                associativeCB.Enabled = false;
                parenthesesCB.Enabled = false;
                parenthesesCB.SelectedIndex = (int) PrintRule.ParenthesesSetting.Never;
                indentCB.Enabled = false;
                prefixLinebreakCB.Enabled = false;
                infixLinebreakCB.Enabled = false;
                suffixLinebreakCB.Enabled = false;
                precedenceUD.Enabled = false;
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            // check if form is filled out correctly
            if (!isValidInput()) return;

            // if so, add the rule (unless abort on collision).
            // unless in edit mode
            if (!editMode && printRuleDictionary.hasRule(matchTextBox.Text))
            {
                var overwriteDecision = MessageBox.Show(
                    $"There is already a rewrite rule matching {matchTextBox.Text}." +
                    " Do you want to replace this rule?",
                    "Rule already exists!",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (overwriteDecision == DialogResult.No)
                {
                    // do not close yet, as the user must have the possibility to correct the match value.
                    return;
                }
                // remove the old one
                printRuleDictionary.removeRule(matchTextBox.Text);
            }
            printRuleDictionary.addRule(matchTextBox.Text, buildRuleFromForm());
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool isValidInput()
        {
            if (string.IsNullOrWhiteSpace(matchTextBox.Text))
            {
                MessageBox.Show("The form is missing required values. " +
                                "Please fill in all values and try again.",
                                "Missing or blank values!",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (matchTextBox.Text.Contains(";") ||
                prefixTextBox.Text.Contains(";") ||
                infixTextBox.Text.Contains(";") ||
                suffixTextBox.Text.Contains(";"))
            {
                MessageBox.Show("The form contains an invalid character (;). " +
                                "Please remove all these characters and try again.",
                                "Invalid character!",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private PrintRule buildRuleFromForm()
        {
            return new PrintRule
            {
                prefix = prefixTextBox.Text,
                infix = infixTextBox.Text,
                suffix = suffixTextBox.Text,
                prefixColor = prefixColorButton.BackColor,
                infixColor = infixColorButton.BackColor,
                suffixColor = suffixColorButton.BackColor,
                printChildren = printChildrenCB.Checked,
                prefixLineBreak = PrintRule.lineBreakSettingFromString((string)prefixLinebreakCB.SelectedItem),
                infixLineBreak = PrintRule.lineBreakSettingFromString((string)infixLinebreakCB.SelectedItem),
                suffixLineBreak = PrintRule.lineBreakSettingFromString((string)suffixLinebreakCB.SelectedItem),
                associative = associativeCB.Checked,
                indent = indentCB.Checked,
                parentheses = PrintRule.parenthesesSettingsFromString((string)parenthesesCB.SelectedItem),
                precedence = (int)precedenceUD.Value
            };
        }

        private void printChildrenCB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                printChildrenCB.Checked = !printChildrenCB.Checked;
            }
        }

        private void associativeCB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                associativeCB.Checked = !associativeCB.Checked;
            }
        }

        private void indentCB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                indentCB.Checked = !indentCB.Checked;
            }
        }

        private void prefixColorButton_Click(object sender, EventArgs e)
        {
            var dialog = new ColorDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                prefixColorButton.BackColor = dialog.Color;
            }
        }

        private void infixColorButton_Click(object sender, EventArgs e)
        {
            var dialog = new ColorDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                infixColorButton.BackColor = dialog.Color;
            }
        }

        private void suffixColorButton_Click(object sender, EventArgs e)
        {
            var dialog = new ColorDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                suffixColorButton.BackColor = dialog.Color;
            }
        }
    }
}
