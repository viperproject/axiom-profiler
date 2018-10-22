//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace AxiomProfiler
{
  public partial class LoadingProgressForm : Form
  {
    private readonly Loader loader;
    
    public LoadingProgressForm(Loader loader)
    {
      this.loader = loader;
      InitializeComponent();
    }

    private void LoadingProgressForm_Load(object sender, EventArgs e)
    {
      backgroundWorker1.RunWorkerAsync();
    }

    private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
    {
      loader.statusUpdate += loaderProgressChanged;
      loader.Load();
    }

    private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      Close();
    }

    private void button1_Click(object sender, EventArgs e)
    {
      backgroundWorker1.CancelAsync();
      loader.Cancel();
    }

    private void loaderProgressChanged(int perc)
    {
      backgroundWorker1.ReportProgress(perc);
    }

    private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
      if (e.ProgressPercentage != 0) {
        progressBar1.Style = ProgressBarStyle.Blocks;
        int perc = e.ProgressPercentage;
        if (perc <= progressBar1.Maximum)
          progressBar1.Value = perc;
      }
    }
  }
}
