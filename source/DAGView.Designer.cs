namespace AxiomProfiler
{
    partial class DAGView
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.subgraphExplanationButton = new System.Windows.Forms.Button();
            this.sourceTreeButton = new System.Windows.Forms.Button();
            this.redrawCompleteGraphButton = new System.Windows.Forms.Button();
            this.showChainButton = new System.Windows.Forms.Button();
            this.showChildrenButton = new System.Windows.Forms.Button();
            this.showParentsButton = new System.Windows.Forms.Button();
            this.hideInstantiationButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.maxRenderDepth = new System.Windows.Forms.NumericUpDown();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxRenderDepth)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.subgraphExplanationButton);
            this.panel1.Controls.Add(this.sourceTreeButton);
            this.panel1.Controls.Add(this.redrawCompleteGraphButton);
            this.panel1.Controls.Add(this.showChainButton);
            this.panel1.Controls.Add(this.showChildrenButton);
            this.panel1.Controls.Add(this.showParentsButton);
            this.panel1.Controls.Add(this.hideInstantiationButton);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.maxRenderDepth);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(538, 60);
            this.panel1.TabIndex = 1;
            // 
            // pathExplanationButton
            // 
            this.subgraphExplanationButton.Location = new System.Drawing.Point(295, 32);
            this.subgraphExplanationButton.Name = "pathExplanationButton";
            this.subgraphExplanationButton.Size = new System.Drawing.Size(104, 23);
            this.subgraphExplanationButton.TabIndex = 7;
            this.subgraphExplanationButton.Text = "Explain Subgraph";
            this.subgraphExplanationButton.UseVisualStyleBackColor = true;
            this.subgraphExplanationButton.Click += new System.EventHandler(this.pathExplanationButton_Click);
            // 
            // sourceTreeButton
            // 
            this.sourceTreeButton.Location = new System.Drawing.Point(95, 31);
            this.sourceTreeButton.Name = "sourceTreeButton";
            this.sourceTreeButton.Size = new System.Drawing.Size(105, 23);
            this.sourceTreeButton.TabIndex = 6;
            this.sourceTreeButton.Text = "Show Source Tree";
            this.sourceTreeButton.UseVisualStyleBackColor = true;
            this.sourceTreeButton.Click += new System.EventHandler(this.sourceTreeButton_Click);
            // 
            // redrawCompleteGraphButton
            // 
            this.redrawCompleteGraphButton.Location = new System.Drawing.Point(102, 2);
            this.redrawCompleteGraphButton.Name = "redrawCompleteGraphButton";
            this.redrawCompleteGraphButton.Size = new System.Drawing.Size(85, 23);
            this.redrawCompleteGraphButton.TabIndex = 1;
            this.redrawCompleteGraphButton.Text = "Redraw Graph";
            this.redrawCompleteGraphButton.UseVisualStyleBackColor = true;
            this.redrawCompleteGraphButton.Click += new System.EventHandler(this.redrawGraph_Click);
            // 
            // showChainButton
            // 
            this.showChainButton.Location = new System.Drawing.Point(274, 3);
            this.showChainButton.Name = "showChainButton";
            this.showChainButton.Size = new System.Drawing.Size(125, 23);
            this.showChainButton.TabIndex = 5;
            this.showChainButton.Text = "Show a Longest Path";
            this.showChainButton.UseVisualStyleBackColor = true;
            this.showChainButton.Click += new System.EventHandler(this.showChainButton_Click);
            // 
            // showChildrenButton
            // 
            this.showChildrenButton.Location = new System.Drawing.Point(206, 32);
            this.showChildrenButton.Name = "showChildrenButton";
            this.showChildrenButton.Size = new System.Drawing.Size(83, 23);
            this.showChildrenButton.TabIndex = 4;
            this.showChildrenButton.Text = "Show Children";
            this.showChildrenButton.UseVisualStyleBackColor = true;
            this.showChildrenButton.Click += new System.EventHandler(this.showChildrenButton_Click);
            // 
            // showParentsButton
            // 
            this.showParentsButton.Location = new System.Drawing.Point(6, 31);
            this.showParentsButton.Name = "showParentsButton";
            this.showParentsButton.Size = new System.Drawing.Size(83, 23);
            this.showParentsButton.TabIndex = 3;
            this.showParentsButton.Text = "Show Parents";
            this.showParentsButton.UseVisualStyleBackColor = true;
            this.showParentsButton.Click += new System.EventHandler(this.showParentsButton_Click);
            // 
            // hideInstantiationButton
            // 
            this.hideInstantiationButton.Location = new System.Drawing.Point(193, 2);
            this.hideInstantiationButton.Name = "hideInstantiationButton";
            this.hideInstantiationButton.Size = new System.Drawing.Size(75, 23);
            this.hideInstantiationButton.TabIndex = 2;
            this.hideInstantiationButton.Text = "Hide Node";
            this.hideInstantiationButton.UseVisualStyleBackColor = true;
            this.hideInstantiationButton.Click += new System.EventHandler(this.hideInstantiationButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(39, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Depth:";
            // 
            // maxRenderDepth
            // 
            this.maxRenderDepth.Location = new System.Drawing.Point(48, 3);
            this.maxRenderDepth.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.maxRenderDepth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.maxRenderDepth.Name = "maxRenderDepth";
            this.maxRenderDepth.Size = new System.Drawing.Size(48, 20);
            this.maxRenderDepth.TabIndex = 0;
            this.maxRenderDepth.Value = new decimal(new int[] {
            20,
            0,
            0,
            0});
            // 
            // DAGView
            // 
            this.Controls.Add(this.panel1);
            this.Name = "DAGView";
            this.Size = new System.Drawing.Size(538, 485);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxRenderDepth)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.NumericUpDown maxRenderDepth;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button hideInstantiationButton;
        private System.Windows.Forms.Button showParentsButton;
        private System.Windows.Forms.Button showChildrenButton;
        private System.Windows.Forms.Button showChainButton;
        private System.Windows.Forms.Button redrawCompleteGraphButton;
        private System.Windows.Forms.Button sourceTreeButton;
        private System.Windows.Forms.Button subgraphExplanationButton;
    }
}