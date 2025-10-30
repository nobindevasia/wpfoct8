using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.Visuals.Axes;
using SciChart.Charting.Visuals.Axes.LabelProviders;
using SciChart.Charting.Themes;
using SciChart.Drawing.Common;
using SciChart.Charting.ChartModifiers;
using SciChart.Data.Model;
using SciChart.Core.Extensions;
using Microsoft.ML;
using Microsoft.ML.Data;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.Core.Models;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Data;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ExploratoryDataAnalysisViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;
        private int _numberOfRows;
        private int _numberOfColumns;
        private int _totalMissingValues;
        private double _missingValuesPercentage;
        private ObservableCollection<FeatureTypeInfo> _featureTypes;
        private ObservableCollection<ColumnMissingInfo> _columnMissingValues;
        private Func<DatabaseConfig>? _getDatabaseConfig;
        private Func<List<InputField>>? _getInputFields;
        private Func<string>? _getTargetField;
        private bool _isLoading;
        private string _loadingMessage = "Loading data...";
        private VisualisationViewModel _visualisationViewModel;
        private OutlierDetectionViewModel _outlierDetectionViewModel;
        private ScatterPlotViewModel _scatterPlotViewModel;
        private ViolinPlotViewModel _violinPlotViewModel;
        private BoxPlotViewModel _boxPlotViewModel;
        private QQPlotViewModel _qqPlotViewModel;
        private UserControl? _correlationHeatmapChart;

        private DatasetSummary? _currentDatasetSummary;
        private List<ColumnStatistics>? _currentColumnStatistics;
        private string? _connectionString;
        private string? _tableName;
        private string[]? _enabledColumns;
        private string? _whereClause;

        public ExploratoryDataAnalysisViewModel(
            IDialogService dialogService,
            IDatabaseAnalyticsService databaseAnalytics,
            VisualisationViewModel visualisationViewModel,
            OutlierDetectionViewModel outlierDetectionViewModel,
            ScatterPlotViewModel scatterPlotViewModel,
            ViolinPlotViewModel violinPlotViewModel,
            BoxPlotViewModel boxPlotViewModel,
            QQPlotViewModel qqPlotViewModel)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _featureTypes = new ObservableCollection<FeatureTypeInfo>();
            _columnMissingValues = new ObservableCollection<ColumnMissingInfo>();
            _visualisationViewModel = visualisationViewModel;
            _outlierDetectionViewModel = outlierDetectionViewModel;
            _scatterPlotViewModel = scatterPlotViewModel;
            _violinPlotViewModel = violinPlotViewModel;
            _boxPlotViewModel = boxPlotViewModel;
            _qqPlotViewModel = qqPlotViewModel;

            AnalyzeDataCommand = new AsyncRelayCommand(async _ => await AnalyzeDataAsync(), _ => CanAnalyzeData());
            GenerateCorrelationCommand = new AsyncRelayCommand(async _ => await GenerateCorrelationMatrixAsync(), _ => CanGenerateCorrelation());
        }

        #region Properties

        public int NumberOfRows
        {
            get => _numberOfRows;
            set => SetProperty(ref _numberOfRows, value);
        }

        public int NumberOfColumns
        {
            get => _numberOfColumns;
            set => SetProperty(ref _numberOfColumns, value);
        }

        public int TotalMissingValues
        {
            get => _totalMissingValues;
            set => SetProperty(ref _totalMissingValues, value);
        }

        public double MissingValuesPercentage
        {
            get => _missingValuesPercentage;
            set => SetProperty(ref _missingValuesPercentage, value);
        }

        public ObservableCollection<FeatureTypeInfo> FeatureTypes
        {
            get => _featureTypes;
            set => SetProperty(ref _featureTypes, value);
        }

        public ObservableCollection<ColumnMissingInfo> ColumnMissingValues
        {
            get => _columnMissingValues;
            set => SetProperty(ref _columnMissingValues, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public VisualisationViewModel VisualisationViewModel
        {
            get => _visualisationViewModel;
            set => SetProperty(ref _visualisationViewModel, value);
        }

        public OutlierDetectionViewModel OutlierDetectionViewModel
        {
            get => _outlierDetectionViewModel;
            set => SetProperty(ref _outlierDetectionViewModel, value);
        }

        public ScatterPlotViewModel ScatterPlotViewModel
        {
            get => _scatterPlotViewModel;
            set => SetProperty(ref _scatterPlotViewModel, value);
        }

        public ViolinPlotViewModel ViolinPlotViewModel
        {
            get => _violinPlotViewModel;
            set => SetProperty(ref _violinPlotViewModel, value);
        }

        public BoxPlotViewModel BoxPlotViewModel
        {
            get => _boxPlotViewModel;
            set => SetProperty(ref _boxPlotViewModel, value);
        }

        public QQPlotViewModel QQPlotViewModel
        {
            get => _qqPlotViewModel;
            set => SetProperty(ref _qqPlotViewModel, value);
        }

        public UserControl? CorrelationHeatmapChart
        {
            get => _correlationHeatmapChart;
            set => SetProperty(ref _correlationHeatmapChart, value);
        }

        #endregion

        #region Commands

        public ICommand AnalyzeDataCommand { get; }
        public ICommand GenerateCorrelationCommand { get; }

        #endregion

        #region Public Methods

        public void SetDependencies(Func<DatabaseConfig> getDatabaseConfig, Func<List<InputField>> getInputFields, Func<string>? getTargetField = null)
        {
            _getDatabaseConfig = getDatabaseConfig;
            _getInputFields = getInputFields;
            _getTargetField = getTargetField;
        }

        public bool HasDataBeenLoaded()
        {
            return _currentDatasetSummary != null;
        }

        public bool HasDataBeenCleaned()
        {
            return _outlierDetectionViewModel.HasOutliersBeenRemoved();
        }

        public void CleanupAfterTraining()
        {
            _outlierDetectionViewModel.CleanupView();
        }

        public DatabaseDataInfo? GetDataForTraining()
        {
            if (_connectionString == null || _tableName == null || _enabledColumns == null)
                return null;

            return new DatabaseDataInfo
            {
                ConnectionString = _connectionString,
                TableName = _tableName,
                Columns = _enabledColumns,
                WhereClause = _whereClause,
                IsCleanedData = HasDataBeenCleaned(),
                RowCount = _numberOfRows
            };
        }

        public async Task<IDataView?> GetCleanedDataAsIDataViewAsync(MLContext mlContext, IEnumerable<string> featureColumns, string targetColumn, ModelType modelType)
        {
            if (!HasDataBeenCleaned() || _connectionString == null || _tableName == null)
                return null;

            try
            {
                if (_outlierDetectionViewModel.IsUsingInMemoryData())
                {
                    var cleanedDataView = _outlierDetectionViewModel.GetCleanedDataView();
                    if (cleanedDataView != null)
                    {
                        Console.WriteLine("Using cleaned data from memory");
                        return cleanedDataView;
                    }
                }

                var cleanedDataInfo = _outlierDetectionViewModel.GetCleanedDataInfo();
                if (cleanedDataInfo == null)
                    return null;

                Console.WriteLine("Falling back to database approach for cleaned data");

                var dataLoader = new DatabaseDataLoader();
                var allColumns = featureColumns.Concat(new[] { targetColumn }).ToArray();

                return dataLoader.LoadDataFromSql(
                    _connectionString,
                    cleanedDataInfo.TableName,
                    allColumns,
                    modelType,
                    targetColumn,
                    cleanedDataInfo.WhereClause
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating cleaned IDataView: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Private Methods

        private bool CanAnalyzeData()
        {
            return _getDatabaseConfig != null && _getInputFields != null && !_isLoading;
        }

        private bool CanGenerateCorrelation()
        {
            return _currentColumnStatistics != null &&
                   _currentColumnStatistics.Any(c => IsNumericType(c.DataType)) &&
                   !_isLoading;
        }

        private async Task AnalyzeDataAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Validating configuration...";

                var databaseConfig = _getDatabaseConfig?.Invoke();
                var inputFields = _getInputFields?.Invoke();

                if (databaseConfig == null)
                {
                    _dialogService.ShowErrorDialog("Database configuration is not available.", "Error");
                    return;
                }

                if (inputFields == null || !inputFields.Any())
                {
                    _dialogService.ShowErrorDialog("No input fields are configured.", "Error");
                    return;
                }

                var enabledFields = inputFields.Where(f => f.IsEnabled).ToList();
                if (!enabledFields.Any())
                {
                    _dialogService.ShowErrorDialog("No input fields are enabled for analysis.", "Error");
                    return;
                }

                var sqlHandler = new SqlHandler(databaseConfig.TableName);
                sqlHandler.Connect(databaseConfig);
                _connectionString = sqlHandler.GetConnectionString();
                _tableName = databaseConfig.TableName;
                _whereClause = databaseConfig.WhereClause;

                var targetField = _getTargetField?.Invoke();
                var allFieldsForEDA = enabledFields.Select(f => f.Name).ToList();
                if (!string.IsNullOrEmpty(targetField) && !allFieldsForEDA.Contains(targetField))
                {
                    allFieldsForEDA.Add(targetField);
                }
                _enabledColumns = allFieldsForEDA.ToArray();

                LoadingMessage = "Testing database connection...";
                await Task.Delay(100);

                if (!await _databaseAnalytics.TestConnectionAsync(_connectionString))
                {
                    _dialogService.ShowErrorDialog("Cannot connect to database. Please check your database settings.", "Connection Failed");
                    return;
                }

                LoadingMessage = "Loading dataset summary...";
                await Task.Delay(100);

                _currentDatasetSummary = await _databaseAnalytics.GetDatasetSummaryAsync(
                    _connectionString, _tableName, _enabledColumns, _whereClause);

                NumberOfRows = (int)_currentDatasetSummary.TotalRows;
                NumberOfColumns = _currentDatasetSummary.TotalColumns;

                LoadingMessage = "Analyzing column statistics...";
                await Task.Delay(100);

                _currentColumnStatistics = await _databaseAnalytics.GetColumnStatisticsAsync(
                    _connectionString, _tableName, _enabledColumns, _whereClause);

                LoadingMessage = "Analyzing feature types...";
                await Task.Delay(100);

                await UpdateFeatureTypesAsync();

                LoadingMessage = "Analyzing missing values...";
                await Task.Delay(100);

                var missingValuesInfo = await _databaseAnalytics.GetMissingValuesAnalysisAsync(
                    _connectionString, _tableName, _enabledColumns, _whereClause);

                await UpdateMissingValuesAsync(missingValuesInfo);

                LoadingMessage = "Setting up visualization components...";
                await Task.Delay(100);

                // For Histograms, ViolinPlot and BoxPlot, exclude target variable from columns list
                // These are univariate analysis tools and don't need the target
                var columnsWithoutTarget = string.IsNullOrEmpty(targetField)
                    ? _enabledColumns
                    : _enabledColumns.Where(col => !string.Equals(col, targetField, StringComparison.OrdinalIgnoreCase)).ToArray();

                _visualisationViewModel.SetDatabaseConnection(_connectionString, _tableName, columnsWithoutTarget, _whereClause);

                LoadingMessage = "Setting up outlier detection...";
                await Task.Delay(100);

                _outlierDetectionViewModel.SetDatabaseConnection(_connectionString, _tableName, _enabledColumns, targetField, _whereClause);
                _scatterPlotViewModel.SetDatabaseConnection(_connectionString, _tableName, _enabledColumns, _whereClause);

                _violinPlotViewModel.SetDatabaseConnection(_connectionString, _tableName, columnsWithoutTarget, _whereClause);
                _boxPlotViewModel.SetDatabaseConnection(_connectionString, _tableName, columnsWithoutTarget, _whereClause);
                _qqPlotViewModel.SetDatabaseConnection(_connectionString, _tableName, columnsWithoutTarget, _whereClause);

                _dialogService.ShowInfoDialog(
                    $"Database-side analysis completed successfully for {enabledFields.Count} enabled fields.\n\n" +
                    $"Dataset: {NumberOfRows:N0} rows � {NumberOfColumns} columns\n" +
                    $"Missing values: {TotalMissingValues:N0} ({MissingValuesPercentage:F2}%)",
                    "Analysis Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error analyzing data: {ex.Message}", "Analysis Error");
                Console.WriteLine($"EDA Analysis error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerateCorrelationMatrixAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Calculating correlations...";

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
                    CorrelationHeatmapChart = errorChart;
                    _dialogService.ShowErrorDialog("At least 2 numeric columns are required for correlation analysis.", "Insufficient Data");
                    return;
                }

                LoadingMessage = $"Computing correlations for {numericColumns.Length} numeric columns...";
                await Task.Delay(100);

                var correlationMatrix = await _databaseAnalytics.GetCorrelationMatrixAsync(
                    _connectionString, _tableName, numericColumns, _whereClause);

                LoadingMessage = "Creating correlation heatmap...";
                await Task.Delay(100);

                var correlationChart = CreateCorrelationHeatmap(correlationMatrix);
                CorrelationHeatmapChart = correlationChart;

                _dialogService.ShowInfoDialog("Correlation matrix generated successfully using database analytics.", "Correlation Analysis");
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating correlation matrix: {ex.Message}", "Correlation Error");
                Console.WriteLine($"Correlation error: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateFeatureTypesAsync()
        {
            if (_currentColumnStatistics == null) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var typeGroups = _currentColumnStatistics
                    .GroupBy(c => GetFeatureTypeCategory(c.DataType))
                    .Select(g => new FeatureTypeInfo
                    {
                        Type = g.Key,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Type);

                FeatureTypes.Clear();
                foreach (var typeInfo in typeGroups)
                {
                    FeatureTypes.Add(typeInfo);
                }
            });
        }

        private async Task UpdateMissingValuesAsync(List<MissingValueInfo> missingValuesInfo)
        {
            var totalCells = NumberOfRows * NumberOfColumns;
            var totalMissing = missingValuesInfo.Sum(m => m.MissingCount);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalMissingValues = (int)totalMissing;
                MissingValuesPercentage = totalCells > 0 ? (double)totalMissing / totalCells * 100 : 0;

                ColumnMissingValues.Clear();
                foreach (var info in missingValuesInfo.OrderByDescending(x => x.MissingCount))
                {
                    ColumnMissingValues.Add(new ColumnMissingInfo
                    {
                        ColumnName = info.ColumnName,
                        MissingCount = (int)info.MissingCount,
                        MissingPercentage = info.MissingPercentage
                    });
                }
            });
        }

        private string GetFeatureTypeCategory(string dataType)
        {
            return dataType.ToLower() switch
            {
                "int" or "bigint" or "smallint" or "tinyint" or
                "decimal" or "numeric" or "money" or "smallmoney" or
                "float" or "real" => "Numeric",
                "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" => "Text/Categorical",
                "date" or "time" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" => "Date/Time",
                "bit" => "Boolean",
                _ => "Other"
            };
        }

        private bool IsNumericType(string dataType)
        {
            if (string.IsNullOrEmpty(dataType))
                return false;

            return dataType.ToLower() switch
            {
                "int" or "bigint" or "smallint" or "tinyint" or
                "decimal" or "numeric" or "money" or "smallmoney" or
                "float" or "real" => true,
                _ => false
            };
        }

        private UserControl CreateErrorControl(string message)
        {
            var errorControl = new UserControl();
            var textBlock = new TextBlock
            {
                Text = message,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20)
            };
            errorControl.Content = textBlock;
            return errorControl;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _visualisationViewModel?.Dispose();
                _outlierDetectionViewModel?.Dispose();
                _violinPlotViewModel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Supporting Classes

    public class FeatureTypeInfo
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ColumnMissingInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public int MissingCount { get; set; }
        public double MissingPercentage { get; set; }
    }

    public class DatabaseDataInfo
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string[] Columns { get; set; } = Array.Empty<string>();
        public string? WhereClause { get; set; }
        public bool IsCleanedData { get; set; }
        public int RowCount { get; set; }
    }

    #endregion
}
