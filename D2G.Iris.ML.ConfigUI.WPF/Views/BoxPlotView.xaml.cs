using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.ViewModels;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.Visuals.PointMarkers;

namespace D2G.Iris.ML.ConfigUI.WPF.Views
{
    public partial class BoxPlotView : UserControl
    {
        private BoxPlotViewModel? _viewModel;

        public BoxPlotView()
        {
            InitializeComponent();
            Loaded += BoxPlotView_Loaded;
        }

        private void BoxPlotView_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = DataContext as BoxPlotViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoxPlotViewModel.BoxPlotData))
            {
                Dispatcher.Invoke(() => UpdateBoxPlot());
            }
        }

        private void UpdateBoxPlot()
        {
            try
            {
                if (_viewModel?.BoxPlotData == null)
                {
                    BoxPlotChart.RenderableSeries.Clear();
                    return;
                }

                var boxData = _viewModel.BoxPlotData;

                using (BoxPlotChart.SuspendUpdates())
                {
                    BoxPlotChart.RenderableSeries.Clear();

                    var boxPlotDataSeries = new BoxPlotDataSeries<double, double>();

                    boxPlotDataSeries.Append(
                        0,
                        boxData.Median,
                        boxData.LowerWhisker,
                        boxData.Q1,
                        boxData.Q3,
                        boxData.UpperWhisker
                    );

                    
                    var boxPlotSeries = new FastBoxPlotRenderableSeries
                    {
                        DataSeries = boxPlotDataSeries,
                        Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 70, 130, 180)),
                        Stroke = System.Windows.Media.Color.FromRgb(70, 130, 180),
                        StrokeThickness = 2,
                        DataPointWidth = 0.3
                    };

                    BoxPlotChart.RenderableSeries.Add(boxPlotSeries);

                   
                    var meanSeries = new XyDataSeries<double, double> { AcceptsUnsortedData = true };
                    meanSeries.Append(0, boxData.Mean);

                    var meanScatter = new XyScatterRenderableSeries
                    {
                        DataSeries = meanSeries,
                        PointMarker = new SciChart.Charting.Visuals.PointMarkers.CrossPointMarker
                        {
                            Width = 12,
                            Height = 12,
                            Stroke = System.Windows.Media.Color.FromRgb(231, 76, 60),
                            StrokeThickness = 3
                        }
                    };
                    BoxPlotChart.RenderableSeries.Add(meanScatter);

                   
                    if (boxData.Outliers.Count > 0)
                    {
                        var outlierSeries = new XyDataSeries<double, double> { AcceptsUnsortedData = true };
                        var maxOutliersToShow = Math.Min(boxData.Outliers.Count, 500); 

                        for (int i = 0; i < maxOutliersToShow; i++)
                        {
                            outlierSeries.Append(0, boxData.Outliers[i].Value);
                        }

                        var outlierScatter = new XyScatterRenderableSeries
                        {
                            DataSeries = outlierSeries,
                            PointMarker = new SciChart.Charting.Visuals.PointMarkers.EllipsePointMarker
                            {
                                Width = 6,
                                Height = 6,
                                Fill = System.Windows.Media.Color.FromRgb(231, 76, 60),
                                Stroke = System.Windows.Media.Color.FromRgb(231, 76, 60)
                            }
                        };
                        BoxPlotChart.RenderableSeries.Add(outlierScatter);
                    }

                    BoxPlotChart.ZoomExtents();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating box plot: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
