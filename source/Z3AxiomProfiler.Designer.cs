namespace Z3AxiomProfiler
{
    partial class Z3AxiomProfiler
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Z3AxiomProfiler));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.toolTipBox = new System.Windows.Forms.RichTextBox();
            this.z3AxiomTree = new System.Windows.Forms.TreeView();
            this.InstantiationPathView = new System.Windows.Forms.ListView();
            this.DepthHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.PrintNameHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.QIdHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.InstancesCountHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
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
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.termWidthTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.typeToggleButton = new System.Windows.Forms.ToolStripButton();
            this.termIdToggle = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.InstantiationPathView);
            this.splitContainer1.Size = new System.Drawing.Size(1184, 687);
            this.splitContainer1.SplitterDistance = 894;
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
            this.splitContainer2.Size = new System.Drawing.Size(894, 687);
            this.splitContainer2.SplitterDistance = 281;
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
            this.toolTipBox.Size = new System.Drawing.Size(281, 687);
            this.toolTipBox.TabIndex = 0;
            this.toolTipBox.Text = "";
            this.toolTipBox.WordWrap = false;
            // 
            // z3AxiomTree
            // 
            this.z3AxiomTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.z3AxiomTree.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.z3AxiomTree.HideSelection = false;
            this.z3AxiomTree.Location = new System.Drawing.Point(0, 0);
            this.z3AxiomTree.Name = "z3AxiomTree";
            this.z3AxiomTree.Size = new System.Drawing.Size(609, 687);
            this.z3AxiomTree.TabIndex = 1;
            this.z3AxiomTree.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.HandleExpand);
            this.z3AxiomTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.HandleTreeNodeSelect);
            this.z3AxiomTree.Enter += new System.EventHandler(this.z3AxiomTree_Enter);
            this.z3AxiomTree.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.z3AxiomTree_KeyPress);
            // 
            // InstantiationPathView
            // 
            this.InstantiationPathView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.DepthHeader,
            this.PrintNameHeader,
            this.QIdHeader,
            this.InstancesCountHeader});
            this.InstantiationPathView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.InstantiationPathView.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.InstantiationPathView.FullRowSelect = true;
            this.InstantiationPathView.Location = new System.Drawing.Point(0, 0);
            this.InstantiationPathView.MultiSelect = false;
            this.InstantiationPathView.Name = "InstantiationPathView";
            this.InstantiationPathView.Size = new System.Drawing.Size(286, 687);
            this.InstantiationPathView.TabIndex = 2;
            this.InstantiationPathView.UseCompatibleStateImageBehavior = false;
            this.InstantiationPathView.View = System.Windows.Forms.View.Details;
            this.InstantiationPathView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.PathItemClick);
            this.InstantiationPathView.Enter += new System.EventHandler(this.InstantiationPathView_Enter);
            // 
            // DepthHeader
            // 
            this.DepthHeader.Text = "Depth";
            this.DepthHeader.Width = 50;
            // 
            // PrintNameHeader
            // 
            this.PrintNameHeader.Text = "Print Name";
            this.PrintNameHeader.Width = 130;
            // 
            // QIdHeader
            // 
            this.QIdHeader.Text = "QId";
            this.QIdHeader.Width = 200;
            // 
            // InstancesCountHeader
            // 
            this.InstancesCountHeader.Text = "#Instances Total";
            this.InstancesCountHeader.Width = 150;
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
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.termWidthTextBox,
            this.toolStripSeparator3,
            this.typeToggleButton,
            this.termIdToggle});
            this.toolStrip1.Location = new System.Drawing.Point(135, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip1.Size = new System.Drawing.Size(364, 25);
            this.toolStrip1.TabIndex = 6;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(97, 22);
            this.toolStripLabel1.Text = "Max Term Width:";
            // 
            // termWidthTextBox
            // 
            this.termWidthTextBox.Name = "termWidthTextBox";
            this.termWidthTextBox.Size = new System.Drawing.Size(40, 25);
            this.termWidthTextBox.Text = "80";
            this.termWidthTextBox.TextBoxTextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.termWidthTextBox.Leave += new System.EventHandler(this.termWidthTextBoxTriggerReprint);
            this.termWidthTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.termWidthKeyPressHandler);
            this.termWidthTextBox.Validated += new System.EventHandler(this.termWidthTextBoxTriggerReprint);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // typeToggleButton
            // 
            this.typeToggleButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.typeToggleButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.typeToggleButton.Name = "typeToggleButton";
            this.typeToggleButton.Size = new System.Drawing.Size(69, 22);
            this.typeToggleButton.Text = "Hide Types";
            this.typeToggleButton.Click += new System.EventHandler(this.toolStripButton1_Click);
            // 
            // termIdToggle
            // 
            this.termIdToggle.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.termIdToggle.Image = ((System.Drawing.Image)(resources.GetObject("termIdToggle.Image")));
            this.termIdToggle.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.termIdToggle.Name = "termIdToggle";
            this.termIdToggle.Size = new System.Drawing.Size(116, 22);
            this.termIdToggle.Text = "Hide Term Identifier";
            this.termIdToggle.Click += new System.EventHandler(this.termIdToggle_Click);
            // 
            // Z3AxiomProfiler
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 711);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Name = "Z3AxiomProfiler";
            this.Text = "Z3 Axiom Profiler";
            this.Load += new System.EventHandler(this.Z3AxiomProfiler_OnLoadEvent);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.RichTextBox toolTipBox;
        private System.Windows.Forms.ListView InstantiationPathView;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.ColumnHeader PrintNameHeader;
        private System.Windows.Forms.ColumnHeader DepthHeader;
        private System.Windows.Forms.ColumnHeader InstancesCountHeader;
        private System.Windows.Forms.ColumnHeader QIdHeader;
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
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox termWidthTextBox;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton typeToggleButton;
        private System.Windows.Forms.ToolStripButton termIdToggle;
    }
}

