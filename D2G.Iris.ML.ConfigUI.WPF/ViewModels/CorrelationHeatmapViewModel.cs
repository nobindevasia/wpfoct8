using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;
using SciChart.Charting.Visuals;
using SciChart.Charting.Visuals.Axes;
using SciChart.Charting.Visuals.Axes.LabelProviders;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.ChartModifiers;
using SciChart.Data.Model;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class CorrelationHeatmapViewModel : BaseViewModel
    {
        #region Fields

        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;

        private string? _connectionString;
        private string? _tableName;
        private string? _whereClause;
        private List<ColumnStatistics>? _currentColumnStatistics;

        private UserControl? _heatmapChart;
        private bool _isGenerating;
        private string _progressMessage = string.Empty;

        #endregion

        #region Properties

        public UserControl? HeatmapChart
        {
            get => _heatmapChart;
            set => SetProperty(ref _heatmapChart, value);
        }

        public bool IsGenerating
        {
            get => _isGenerating;
            set => SetProperty(ref _isGenerating, value);
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        public ICommand GenerateCorrelationCommand { get; }

        #endregion

        #region Constructor

        public CorrelationHeatmapViewModel(
            IDialogService dialogService,
            IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;

            GenerateCorrelationCommand = new AsyncRelayCommand(
                async _ => await GenerateCorrelationMatrixAsync(),
                _ => CanGenerateCorrelation()
            );
        }

        #endregion

        #region Public Methods

        public void SetDatabaseConnection(
            string connectionString,
            string tableName,
            List<ColumnStatistics> columnStatistics,
            string? whereClause = null)
        {
            _connectionString = connectionString;
            _tableName = tableName;
            _currentColumnStatistics = columnStatistics;
            _whereClause = whereClause;
        }

        #endregion

        #region Private Methods

        private async Task GenerateCorrelationMatrixAsync()
        {
            try
            {
                IsGenerating = true;
                ProgressMessage = "Calculating correlations...";

                if (_currentColumnStatistics == null || _connectionString == null || _tableName == null)
                {
                    _dialogService.ShowErrorDialog("No data available. Please analyze data first.", "Error");
                    return;
                }

                var numericColumns = _currentColumnStatistics
                    .Where(c => IsNumericType(c.DataType))
                    .Select(c => c.ColumnName)
                    .ToArray();

                if (numericColumns.Length < 2)
                {
                    var errorChart = CreateErrorControl("At least 2 numeric columns are required for correlation analysis.");
                    HeatmapChart = errorChart;
                    _dialogService.ShowErrorDialog("At least 2 numeric columns are required for correlation analysis.", "Insufficient Data");
                    return;
                }

                ProgressMessage = $"Computing correlations for {numericColumns.Length} numeric columns...";
                await Task.Delay(100);

                var correlationMatrix = await _databaseAnalytics.GetCorrelationMatrixAsync(
                    _connectionString, _tableName, numericColumns, _whereClause);

                ProgressMessage = "Creating correlation heatmap...";
                await Task.Delay(100);

                var correlationChart = CreateCorrelationHeatmap(correlationMatrix);
                HeatmapChart = correlationChart;

                _dialogService.ShowInfoDialog("Correlation matrix generated successfully using database analytics.", "Correlation Analysis");
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating correlation matrix: {ex.Message}", "Correlation Error");
                Console.WriteLine($"Correlation error: {ex}");
            }
            finally
            {
                IsGenerating = false;
                ProgressMessage = string.Empty;
            }
        }

        private bool CanGenerateCorrelation()
        {
            return !string.IsNullOrEmpty(_connectionString) &&
                   !string.IsNullOrEmpty(_tableName) &&
                   _currentColumnStatistics != null &&
                   !IsGenerating;
        }

        private bool IsNumericType(string dataType)
        {
            return dataType.ToLower() switch
            {
                "int" or "bigint" or "smallint" or "tinyint" or
                "decimal" or "numeric" or "money" or "smallmoney" or
                "float" or "real" => true,
                _ => false
            };
        }

        private UserControl CreateCorrelationHeatmap(CorrelationMatrix correlationMatrix)
        {
            if (correlationMatrix.Columns.Count < 2)
            {
                return CreateErrorControl("Insufficient correlation data available.");
            }

            var columnNames = correlationMatrix.Columns;
            var size = columnNames.Count;

            var containerControl = new UserControl();
            var mainGrid = new Grid();

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = "Correlation Heatmap",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };
            Grid.SetRow(titleBlock, 0);
            mainGrid.Children.Add(titleBlock);

            var correlationData = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    correlationData[i, j] = correlationMatrix.GetCorrelation(columnNames[i], columnNames[j]);
                }
            }

            var sciChartSurface = new SciChartSurface
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(5),
                Background = Brushes.White,
                Padding = new Thickness(10)
            };

            var heatmapDataSeries = new UniformHeatmapDataSeries<int, int, double>(correlationData, 0, 1, 0, 1);
            var heatmapSeries = new FastUniformHeatmapRenderableSeries
            {
                DataSeries = heatmapDataSeries,
                DrawTextInCell = true,
                Opacity = 1.0
            };

            var colorMap = new HeatmapColorPalette
            {
                Minimum = -1.0,
                Maximum = 1.0
            };

            colorMap.GradientStops.Add(new GradientStop(Colors.Blue, 0.0));
            colorMap.GradientStops.Add(new GradientStop(Colors.Cyan, 0.25));
            colorMap.GradientStops.Add(new GradientStop(Colors.White, 0.5));
            colorMap.GradientStops.Add(new GradientStop(Colors.Yellow, 0.75));
            colorMap.GradientStops.Add(new GradientStop(Colors.Red, 1.0));

            heatmapSeries.ColorMap = colorMap;

            var xAxis = new NumericAxis
            {
                AxisTitle = "Features",
                VisibleRange = new DoubleRange(-0.5, size - 0.5),
                MajorDelta = 1,
                MinorDelta = 1,
                DrawMinorTicks = false,
                DrawMajorTicks = true,
                DrawMajorGridLines = true,
                DrawMinorGridLines = false,
                DrawMajorBands = false,
                AutoTicks = false,
                LabelProvider = new FeatureNameLabelProvider(columnNames.ToArray()),
                AxisAlignment = AxisAlignment.Bottom
            };

            var yAxis = new NumericAxis
            {
                AxisTitle = "Features",
                VisibleRange = new DoubleRange(-0.5, size - 0.5),
                MajorDelta = 1,
                MinorDelta = 1,
                DrawMinorTicks = false,
                DrawMajorTicks = true,
                DrawMajorGridLines = true,
                DrawMinorGridLines = false,
                DrawMajorBands = false,
                AutoTicks = false,
                LabelProvider = new ReversedFeatureNameLabelProvider(columnNames.ToArray()),
                AxisAlignment = AxisAlignment.Left,
                FlipCoordinates = true
            };

            sciChartSurface.XAxes.Add(xAxis);
            sciChartSurface.YAxes.Add(yAxis);
            sciChartSurface.RenderableSeries.Add(heatmapSeries);

            sciChartSurface.ChartModifier = new ModifierGroup(
                new MouseWheelZoomModifier(),
                new RubberBandXyZoomModifier(),
                new ZoomExtentsModifier(),
                new ZoomPanModifier { ExecuteOn = ExecuteOn.MouseRightButton },
                new CursorModifier { ShowTooltip = true, ShowAxisLabels = true },
                new XAxisDragModifier(),
                new YAxisDragModifier()
            );

            Grid.SetRow(sciChartSurface, 1);
            mainGrid.Children.Add(sciChartSurface);

            var legendPanel = CreateLegendPanel();
            Grid.SetRow(legendPanel, 2);
            mainGrid.Children.Add(legendPanel);

            containerControl.Content = mainGrid;
            return containerControl;
        }

        private StackPanel CreateLegendPanel()
        {
            var legendPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            legendPanel.Children.Add(new TextBlock
            {
                Text = "Database-Computed Correlations: ",
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            var legendItems = new[]
            {
                (Colors.Blue, "-1 (Strong Negative)"),
                (Colors.Cyan, "-0.5 (Negative)"),
                (Colors.White, "0 (No Correlation)"),
                (Colors.Yellow, "0.5 (Positive)"),
                (Colors.Red, "+1 (Strong Positive)")
            };

            foreach (var (color, description) in legendItems)
            {
                legendPanel.Children.Add(new Rectangle
                {
                    Width = 20,
                    Height = 15,
                    Fill = new SolidColorBrush(color),
                    Margin = new Thickness(0, 0, 5, 0)
                });

                legendPanel.Children.Add(new TextBlock
                {
                    Text = description,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 15, 0),
                    FontSize = 10
                });
            }

            return legendPanel;
        }

        private UserControl CreateErrorControl(string message)
        {
            var containerControl = new UserControl();
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brushes.Red,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            stackPanel.Children.Add(messageBlock);
            containerControl.Content = stackPanel;
            return containerControl;
        }

        #endregion

        #region Label Providers for SciChart

        public class FeatureNameLabelProvider : LabelProviderBase
        {
            private readonly string[] featureNames;

            public FeatureNameLabelProvider(string[] featureNames)
            {
                this.featureNames = featureNames;
            }

            public override string FormatLabel(IComparable dataValue)
            {
                try
                {
                    int index = (int)Math.Floor(Convert.ToDouble(dataValue) + 0.5);
                    int reversedIndex = featureNames.Length - 1 - index;
                    if (reversedIndex >= 0 && reversedIndex < featureNames.Length)
                    {
                        string name = featureNames[reversedIndex];
                        return name.Length > 12 ? name.Substring(0, 12) + "..." : name;
                    }
                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            public override string FormatCursorLabel(IComparable dataValue)
            {
                try
                {
                    int index = (int)Math.Floor(Convert.ToDouble(dataValue) + 0.5);
                    int reversedIndex = featureNames.Length - 1 - index;
                    if (reversedIndex >= 0 && reversedIndex < featureNames.Length)
                        return featureNames[reversedIndex];
                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public class ReversedFeatureNameLabelProvider : LabelProviderBase
        {
            private readonly string[] featureNames;

            public ReversedFeatureNameLabelProvider(string[] featureNames)
            {
                this.featureNames = featureNames;
            }

            public override string FormatLabel(IComparable dataValue)
            {
                try
                {
                    int index = (int)Math.Floor(Convert.ToDouble(dataValue) + 0.5);
                    if (index >= 0 && index < featureNames.Length)
                    {
                        int reversedIndex = featureNames.Length - 1 - index;
                        string name = featureNames[reversedIndex];
                        return name.Length > 12 ? name.Substring(0, 12) + "..." : name;
                    }
                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            public override string FormatCursorLabel(IComparable dataValue)
            {
                try
                {
                    int index = (int)Math.Floor(Convert.ToDouble(dataValue) + 0.5);
                    if (index >= 0 && index < featureNames.Length)
                    {
                        int reversedIndex = featureNames.Length - 1 - index;
                        return featureNames[reversedIndex];
                    }
                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        #endregion
    }
}
