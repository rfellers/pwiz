﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PrecursorEnrichmentForm : WorkspaceForm
    {
        private ZedGraphControl _zedGraphControl;
        private IDictionary<CohortKey, IDictionary<double, int>> _queryRows;
        public PrecursorEnrichmentForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _zedGraphControl = new ZedGraphControl
                                   {
                                       Dock = DockStyle.Fill,
                                   };
            splitContainer1.Panel2.Controls.Add(_zedGraphControl);
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            _queryRows = null;
            IDictionary<CohortKey, IDictionary<double, int>> rows = null;
            using (var session = Workspace.OpenSession())
            {
                var broker = new LongOperationBroker(new Action<LongOperationBroker>(delegate
                                                                                         {
                                                                                             rows = QueryRecords(session);
                                                                                         }),
                                                     new LongWaitDialog(this, "Querying database"), session);
                if (broker.LaunchJob())
                {
                    _queryRows = rows;
                }
            }
            RefreshUi();
        }

        private bool _inRefreshUi;

        private void RefreshUi() {
            if (_inRefreshUi)
            {
                return;
            }
            try
            {
                
            _inRefreshUi = true;

            _zedGraphControl.GraphPane.CurveList.Clear();
            _zedGraphControl.GraphPane.GraphObjList.Clear();
            if (_queryRows == null)
            {
                return;
            }
            var values = new Dictionary<CohortKey, double[]>();
            var xValues = new double[101];
            for (int i = 0; i < xValues.Length; i++ )
            {
                xValues[i] = i;
            }
            foreach (var row in _queryRows)
            {
                double[] vector;
                if (!values.TryGetValue(row.Key, out vector))
                {
                    vector = new double[xValues.Length];
                    values.Add(row.Key, vector);
                }
                foreach (var entry in row.Value)
                {
                    
                    if (entry.Key < 0 || entry.Key > 1)
                    {
                        continue;
                    }
                    vector[(int) (entry.Key * 100)] += entry.Value;
                }
            }
            var cohortKeys = new List<CohortKey>(values.Keys);
            cohortKeys.Sort();
            if (dataGridView1.Rows.Count != cohortKeys.Count)
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Rows.Add(cohortKeys.Count);
            }
            for (int i = 0; i < cohortKeys.Count; i++)
            {
                var cohortKey = cohortKeys[i];
                var yValues = values[cohortKey];
                var row = dataGridView1.Rows[i];
                if (dataGridView1.SelectedRows.Count == 0 || dataGridView1.SelectedRows.Contains(row))
                {
                    _zedGraphControl.GraphPane.AddBar(cohortKey.ToString(), xValues, yValues, DistributionResultsForm.GetColor(i, cohortKeys.Count));
                }
                row.Cells[colCohort.Index].Value = cohortKey;
                var statsX = new Statistics(xValues);
                var statsY = new Statistics(yValues);
                row.Cells[colMean.Index].Value = statsX.Mean(statsY);
                row.Cells[colStdDev.Index].Value = statsX.StdDev(statsY);
                row.Cells[colMedian.Index].Value = statsX.Median(statsY);
            }
            _zedGraphControl.GraphPane.AxisChange();
            _zedGraphControl.Invalidate();
            }
            finally
            {
                _inRefreshUi = false;
            }
        }

        private IDictionary<CohortKey, IDictionary<double, int>> QueryRecords(ISession session)
        {
            double minScore;
            int minTracers;
            Double.TryParse(tbxMinScore.Text, out minScore);
            int.TryParse(tbxMinTracers.Text, out minTracers);
            IQuery query;
            bool byFile = cbxByFile.Checked;
            if (byFile)
            {
                query = session.CreateQuery(
                    "SELECT D.PrecursorEnrichment, Count(D.Id), D.PeptideFileAnalysis.PeptideAnalysis.Peptide.Sequence, D.PeptideFileAnalysis.MsDataFile.Label FROM DbPeptideDistribution D WHERE D.PeptideQuantity = 0 AND D.Score >= :minScore GROUP BY D.PeptideFileAnalysis.PeptideAnalysis.Peptide.Sequence, D.PrecursorEnrichment, D.PeptideFileAnalysis.MsDataFile.Label")
                    .SetParameter("minScore", minScore);
            }
            else
            {
                query =
                    session.CreateQuery(
                    "SELECT D.PrecursorEnrichment, Count(D.Id), D.PeptideFileAnalysis.PeptideAnalysis.Peptide.Sequence, D.PeptideFileAnalysis.MsDataFile.Cohort, D.PeptideFileAnalysis.MsDataFile.TimePoint FROM DbPeptideDistribution D WHERE D.PeptideQuantity = 0 AND D.Score >= :minScore GROUP BY D.PeptideFileAnalysis.PeptideAnalysis.Peptide.Sequence, D.PrecursorEnrichment, D.PeptideFileAnalysis.MsDataFile.Cohort, D.PeptideFileAnalysis.MsDataFile.TimePoint")
                    .SetParameter("minScore", minScore);
            }
            var result = new Dictionary<CohortKey, IDictionary<double, int>>();
            foreach (object[] row in query.List())
            {
                if (row[0] == null)
                {
                    continue;
                }
                string cohort = null;
                double? timePoint = null;
                var peptideSequence = Convert.ToString(row[2]);
                if (minTracers > 0 && Workspace.GetMaxTracerCount(peptideSequence) < minTracers)
                {
                    continue;
                }
                if (row[3] != null)
                {
                    cohort = Convert.ToString(row[3]);
                }
                if (!byFile && row[4] != null)
                {
                    timePoint = Convert.ToDouble(row[4]);
                }
                var cohortKey = new CohortKey(cohort, timePoint);
                IDictionary<double, int> dict;
                if (!result.TryGetValue(cohortKey, out dict))
                {
                    dict = new Dictionary<double, int>();
                    result.Add(cohortKey, dict);
                }
                int count;
                double precursorEnrichment = Convert.ToDouble(row[0]);
                dict.TryGetValue(precursorEnrichment, out count);
                count += Convert.ToInt32(row[1]);
                dict[precursorEnrichment] = count;
            }
            return result;
        }

        private class CohortKey : IComparable<CohortKey>
        {
            public CohortKey(string cohort, double? timePoint)
            {
                Cohort = cohort;
                TimePoint = timePoint;
            }
            public string Cohort { get; private set; }
            public double? TimePoint { get; private set; }
            public int CompareTo(CohortKey other)
            {
                int result = (Cohort ?? "").CompareTo(other.Cohort ?? "");
                if (result != 0)
                {
                    return result;
                }
                return (TimePoint ?? 0.0).CompareTo(other.TimePoint ?? 0.0);
            }
            public override string ToString()
            {
                if (Cohort == null)
                {
                    return ((object) TimePoint ?? "").ToString();
                }
                if (TimePoint == null)
                {
                    return Cohort;
                }
                return Cohort + " " + TimePoint;
            }

            public bool Equals(CohortKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(other.Cohort, Cohort) && other.TimePoint.Equals(TimePoint);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != typeof (CohortKey)) return false;
                return Equals((CohortKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Cohort != null ? Cohort.GetHashCode() : 0)*397) ^ (TimePoint.HasValue ? TimePoint.Value.GetHashCode() : 0);
                }
            }
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            RefreshUi();
        }
    }
}