using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals;
using SciChart.Charting.Visuals.Axes;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.ChartModifiers;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class FeatureImportanceViewModel : BaseViewModel
    {
        private Control? _featureImportanceChart;
        private bool _hasData;
        private int _featureCount;
        private string _topFeature;
        private double _topFeatureScore;

        public FeatureImportanceViewModel()
        {
            _hasData = false;
            _featureCount = 0;
            _topFeature = string.Empty;
            _topFeatureScore = 0;
        }

        #region Properties

        public Control? FeatureImportanceChart
        {
            get => _featureImportanceChart;
            set => SetProperty(ref _featureImportanceChart, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        public int FeatureCount
        {
            get => _featureCount;
            set => SetProperty(ref _featureCount, value);
        }

        public string TopFeature
        {
            get => _topFeature;
            set => SetProperty(ref _topFeature, value);
        }

        public double TopFeatureScore
        {
            get => _topFeatureScore;
            set => SetProperty(ref _topFeatureScore, value);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the feature importance visualization with new data
        /// </summary>
        public void UpdateFeatureImportance(List<string> featureNames, List<double> importanceScores)
        {
            if (featureNames == null || importanceScores == null ||
                featureNames.Count == 0 || importanceScores.Count == 0 ||
                featureNames.Count != importanceScores.Count)
            {
                HasData = false;
                return;
            }

            FeatureCount = featureNames.Count;

            // Sort by absolute importance (descending) - negative PFI means important
            var sortedData = featureNames
                .Zip(importanceScores, (name, score) => (Name: name, Score: score))
                .OrderByDescending(x => Math.Abs(x.Score))
                .ToList();

            // Get top feature (use absolute value for display)
            if (sortedData.Count > 0)
            {
                TopFeature = sortedData[0].Name;
                TopFeatureScore = Math.Abs(sortedData[0].Score);
            }

            // Create chart on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    FeatureImportanceChart = CreateFeatureImportanceChart(sortedData);
                    HasData = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating feature importance chart: {ex.Message}");
                    HasData = false;
                }
            });
        }

        /// <summary>
        /// Clears all feature importance data
        /// </summary>
        public void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                FeatureImportanceChart = null;
                HasData = false;
                FeatureCount = 0;
                TopFeature = string.Empty;
                TopFeatureScore = 0;
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a horizontal bar chart for feature importance
        /// </summary>
        private Control CreateFeatureImportanceChart(List<(string Name, double Score)> sortedData)
        {
            var surface = new SciChartSurface
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            // Configure X-axis (feature indices - will show feature names)
            var xAxis = new NumericAxis
            {
                AxisTitle = "Features",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false,
                AxisAlignment = AxisAlignment.Bottom
            };

            // Configure Y-axis (importance scores)
            var yAxis = new NumericAxis
            {
                AxisTitle = "Importance Score",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false,
                AxisAlignment = AxisAlignment.Left
            };

            surface.XAxes.Add(xAxis);
            surface.YAxes.Add(yAxis);

            // Create data series - swap X and Y to create horizontal bars
            // X = feature index (categorical), Y = importance score
            var dataSeries = new XyDataSeries<double, double>
            {
                AcceptsUnsortedData = true
            };

            // For horizontal bars: Append(Y-value, X-value) then rotate axes
            for (int i = 0; i < sortedData.Count; i++)
            {
                // Use absolute value for better visualization (negative PFI means important)
                double absoluteScore = Math.Abs(sortedData[i].Score);
                dataSeries.Append(i, absoluteScore);  // X=index, Y=score
            }

            // Create horizontal bar chart by rotating the axes
            var barSeries = new FastColumnRenderableSeries
            {
                DataSeries = dataSeries,
                DataPointWidth = 0.7,
                StrokeThickness = 0,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(52, 152, 219), 0),    // Blue
                        new GradientStop(Color.FromRgb(46, 204, 113), 1)     // Green
                    }
                }
            };

            surface.RenderableSeries.Add(barSeries);

            // Add modifiers for interactivity
            surface.ChartModifier = new ModifierGroup(
                new ZoomPanModifier { IsEnabled = true },
                new ZoomExtentsModifier { IsAnimated = true },
                new MouseWheelZoomModifier(),
                new CursorModifier
                {
                    ShowTooltip = true,
                    ShowAxisLabels = true
                }
            );

            // Custom X-axis labels with feature names
            xAxis.LabelProvider = new FeatureNameLabelProvider(sortedData.Select(x => x.Name).ToList());

            // Wrap SciChartSurface in a ContentControl (which is a UserControl)
            var container = new ContentControl
            {
                Content = surface
            };

            return container;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Clear();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Custom label provider to show feature names on Y-axis
    /// </summary>
    public class FeatureNameLabelProvider : SciChart.Charting.Visuals.Axes.LabelProviders.LabelProviderBase
    {
        private readonly List<string> _featureNames;

        public FeatureNameLabelProvider(List<string> featureNames)
        {
            _featureNames = featureNames ?? new List<string>();
        }

        public override string FormatLabel(IComparable dataValue)
        {
            if (dataValue is double index)
            {
                int idx = (int)Math.Round(index);
                if (idx >= 0 && idx < _featureNames.Count)
                {
                    return _featureNames[idx];
                }
            }
            return string.Empty;
        }

        public override string FormatCursorLabel(IComparable dataValue)
        {
            return FormatLabel(dataValue);
        }
    }
}
