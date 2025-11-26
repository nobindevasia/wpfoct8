using System.Windows.Controls;
using D2G.Iris.ML.ConfigUI.WPF.ViewModels;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.RenderableSeries;
using System.Windows.Media;

namespace D2G.Iris.ML.ConfigUI.WPF.Views
{
    public partial class ClusterVisualizationView : UserControl
    {
        public ClusterVisualizationView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ClusterVisualizationViewModel viewModel)
            {
                viewModel.ClusterSeries.CollectionChanged += (s, args) => UpdateChart(viewModel);
                UpdateChart(viewModel);
            }
        }

        private void UpdateChart(ClusterVisualizationViewModel viewModel)
        {
            ClusterChart.RenderableSeries.Clear();

            var colors = new[]
            {
                Colors.Red, Colors.Blue, Colors.Green, Colors.Orange,
                Colors.Purple, Colors.Brown, Colors.Pink, Colors.Cyan,
                Colors.Magenta, Colors.Yellow, Colors.Lime, Colors.Teal
            };

            int colorIndex = 0;
            foreach (var series in viewModel.ClusterSeries)
            {
                var color = colors[colorIndex % colors.Length];

                var scatterSeries = new XyScatterRenderableSeries
                {
                    DataSeries = series
                };

                // If this is the centroid series (usually the last one), make it stand out
                if (series.SeriesName == "Centroids")
                {
                    scatterSeries.Stroke = Colors.Black;
                    scatterSeries.StrokeThickness = 3;

                    // Make centroids larger
                    var centroidMarker = new SciChart.Charting.Visuals.PointMarkers.EllipsePointMarker
                    {
                        Width = 16,
                        Height = 16,
                        Fill = Colors.Black,
                        Stroke = Colors.White,
                        StrokeThickness = 2
                    };
                    scatterSeries.PointMarker = centroidMarker;
                }
                else
                {
                    scatterSeries.Stroke = color;
                    scatterSeries.StrokeThickness = 2;

                    // Regular cluster points
                    var pointMarker = new SciChart.Charting.Visuals.PointMarkers.EllipsePointMarker
                    {
                        Width = 8,
                        Height = 8,
                        Fill = color,
                        Stroke = Colors.White,
                        StrokeThickness = 1
                    };
                    scatterSeries.PointMarker = pointMarker;
                }

                ClusterChart.RenderableSeries.Add(scatterSeries);
                colorIndex++;
            }

            if (viewModel.ClusterSeries.Count > 0)
            {
                ClusterChart.ZoomExtents();
            }
        }
    }
}
