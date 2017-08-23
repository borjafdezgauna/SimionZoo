﻿using Caliburn.Micro;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Badger.ViewModels
{
    public class DataSeries
    {
        private double[] _values = null;
        public double[] Values
        {
            get
            {
                if (_values is null)
                    throw new InvalidOperationException("Values has not been initiliazed; you must call SetLength() before accessing the Values property.");
                return _values;
            }
            set { _values = value; }
        }
        public StatData Stats = new StatData();

        public void SetLength(int numValues)
        {
            Values = new double[numValues];
        }

        public void CalculateStats()
        {
            //calculate avg, min and max
            double sum = 0.0;
            Stats.min = Values[0]; Stats.max = Values[0];
            foreach (double val in Values)
            {
                sum += val;
                if (val > Stats.max) Stats.max = val;
                if (val < Stats.min) Stats.min = val;
            }

            Stats.avg = sum / Values.Length;

            //calculate std. deviation
            double diff;
            sum = 0.0;
            foreach (double val in Values)
            {
                diff = val - Stats.avg;
                sum += diff * diff;
            }

            Stats.stdDev = Math.Sqrt(sum / Values.Length);
        }

    }
    public class TrackVariableData
    {
        public TrackVariableData(int numEpisodes, int evalulationEpisodes, int trainingEpisodes)
        {
            lastEvaluationEpisodeData = new DataSeries();
            if (numEpisodes > 0)
            {
                experimentAverageData = new DataSeries();
                experimentAverageData.SetLength(numEpisodes);
            }
            
            experimentEvaluationData = new List<DataSeries>(evalulationEpisodes);
            for (int i = 0; i < evalulationEpisodes; i++)
            {
                experimentEvaluationData.Add(new DataSeries());
            }
            
            experimentTrainingData = new List<DataSeries>(trainingEpisodes);
            for (int i = 0; i < trainingEpisodes; i++)
            {
                experimentTrainingData.Add(new DataSeries());
            }
        }

        public DataSeries lastEvaluationEpisodeData;
        public DataSeries experimentAverageData;
        public List<DataSeries> experimentEvaluationData;
        public List<DataSeries> experimentTrainingData;

        public void calculateStats()
        {
            if (lastEvaluationEpisodeData != null) lastEvaluationEpisodeData.CalculateStats();
            if (experimentAverageData != null) experimentAverageData.CalculateStats();

            foreach (var item in experimentEvaluationData)
                item.CalculateStats();

            foreach (var item in experimentTrainingData)
                item.CalculateStats();
        }
    }
    public class TrackData
    {
        public bool bSuccesful;
        public double[] simTime;
        public double[] realTime;
        public Dictionary<string, string> forkValues;
        private Dictionary<string, TrackVariableData> variablesData = new Dictionary<string, TrackVariableData>();

        public TrackData(int maxNumSteps, int numEpisodes, int evalulationEpisodes, int trainingEpisodes, List<string> variables)
        {
            simTime = new double[maxNumSteps];
            realTime = new double[maxNumSteps];
            foreach (string variable in variables)
            {
                this.variablesData[variable] = new TrackVariableData(numEpisodes, evalulationEpisodes, trainingEpisodes);
            }
        }

        private void addVariableData(string variable, TrackVariableData variableData)
        {
            this.variablesData.Add(variable, variableData);
        }

        public TrackVariableData getVariableData(string variable)
        {
            if (variablesData.ContainsKey(variable)) return variablesData[variable];
            else return null;
        }
    }
    public class LogQueryResultTrackViewModel : PropertyChangedBase
    {
        //data read fromm the log files: might be more than one track before applying a group function
        private List<TrackData> m_trackData = new List<TrackData>();
        //public merged track data: cannot be accessed before calling consolidateGroups()
        public TrackData trackData
        {
            get { if (m_trackData.Count == 1) return m_trackData[0]; return null; }
            set { }
        }
        public bool bHasData
        {
            get { return m_trackData.Count > 0; }
        }

        //fork values given to this group
        private Dictionary<string, string> m_forkValues = new Dictionary<string, string>();
        public Dictionary<string, string> forkValues
        {
            get { return m_forkValues; }
            set { m_forkValues = value; NotifyOfPropertyChange(() => forkValues); }
        }
        private string m_parentExperimentName = "";

        public LogQueryResultTrackViewModel(string experimentName)
        {
            m_parentExperimentName = experimentName;
        }
        public string trackId
        {
            get
            {
                if (m_forkValues.Count == 0) return m_parentExperimentName;
                string id = "";
                foreach (KeyValuePair<string, string> entry in m_forkValues)
                {
                    id += entry.Key + "=" + entry.Value + ",";
                }
                id = id.Trim(',');
                return id;
            }
        }
        private string m_groupId = null;
        public string groupId
        {
            get { if (m_groupId != null) return m_groupId; return trackId; }
            set { m_groupId = value; NotifyOfPropertyChange(() => groupId); }
        }

        public void addTrackData(TrackData newTrackData)
        {
            m_trackData.Add(newTrackData);
        }

        //this function selects a unique track fromm each group (if there's more than one track)
        public void consolidateGroups(string function, string variable, List<string> groupBy)
        {
            if (m_trackData.Count > 1)
            {
                TrackData selectedTrack = null;
                double min = double.MaxValue, max = double.MinValue;
                foreach (TrackData track in m_trackData)
                {
                    TrackVariableData variableData = track.getVariableData(variable);
                    if (variableData != null)
                    {
                        if (function == LogQuery.functionMax && Math.Abs(variableData.lastEvaluationEpisodeData.Stats.avg) > max)
                        {
                            max = Math.Abs(variableData.lastEvaluationEpisodeData.Stats.avg);
                            selectedTrack = track;
                        }
                        if (function == LogQuery.functionMin && Math.Abs(variableData.lastEvaluationEpisodeData.Stats.avg) < min)
                        {
                            min = Math.Abs(variableData.lastEvaluationEpisodeData.Stats.avg);
                            selectedTrack = track;
                        }
                    }
                }
                m_trackData.Clear();
                m_trackData.Add(selectedTrack);

                //create a copy of the dictionary to solve the following issue:
                //after generating the first report (with groups) the forkValues of the experiments are cleared
                //and therefore, no more report can be generated afterwards
                forkValues = new Dictionary<string, string>(selectedTrack.forkValues);

                if (groupBy.Count > 0)
                {
                    //we remove those forks used to group from the forkValues
                    //because *hopefully* we only use them to name the track
                    m_groupId = "";
                    foreach (string group in groupBy)
                    {
                        m_groupId += group + "=" + forkValues[group] + ",";
                        forkValues.Remove(group);
                    }
                    groupId = m_groupId.TrimEnd(',');
                }
            }
        }
    }
}
