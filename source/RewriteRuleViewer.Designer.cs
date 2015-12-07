namespace Z3AxiomProfiler
{
    partial class RewriteRuleViewer
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
            this.deleteRuleButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.importButton = new System.Windows.Forms.Button();
            this.addRuleButton = new System.Windows.Forms.Button();
            this.rulesView = new System.Windows.Forms.ListView();
            this.MatchesHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.PrefixHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.InfixHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.PostfixHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChildrenHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.toolBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolBar
            // 
            this.toolBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toolBar.Controls.Add(this.deleteRuleButton);
            this.toolBar.Controls.Add(this.exportButton);
            this.toolBar.Controls.Add(this.importButton);
            this.toolBar.Controls.Add(this.addRuleButton);
            this.toolBar.Location = new System.Drawing.Point(0, 0);
            this.toolBar.Name = "toolBar";
            this.toolBar.Size = new System.Drawing.Size(532, 25);
            this.toolBar.TabIndex = 0;
            // 
            // deleteRuleButton
            // 
            this.deleteRuleButton.Location = new System.Drawing.Point(76, 1);
            this.deleteRuleButton.Name = "deleteRuleButton";
            this.deleteRuleButton.Size = new System.Drawing.Size(75, 23);
            this.deleteRuleButton.TabIndex = 4;
            this.deleteRuleButton.Text = "Delete Rule";
            this.deleteRuleButton.UseVisualStyleBackColor = true;
            // 
            // exportButton
            // 
            this.exportButton.Location = new System.Drawing.Point(226, 1);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(75, 23);
            this.exportButton.TabIndex = 3;
            this.exportButton.Text = "Export...";
            this.exportButton.UseVisualStyleBackColor = true;
            // 
            // importButton
            // 
            this.importButton.Location = new System.Drawing.Point(151, 1);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(75, 23);
            this.importButton.TabIndex = 2;
            this.importButton.Text = "Import...";
            this.importButton.UseVisualStyleBackColor = true;
            // 
            // addRuleButton
            // 
            this.addRuleButton.Location = new System.Drawing.Point(1, 1);
            this.addRuleButton.Name = "addRuleButton";
            this.addRuleButton.Size = new System.Drawing.Size(75, 23);
            this.addRuleButton.TabIndex = 0;
            this.addRuleButton.Text = "Add Rule";
            this.addRuleButton.UseVisualStyleBackColor = true;
            this.addRuleButton.Click += new System.EventHandler(this.addRuleButton_Click);
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
            this.PostfixHeader,
            this.ChildrenHeader});
            this.rulesView.Location = new System.Drawing.Point(0, 25);
            this.rulesView.Name = "rulesView";
            this.rulesView.Size = new System.Drawing.Size(533, 520);
            this.rulesView.TabIndex = 1;
            this.rulesView.UseCompatibleStateImageBehavior = false;
            this.rulesView.View = System.Windows.Forms.View.Details;
            // 
            // MatchesHeader
            // 
            this.MatchesHeader.Text = "Matches";
            this.MatchesHeader.Width = 80;
            // 
            // PrefixHeader
            // 
            this.PrefixHeader.Text = "Constant Value / Prefix";
            this.PrefixHeader.Width = 150;
            // 
            // InfixHeader
            // 
            this.InfixHeader.Text = "Infix";
            // 
            // PostfixHeader
            // 
            this.PostfixHeader.Text = "Postfix";
            // 
            // ChildrenHeader
            // 
            this.ChildrenHeader.Text = "Show Children";
            this.ChildrenHeader.Width = 82;
            // 
            // RewriteRuleViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(533, 545);
            this.Controls.Add(this.rulesView);
            this.Controls.Add(this.toolBar);
            this.Name = "RewriteRuleViewer";
            this.Text = "Rewrite Rule Viewer";
            this.toolBar.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel toolBar;
        private System.Windows.Forms.ListView rulesView;
        private System.Windows.Forms.ColumnHeader MatchesHeader;
        private System.Windows.Forms.ColumnHeader PrefixHeader;
        private System.Windows.Forms.ColumnHeader InfixHeader;
        private System.Windows.Forms.ColumnHeader PostfixHeader;
        private System.Windows.Forms.ColumnHeader ChildrenHeader;
        private System.Windows.Forms.Button addRuleButton;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button deleteRuleButton;
    }
}