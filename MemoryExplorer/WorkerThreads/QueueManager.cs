﻿using Library.LogFileHelper;
using MemoryExplorer.Model;
using MemoryExplorer.Worker;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System;
using System.Collections.Concurrent;
using MemoryExplorer.Profiles;
using MemoryExplorer.Data;

namespace MemoryExplorer.WorkerThreads
{
    public class QueueManagerThread
    {
        private BackgroundWorker _backgroundWorker = new BackgroundWorker();
        private BlockingCollection<Job> _ingesterInbound = null;
        private BlockingCollection<Job> _ingesterOutbound = null;
        private BlockingCollection<Job> _processorInbound = null;
        private BlockingCollection<Job> _processorOutbound = null;
        private DataModel _model;
        private DataProviderBase _dataProvider = null;
        private Profile _profile = null;

        public QueueManagerThread(DataModel model)
        {
            _model = model;
            _profile = model.ActiveProfile;
            _dataProvider = model.DataProvider;
            _backgroundWorker.DoWork += new DoWorkEventHandler(QueueManagerThread_DoWork);
            _backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(QueueManagerThread_RunWorkerCompleted);
            _backgroundWorker.WorkerSupportsCancellation = true;

            // Start the asynchronous operation.
            _backgroundWorker.RunWorkerAsync();

        }
        public void Stop()
        {
            Debug.WriteLine("The queue manager is closing");

            _backgroundWorker.CancelAsync();
        }
        private void QueueManagerThread_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            BackgroundWorker worker = sender as BackgroundWorker;            
            _ingesterOutbound = _model.IngesterOut; 
            _ingesterInbound = _model.IngesterIn;
            _processorInbound = _model.ProcessorIn;
            _processorOutbound = _model.ProcessorOut;

            foreach (var item in _processorInbound.GetConsumingEnumerable())
            {
                Job j = (Job)item;
                _model.DecrementActiveJobs();
                switch (j.Status)
                {
                    case JobStatus.Failed:
                        _model.WriteToLogfile("Processor Error from " + j.Action);
                        _model.WriteToLogfile("\t" + j.ErrorMessage);
                        string messageBoxText = "There was a problem loading the data provider.\n" + j.ErrorMessage;
                        System.Windows.MessageBox.Show(messageBoxText, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case JobStatus.Complete:
                        switch (j.Action)
                        {
                            case JobAction.SetDataProvider:
                                SetDataProvider(ref j);
                                break;
                            case JobAction.GetProfileIdentification:
                                GetProfileIdentification(ref j);
                                break;
                            case JobAction.LoadProfile:
                                LoadProfile(ref j);
                                break;
                            case JobAction.FindKernelDtb:
                                FindKernelDtb(ref j);
                                break;
                            case JobAction.LoadKernelAddressSpace:
                                LoadKernelAddressSpace(ref j);
                                break;
                            case JobAction.FindKernelImage:
                                FindKernelImage(ref j);
                                break;
                            case JobAction.FindUserSharedData:
                                FindUserSharedData(ref j);
                                break;
                            case JobAction.EnumerateObjectTypes:
                                EnumerateObjectTypes(ref j);
                                break;
                            default:
                                break;
                        }
                        break;
                    default:
                        break;
                }
                if (worker.CancellationPending)
                    break;
            }                
        }

        private void EnumerateObjectTypes(ref Job j)
        {
            Job j2 = new Job();
            j2.Action = JobAction.EnumerateObjectTypes;
            _ingesterOutbound.Add(j2);
            Thread.Sleep(1000);
            Job j1 = new Job();
            j1.Action = JobAction.EnumerateObjectTree;
            _processorOutbound.Add(j1);
            _model.IncrementActiveJobs("Building Object Tree");
        }

        private void FindUserSharedData(ref Job j)
        {
            Job j1 = new Job();
            j1.Action = JobAction.EnumerateObjectTypes;
            _processorOutbound.Add(j1);
            _model.IncrementActiveJobs("Detecting Object Types");
            Job j2 = new Job();
            j2.Action = JobAction.FindUserSharedData;
            foreach (var item in j.ActionMessage)
            {
                j2.ActionMessage.Add(item);
            }
            _ingesterOutbound.Add(j2);
        }

        private void FindKernelImage(ref Job j)
        {
            Job j1 = new Job();
            j1.Action = JobAction.FindUserSharedData;
            _processorOutbound.Add(j1);
            _model.IncrementActiveJobs("Loading User Shared Data");
            Job j2 = new Job();
            j2.Action = JobAction.FindKernelImage;
            foreach (var item in j.ActionMessage)
            {
                j2.ActionMessage.Add(item);
            }            
            _ingesterOutbound.Add(j2);
        }

        private void LoadKernelAddressSpace(ref Job j)
        {
            Job j1 = new Job();
            j1.Action = JobAction.FindKernelImage;
            _processorOutbound.Add(j1);
            _model.IncrementActiveJobs("Finding Kernel Image");
            Job j2 = new Job();
            j2.Action = JobAction.LoadKernelAddressSpace;
            _ingesterOutbound.Add(j2);
        }

        private void FindKernelDtb(ref Job j)
        {
            Job j1 = new Job();
            j1.Action = JobAction.LoadKernelAddressSpace;
            _processorOutbound.Add(j1);
            _model.IncrementActiveJobs("Loading Kernel Address Space");
            Job j2 = new Job();
            j2.Action = JobAction.FindKernelDtb;
            _ingesterOutbound.Add(j2);
        }

        private void LoadProfile(ref Job j)
        {
            Job j1 = new Job();
            j1.Action = JobAction.FindKernelDtb;
            _processorOutbound.Add(j1);
            _model.IncrementActiveJobs("Detecting Kernel DTB");
        }

        private void GetProfileIdentification(ref Job j)
        {
            // there is a possibility that more than one guidage was identified
            // so I need to work out how to deal with that
            if (j.ActionMessage.Count > 1)
            {
                string messageBoxText = "Memory Explorer has identified more than one GUIDAGE in the image.\nGoing to proceed with the first one.";
                System.Windows.MessageBox.Show(messageBoxText, "Strangeness", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            if (j.ActionMessage.Count > 0)
            {
                Job j1 = new Job();
                j1.Action = JobAction.LoadProfile;
                j1.ActionMessage.Add(j.ActionMessage[0]);
                _processorOutbound.Add(j1);
                Job j2 = new Job();
                j2.Action = JobAction.LoadProfileId;
                _ingesterOutbound.Add(j2);
                _model.IncrementActiveJobs("Loading Profile");
            }
            else
            {
                string messageBoxText = "Processing Halted because Memory Explorer couldn't find a Profile from RSDS";
                System.Windows.MessageBox.Show(messageBoxText, "Can't Proceed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetDataProvider(ref Job j)
        {
            Job j1 = new Job();
            j1.Action = JobAction.GetProfileIdentification;
            _processorOutbound.Add(j1);
            Job j2 = new Job();
            j2.Action = JobAction.SetCacheFolder;
            j2.ActionMessage.Add(j.ActionMessage[0]);
            _ingesterOutbound.Add(j2);
            _model.IncrementActiveJobs("Detecting Profile");
        }

        private void QueueManagerThread_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // First, handle the case where an exception was thrown.
            if (e.Error != null)
            {
                System.Windows.MessageBox.Show(e.Error.Message);
            }
            else if (e.Cancelled)
            {
                // Next, handle the case where the user canceled 
                // the operation.
                // Note that due to a race condition in 
                // the DoWork event handler, the Cancelled
                // flag may not have been set, even though
                // CancelAsync was called.
                //resultLabel.Text = "Canceled";
            }
            else
            {
                // Finally, handle the case where the operation 
                // succeeded.
                //resultLabel.Text = e.Result.ToString();
            }

            // Enable the UpDown control.
            //this.numericUpDown1.Enabled = true;

            // Enable the Start button.
            //startAsyncButton.Enabled = true;

            // Disable the Cancel button.
            //cancelAsyncButton.Enabled = false;
        }
    }
}
