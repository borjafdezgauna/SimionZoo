﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Collections.ObjectModel;
using AppXML.Models;
using Caliburn.Micro;
using System.Windows.Forms;
using System.Windows;
using System.IO;
using System.Dynamic;
using System.Threading;
using AppXML.Data;
using Herd;

namespace AppXML.ViewModels
{

    public class ExperimentQueueViewModel : PropertyChangedBase
    {
        
        private WindowViewModel m_parent;

        private string m_name;
        public string name { get { return m_name; } set { m_name = name; } }

        public bool isEmpty() { return m_experimentQueue.Count == 0; }

        private bool m_bModified = false;
        public bool bModified { get { return m_bModified; } set { m_bModified = value; } }

        private BindableCollection<ExperimentViewModel> m_experimentQueue
            = new BindableCollection<ExperimentViewModel>();
        public BindableCollection<ExperimentViewModel> experimentQueue { get { return m_experimentQueue; } set { m_experimentQueue = value; } }

        public void getEnqueuedExperimentList(ref List<ExperimentViewModel> outList)
        {
            outList.Clear();
            foreach (ExperimentViewModel experiment in m_experimentQueue)
            {
                //(for now,) all the experiments are in the queue are considered ready for execution
                outList.Add(experiment);
            }
        }
        public void getLoggedExperimentList(ref List<ExperimentViewModel> outList)
        {
            outList.Clear();
            foreach (ExperimentViewModel experiment in m_experimentQueue)
            {
                if (experiment.bDataAvailable)
                    outList.Add(experiment);
            }
        }

        private int m_selectedIndex= -1;
        public int selectedIndex
        {
            get { return m_selectedIndex; }
            set
            {
                if (m_selectedIndex >= 0) m_experimentQueue[m_selectedIndex].bIsSelected = false;
                m_selectedIndex = value;
            if (m_selectedIndex >= 0)
            {
                m_experimentQueue[m_selectedIndex].bIsSelected = true;
                m_parent.loadExperimentInEditor(m_experimentQueue[m_selectedIndex].experimentXML);
                m_parent.bIsExperimentQueueNotEmpty = true;
            }
            NotifyOfPropertyChange(()=>selectedIndex);}
        }

        public void markModified(bool modified)
        {
            m_bModified = modified;
            NotifyOfPropertyChange(() => bModified);
        }
        public void clear()
        {
            m_experimentQueue.Clear();
            NotifyOfPropertyChange(()=>experimentQueue);
        }

        public ExperimentQueueViewModel()
        {
            m_selectedIndex = -1;

        }
        public void setParent(WindowViewModel parent)
        {
            m_parent= parent;
        }

        public void addExperiment(ExperimentViewModel exp)
        {
            if (m_selectedIndex >= 0)
                m_experimentQueue.Insert(m_selectedIndex + 1, exp);
            else m_experimentQueue.Add(exp);
            markModified(true);

            NotifyOfPropertyChange(() => experimentQueue);
        }
        public void addExperiment(string name,XmlDocument expXML, string path= "")
        {
            ExperimentViewModel newExperiment = new ExperimentViewModel(name, expXML, path);
            m_experimentQueue.Add(newExperiment);
            markModified(true);
            Task.Run(() => newExperiment.checkLogFilesAlreadyExist());

            NotifyOfPropertyChange(() => experimentQueue);
        }

        public void removeSelectedExperiments()
        {
            if (m_selectedIndex>=0)
            {
                markModified(true);

                m_experimentQueue.RemoveAt(m_selectedIndex);
                NotifyOfPropertyChange(() => experimentQueue);
            }
        }
        public void modifySelectedExperiment(XmlDocument modifiedExperimentXML)
        {
            if(m_selectedIndex>=0)
            {
                markModified(true);

                m_experimentQueue[m_selectedIndex].experimentXML = modifiedExperimentXML;
            }
        }


        public bool save()
        {
            string xmlFileName = null;

            if (experimentQueue.Count == 0) return false;

            //Save dialog -> returns the experiment queue file
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Experiment batch | *.exp-batch";
            sfd.InitialDirectory = "../experiments";
            string CombinedPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "../experiments");
            if (!Directory.Exists(CombinedPath))
                System.IO.Directory.CreateDirectory(CombinedPath);
            sfd.InitialDirectory = System.IO.Path.GetFullPath(CombinedPath);

            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                xmlFileName = sfd.FileName;
            }
            else return false;

            //clean output directory if it exists
            xmlFileName = xmlFileName.Split('.')[0];
            xmlFileName = Utility.GetRelativePathTo(Directory.GetCurrentDirectory(), xmlFileName);
            if (Directory.Exists(xmlFileName))
            {
                try
                {
                    Directory.Delete(xmlFileName, true);
                }
                catch
                {
                    CaliburnUtility.showWarningDialog("It has not been possible to remove the directory: "
                        + xmlFileName + ". Make sure that it's not been using for other app."
                        , "ERROR");
                    //DialogViewModel dvm = new DialogViewModel(null, "It has not been possible to remove the directory: " + xmlFileName + ". Make sure that it's not been using for other app.", DialogViewModel.DialogType.Info);
                    //dynamic settings = new ExpandoObject();
                    //settings.WindowStyle = WindowStyle.ThreeDBorderWindow;
                    //settings.ShowInTaskbar = true;
                    //settings.Title = "ERROR";

                    //new WindowManager().ShowDialog(dvm, null, settings);
                    return false;
                }
            }
            
            XmlDocument experimentXMLDoc = new XmlDocument();
            XmlElement experimentBatchesNode = experimentXMLDoc.CreateElement("Experiments");
            experimentXMLDoc.AppendChild(experimentBatchesNode);

            List<string> names = new List<string>();

            //set the experiment name
            name = xmlFileName;

            foreach (ExperimentViewModel experiment in experimentQueue)
            {
                //avoid duplicated names
                while (names.Contains(experiment.name))
                    experiment.name += "c";
                names.Add(experiment.name);

                XmlDocument docume = experiment.experimentXML;
                string folderPath = xmlFileName + "/" + experiment.name;
                Directory.CreateDirectory(folderPath);
                string filePath = folderPath + "/" + experiment.name + ".experiment";
                docume.Save(filePath);
                //crear carpeta para archivo xml y carpetas para sus hijos
                //añadir el nodo al fichero xml
                XmlElement experimentNode = experimentXMLDoc.CreateElement(experiment.name);
                experimentNode.SetAttribute("Path", filePath);
                experimentBatchesNode.AppendChild(experimentNode);

                experiment.filePath = filePath;
            }

            experimentXMLDoc.Save(xmlFileName + ".exp-batch");
            markModified(false);
            return true;
        }

        public void load()
        {
            string fileDoc = null;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Experiment batch | *.exp-batch";
            ofd.InitialDirectory = Path.Combine(Path.GetDirectoryName(Directory.GetCurrentDirectory()), "experiments");
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                fileDoc = ofd.FileName;
            }
            else return;
            //this doesn't seem to work
            //Cursor.Current = Cursors.WaitCursor;
            //System.Windows.Forms.Application.DoEvents();

            //LOAD THE EXPERIMENT BATCH IN THE QUEUE
            XmlDocument batchDoc = new XmlDocument();
            batchDoc.Load(fileDoc);
            XmlElement fileRoot = batchDoc.DocumentElement;
            if (fileRoot.Name != "Experiments")
                return;

            foreach (XmlElement element in fileRoot.ChildNodes)
            {
                string expName = element.Name;
                string path = element.Attributes["Path"].Value;
                if (File.Exists(path))
                {
                    XmlDocument expDocument = new XmlDocument();
                    expDocument.Load(path);
                    addExperiment(element.Name, expDocument, path);
                }
            }
            markModified(true);
        }

        private bool m_bLogFilesAvailable= false;
        public bool bLogFilesAvailable { get { return m_bLogFilesAvailable; }
            set { m_bLogFilesAvailable = value; NotifyOfPropertyChange(()=> bLogFilesAvailable); }
        }

        public void checkLogFilesAlreadyExist()
        {
            bool anyLogFile = false;
            foreach (ExperimentViewModel experiment in experimentQueue)
            {
                anyLogFile = anyLogFile || experiment.checkLogFilesAlreadyExist();
            }
            if (anyLogFile != m_bLogFilesAvailable) bLogFilesAvailable = anyLogFile;
        }
    }
}
