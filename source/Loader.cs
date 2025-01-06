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
using System.Threading.Tasks;

namespace AxiomProfiler
{

    public class ParameterConfiguration
    {
        public string z3LogFile = "";
        public bool skipDecisions = true;
        public int checkToConsider = 0; // 0 is a special value, meaning "process all checks"
        public bool headless = false;
        public bool timing = false;


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
            if (config.headless)
                statusUpdate += headlessStatusUpdate;
            workingDirectory = new FileInfo(config.z3LogFile).DirectoryName;

            statusUpdate(0);

           
            taskLoadZ3LogFile(config.z3LogFile);

            if (!config.headless)
                Thread.Sleep(250);
        }

        void headlessStatusUpdate(int perc)
        {
            if (Console.IsOutputRedirected) return;
            if (perc != 0) {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
            }
            Console.CursorVisible = false;
            for (int i = 0; i < perc/20; i++) {
                Console.Write("#");
            }
            Console.WriteLine(" " + perc / 10 + "." + perc % 10 + "%");
            Console.CursorVisible = true;
        }

        void taskLoadZ3LogFile(string logFile)
        {

            FileInfo fi = new FileInfo(logFile);
            long curPos = 0;
            int lineNo = 0;
            int oldperc = 0;
            processor.model.LogFileName = logFile;
            var parseMs = 0L;
            var analysisMs = 0L;
            try
            {
                using (var rd = File.OpenText(logFile))
                {
                    var watch = Stopwatch.StartNew();
                    bool stop = false;
                    var task = Task.Run(() => {
                    string l;
                    while ((l = rd.ReadLine()) != null && !isCancelled)
                    {
                        stop = false;
                        if (rd.Peek() != -1)
                        {
                            try
                            {
                                processor.ParseSingleLine(l);
                            }
                            catch (LogProcessor.OldLogFormatException)
                            {
                                if (!config.headless)
                                    System.Windows.Forms.MessageBox.Show("Please pass \"PROOF=true\" to Z3 when generating logs.", "Invalid Log File");
                                isCancelled = true;
                                return;
                            }
                            catch (LogProcessor.Z3VersionException)
                            {
                                if (!config.headless)
                                    System.Windows.Forms.MessageBox.Show("The version of Z3 that generated this log is not supported. Please upgrade to the latest version.", "Unsupported Z3 Version");
                                isCancelled = true;
                                return;
                            }
#if !DEBUG
                            catch (Exception e)
                            {
                                if (!config.headless)
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
                    });
                    while (!stop) {
                        stop = true;
                        if (task.Wait(TimeSpan.FromMilliseconds(1000))) {
                            stop = false;
                            break;
                        }
                    }
                    if (stop)
                        isCancelled = true;

                    watch.Stop();
                    parseMs = watch.ElapsedMilliseconds;
                    watch.Restart();

                    processor.Finish();
                    processor.ComputeCost();
                    processor.model.BuildInstantiationDAG();

                    watch.Stop();
                    analysisMs = watch.ElapsedMilliseconds;
                }
            }
            catch (FileNotFoundException)
            {
                if (!config.headless)
                    System.Windows.Forms.MessageBox.Show("The provided file path was invalid.", "File Not Found");
                isCancelled = true;
            }
            statusUpdate(1000);
            if (!config.timing) return;
            if (isCancelled) Console.WriteLine("[Parse] Err " + oldperc + "‰");
            else Console.WriteLine("[Parse] " + parseMs + "ms");
            Console.WriteLine("[Graph] " + analysisMs + "ms");
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
