using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Charting.Visuals.Axes;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.ChartModifiers;
using SciChart.Data.Model;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class RocCurveViewModel : BaseViewModel
    {
        private UserControl? _rocCurveChart;
        private double _aucScore;
        private double _optimalThreshold;
        private double _tprAtOptimal;
        private double _fprAtOptimal;
        private bool _hasData;

        public RocCurveViewModel()
        {
            _aucScore = 0;
            _optimalThreshold = 0.5;
            _tprAtOptimal = 0;
            _fprAtOptimal = 0;
            _hasData = false;
        }

        #region Properties

        public UserControl? RocCurveChart
        {
            get => _rocCurveChart;
            set => SetProperty(ref _rocCurveChart, value);
        }

        public double AucScore
        {
            get => _aucScore;
            set => SetProperty(ref _aucScore, value);
        }

        public double OptimalThreshold
        {
            get => _optimalThreshold;
            set => SetProperty(ref _optimalThreshold, value);
        }

        public double TprAtOptimal
        {
            get => _tprAtOptimal;
            set => SetProperty(ref _tprAtOptimal, value);
        }

        public double FprAtOptimal
        {
            get => _fprAtOptimal;
            set => SetProperty(ref _fprAtOptimal, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the ROC curve with FPR, TPR, and threshold data
        /// </summary>
        public void UpdateRocCurve(List<double> fpr, List<double> tpr, List<double> thresholds, double aucScore)
        {
            if (fpr == null || tpr == null || fpr.Count == 0 || tpr.Count == 0 || fpr.Count != tpr.Count)
            {
                HasData = false;
                return;
            }

            AucScore = aucScore;

            // Find optimal threshold (closest to top-left corner)
            var optimalIndex = FindOptimalThresholdIndex(fpr, tpr);
            if (optimalIndex >= 0 && optimalIndex < thresholds.Count)
            {
                OptimalThreshold = thresholds[optimalIndex];
                TprAtOptimal = tpr[optimalIndex];
                FprAtOptimal = fpr[optimalIndex];
            }

            // Create the chart on the UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var chart = CreateRocCurveChart(fpr, tpr, optimalIndex);
                RocCurveChart = chart;
                HasData = true;
            });
        }

        /// <summary>
        /// Clears all ROC curve data
        /// </summary>
        public void Clear()
        {
            RocCurveChart = null;
            AucScore = 0;
            OptimalThreshold = 0.5;
            TprAtOptimal = 0;
            FprAtOptimal = 0;
            HasData = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Finds the optimal threshold index using Youden's J statistic
        /// </summary>
        private int FindOptimalThresholdIndex(List<double> fpr, List<double> tpr)
        {
            var maxJ = double.MinValue;
            var optimalIndex = 0;

            for (int i = 0; i < fpr.Count; i++)
            {
                // Youden's J statistic = TPR - FPR
                var j = tpr[i] - fpr[i];
                if (j > maxJ)
                {
                    maxJ = j;
                    optimalIndex = i;
                }
            }

            return optimalIndex;
        }

        /// <summary>
        /// Creates the ROC curve chart using SciChart
        /// </summary>
        private UserControl CreateRocCurveChart(List<double> fpr, List<double> tpr, int optimalIndex)
        {
            var containerControl = new UserControl();
            var sciChartSurface = new SciChartSurface
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.White,
                Padding = new Thickness(0)
            };

            // Create X-axis (False Positive Rate)
            var xAxis = new NumericAxis
            {
                AxisTitle = "False Positive Rate (FPR)",
                VisibleRange = new DoubleRange(0, 1),
                GrowBy = new DoubleRange(0.05, 0.05),
                DrawMajorBands = false,
                DrawMinorGridLines = false,
                AxisAlignment = AxisAlignment.Bottom,
                FontSize = 12,
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.SemiBold
            };

            // Create Y-axis (True Positive Rate)
            var yAxis = new NumericAxis
            {
                AxisTitle = "True Positive Rate (TPR) / Sensitivity",
                VisibleRange = new DoubleRange(0, 1),
                GrowBy = new DoubleRange(0.05, 0.05),
                DrawMajorBands = false,
                DrawMinorGridLines = false,
                AxisAlignment = AxisAlignment.Left,
                FontSize = 12,
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.SemiBold
            };

            sciChartSurface.XAxes.Add(xAxis);
            sciChartSurface.YAxes.Add(yAxis);

            // Create ROC Curve line series
            var rocDataSeries = new XyDataSeries<double, double> { SeriesName = $"ROC Curve (AUC = {AucScore:F4})" };
            for (int i = 0; i < fpr.Count; i++)
            {
                rocDataSeries.Append(fpr[i], tpr[i]);
            }

            var rocLineSeries = new FastLineRenderableSeries
            {
                DataSeries = rocDataSeries,
                StrokeThickness = 3,
                Stroke = Color.FromRgb(52, 152, 219), // #3498DB
                AntiAliasing = true
            };

            sciChartSurface.RenderableSeries.Add(rocLineSeries);

            // Create diagonal reference line (random classifier)
            var diagonalDataSeries = new XyDataSeries<double, double> { SeriesName = "Random Classifier (AUC = 0.50)" };
            diagonalDataSeries.Append(0, 0);
            diagonalDataSeries.Append(1, 1);

            var diagonalLineSeries = new FastLineRenderableSeries
            {
                DataSeries = diagonalDataSeries,
                StrokeThickness = 2,
                Stroke = Color.FromRgb(149, 165, 166), // #95A5A6
                StrokeDashArray = new double[] { 5, 5 },
                AntiAliasing = true
            };

            sciChartSurface.RenderableSeries.Add(diagonalLineSeries);

            // Add optimal point marker
            if (optimalIndex >= 0 && optimalIndex < fpr.Count)
            {
                var optimalDataSeries = new XyDataSeries<double, double> { SeriesName = "Optimal Threshold" };
                optimalDataSeries.Append(fpr[optimalIndex], tpr[optimalIndex]);

                var optimalPointSeries = new XyScatterRenderableSeries
                {
                    DataSeries = optimalDataSeries,
                    PointMarker = new SciChart.Charting.Visuals.PointMarkers.EllipsePointMarker
                    {
                        Width = 12,
                        Height = 12,
                        Fill = Color.FromRgb(231, 76, 60), // #E74C3C
                        Stroke = Colors.White,
                        StrokeThickness = 2
                    }
                };

                sciChartSurface.RenderableSeries.Add(optimalPointSeries);

                // Add annotation for optimal point
                var annotation = new TextAnnotation
                {
                    Text = $"Optimal Point\n({fpr[optimalIndex]:F3}, {tpr[optimalIndex]:F3})",
                    X1 = fpr[optimalIndex],
                    Y1 = tpr[optimalIndex],
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    HorizontalAnchorPoint = HorizontalAnchorPoint.Left,
                    VerticalAnchorPoint = VerticalAnchorPoint.Bottom,
                    Margin = new Thickness(5, -5, 0, 0)
                };

                sciChartSurface.Annotations.Add(annotation);
            }

            // Add chart modifiers for interactivity
            sciChartSurface.ChartModifier = new ModifierGroup(
                new ZoomPanModifier { ExecuteOn = ExecuteOn.MouseRightButton },
                new MouseWheelZoomModifier(),
                new ZoomExtentsModifier(),
                new CursorModifier
                {
                    ShowTooltip = true,
                    ShowAxisLabels = true
                },
                new LegendModifier
                {
                    ShowLegend = true,
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(10),
                    GetLegendDataFor = SourceMode.AllSeries
                }
            );

            containerControl.Content = sciChartSurface;
            return containerControl;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RocCurveChart = null;
            }
            base.Dispose(disposing);
        }
    }
}
