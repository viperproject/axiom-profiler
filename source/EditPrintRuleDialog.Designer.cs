namespace Z3AxiomProfiler
{
    partial class EditPrintRuleDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.prefixTextBox = new System.Windows.Forms.TextBox();
            this.prefixLabel = new System.Windows.Forms.Label();
            this.printChildrenCB = new System.Windows.Forms.CheckBox();
            this.infixTextBox = new System.Windows.Forms.TextBox();
            this.suffixTextBox = new System.Windows.Forms.TextBox();
            this.infixLabel = new System.Windows.Forms.Label();
            this.suffixLabel = new System.Windows.Forms.Label();
            this.matchTextBox = new System.Windows.Forms.TextBox();
            this.matchLabel = new System.Windows.Forms.Label();
            this.saveButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.LinebrakeingLabel = new System.Windows.Forms.Label();
            this.prefixLinebreakCB = new System.Windows.Forms.ComboBox();
            this.prefixLinebreakLabel = new System.Windows.Forms.Label();
            this.infixLinebreakLabel = new System.Windows.Forms.Label();
            this.infixLinebreakCB = new System.Windows.Forms.ComboBox();
            this.suffixLinebreakLabel = new System.Windows.Forms.Label();
            this.suffixLinebreakCB = new System.Windows.Forms.ComboBox();
            this.precedenceUD = new System.Windows.Forms.NumericUpDown();
            this.opPrecedenceLabel = new System.Windows.Forms.Label();
            this.associativeCB = new System.Windows.Forms.CheckBox();
            this.parenthesesCB = new System.Windows.Forms.ComboBox();
            this.parenthesesLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.precedenceUD)).BeginInit();
            this.SuspendLayout();
            // 
            // prefixTextBox
            // 
            this.prefixTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.prefixTextBox.Location = new System.Drawing.Point(77, 32);
            this.prefixTextBox.Name = "prefixTextBox";
            this.prefixTextBox.Size = new System.Drawing.Size(298, 20);
            this.prefixTextBox.TabIndex = 2;
            // 
            // prefixLabel
            // 
            this.prefixLabel.AutoSize = true;
            this.prefixLabel.Location = new System.Drawing.Point(12, 35);
            this.prefixLabel.Name = "prefixLabel";
            this.prefixLabel.Size = new System.Drawing.Size(36, 13);
            this.prefixLabel.TabIndex = 24;
            this.prefixLabel.Text = "Prefix:";
            // 
            // printChildrenCB
            // 
            this.printChildrenCB.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.printChildrenCB.AutoSize = true;
            this.printChildrenCB.Checked = true;
            this.printChildrenCB.CheckState = System.Windows.Forms.CheckState.Checked;
            this.printChildrenCB.Location = new System.Drawing.Point(287, 8);
            this.printChildrenCB.Name = "printChildrenCB";
            this.printChildrenCB.Size = new System.Drawing.Size(88, 17);
            this.printChildrenCB.TabIndex = 1;
            this.printChildrenCB.Text = "Print Children";
            this.printChildrenCB.UseVisualStyleBackColor = true;
            this.printChildrenCB.CheckedChanged += new System.EventHandler(this.printChildrenCB_CheckedChanged);
            this.printChildrenCB.KeyDown += new System.Windows.Forms.KeyEventHandler(this.printChildrenCB_KeyDown);
            // 
            // infixTextBox
            // 
            this.infixTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.infixTextBox.Location = new System.Drawing.Point(77, 58);
            this.infixTextBox.Name = "infixTextBox";
            this.infixTextBox.Size = new System.Drawing.Size(298, 20);
            this.infixTextBox.TabIndex = 3;
            // 
            // suffixTextBox
            // 
            this.suffixTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.suffixTextBox.Location = new System.Drawing.Point(77, 85);
            this.suffixTextBox.Name = "suffixTextBox";
            this.suffixTextBox.Size = new System.Drawing.Size(298, 20);
            this.suffixTextBox.TabIndex = 4;
            // 
            // infixLabel
            // 
            this.infixLabel.AutoSize = true;
            this.infixLabel.Location = new System.Drawing.Point(12, 61);
            this.infixLabel.Name = "infixLabel";
            this.infixLabel.Size = new System.Drawing.Size(29, 13);
            this.infixLabel.TabIndex = 20;
            this.infixLabel.Text = "Infix:";
            // 
            // suffixLabel
            // 
            this.suffixLabel.AutoSize = true;
            this.suffixLabel.Location = new System.Drawing.Point(12, 88);
            this.suffixLabel.Name = "suffixLabel";
            this.suffixLabel.Size = new System.Drawing.Size(36, 13);
            this.suffixLabel.TabIndex = 21;
            this.suffixLabel.Text = "Suffix:";
            // 
            // matchTextBox
            // 
            this.matchTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.matchTextBox.Location = new System.Drawing.Point(77, 6);
            this.matchTextBox.Name = "matchTextBox";
            this.matchTextBox.Size = new System.Drawing.Size(204, 20);
            this.matchTextBox.TabIndex = 0;
            // 
            // matchLabel
            // 
            this.matchLabel.AutoSize = true;
            this.matchLabel.Location = new System.Drawing.Point(12, 9);
            this.matchLabel.Name = "matchLabel";
            this.matchLabel.Size = new System.Drawing.Size(40, 13);
            this.matchLabel.TabIndex = 23;
            this.matchLabel.Text = "Match:";
            // 
            // saveButton
            // 
            this.saveButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.saveButton.Location = new System.Drawing.Point(107, 236);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(75, 23);
            this.saveButton.TabIndex = 11;
            this.saveButton.Text = "Save Rule";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.addButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.cancelButton.Location = new System.Drawing.Point(197, 236);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 12;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // LinebrakeingLabel
            // 
            this.LinebrakeingLabel.AutoSize = true;
            this.LinebrakeingLabel.Location = new System.Drawing.Point(12, 114);
            this.LinebrakeingLabel.Name = "LinebrakeingLabel";
            this.LinebrakeingLabel.Size = new System.Drawing.Size(98, 13);
            this.LinebrakeingLabel.TabIndex = 25;
            this.LinebrakeingLabel.Text = "Linebreak Settings:";
            // 
            // prefixLinebreakCB
            // 
            this.prefixLinebreakCB.DisplayMember = "Before";
            this.prefixLinebreakCB.FormattingEnabled = true;
            this.prefixLinebreakCB.Items.AddRange(new object[] {
            "Before",
            "After",
            "None"});
            this.prefixLinebreakCB.Location = new System.Drawing.Point(54, 133);
            this.prefixLinebreakCB.Name = "prefixLinebreakCB";
            this.prefixLinebreakCB.Size = new System.Drawing.Size(60, 21);
            this.prefixLinebreakCB.TabIndex = 5;
            // 
            // prefixLinebreakLabel
            // 
            this.prefixLinebreakLabel.AutoSize = true;
            this.prefixLinebreakLabel.Location = new System.Drawing.Point(12, 136);
            this.prefixLinebreakLabel.Name = "prefixLinebreakLabel";
            this.prefixLinebreakLabel.Size = new System.Drawing.Size(36, 13);
            this.prefixLinebreakLabel.TabIndex = 13;
            this.prefixLinebreakLabel.Text = "Prefix:";
            // 
            // infixLinebreakLabel
            // 
            this.infixLinebreakLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.infixLinebreakLabel.AutoSize = true;
            this.infixLinebreakLabel.Location = new System.Drawing.Point(155, 136);
            this.infixLinebreakLabel.Name = "infixLinebreakLabel";
            this.infixLinebreakLabel.Size = new System.Drawing.Size(29, 13);
            this.infixLinebreakLabel.TabIndex = 15;
            this.infixLinebreakLabel.Text = "Infix:";
            // 
            // infixLinebreakCB
            // 
            this.infixLinebreakCB.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.infixLinebreakCB.DisplayMember = "Before";
            this.infixLinebreakCB.FormattingEnabled = true;
            this.infixLinebreakCB.Items.AddRange(new object[] {
            "Before",
            "After",
            "None"});
            this.infixLinebreakCB.Location = new System.Drawing.Point(190, 133);
            this.infixLinebreakCB.Name = "infixLinebreakCB";
            this.infixLinebreakCB.Size = new System.Drawing.Size(60, 21);
            this.infixLinebreakCB.TabIndex = 6;
            // 
            // suffixLinebreakLabel
            // 
            this.suffixLinebreakLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.suffixLinebreakLabel.AutoSize = true;
            this.suffixLinebreakLabel.Location = new System.Drawing.Point(273, 136);
            this.suffixLinebreakLabel.Name = "suffixLinebreakLabel";
            this.suffixLinebreakLabel.Size = new System.Drawing.Size(36, 13);
            this.suffixLinebreakLabel.TabIndex = 17;
            this.suffixLinebreakLabel.Text = "Suffix:";
            // 
            // suffixLinebreakCB
            // 
            this.suffixLinebreakCB.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.suffixLinebreakCB.DisplayMember = "Before";
            this.suffixLinebreakCB.FormattingEnabled = true;
            this.suffixLinebreakCB.Items.AddRange(new object[] {
            "Before",
            "After",
            "None"});
            this.suffixLinebreakCB.Location = new System.Drawing.Point(315, 133);
            this.suffixLinebreakCB.Name = "suffixLinebreakCB";
            this.suffixLinebreakCB.Size = new System.Drawing.Size(60, 21);
            this.suffixLinebreakCB.TabIndex = 7;
            // 
            // precedenceUD
            // 
            this.precedenceUD.Location = new System.Drawing.Point(129, 189);
            this.precedenceUD.Name = "precedenceUD";
            this.precedenceUD.Size = new System.Drawing.Size(40, 20);
            this.precedenceUD.TabIndex = 10;
            // 
            // opPrecedenceLabel
            // 
            this.opPrecedenceLabel.AutoSize = true;
            this.opPrecedenceLabel.Location = new System.Drawing.Point(12, 191);
            this.opPrecedenceLabel.Name = "opPrecedenceLabel";
            this.opPrecedenceLabel.Size = new System.Drawing.Size(111, 13);
            this.opPrecedenceLabel.TabIndex = 19;
            this.opPrecedenceLabel.Text = "Operator precedence:";
            // 
            // associativeCB
            // 
            this.associativeCB.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.associativeCB.AutoSize = true;
            this.associativeCB.Location = new System.Drawing.Point(295, 163);
            this.associativeCB.Name = "associativeCB";
            this.associativeCB.Size = new System.Drawing.Size(80, 17);
            this.associativeCB.TabIndex = 9;
            this.associativeCB.Text = "Associative";
            this.associativeCB.UseVisualStyleBackColor = true;
            this.associativeCB.KeyDown += new System.Windows.Forms.KeyEventHandler(this.associativeCB_KeyDown);
            // 
            // parenthesesCB
            // 
            this.parenthesesCB.FormattingEnabled = true;
            this.parenthesesCB.Items.AddRange(new object[] {
            "Always",
            "Precedence",
            "Never"});
            this.parenthesesCB.Location = new System.Drawing.Point(129, 161);
            this.parenthesesCB.Name = "parenthesesCB";
            this.parenthesesCB.Size = new System.Drawing.Size(121, 21);
            this.parenthesesCB.TabIndex = 8;
            // 
            // parenthesesLabel
            // 
            this.parenthesesLabel.AutoSize = true;
            this.parenthesesLabel.Location = new System.Drawing.Point(12, 164);
            this.parenthesesLabel.Name = "parenthesesLabel";
            this.parenthesesLabel.Size = new System.Drawing.Size(69, 13);
            this.parenthesesLabel.TabIndex = 22;
            this.parenthesesLabel.Text = "Parentheses:";
            // 
            // EditPrintRuleDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(380, 271);
            this.ControlBox = false;
            this.Controls.Add(this.parenthesesLabel);
            this.Controls.Add(this.parenthesesCB);
            this.Controls.Add(this.associativeCB);
            this.Controls.Add(this.opPrecedenceLabel);
            this.Controls.Add(this.precedenceUD);
            this.Controls.Add(this.suffixLinebreakLabel);
            this.Controls.Add(this.suffixLinebreakCB);
            this.Controls.Add(this.infixLinebreakLabel);
            this.Controls.Add(this.infixLinebreakCB);
            this.Controls.Add(this.prefixLinebreakLabel);
            this.Controls.Add(this.prefixLinebreakCB);
            this.Controls.Add(this.LinebrakeingLabel);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.saveButton);
            this.Controls.Add(this.matchLabel);
            this.Controls.Add(this.matchTextBox);
            this.Controls.Add(this.suffixLabel);
            this.Controls.Add(this.infixLabel);
            this.Controls.Add(this.suffixTextBox);
            this.Controls.Add(this.infixTextBox);
            this.Controls.Add(this.printChildrenCB);
            this.Controls.Add(this.prefixLabel);
            this.Controls.Add(this.prefixTextBox);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(384, 287);
            this.Name = "EditPrintRuleDialog";
            this.Text = "Edit Print Rule";
            ((System.ComponentModel.ISupportInitialize)(this.precedenceUD)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox prefixTextBox;
        private System.Windows.Forms.Label prefixLabel;
        private System.Windows.Forms.CheckBox printChildrenCB;
        private System.Windows.Forms.TextBox infixTextBox;
        private System.Windows.Forms.TextBox suffixTextBox;
        private System.Windows.Forms.Label infixLabel;
        private System.Windows.Forms.Label suffixLabel;
        private System.Windows.Forms.TextBox matchTextBox;
        private System.Windows.Forms.Label matchLabel;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label LinebrakeingLabel;
        private System.Windows.Forms.ComboBox prefixLinebreakCB;
        private System.Windows.Forms.Label prefixLinebreakLabel;
        private System.Windows.Forms.Label infixLinebreakLabel;
        private System.Windows.Forms.ComboBox infixLinebreakCB;
        private System.Windows.Forms.Label suffixLinebreakLabel;
        private System.Windows.Forms.ComboBox suffixLinebreakCB;
        private System.Windows.Forms.NumericUpDown precedenceUD;
        private System.Windows.Forms.Label opPrecedenceLabel;
        private System.Windows.Forms.CheckBox associativeCB;
        private System.Windows.Forms.ComboBox parenthesesCB;
        private System.Windows.Forms.Label parenthesesLabel;
    }
}