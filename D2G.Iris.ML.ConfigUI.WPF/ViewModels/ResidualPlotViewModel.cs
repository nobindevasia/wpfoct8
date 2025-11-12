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
using SciChart.Charting.Visuals.PointMarkers;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ResidualPlotViewModel : BaseViewModel
    {
        private Control? _residualsVsPredictedChart;
        private Control? _predictedVsActualChart;
        private Control? _residualHistogramChart;
        private bool _hasData;
        private double _meanResidual;
        private double _stdResidual;

        public ResidualPlotViewModel()
        {
            _hasData = false;
            _meanResidual = 0;
            _stdResidual = 0;
        }

        #region Properties

        public Control? ResidualsVsPredictedChart
        {
            get => _residualsVsPredictedChart;
            set => SetProperty(ref _residualsVsPredictedChart, value);
        }

        public Control? PredictedVsActualChart
        {
            get => _predictedVsActualChart;
            set => SetProperty(ref _predictedVsActualChart, value);
        }

        public Control? ResidualHistogramChart
        {
            get => _residualHistogramChart;
            set => SetProperty(ref _residualHistogramChart, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        public double MeanResidual
        {
            get => _meanResidual;
            set => SetProperty(ref _meanResidual, value);
        }

        public double StdResidual
        {
            get => _stdResidual;
            set => SetProperty(ref _stdResidual, value);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the residual plot visualizations with new data
        /// </summary>
        public void UpdateResidualPlots(List<double> actualValues, List<double> predictedValues, List<double> residuals)
        {
            if (actualValues == null || predictedValues == null || residuals == null ||
                actualValues.Count == 0 || predictedValues.Count == 0 || residuals.Count == 0 ||
                actualValues.Count != predictedValues.Count || actualValues.Count != residuals.Count)
            {
                HasData = false;
                return;
            }

            // Calculate statistics
            MeanResidual = residuals.Average();
            StdResidual = Math.Sqrt(residuals.Select(r => Math.Pow(r - MeanResidual, 2)).Average());

            // Create charts on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    ResidualsVsPredictedChart = CreateResidualsVsPredictedChart(predictedValues, residuals);
                    PredictedVsActualChart = CreatePredictedVsActualChart(actualValues, predictedValues);
                    ResidualHistogramChart = CreateResidualHistogramChart(residuals);
                    HasData = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating residual plots: {ex.Message}");
                    HasData = false;
                }
            });
        }

        /// <summary>
        /// Clears all residual plot data
        /// </summary>
        public void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ResidualsVsPredictedChart = null;
                PredictedVsActualChart = null;
                ResidualHistogramChart = null;
                HasData = false;
                MeanResidual = 0;
                StdResidual = 0;
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates residuals vs predicted values scatter plot
        /// </summary>
        private Control CreateResidualsVsPredictedChart(List<double> predicted, List<double> residuals)
        {
            var containerControl = new UserControl();
            var surface = new SciChartSurface
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0)
            };

            var xAxis = new NumericAxis
            {
                AxisTitle = "Predicted Values",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false
            };

            var yAxis = new NumericAxis
            {
                AxisTitle = "Residuals (Actual - Predicted)",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false
            };

            surface.XAxes.Add(xAxis);
            surface.YAxes.Add(yAxis);

            // Scatter series for residuals
            var scatterData = new XyDataSeries<double, double> { AcceptsUnsortedData = true };
            for (int i = 0; i < predicted.Count; i++)
            {
                scatterData.Append(predicted[i], residuals[i]);
            }

            // Add zero reference line first (so it appears behind scatter points)
            var zeroLine = new XyDataSeries<double, double>();
            double minX = predicted.Min();
            double maxX = predicted.Max();
            zeroLine.Append(minX, 0);
            zeroLine.Append(maxX, 0);

            var referenceLine = new FastLineRenderableSeries
            {
                DataSeries = zeroLine,
                Stroke = Colors.Red,
                StrokeThickness = 3,
                StrokeDashArray = new double[] { 8, 4 },
                AntiAliasing = true
            };

            surface.RenderableSeries.Add(referenceLine);

            // Add scatter series on top
            var scatterSeries = new XyScatterRenderableSeries
            {
                DataSeries = scatterData,
                PointMarker = new EllipsePointMarker
                {
                    Width = 4,
                    Height = 4,
                    Fill = Color.FromArgb(100, 52, 152, 219),
                    Stroke = Color.FromArgb(180, 41, 128, 185),
                    StrokeThickness = 0.5
                }
            };

            surface.RenderableSeries.Add(scatterSeries);

            surface.ChartModifier = new ModifierGroup(
                new ZoomPanModifier(),
                new ZoomExtentsModifier(),
                new MouseWheelZoomModifier(),
                new CursorModifier { ShowTooltip = true }
            );

            containerControl.Content = surface;
            return containerControl;
        }

        /// <summary>
        /// Creates predicted vs actual scatter plot with diagonal line
        /// </summary>
        private Control CreatePredictedVsActualChart(List<double> actual, List<double> predicted)
        {
            var containerControl = new UserControl();
            var surface = new SciChartSurface
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0)
            };

            var xAxis = new NumericAxis
            {
                AxisTitle = "Actual Values",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false
            };

            var yAxis = new NumericAxis
            {
                AxisTitle = "Predicted Values",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false
            };

            surface.XAxes.Add(xAxis);
            surface.YAxes.Add(yAxis);

            // Scatter series
            var scatterData = new XyDataSeries<double, double> { AcceptsUnsortedData = true };
            for (int i = 0; i < actual.Count; i++)
            {
                scatterData.Append(actual[i], predicted[i]);
            }

            // Add perfect prediction diagonal line first (behind scatter)
            double minVal = Math.Min(actual.Min(), predicted.Min());
            double maxVal = Math.Max(actual.Max(), predicted.Max());

            var diagonalLine = new XyDataSeries<double, double>();
            diagonalLine.Append(minVal, minVal);
            diagonalLine.Append(maxVal, maxVal);

            var referenceLine = new FastLineRenderableSeries
            {
                DataSeries = diagonalLine,
                Stroke = Colors.Red,
                StrokeThickness = 3,
                StrokeDashArray = new double[] { 8, 4 },
                AntiAliasing = true
            };

            surface.RenderableSeries.Add(referenceLine);

            // Add scatter series on top
            var scatterSeries = new XyScatterRenderableSeries
            {
                DataSeries = scatterData,
                PointMarker = new EllipsePointMarker
                {
                    Width = 4,
                    Height = 4,
                    Fill = Color.FromArgb(100, 46, 204, 113),
                    Stroke = Color.FromArgb(180, 39, 174, 96),
                    StrokeThickness = 0.5
                }
            };

            surface.RenderableSeries.Add(scatterSeries);

            surface.ChartModifier = new ModifierGroup(
                new ZoomPanModifier(),
                new ZoomExtentsModifier(),
                new MouseWheelZoomModifier(),
                new CursorModifier { ShowTooltip = true }
            );

            containerControl.Content = surface;
            return containerControl;
        }

        /// <summary>
        /// Creates histogram of residuals
        /// </summary>
        private Control CreateResidualHistogramChart(List<double> residuals)
        {
            var containerControl = new UserControl();
            var surface = new SciChartSurface
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0)
            };

            var xAxis = new NumericAxis
            {
                AxisTitle = "Residuals",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false
            };

            var yAxis = new NumericAxis
            {
                AxisTitle = "Frequency",
                GrowBy = new SciChart.Data.Model.DoubleRange(0.1, 0.1),
                DrawMajorBands = false,
                DrawMinorGridLines = false
            };

            surface.XAxes.Add(xAxis);
            surface.YAxes.Add(yAxis);

            // Calculate histogram bins
            int bins = Math.Min(30, (int)Math.Sqrt(residuals.Count));
            double min = residuals.Min();
            double max = residuals.Max();
            double binWidth = (max - min) / bins;

            var histogram = new Dictionary<double, int>();
            for (int i = 0; i < bins; i++)
            {
                double binCenter = min + (i + 0.5) * binWidth;
                histogram[binCenter] = 0;
            }

            foreach (var residual in residuals)
            {
                int binIndex = (int)((residual - min) / binWidth);
                if (binIndex >= bins) binIndex = bins - 1;
                if (binIndex < 0) binIndex = 0;
                double binCenter = min + (binIndex + 0.5) * binWidth;
                histogram[binCenter]++;
            }

            var columnData = new XyDataSeries<double, double>();
            foreach (var kvp in histogram.OrderBy(x => x.Key))
            {
                columnData.Append(kvp.Key, kvp.Value);
            }

            var columnSeries = new FastColumnRenderableSeries
            {
                DataSeries = columnData,
                DataPointWidth = 0.8,
                Fill = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                StrokeThickness = 1
            };

            surface.RenderableSeries.Add(columnSeries);

            surface.ChartModifier = new ModifierGroup(
                new ZoomPanModifier(),
                new ZoomExtentsModifier(),
                new MouseWheelZoomModifier()
            );

            containerControl.Content = surface;
            return containerControl;
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
}
