namespace Z3AxiomProfiler
{
    partial class InstantiationFilter
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
            this.maxDepthLabel = new System.Windows.Forms.Label();
            this.maxDepthUpDown = new System.Windows.Forms.NumericUpDown();
            this.quantSelectionBox = new System.Windows.Forms.CheckedListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.NumNodesDescLabel = new System.Windows.Forms.Label();
            this.numberNodesMatchingLabel = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.maxNewNodesUpDown = new System.Windows.Forms.NumericUpDown();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.maxDepthUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxNewNodesUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // maxDepthLabel
            // 
            this.maxDepthLabel.AutoSize = true;
            this.maxDepthLabel.Location = new System.Drawing.Point(10, 9);
            this.maxDepthLabel.Name = "maxDepthLabel";
            this.maxDepthLabel.Size = new System.Drawing.Size(62, 13);
            this.maxDepthLabel.TabIndex = 0;
            this.maxDepthLabel.Text = "Max Depth:";
            // 
            // maxDepthUpDown
            // 
            this.maxDepthUpDown.Location = new System.Drawing.Point(80, 7);
            this.maxDepthUpDown.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.maxDepthUpDown.Name = "maxDepthUpDown";
            this.maxDepthUpDown.Size = new System.Drawing.Size(58, 20);
            this.maxDepthUpDown.TabIndex = 1;
            this.maxDepthUpDown.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.maxDepthUpDown.ValueChanged += new System.EventHandler(this.updateFilter);
            // 
            // quantSelectionBox
            // 
            this.quantSelectionBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.quantSelectionBox.CheckOnClick = true;
            this.quantSelectionBox.FormattingEnabled = true;
            this.quantSelectionBox.Location = new System.Drawing.Point(15, 61);
            this.quantSelectionBox.Name = "quantSelectionBox";
            this.quantSelectionBox.Size = new System.Drawing.Size(368, 124);
            this.quantSelectionBox.TabIndex = 2;
            this.quantSelectionBox.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.quantSelectionBox_ItemCheck);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 39);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Quantifiers:";
            // 
            // NumNodesDescLabel
            // 
            this.NumNodesDescLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.NumNodesDescLabel.AutoSize = true;
            this.NumNodesDescLabel.Location = new System.Drawing.Point(12, 199);
            this.NumNodesDescLabel.Name = "NumNodesDescLabel";
            this.NumNodesDescLabel.Size = new System.Drawing.Size(114, 13);
            this.NumNodesDescLabel.TabIndex = 4;
            this.NumNodesDescLabel.Text = "Number of new nodes:";
            // 
            // numberNodesMatchingLabel
            // 
            this.numberNodesMatchingLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.numberNodesMatchingLabel.AutoSize = true;
            this.numberNodesMatchingLabel.Location = new System.Drawing.Point(132, 199);
            this.numberNodesMatchingLabel.Name = "numberNodesMatchingLabel";
            this.numberNodesMatchingLabel.Size = new System.Drawing.Size(67, 13);
            this.numberNodesMatchingLabel.TabIndex = 5;
            this.numberNodesMatchingLabel.Text = "calculating...";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(146, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(159, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Maximum number of new nodes:";
            // 
            // maxNewNodesUpDown
            // 
            this.maxNewNodesUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.maxNewNodesUpDown.Location = new System.Drawing.Point(311, 7);
            this.maxNewNodesUpDown.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.maxNewNodesUpDown.Name = "maxNewNodesUpDown";
            this.maxNewNodesUpDown.Size = new System.Drawing.Size(71, 20);
            this.maxNewNodesUpDown.TabIndex = 7;
            this.maxNewNodesUpDown.Value = new decimal(new int[] {
            20,
            0,
            0,
            0});
            this.maxNewNodesUpDown.ValueChanged += new System.EventHandler(this.updateFilter);
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.okButton.Location = new System.Drawing.Point(123, 226);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 8;
            this.okButton.Text = "Ok";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cancelButton.Location = new System.Drawing.Point(204, 226);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 9;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // InstantiationFilter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(399, 261);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.maxNewNodesUpDown);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.numberNodesMatchingLabel);
            this.Controls.Add(this.NumNodesDescLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.quantSelectionBox);
            this.Controls.Add(this.maxDepthUpDown);
            this.Controls.Add(this.maxDepthLabel);
            this.MinimumSize = new System.Drawing.Size(415, 300);
            this.Name = "InstantiationFilter";
            this.Text = "Instantiation Filter";
            ((System.ComponentModel.ISupportInitialize)(this.maxDepthUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maxNewNodesUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label maxDepthLabel;
        private System.Windows.Forms.NumericUpDown maxDepthUpDown;
        private System.Windows.Forms.CheckedListBox quantSelectionBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label NumNodesDescLabel;
        private System.Windows.Forms.Label numberNodesMatchingLabel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown maxNewNodesUpDown;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
    }
}