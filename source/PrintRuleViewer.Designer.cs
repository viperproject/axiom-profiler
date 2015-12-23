namespace Z3AxiomProfiler
{
    partial class PrintRuleViewer
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
            this.toolBar = new System.Windows.Forms.Panel();
            this.editButton = new System.Windows.Forms.Button();
            this.deleteRuleButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.importButton = new System.Windows.Forms.Button();
            this.newRuleButton = new System.Windows.Forms.Button();
            this.rulesView = new System.Windows.Forms.ListView();
            this.MatchesHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.PrefixHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.InfixHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuffixHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChildrenHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.prefixLinebreakHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.infixLinebreakHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.suffixLinebreakHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.associativityHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.parenthesesHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.precedenceHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.indentHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.toolBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolBar
            // 
            this.toolBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toolBar.Controls.Add(this.editButton);
            this.toolBar.Controls.Add(this.deleteRuleButton);
            this.toolBar.Controls.Add(this.exportButton);
            this.toolBar.Controls.Add(this.importButton);
            this.toolBar.Controls.Add(this.newRuleButton);
            this.toolBar.Location = new System.Drawing.Point(0, 0);
            this.toolBar.Name = "toolBar";
            this.toolBar.Size = new System.Drawing.Size(913, 25);
            this.toolBar.TabIndex = 0;
            // 
            // editButton
            // 
            this.editButton.Location = new System.Drawing.Point(76, 1);
            this.editButton.Name = "editButton";
            this.editButton.Size = new System.Drawing.Size(75, 23);
            this.editButton.TabIndex = 4;
            this.editButton.Text = "Edit Rule";
            this.editButton.UseVisualStyleBackColor = true;
            this.editButton.Click += new System.EventHandler(this.editButton_Click);
            // 
            // deleteRuleButton
            // 
            this.deleteRuleButton.Location = new System.Drawing.Point(151, 1);
            this.deleteRuleButton.Name = "deleteRuleButton";
            this.deleteRuleButton.Size = new System.Drawing.Size(75, 23);
            this.deleteRuleButton.TabIndex = 1;
            this.deleteRuleButton.Text = "Delete Rule";
            this.deleteRuleButton.UseVisualStyleBackColor = true;
            this.deleteRuleButton.Click += new System.EventHandler(this.deleteRuleButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Location = new System.Drawing.Point(301, 1);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(75, 23);
            this.exportButton.TabIndex = 3;
            this.exportButton.Text = "Export...";
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // importButton
            // 
            this.importButton.Location = new System.Drawing.Point(226, 1);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(75, 23);
            this.importButton.TabIndex = 2;
            this.importButton.Text = "Import...";
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.Click += new System.EventHandler(this.importButton_Click);
            // 
            // newRuleButton
            // 
            this.newRuleButton.Location = new System.Drawing.Point(1, 1);
            this.newRuleButton.Name = "newRuleButton";
            this.newRuleButton.Size = new System.Drawing.Size(75, 23);
            this.newRuleButton.TabIndex = 0;
            this.newRuleButton.Text = "New Rule";
            this.newRuleButton.UseVisualStyleBackColor = true;
            this.newRuleButton.Click += new System.EventHandler(this.addRuleButton_Click);
            // 
            // rulesView
            // 
            this.rulesView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rulesView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.MatchesHeader,
            this.PrefixHeader,
            this.InfixHeader,
            this.SuffixHeader,
            this.ChildrenHeader,
            this.prefixLinebreakHeader,
            this.infixLinebreakHeader,
            this.suffixLinebreakHeader,
            this.associativityHeader,
            this.parenthesesHeader,
            this.precedenceHeader,
            this.indentHeader});
            this.rulesView.FullRowSelect = true;
            this.rulesView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.rulesView.Location = new System.Drawing.Point(0, 25);
            this.rulesView.MultiSelect = false;
            this.rulesView.Name = "rulesView";
            this.rulesView.Size = new System.Drawing.Size(914, 498);
            this.rulesView.TabIndex = 4;
            this.rulesView.UseCompatibleStateImageBehavior = false;
            this.rulesView.View = System.Windows.Forms.View.Details;
            this.rulesView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.rulesView_KeyDown);
            // 
            // MatchesHeader
            // 
            this.MatchesHeader.Text = "Matches";
            this.MatchesHeader.Width = 80;
            // 
            // PrefixHeader
            // 
            this.PrefixHeader.Text = "Constant Value / Prefix";
            this.PrefixHeader.Width = 130;
            // 
            // InfixHeader
            // 
            this.InfixHeader.Text = "Infix";
            this.InfixHeader.Width = 40;
            // 
            // SuffixHeader
            // 
            this.SuffixHeader.Text = "Suffix";
            this.SuffixHeader.Width = 40;
            // 
            // ChildrenHeader
            // 
            this.ChildrenHeader.Text = "Show Children";
            this.ChildrenHeader.Width = 80;
            // 
            // prefixLinebreakHeader
            // 
            this.prefixLinebreakHeader.Text = "Prefix Linbreak";
            this.prefixLinebreakHeader.Width = 80;
            // 
            // infixLinebreakHeader
            // 
            this.infixLinebreakHeader.Text = "Infix Linebreak";
            this.infixLinebreakHeader.Width = 80;
            // 
            // suffixLinebreakHeader
            // 
            this.suffixLinebreakHeader.Text = "Suffix Linebreak";
            this.suffixLinebreakHeader.Width = 80;
            // 
            // associativityHeader
            // 
            this.associativityHeader.Text = "Associative";
            this.associativityHeader.Width = 80;
            // 
            // parenthesesHeader
            // 
            this.parenthesesHeader.Text = "Parentheses";
            this.parenthesesHeader.Width = 80;
            // 
            // precedenceHeader
            // 
            this.precedenceHeader.Text = "Precedence";
            this.precedenceHeader.Width = 80;
            // 
            // indentHeader
            // 
            this.indentHeader.Text = "Indent";
            // 
            // PrintRuleViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(914, 523);
            this.Controls.Add(this.rulesView);
            this.Controls.Add(this.toolBar);
            this.MinimumSize = new System.Drawing.Size(450, 238);
            this.Name = "PrintRuleViewer";
            this.Text = "Print Rule Viewer";
            this.toolBar.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel toolBar;
        private System.Windows.Forms.ListView rulesView;
        private System.Windows.Forms.ColumnHeader MatchesHeader;
        private System.Windows.Forms.ColumnHeader PrefixHeader;
        private System.Windows.Forms.ColumnHeader InfixHeader;
        private System.Windows.Forms.ColumnHeader SuffixHeader;
        private System.Windows.Forms.ColumnHeader ChildrenHeader;
        private System.Windows.Forms.Button newRuleButton;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button deleteRuleButton;
        private System.Windows.Forms.ColumnHeader prefixLinebreakHeader;
        private System.Windows.Forms.ColumnHeader infixLinebreakHeader;
        private System.Windows.Forms.ColumnHeader suffixLinebreakHeader;
        private System.Windows.Forms.ColumnHeader associativityHeader;
        private System.Windows.Forms.ColumnHeader parenthesesHeader;
        private System.Windows.Forms.ColumnHeader precedenceHeader;
        private System.Windows.Forms.Button editButton;
        private System.Windows.Forms.ColumnHeader indentHeader;
    }
}