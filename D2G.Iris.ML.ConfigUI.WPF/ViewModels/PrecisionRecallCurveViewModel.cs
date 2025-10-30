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
    public class PrecisionRecallCurveViewModel : BaseViewModel
    {
        private UserControl? _precisionRecallChart;
        private double _averagePrecision;
        private double _optimalThreshold;
        private double _precisionAtOptimal;
        private double _recallAtOptimal;
        private double _f1AtOptimal;
        private bool _hasData;

        public PrecisionRecallCurveViewModel()
        {
            _averagePrecision = 0;
            _optimalThreshold = 0.5;
            _precisionAtOptimal = 0;
            _recallAtOptimal = 0;
            _f1AtOptimal = 0;
            _hasData = false;
        }

        #region Properties

        public UserControl? PrecisionRecallChart
        {
            get => _precisionRecallChart;
            set => SetProperty(ref _precisionRecallChart, value);
        }

        public double AveragePrecision
        {
            get => _averagePrecision;
            set => SetProperty(ref _averagePrecision, value);
        }

        public double OptimalThreshold
        {
            get => _optimalThreshold;
            set => SetProperty(ref _optimalThreshold, value);
        }

        public double PrecisionAtOptimal
        {
            get => _precisionAtOptimal;
            set => SetProperty(ref _precisionAtOptimal, value);
        }

        public double RecallAtOptimal
        {
            get => _recallAtOptimal;
            set => SetProperty(ref _recallAtOptimal, value);
        }

        public double F1AtOptimal
        {
            get => _f1AtOptimal;
            set => SetProperty(ref _f1AtOptimal, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        #endregion

        #region Public Methods




        public void UpdatePrecisionRecallCurve(List<double> precision, List<double> recall, List<double> thresholds, double averagePrecision)
        {
            if (precision == null || recall == null || precision.Count == 0 || recall.Count == 0 || precision.Count != recall.Count)
            {
                HasData = false;
                return;
            }

            AveragePrecision = averagePrecision;


            var optimalIndex = FindOptimalThresholdIndex(precision, recall);
            if (optimalIndex >= 0 && optimalIndex < thresholds.Count)
            {
                OptimalThreshold = thresholds[optimalIndex];
                PrecisionAtOptimal = precision[optimalIndex];
                RecallAtOptimal = recall[optimalIndex];
                F1AtOptimal = 2 * (PrecisionAtOptimal * RecallAtOptimal) / (PrecisionAtOptimal + RecallAtOptimal);
            }


            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var chart = CreatePrecisionRecallChart(precision, recall, optimalIndex);
                PrecisionRecallChart = chart;
                HasData = true;
            });
        }




        public void Clear()
        {
            PrecisionRecallChart = null;
            AveragePrecision = 0;
            OptimalThreshold = 0.5;
            PrecisionAtOptimal = 0;
            RecallAtOptimal = 0;
            F1AtOptimal = 0;
            HasData = false;
        }

        #endregion

        #region Private Methods




        private int FindOptimalThresholdIndex(List<double> precision, List<double> recall)
        {
            var maxF1 = double.MinValue;
            var optimalIndex = 0;

            for (int i = 0; i < precision.Count; i++)
            {
                if (precision[i] + recall[i] == 0) continue;


                var f1 = 2 * (precision[i] * recall[i]) / (precision[i] + recall[i]);
                if (f1 > maxF1)
                {
                    maxF1 = f1;
                    optimalIndex = i;
                }
            }

            return optimalIndex;
        }




        private UserControl CreatePrecisionRecallChart(List<double> precision, List<double> recall, int optimalIndex)
        {
            var containerControl = new UserControl();
            var sciChartSurface = new SciChartSurface
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.White,
                Padding = new Thickness(0)
            };


            var xAxis = new NumericAxis
            {
                AxisTitle = "Recall (Sensitivity / True Positive Rate)",
                VisibleRange = new DoubleRange(0, 1),
                GrowBy = new DoubleRange(0.05, 0.05),
                DrawMajorBands = false,
                DrawMinorGridLines = false,
                AxisAlignment = AxisAlignment.Bottom,
                FontSize = 12,
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.SemiBold
            };


            var yAxis = new NumericAxis
            {
                AxisTitle = "Precision (Positive Predictive Value)",
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


            var prDataSeries = new XyDataSeries<double, double> { SeriesName = $"PR Curve (AP = {AveragePrecision:F4})" };
            for (int i = 0; i < recall.Count; i++)
            {
                prDataSeries.Append(recall[i], precision[i]);
            }

            var prLineSeries = new FastLineRenderableSeries
            {
                DataSeries = prDataSeries,
                StrokeThickness = 3,
                Stroke = Color.FromRgb(46, 204, 113),
                AntiAliasing = true
            };

            sciChartSurface.RenderableSeries.Add(prLineSeries);




            var baselineDataSeries = new XyDataSeries<double, double> { SeriesName = "Baseline (Random Classifier)" };
            baselineDataSeries.Append(0, 0.5);
            baselineDataSeries.Append(1, 0.5);

            var baselineLineSeries = new FastLineRenderableSeries
            {
                DataSeries = baselineDataSeries,
                StrokeThickness = 2,
                Stroke = Color.FromRgb(149, 165, 166),
                StrokeDashArray = new double[] { 5, 5 },
                AntiAliasing = true
            };

            sciChartSurface.RenderableSeries.Add(baselineLineSeries);


            if (optimalIndex >= 0 && optimalIndex < recall.Count)
            {
                var optimalDataSeries = new XyDataSeries<double, double> { SeriesName = "Optimal Threshold (Max F1)" };
                optimalDataSeries.Append(recall[optimalIndex], precision[optimalIndex]);

                var optimalPointSeries = new XyScatterRenderableSeries
                {
                    DataSeries = optimalDataSeries,
                    PointMarker = new SciChart.Charting.Visuals.PointMarkers.EllipsePointMarker
                    {
                        Width = 12,
                        Height = 12,
                        Fill = Color.FromRgb(231, 76, 60),
                        Stroke = Colors.White,
                        StrokeThickness = 2
                    }
                };

                sciChartSurface.RenderableSeries.Add(optimalPointSeries);


                var annotation = new TextAnnotation
                {
                    Text = $"Optimal Point (Max F1)\n({recall[optimalIndex]:F3}, {precision[optimalIndex]:F3})",
                    X1 = recall[optimalIndex],
                    Y1 = precision[optimalIndex],
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    HorizontalAnchorPoint = HorizontalAnchorPoint.Left,
                    VerticalAnchorPoint = VerticalAnchorPoint.Top,
                    Margin = new Thickness(5, 5, 0, 0)
                };

                sciChartSurface.Annotations.Add(annotation);
            }


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
                    ShowVisibilityCheckboxes = false,
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
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
                PrecisionRecallChart = null;
            }
            base.Dispose(disposing);
        }
    }
}
