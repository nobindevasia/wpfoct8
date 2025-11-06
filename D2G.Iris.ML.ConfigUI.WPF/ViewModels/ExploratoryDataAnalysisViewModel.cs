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
        private CorrelationHeatmapViewModel _correlationHeatmapViewModel;
        private PairPlotViewModel _pairPlotViewModel;

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
            QQPlotViewModel qqPlotViewModel,
            CorrelationHeatmapViewModel correlationHeatmapViewModel,
            PairPlotViewModel pairPlotViewModel)
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
            _correlationHeatmapViewModel = correlationHeatmapViewModel;
            _pairPlotViewModel = pairPlotViewModel;

            AnalyzeDataCommand = new AsyncRelayCommand(async _ => await AnalyzeDataAsync(), _ => CanAnalyzeData());
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

        public CorrelationHeatmapViewModel CorrelationHeatmap => _correlationHeatmapViewModel;

        public PairPlotViewModel PairPlotViewModel
        {
            get => _pairPlotViewModel;
            set => SetProperty(ref _pairPlotViewModel, value);
        }

        #endregion

        #region Commands

        public ICommand AnalyzeDataCommand { get; }

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
                _correlationHeatmapViewModel.SetDatabaseConnection(_connectionString, _tableName, _currentColumnStatistics, _whereClause);
                _pairPlotViewModel.SetDatabaseConnection(_connectionString, _tableName, columnsWithoutTarget, _whereClause);

                _dialogService.ShowInfoDialog(
                    $"Analysis completed successfully for {enabledFields.Count} enabled fields.\n\n" +
                    $"Dataset: {NumberOfRows:N0} rows {NumberOfColumns} columns\n" +
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

        #endregion

        #region IDisposable

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

        #endregion
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
