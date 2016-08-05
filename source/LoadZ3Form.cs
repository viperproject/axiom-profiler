//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.IO;
using System.Windows.Forms;

namespace AxiomProfiler
{
    public partial class LoadZ3Form : Form
    {
        public LoadZ3Form()
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
            string allZ3Options = config.z3Options;
            z3FilePath.Text = config.z3InputFile ?? "";
            z3Timeout.Text = config.timeout.ToString();
            if (allZ3Options.Contains("PROOF=true"))
            {
                rb_proofLogging.Checked = true;
                allZ3Options = allZ3Options.Replace("  ", " ");
                allZ3Options = allZ3Options.Trim();
            }
            else
            {
                rb_proofLogging.Checked = false;
            }
            z3Options.Text = allZ3Options;
        }

        public ParameterConfiguration GetParameterConfiguration()
        {
            ParameterConfiguration config = ParameterConfiguration.loadParameterConfigurationFromSettings();
            string allZ3Options = z3Options.Text;
            config.z3InputFile = z3FilePath.Text;
            config.z3Options = allZ3Options;
            Int32.TryParse(z3Timeout.Text, out config.timeout);
            return config;
        }

        private string loadZ3File(string filename)
        {

            FileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Open Z3 file";
            if (File.Exists(filename))
            {
                openFileDialog.FileName = filename;
            }
            openFileDialog.Filter = "Z3 files (*.z3;*.smt;*.smt2;*.sx;*.smp;*.simplif;*.dimacs) |*.z3;*.smt;*.smt2;*.sx;*.smp;*.simplif;*.dimacs| " +
                                    "Z3 native files (*.z3) |*.z3| " +
                                    "SMT-LIB files (*.smt;*.smt2) |*.smt;*.smt2| " +
                                    "Simplify files (*.sx;*.smp;*.simplify) |*.sx;*.smp;*.simplify| " +
                                    "DIMACS files (*.dimacs) |*.dimacs| " +
                                    "All files (*.*) |*.*";
            if ((openFileDialog.ShowDialog() == DialogResult.OK) && File.Exists(openFileDialog.FileName))
            {
                return openFileDialog.FileName;
            }
            return filename;
        }

        private void buttonOpenZ3_Click(object sender, EventArgs e)
        {
            z3FilePath.Text = loadZ3File(z3FilePath.Text);
        }
    }
}
