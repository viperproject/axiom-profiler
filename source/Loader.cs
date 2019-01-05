//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AxiomProfiler.QuantifierModel;

namespace AxiomProfiler
{

    public class ParameterConfiguration
    {
        public string z3LogFile = "";
        public bool skipDecisions = true;
        public int checkToConsider = 0; // 0 is a special value, meaning "process all checks"


        public static ParameterConfiguration loadParameterConfigurationFromSettings()
        {
            ParameterConfiguration config = new ParameterConfiguration();
            
            config.z3LogFile = Properties.Settings.Default.LogFile;

            return config;
        }

        public static bool saveParameterConfigurationToSettings(ParameterConfiguration config)
        {
            try
            {
                Properties.Settings.Default.LogFile = config.z3LogFile;

                Properties.Settings.Default.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

    }

    public delegate void loaderProgressUpdater(int perc);

    public class Loader
    {
        private string workingDirectory;
        private Process currentProcess;

        private bool isCancelled;

        public event loaderProgressUpdater statusUpdate;

        private ParameterConfiguration config;
        private LogProcessor processor;

        public Loader(ParameterConfiguration config)
        {
            List<FileInfo> filelist = new List<FileInfo>();

            this.config = config;

            processor = new LogProcessor(filelist, config.skipDecisions, config.checkToConsider);
        }

        public void Cancel()
        {
            isCancelled = true;
            currentProcess?.Kill();
        }

        public void Load()
        {
            workingDirectory = new FileInfo(config.z3LogFile).DirectoryName;

            statusUpdate(0);

           
            taskLoadZ3LogFile(config.z3LogFile);

            statusUpdate(1000);
            Thread.Sleep(250);
        }

        void taskLoadZ3LogFile(string logFile)
        {
            statusUpdate(0);

            FileInfo fi = new FileInfo(logFile);
            long curPos = 0;
            int lineNo = 0;
            int oldperc = 0;
            processor.model.LogFileName = logFile;
            try
            {
                using (var rd = File.OpenText(logFile))
                {
                    string l;
                    while ((l = rd.ReadLine()) != null && !isCancelled)
                    {
                        if (rd.Peek() != -1)
                        {
                            try
                            {
                                processor.ParseSingleLine(l);
                            }
                            catch (LogProcessor.OldLogFormatException)
                            {
                                System.Windows.Forms.MessageBox.Show("Please pass \"PROOF=true\" to Z3 when generating logs.", "Invalid Log File");
                                isCancelled = true;
                                return;
                            }
                            catch (LogProcessor.Z3VersionException)
                            {
                                System.Windows.Forms.MessageBox.Show("The version of Z3 that generated this log is not supported. Please upgrade to the latest version.", "Unsupported Z3 Version");
                                isCancelled = true;
                                return;
                            }
#if !DEBUG
                            catch (Exception e)
                            {
                                System.Windows.Forms.MessageBox.Show($"An exception occured while parsing the log: {e.Message}", "Parsing Log Failed");
                                isCancelled = true;
                                return;
                            }
#endif
                        }
                        curPos += l.Length + 2;

                        if (fi.Length == 0) continue;

                        lineNo++;
                        int perc = (int)(curPos * 999 / fi.Length);
                        if (oldperc != perc)
                        {
                            statusUpdate(perc);
                            oldperc = perc;
                        }
                    }
                    processor.Finish();
                    processor.ComputeCost();
                    processor.model.BuildInstantiationDAG();
                }
            }
            catch (FileNotFoundException)
            {
                System.Windows.Forms.MessageBox.Show("The provided file path was invalid.", "File Not Found");
                isCancelled = true;
            }
            statusUpdate(1000);
        }

        Process createLoaderProcess(string executable, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.FileName = executable;
            startInfo.ErrorDialog = false;

            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            Process process = new Process();
            process.StartInfo = startInfo;
            currentProcess = process;
            return process;
        }

        public Model GetModel()
        {
            return processor.model;
        }
    }
}
