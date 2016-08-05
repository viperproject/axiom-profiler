namespace AxiomProfiler
{
    partial class AxiomProfiler
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.toolTipBox = new System.Windows.Forms.RichTextBox();
            this.z3AxiomTree = new System.Windows.Forms.TreeView();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadZ3TraceLogToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.profileZ3TraceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadZ3FromBoogieToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.colorVisualizationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.searchTreeVisualizationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quantifierBlameVisualizationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cSVToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.allConflictsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.randomConflictsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolsPanel = new System.Windows.Forms.Panel();
            this.rewritingRulesButton = new System.Windows.Forms.Button();
            this.enableRewritingCB = new System.Windows.Forms.CheckBox();
            this.showTermIdCB = new System.Windows.Forms.CheckBox();
            this.showTypesCB = new System.Windows.Forms.CheckBox();
            this.maxTermDepthUD = new System.Windows.Forms.NumericUpDown();
            this.maxTermDepthLabel = new System.Windows.Forms.Label();
            this.maxTermWidthUD = new System.Windows.Forms.NumericUpDown();
            this.widthLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.toolsPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxTermDepthUD)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxTermWidthUD)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 56);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            this.splitContainer1.Panel2MinSize = 400;
            this.splitContainer1.Size = new System.Drawing.Size(1184, 655);
            this.splitContainer1.SplitterDistance = 779;
            this.splitContainer1.TabIndex = 3;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.toolTipBox);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.z3AxiomTree);
            this.splitContainer2.Size = new System.Drawing.Size(779, 655);
            this.splitContainer2.SplitterDistance = 359;
            this.splitContainer2.TabIndex = 3;
            // 
            // toolTipBox
            // 
            this.toolTipBox.AcceptsTab = true;
            this.toolTipBox.CausesValidation = false;
            this.toolTipBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolTipBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.toolTipBox.Location = new System.Drawing.Point(0, 0);
            this.toolTipBox.Name = "toolTipBox";
            this.toolTipBox.ReadOnly = true;
            this.toolTipBox.Size = new System.Drawing.Size(359, 655);
            this.toolTipBox.TabIndex = 0;
            this.toolTipBox.Text = "";
            this.toolTipBox.WordWrap = false;
            // 
            // z3AxiomTree
            // 
            this.z3AxiomTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.z3AxiomTree.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.z3AxiomTree.Location = new System.Drawing.Point(0, 0);
            this.z3AxiomTree.Name = "z3AxiomTree";
            this.z3AxiomTree.Size = new System.Drawing.Size(416, 655);
            this.z3AxiomTree.TabIndex = 1;
            this.z3AxiomTree.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.HandleExpand);
            this.z3AxiomTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.HandleTreeNodeSelect);
            this.z3AxiomTree.Enter += new System.EventHandler(this.z3AxiomTree_Enter);
            this.z3AxiomTree.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.z3AxiomTree_KeyPress);
            this.z3AxiomTree.Leave += new System.EventHandler(this.z3AxiomTree_Leave);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadZ3TraceLogToolStripMenuItem,
            this.profileZ3TraceToolStripMenuItem,
            this.loadZ3FromBoogieToolStripMenuItem,
            this.searchToolStripMenuItem,
            this.toolStripSeparator2,
            this.colorVisualizationToolStripMenuItem,
            this.searchTreeVisualizationToolStripMenuItem,
            this.quantifierBlameVisualizationToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // loadZ3TraceLogToolStripMenuItem
            // 
            this.loadZ3TraceLogToolStripMenuItem.Name = "loadZ3TraceLogToolStripMenuItem";
            this.loadZ3TraceLogToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.loadZ3TraceLogToolStripMenuItem.Text = "&Load Z3 Output File";
            this.loadZ3TraceLogToolStripMenuItem.Click += new System.EventHandler(this.LoadZ3Logfile_Click);
            // 
            // profileZ3TraceToolStripMenuItem
            // 
            this.profileZ3TraceToolStripMenuItem.Name = "profileZ3TraceToolStripMenuItem";
            this.profileZ3TraceToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.profileZ3TraceToolStripMenuItem.Text = "Profile Z3 Trace";
            this.profileZ3TraceToolStripMenuItem.Click += new System.EventHandler(this.LoadZ3_Click);
            // 
            // loadZ3FromBoogieToolStripMenuItem
            // 
            this.loadZ3FromBoogieToolStripMenuItem.Name = "loadZ3FromBoogieToolStripMenuItem";
            this.loadZ3FromBoogieToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.loadZ3FromBoogieToolStripMenuItem.Text = "Profile Z3 Trace for &Boogie Execution";
            this.loadZ3FromBoogieToolStripMenuItem.Click += new System.EventHandler(this.LoadBoogie_Click);
            // 
            // searchToolStripMenuItem
            // 
            this.searchToolStripMenuItem.Name = "searchToolStripMenuItem";
            this.searchToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.searchToolStripMenuItem.Text = "&Search";
            this.searchToolStripMenuItem.Click += new System.EventHandler(this.searchToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(264, 6);
            // 
            // colorVisualizationToolStripMenuItem
            // 
            this.colorVisualizationToolStripMenuItem.Name = "colorVisualizationToolStripMenuItem";
            this.colorVisualizationToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.colorVisualizationToolStripMenuItem.Text = "&Color Visualization";
            this.colorVisualizationToolStripMenuItem.Click += new System.EventHandler(this.colorVisualizationToolStripMenuItem_Click);
            // 
            // searchTreeVisualizationToolStripMenuItem
            // 
            this.searchTreeVisualizationToolStripMenuItem.Name = "searchTreeVisualizationToolStripMenuItem";
            this.searchTreeVisualizationToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.searchTreeVisualizationToolStripMenuItem.Text = "Search &Tree Visualization";
            this.searchTreeVisualizationToolStripMenuItem.Click += new System.EventHandler(this.searchTreeVisualizationToolStripMenuItem_Click);
            // 
            // quantifierBlameVisualizationToolStripMenuItem
            // 
            this.quantifierBlameVisualizationToolStripMenuItem.Name = "quantifierBlameVisualizationToolStripMenuItem";
            this.quantifierBlameVisualizationToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.quantifierBlameVisualizationToolStripMenuItem.Text = "Quantifier Blame Visualization";
            this.quantifierBlameVisualizationToolStripMenuItem.Click += new System.EventHandler(this.quantifierBlameVisualizationToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(264, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(267, 22);
            this.exitToolStripMenuItem.Text = "&Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.Exit_Click);
            // 
            // cSVToolStripMenuItem1
            // 
            this.cSVToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allConflictsToolStripMenuItem,
            this.randomConflictsToolStripMenuItem1});
            this.cSVToolStripMenuItem1.Name = "cSVToolStripMenuItem1";
            this.cSVToolStripMenuItem1.Size = new System.Drawing.Size(40, 20);
            this.cSVToolStripMenuItem1.Text = "CS&V";
            // 
            // allConflictsToolStripMenuItem
            // 
            this.allConflictsToolStripMenuItem.Name = "allConflictsToolStripMenuItem";
            this.allConflictsToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.allConflictsToolStripMenuItem.Text = "&All conflicts";
            this.allConflictsToolStripMenuItem.Click += new System.EventHandler(this.allConflictsToolStripMenuItem_Click);
            // 
            // randomConflictsToolStripMenuItem1
            // 
            this.randomConflictsToolStripMenuItem1.Name = "randomConflictsToolStripMenuItem1";
            this.randomConflictsToolStripMenuItem1.Size = new System.Drawing.Size(191, 22);
            this.randomConflictsToolStripMenuItem1.Text = "1000 random conflicts";
            this.randomConflictsToolStripMenuItem1.Click += new System.EventHandler(this.randomConflictsToolStripMenuItem1_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.helpToolStripMenuItem1});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "&Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.aboutToolStripMenuItem.Text = "&About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem1
            // 
            this.helpToolStripMenuItem1.Name = "helpToolStripMenuItem1";
            this.helpToolStripMenuItem1.Size = new System.Drawing.Size(107, 22);
            this.helpToolStripMenuItem1.Text = "&Help";
            this.helpToolStripMenuItem1.Click += new System.EventHandler(this.helpToolStripMenuItem1_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.cSVToolStripMenuItem1,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1184, 24);
            this.menuStrip1.TabIndex = 2;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolsPanel
            // 
            this.toolsPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toolsPanel.Controls.Add(this.rewritingRulesButton);
            this.toolsPanel.Controls.Add(this.enableRewritingCB);
            this.toolsPanel.Controls.Add(this.showTermIdCB);
            this.toolsPanel.Controls.Add(this.showTypesCB);
            this.toolsPanel.Controls.Add(this.maxTermDepthUD);
            this.toolsPanel.Controls.Add(this.maxTermDepthLabel);
            this.toolsPanel.Controls.Add(this.maxTermWidthUD);
            this.toolsPanel.Controls.Add(this.widthLabel);
            this.toolsPanel.Location = new System.Drawing.Point(0, 27);
            this.toolsPanel.Name = "toolsPanel";
            this.toolsPanel.Size = new System.Drawing.Size(1184, 25);
            this.toolsPanel.TabIndex = 7;
            // 
            // rewritingRulesButton
            // 
            this.rewritingRulesButton.Location = new System.Drawing.Point(679, 1);
            this.rewritingRulesButton.Name = "rewritingRulesButton";
            this.rewritingRulesButton.Size = new System.Drawing.Size(100, 23);
            this.rewritingRulesButton.TabIndex = 7;
            this.rewritingRulesButton.Text = "Printing Rules";
            this.rewritingRulesButton.UseVisualStyleBackColor = true;
            this.rewritingRulesButton.Click += new System.EventHandler(this.rewritingRulesButton_Click);
            // 
            // enableRewritingCB
            // 
            this.enableRewritingCB.AutoSize = true;
            this.enableRewritingCB.Checked = true;
            this.enableRewritingCB.CheckState = System.Windows.Forms.CheckState.Checked;
            this.enableRewritingCB.Location = new System.Drawing.Point(538, 4);
            this.enableRewritingCB.Name = "enableRewritingCB";
            this.enableRewritingCB.Size = new System.Drawing.Size(135, 17);
            this.enableRewritingCB.TabIndex = 6;
            this.enableRewritingCB.Text = "Enable Custom Printing";
            this.enableRewritingCB.UseVisualStyleBackColor = true;
            this.enableRewritingCB.CheckedChanged += new System.EventHandler(this.enableRewritingCB_CheckedChanged);
            // 
            // showTermIdCB
            // 
            this.showTermIdCB.AutoSize = true;
            this.showTermIdCB.Checked = true;
            this.showTermIdCB.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showTermIdCB.Location = new System.Drawing.Point(404, 4);
            this.showTermIdCB.Name = "showTermIdCB";
            this.showTermIdCB.Size = new System.Drawing.Size(128, 17);
            this.showTermIdCB.TabIndex = 5;
            this.showTermIdCB.Text = "Show Term Identifiers";
            this.showTermIdCB.UseVisualStyleBackColor = true;
            this.showTermIdCB.CheckedChanged += new System.EventHandler(this.showTermIdCB_CheckedChanged);
            // 
            // showTypesCB
            // 
            this.showTypesCB.AutoSize = true;
            this.showTypesCB.Checked = true;
            this.showTypesCB.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showTypesCB.Location = new System.Drawing.Point(313, 4);
            this.showTypesCB.Name = "showTypesCB";
            this.showTypesCB.Size = new System.Drawing.Size(85, 17);
            this.showTypesCB.TabIndex = 4;
            this.showTypesCB.Text = "Show Types";
            this.showTypesCB.UseVisualStyleBackColor = true;
            this.showTypesCB.CheckedChanged += new System.EventHandler(this.showTypesCB_CheckedChanged);
            // 
            // maxTermDepthUD
            // 
            this.maxTermDepthUD.Location = new System.Drawing.Point(257, 3);
            this.maxTermDepthUD.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.maxTermDepthUD.Name = "maxTermDepthUD";
            this.maxTermDepthUD.Size = new System.Drawing.Size(50, 20);
            this.maxTermDepthUD.TabIndex = 3;
            this.maxTermDepthUD.Value = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.maxTermDepthUD.ValueChanged += new System.EventHandler(this.maxTermDepthUD_ValueChanged);
            // 
            // maxTermDepthLabel
            // 
            this.maxTermDepthLabel.AutoSize = true;
            this.maxTermDepthLabel.Location = new System.Drawing.Point(162, 6);
            this.maxTermDepthLabel.Name = "maxTermDepthLabel";
            this.maxTermDepthLabel.Size = new System.Drawing.Size(89, 13);
            this.maxTermDepthLabel.TabIndex = 2;
            this.maxTermDepthLabel.Text = "Max Term Depth:";
            // 
            // maxTermWidthUD
            // 
            this.maxTermWidthUD.Location = new System.Drawing.Point(106, 3);
            this.maxTermWidthUD.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.maxTermWidthUD.Name = "maxTermWidthUD";
            this.maxTermWidthUD.Size = new System.Drawing.Size(50, 20);
            this.maxTermWidthUD.TabIndex = 1;
            this.maxTermWidthUD.Value = new decimal(new int[] {
            80,
            0,
            0,
            0});
            this.maxTermWidthUD.ValueChanged += new System.EventHandler(this.maxTermWidthUD_ValueChanged);
            // 
            // widthLabel
            // 
            this.widthLabel.AutoSize = true;
            this.widthLabel.Location = new System.Drawing.Point(12, 6);
            this.widthLabel.Name = "widthLabel";
            this.widthLabel.Size = new System.Drawing.Size(88, 13);
            this.widthLabel.TabIndex = 0;
            this.widthLabel.Text = "Max Term Width:";
            // 
            // AxiomProfiler
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 711);
            this.Controls.Add(this.toolsPanel);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Name = "AxiomProfiler";
            this.Text = "Axiom Profiler";
            this.Load += new System.EventHandler(this.AxiomProfiler_OnLoadEvent);
            this.splitContainer1.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolsPanel.ResumeLayout(false);
            this.toolsPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxTermDepthUD)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxTermWidthUD)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.RichTextBox toolTipBox;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.TreeView z3AxiomTree;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadZ3TraceLogToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem profileZ3TraceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadZ3FromBoogieToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem colorVisualizationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem searchTreeVisualizationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem quantifierBlameVisualizationToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cSVToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem allConflictsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem randomConflictsToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.Panel toolsPanel;
        private System.Windows.Forms.NumericUpDown maxTermWidthUD;
        private System.Windows.Forms.Label widthLabel;
        private System.Windows.Forms.CheckBox showTermIdCB;
        private System.Windows.Forms.CheckBox showTypesCB;
        private System.Windows.Forms.NumericUpDown maxTermDepthUD;
        private System.Windows.Forms.Label maxTermDepthLabel;
        private System.Windows.Forms.CheckBox enableRewritingCB;
        private System.Windows.Forms.Button rewritingRulesButton;
    }
}

