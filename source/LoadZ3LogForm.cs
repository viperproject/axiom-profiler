//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.IO;
using System.Windows.Forms;

namespace Z3AxiomProfiler
{
    public partial class LoadZ3LogForm : Form
    {
        public LoadZ3LogForm()
        {
            InitializeComponent();
            buttonLoad.Focus();
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }

        public void reloadParameterConfiguration()
        {
            ParameterConfiguration config = ParameterConfiguration.loadParameterConfigurationFromSettings();
            setParameterConfiguration(config);
        }

        public void setParameterConfiguration(ParameterConfiguration config)
        {
            logFilePath.Text = (config.z3LogFile == null) ? "" : config.z3LogFile;
        }

        public ParameterConfiguration GetParameterConfiguration()
        {
            ParameterConfiguration config = ParameterConfiguration.loadParameterConfigurationFromSettings();
            config.z3LogFile = logFilePath.Text;
            return config;
        }

        private string loadZ3LogFile(string filename)
        {
            FileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Open Z3 Output File";
            if (File.Exists(filename))
            {
                openFileDialog.FileName = filename;
            }
            openFileDialog.Filter = "Z3 logfiles (*.z3log;*.log) |*.z3log;*.log| Z3 result model (*.model;*.vccmodel) |*.model;*.vccmodel| All files (*.*) |*.*";
            if ((openFileDialog.ShowDialog() == DialogResult.OK) && File.Exists(openFileDialog.FileName))
            {
                return openFileDialog.FileName;
            }
            return filename;
        }

        private void buttonOpenZ3Log_Click(object sender, EventArgs e)
        {
            logFilePath.Text = loadZ3LogFile(logFilePath.Text);
        }

        private void LoadZ3LogForm_Shown(object sender, EventArgs e)
        {
            buttonLoad.Focus();
        }

    }
}
