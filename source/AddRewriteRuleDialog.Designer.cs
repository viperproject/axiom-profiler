namespace Z3AxiomProfiler
{
    partial class AddRewriteRuleDialog
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
            this.postfixTextBox = new System.Windows.Forms.TextBox();
            this.infixLabel = new System.Windows.Forms.Label();
            this.suffixLabel = new System.Windows.Forms.Label();
            this.matchTextBox = new System.Windows.Forms.TextBox();
            this.matchLabel = new System.Windows.Forms.Label();
            this.addButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // prefixTextBox
            // 
            this.prefixTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.prefixTextBox.Location = new System.Drawing.Point(77, 32);
            this.prefixTextBox.Name = "prefixTextBox";
            this.prefixTextBox.Size = new System.Drawing.Size(215, 20);
            this.prefixTextBox.TabIndex = 2;
            // 
            // prefixLabel
            // 
            this.prefixLabel.AutoSize = true;
            this.prefixLabel.Location = new System.Drawing.Point(12, 35);
            this.prefixLabel.Name = "prefixLabel";
            this.prefixLabel.Size = new System.Drawing.Size(36, 13);
            this.prefixLabel.TabIndex = 3;
            this.prefixLabel.Text = "Prefix:";
            // 
            // printChildrenCB
            // 
            this.printChildrenCB.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.printChildrenCB.AutoSize = true;
            this.printChildrenCB.Checked = true;
            this.printChildrenCB.CheckState = System.Windows.Forms.CheckState.Checked;
            this.printChildrenCB.Location = new System.Drawing.Point(197, 8);
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
            this.infixTextBox.Size = new System.Drawing.Size(215, 20);
            this.infixTextBox.TabIndex = 3;
            // 
            // postfixTextBox
            // 
            this.postfixTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.postfixTextBox.Location = new System.Drawing.Point(77, 85);
            this.postfixTextBox.Name = "postfixTextBox";
            this.postfixTextBox.Size = new System.Drawing.Size(215, 20);
            this.postfixTextBox.TabIndex = 4;
            // 
            // infixLabel
            // 
            this.infixLabel.AutoSize = true;
            this.infixLabel.Location = new System.Drawing.Point(12, 61);
            this.infixLabel.Name = "infixLabel";
            this.infixLabel.Size = new System.Drawing.Size(29, 13);
            this.infixLabel.TabIndex = 7;
            this.infixLabel.Text = "Infix:";
            // 
            // suffixLabel
            // 
            this.suffixLabel.AutoSize = true;
            this.suffixLabel.Location = new System.Drawing.Point(12, 88);
            this.suffixLabel.Name = "suffixLabel";
            this.suffixLabel.Size = new System.Drawing.Size(36, 13);
            this.suffixLabel.TabIndex = 8;
            this.suffixLabel.Text = "Suffix:";
            // 
            // matchTextBox
            // 
            this.matchTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.matchTextBox.Location = new System.Drawing.Point(77, 6);
            this.matchTextBox.Name = "matchTextBox";
            this.matchTextBox.Size = new System.Drawing.Size(99, 20);
            this.matchTextBox.TabIndex = 0;
            // 
            // matchLabel
            // 
            this.matchLabel.AutoSize = true;
            this.matchLabel.Location = new System.Drawing.Point(12, 9);
            this.matchLabel.Name = "matchLabel";
            this.matchLabel.Size = new System.Drawing.Size(40, 13);
            this.matchLabel.TabIndex = 10;
            this.matchLabel.Text = "Match:";
            // 
            // addButton
            // 
            this.addButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.addButton.Location = new System.Drawing.Point(65, 111);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(75, 23);
            this.addButton.TabIndex = 5;
            this.addButton.Text = "Add Rule";
            this.addButton.UseVisualStyleBackColor = true;
            this.addButton.Click += new System.EventHandler(this.addButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.cancelButton.Location = new System.Drawing.Point(155, 111);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 6;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // AddRewriteRuleDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(297, 141);
            this.ControlBox = false;
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.addButton);
            this.Controls.Add(this.matchLabel);
            this.Controls.Add(this.matchTextBox);
            this.Controls.Add(this.suffixLabel);
            this.Controls.Add(this.infixLabel);
            this.Controls.Add(this.postfixTextBox);
            this.Controls.Add(this.infixTextBox);
            this.Controls.Add(this.printChildrenCB);
            this.Controls.Add(this.prefixLabel);
            this.Controls.Add(this.prefixTextBox);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1000, 180);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(300, 180);
            this.Name = "AddRewriteRuleDialog";
            this.Text = "Add Rewrite Rule";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox prefixTextBox;
        private System.Windows.Forms.Label prefixLabel;
        private System.Windows.Forms.CheckBox printChildrenCB;
        private System.Windows.Forms.TextBox infixTextBox;
        private System.Windows.Forms.TextBox postfixTextBox;
        private System.Windows.Forms.Label infixLabel;
        private System.Windows.Forms.Label suffixLabel;
        private System.Windows.Forms.TextBox matchTextBox;
        private System.Windows.Forms.Label matchLabel;
        private System.Windows.Forms.Button addButton;
        private System.Windows.Forms.Button cancelButton;
    }
}