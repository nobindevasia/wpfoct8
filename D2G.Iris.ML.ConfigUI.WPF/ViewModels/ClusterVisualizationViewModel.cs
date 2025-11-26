using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using D2G.Iris.ML.Core.Models;
using SciChart.Charting.Model.DataSeries;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ClusterVisualizationViewModel : BaseViewModel
    {
        private ObservableCollection<XyDataSeries<double, double>> _clusterSeries;
        private string _clusterSummary = "No clustering results available yet.";
        private bool _hasData;
        private Dictionary<int, int> _clusterSizes;

        public ClusterVisualizationViewModel()
        {
            _clusterSeries = new ObservableCollection<XyDataSeries<double, double>>();
            _clusterSizes = new Dictionary<int, int>();
        }

        #region Properties

        public ObservableCollection<XyDataSeries<double, double>> ClusterSeries
        {
            get => _clusterSeries;
            set => SetProperty(ref _clusterSeries, value);
        }

        public string ClusterSummary
        {
            get => _clusterSummary;
            set => SetProperty(ref _clusterSummary, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        #endregion

        
        public void UpdateClusterVisualization(TrainingResult result)
        {
            if (result == null || result.ClusterAssignments == null || result.DataPoints == null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HasData = false;
                    ClusterSummary = "No clustering results available.";
                });
                return;
            }

            // Ensure UI updates happen on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateClusterVisualizationOnUIThread(result);
            });
        }

        private void UpdateClusterVisualizationOnUIThread(TrainingResult result)
        {
            try
            {
                _clusterSizes = result.ClusterSizes ?? new Dictionary<int, int>();

                ClusterSeries.Clear();

                // For large datasets, sample points for visualization
                const int maxVisualizationPoints = 5000;
                var totalPoints = result.ClusterAssignments.Count;
                var samplingRate = totalPoints > maxVisualizationPoints
                    ? maxVisualizationPoints / (double)totalPoints
                    : 1.0;

                var clusterGroups = result.ClusterAssignments
                    .Select((clusterId, index) => new { ClusterId = (int)clusterId, Index = index })
                    .Where((item, idx) => samplingRate >= 1.0 || (idx % (int)(1.0 / samplingRate)) == 0)
                    .GroupBy(x => x.ClusterId);


                foreach (var group in clusterGroups.OrderBy(g => g.Key))
                {
                    var series = new XyDataSeries<double, double>
                    {
                        SeriesName = $"Cluster {group.Key}",
                        AcceptsUnsortedData = true  // Cluster data may not be sorted by X
                    };


                    foreach (var item in group)
                    {
                        var dataPoint = result.DataPoints[item.Index];
                        if (dataPoint.Length >= 2)
                        {
                            series.Append(dataPoint[0], dataPoint[1]);
                        }
                    }

                    ClusterSeries.Add(series);
                }


                if (result.ClusterCentroids != null && result.ClusterCentroids.Count > 0)
                {
                    var centroidSeries = new XyDataSeries<double, double>
                    {
                        SeriesName = "Centroids",
                        AcceptsUnsortedData = true
                    };

                    foreach (var centroid in result.ClusterCentroids)
                    {
                        if (centroid.Length >= 2)
                        {
                            centroidSeries.Append(centroid[0], centroid[1]);
                        }
                    }

                    ClusterSeries.Add(centroidSeries);
                }


                GenerateClusterSummary(result);

                HasData = true;
            }
            catch (Exception ex)
            {
                HasData = false;
                ClusterSummary = $"Error visualizing clusters: {ex.Message}";
            }
        }

        private void GenerateClusterSummary(TrainingResult result)
        {
            var summary = $"Clustering completed successfully.\n\n";
            summary += $"Algorithm: {result.AlgorithmUsed}\n";
            summary += $"Number of Clusters: {_clusterSizes.Count}\n";
            summary += $"Total Data Points: {result.ClusterAssignments?.Count ?? 0:N0}\n";

            const int maxVisualizationPoints = 5000;
            var totalPoints = result.ClusterAssignments?.Count ?? 0;
            if (totalPoints > maxVisualizationPoints)
            {
                summary += $"Visualization: Showing ~{maxVisualizationPoints:N0} sampled points (dataset too large)\n";
            }
            summary += "\n";

            summary += "Cluster Distribution:\n";
            var totalCount = result.ClusterAssignments?.Count ?? 1;
            foreach (var kvp in _clusterSizes.OrderBy(x => x.Key))
            {
                double percentage = (kvp.Value * 100.0) / totalCount;
                summary += $"  Cluster {kvp.Key}: {kvp.Value} points ({percentage:F1}%)\n";
            }

            if (result.Metrics is Core.Models.ClusteringMetrics metrics)
            {
                summary += $"\nQuality Metrics:\n";
                summary += $"  Average Distance: {metrics.AverageDistance:F4}\n";
                summary += $"  Silhouette Score: {metrics.SilhouetteScore:F4} (closer to 1 is better)\n";
                summary += $"  Davies-Bouldin Index: {metrics.DaviesBouldinIndex:F4} (lower is better)\n";
            }

            summary += $"\nNote: Visualization shows first two features/dimensions.";

            ClusterSummary = summary;
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ClusterSeries.Clear();
                _clusterSizes.Clear();
                HasData = false;
                ClusterSummary = "No clustering results available yet.";
            });
        }
    }
}
