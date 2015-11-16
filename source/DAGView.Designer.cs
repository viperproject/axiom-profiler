namespace Z3AxiomProfiler
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
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.maxRenderDepth);
            this.panel1.Location = new System.Drawing.Point(0, -1);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(616, 27);
            this.panel1.TabIndex = 1;
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
            this.maxRenderDepth.Name = "maxRenderDepth";
            this.maxRenderDepth.Size = new System.Drawing.Size(48, 20);
            this.maxRenderDepth.TabIndex = 0;
            this.maxRenderDepth.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.maxRenderDepth.ValueChanged += new System.EventHandler(this.numericUpDown1_ValueChanged);
            // 
            // DAGView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(616, 525);
            this.Controls.Add(this.panel1);
            this.Name = "DAGView";
            this.Text = "DAGView";
            this.Load += new System.EventHandler(this.DAGView_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxRenderDepth)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.NumericUpDown maxRenderDepth;
        private System.Windows.Forms.Label label1;
    }
}