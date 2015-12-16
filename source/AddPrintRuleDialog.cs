using System;
using System.Windows.Forms;
using Z3AxiomProfiler.PrettyPrinting;

namespace Z3AxiomProfiler
{
    public partial class AddPrintRuleDialog : Form
    {
        private readonly PrintRuleDictionary printRuleDictionary;
        public AddPrintRuleDialog(PrintRuleDictionary printRuleDictionary)
        {
            InitializeComponent();
            this.printRuleDictionary = printRuleDictionary;
            prefixLinebreakCB.SelectedIndex = 1;
            infixLinebreakCB.SelectedIndex = 1;
            suffixLinebreakCB.SelectedIndex = 0;
        }

        private void printChildrenCB_CheckedChanged(object sender, EventArgs e)
        {
            if (printChildrenCB.Checked)
            {
                prefixLabel.Text = "Prefix:";
                infixTextBox.Enabled = true;
                postfixTextBox.Enabled = true;
            }
            else
            {
                prefixLabel.Text = "New value:";
                infixTextBox.Enabled = false;
                postfixTextBox.Enabled = false;
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
            if (printRuleDictionary.hasRule(matchTextBox.Text))
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
                postfixTextBox.Text.Contains(";"))
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
                suffix = postfixTextBox.Text,
                printChildren = printChildrenCB.Checked
            };
        }

        private void printChildrenCB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                printChildrenCB.Checked = !printChildrenCB.Checked;
            }
        }
    }
}
