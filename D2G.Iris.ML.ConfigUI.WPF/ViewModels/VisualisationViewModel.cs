using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SciChart.Charting.Model.DataSeries;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.Commands;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class VisualisationViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;
        private ObservableCollection<HistogramViewModel> _histograms;
        private HistogramViewModel? _selectedHistogram;
        private bool _isDetailViewVisible;
        private bool _isGeneratingHistograms;
        private string _progressMessage = string.Empty;
        private CancellationTokenSource? _cancellationTokenSource;
        private string _dataInfo = string.Empty;

        private string? _connectionString;
        private string? _tableName;
        private string[]? _columns;
        private string? _whereClause;

        public VisualisationViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _histograms = new ObservableCollection<HistogramViewModel>();

            SelectHistogramCommand = new AsyncRelayCommand(async obj => await SelectHistogramAsync(obj as HistogramViewModel));
            BackToOverviewCommand = new RelayCommand(_ => BackToOverview());
            CancelGenerationCommand = new RelayCommand(_ => CancelGeneration());
            GenerateHistogramsCommand = new AsyncRelayCommand(async _ => await GenerateHistogramsAsync(), _ => CanGenerateHistograms());
        }

        #region Properties

        public ObservableCollection<HistogramViewModel> Histograms
        {
            get => _histograms;
            set => SetProperty(ref _histograms, value);
        }

        public HistogramViewModel? SelectedHistogram
        {
            get => _selectedHistogram;
            set => SetProperty(ref _selectedHistogram, value);
        }

        public bool IsDetailViewVisible
        {
            get => _isDetailViewVisible;
            set => SetProperty(ref _isDetailViewVisible, value);
        }

        public ICommand SelectHistogramCommand { get; }
        public ICommand BackToOverviewCommand { get; }
        public ICommand CancelGenerationCommand { get; }
        public ICommand GenerateHistogramsCommand { get; }

        public string DataInfo
        {
            get => _dataInfo;
            set => SetProperty(ref _dataInfo, value);
        }

        public bool IsGeneratingHistograms
        {
            get => _isGeneratingHistograms;
            set => SetProperty(ref _isGeneratingHistograms, value);
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        #endregion

        #region Public Methods

        public void SetDatabaseConnection(string connectionString, string tableName, string[] columns, string? whereClause = null)
        {
            _connectionString = connectionString;
            _tableName = tableName;
            _columns = columns;
            _whereClause = whereClause;
            UpdateDataInfo();
        }

        public async Task GenerateHistogramPreviewsAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || _columns == null)
            {
                _dialogService.ShowErrorDialog("Database connection not configured.", "Configuration Error");
                return;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            IsGeneratingHistograms = true;
            Histograms.Clear();

            try
            {
                ProgressMessage = "Getting table schema...";
                var schema = await _databaseAnalytics.GetTableSchemaAsync(_connectionString, _tableName);

                var availableColumns = _columns.Where(col => schema.Any(s => s.ColumnName.Equals(col, StringComparison.OrdinalIgnoreCase))).ToArray();

                if (!availableColumns.Any())
                {
                    _dialogService.ShowErrorDialog(
                        $"No valid columns found for visualization.\n\n" +
                        $"Requested {_columns.Length} columns but none exist in table '{_tableName}'.\n" +
                        $"Schema has {schema.Count} columns: {string.Join(", ", schema.Select(s => s.ColumnName).Take(5))}{(schema.Count > 5 ? "..." : "")}",
                        "Data Error");
                    return;
                }

                ProgressMessage = "Analyzing column types...";
                await Task.Delay(100, cancellationToken);

                var numericColumns = new List<string>();
                var categoricalColumns = new List<string>();

                foreach (var column in availableColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var columnSchema = schema.FirstOrDefault(s => s.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase));
                    if (columnSchema != null)
                    {
                        if (IsNumericType(columnSchema.DataType))
                        {
                            numericColumns.Add(column);
                        }
                        else if (IsTextType(columnSchema.DataType))
                        {
                            categoricalColumns.Add(column);
                        }
                    }
                }

                var totalColumns = numericColumns.Count + categoricalColumns.Count;
                var processedColumns = 0;

                foreach (var column in numericColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    processedColumns++;
                    ProgressMessage = $"Generating histogram for {column} ({processedColumns}/{totalColumns})...";

                    var histogram = await CreateNumericHistogramAsync(column, cancellationToken);
                    if (histogram != null)
                    {
                        Histograms.Add(histogram);
                    }

                    await Task.Delay(50, cancellationToken);
                }

                foreach (var column in categoricalColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    processedColumns++;
                    ProgressMessage = $"Generating frequency chart for {column} ({processedColumns}/{totalColumns})...";

                    var histogram = await CreateCategoricalHistogramAsync(column, cancellationToken);
                    if (histogram != null)
                    {
                        Histograms.Add(histogram);
                    }

                    await Task.Delay(50, cancellationToken);
                }

                ProgressMessage = "Histograms generated successfully";
                await Task.Delay(500, cancellationToken);

                _dialogService.ShowInfoDialog(
                    $"Generated {Histograms.Count} visualizations using database-side analytics.\n\n" +
                    $"Numeric columns: {numericColumns.Count}\n" +
                    $"Categorical columns: {categoricalColumns.Count}",
                    "Visualization Complete");
            }
            catch (OperationCanceledException)
            {
                ProgressMessage = "Generation cancelled";
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating histograms: {ex.Message}", "Generation Error");
                Console.WriteLine($"Histogram generation error: {ex}");
            }
            finally
            {
                IsGeneratingHistograms = false;
                ProgressMessage = string.Empty;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        public async Task GenerateHistogramsAsync()
        {
            await GenerateHistogramPreviewsAsync();
        }

        #endregion

        #region Private Methods

        private async Task TestBasicDatabaseOperations()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName))
            {
                _dialogService.ShowErrorDialog("Database not configured", "Test Error");
                return;
            }

            try
            {
                _dialogService.ShowInfoDialog("Testing database connection...", "Test");

                var connectionTest = await _databaseAnalytics.TestConnectionAsync(_connectionString);
                if (!connectionTest)
                {
                    _dialogService.ShowErrorDialog("Connection test failed", "Test Error");
                    return;
                }

                _dialogService.ShowInfoDialog("Testing schema retrieval...", "Test");

                var schema = await _databaseAnalytics.GetTableSchemaAsync(_connectionString, _tableName);
                _dialogService.ShowInfoDialog($"Schema test passed. Found {schema.Count} columns", "Test Success");

                if (schema.Any())
                {
                    var firstNumericColumn = schema.FirstOrDefault(s => IsNumericType(s.DataType))?.ColumnName;
                    if (firstNumericColumn != null)
                    {
                        _dialogService.ShowInfoDialog($"Testing histogram steps on column: {firstNumericColumn}", "Histogram Test");

                        var histogramDebugInfo = await _databaseAnalytics.DebugHistogramStepsAsync(_connectionString, _tableName, firstNumericColumn, 10, _whereClause);
                        _dialogService.ShowInfoDialog($"Histogram debug results:\n{histogramDebugInfo}", "Histogram Debug Results");
                    }
                    else
                    {
                        _dialogService.ShowInfoDialog("No numeric columns found for histogram testing", "Test Info");
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Test failed: {ex.GetType().Name}\n{ex.Message}", "Test Error");
                throw;
            }
        }

        private bool CanGenerateHistograms()
        {
            return !string.IsNullOrEmpty(_connectionString) &&
                   !string.IsNullOrEmpty(_tableName) &&
                   _columns?.Length > 0 &&
                   !IsGeneratingHistograms;
        }

        private void UpdateDataInfo()
        {
            if (!string.IsNullOrEmpty(_connectionString) && _columns?.Length > 0)
            {
                DataInfo = $"Database ready: {_columns.Length} columns available for visualization";
            }
            else
            {
                DataInfo = "No database connection - run data analysis first";
            }
        }

        private async Task<HistogramViewModel?> CreateNumericHistogramAsync(string columnName, CancellationToken cancellationToken)
        {
            try
            {
                const int bins = 20;
                var histogramData = await _databaseAnalytics.GetHistogramDataAsync(
                    _connectionString!, _tableName!, columnName, bins, _whereClause);

                if (!histogramData.Any())
                {
                    return null;
                }

                var dataSeries = new XyDataSeries<double, long>();
                var histogramBins = new List<HistogramBin>();

                foreach (var bin in histogramData.OrderBy(h => h.BinIndex))
                {
                    dataSeries.Append(bin.BinCenter, bin.Count);
                    histogramBins.Add(new HistogramBin
                    {
                        BinStart = bin.BinStart,
                        BinEnd = bin.BinEnd,
                        BinCenter = bin.BinCenter,
                        Count = (int)bin.Count,
                        BinIndex = bin.BinIndex
                    });
                }

                var columnStats = await GetColumnStatisticsAsync(columnName);
                var statistics = CreateNumericStatisticalSummary(columnStats);

                return new HistogramViewModel
                {
                    ColumnName = columnName,
                    ColumnType = "Numeric",
                    Bins = histogramBins,
                    TotalCount = (int)histogramData.Sum(h => h.Count),
                    DataSeries = dataSeries,
                    Statistics = statistics,
                    IsPreviewOnly = false
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<HistogramViewModel?> CreateCategoricalHistogramAsync(string columnName, CancellationToken cancellationToken)
        {
            try
            {
                const int maxCategories = 20;
                var categoryData = await _databaseAnalytics.GetCategoricalDataAsync(
                    _connectionString!, _tableName!, columnName, maxCategories, _whereClause);

                if (!categoryData.Any())
                    return null;

                var dataSeries = new XyDataSeries<double, long>();
                var histogramBins = new List<HistogramBin>();

                for (int i = 0; i < categoryData.Count; i++)
                {
                    var category = categoryData[i];
                    dataSeries.Append(i + 0.5, category.Count);
                    histogramBins.Add(new HistogramBin
                    {
                        BinStart = i,
                        BinEnd = i + 1,
                        BinCenter = i + 0.5,
                        Count = (int)category.Count,
                        CategoryName = category.Category,
                        BinIndex = i
                    });
                }

                var statistics = CreateCategoricalStatisticalSummary(categoryData);

                return new HistogramViewModel
                {
                    ColumnName = columnName,
                    ColumnType = "Categorical",
                    Bins = histogramBins,
                    TotalCount = (int)categoryData.Sum(c => c.Count),
                    DataSeries = dataSeries,
                    Statistics = statistics,
                    IsPreviewOnly = false
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<ColumnStatistics?> GetColumnStatisticsAsync(string columnName)
        {
            try
            {
                var stats = await _databaseAnalytics.GetColumnStatisticsAsync(
                    _connectionString!, _tableName!, new[] { columnName }, _whereClause,
                    includePercentiles: true, includeMoments: false);
                return stats.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private StatisticalSummary CreateNumericStatisticalSummary(ColumnStatistics? columnStats)
        {
            if (columnStats == null)
            {
                return new StatisticalSummary();
            }

            var q1 = columnStats.Q1;
            var q3 = columnStats.Q3;
            var median = columnStats.Median;
            var missingValues = columnStats.Count - columnStats.NonNullCount;

            return new StatisticalSummary
            {
                Mean = columnStats.Mean ?? 0,
                Median = median,
                StandardDeviation = columnStats.StandardDeviation ?? 0,
                Variance = columnStats.Variance ?? 0,
                Min = columnStats.Min ?? 0,
                Max = columnStats.Max ?? 0,
                Range = (columnStats.Max ?? 0) - (columnStats.Min ?? 0),
                Q1 = q1,
                Q3 = q3,
                IQR = q3 - q1,
                MissingValues = missingValues
            };
        }

        private StatisticalSummary CreateCategoricalStatisticalSummary(List<CategoryFrequency> categoryData)
        {
            return new StatisticalSummary
            {
                MissingValues = 0,
                Mean = 0,
                Median = 0,
                StandardDeviation = 0,
                Variance = 0,
                Min = 0,
                Max = 0,
                Range = 0,
                Q1 = 0,
                Q3 = 0,
                IQR = 0
            };
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

        private bool IsTextType(string dataType)
        {
            return dataType.ToLower() switch
            {
                "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" => true,
                _ => false
            };
        }

        private async Task SelectHistogramAsync(HistogramViewModel? histogram)
        {
            if (histogram == null) return;

            try
            {
                foreach (var h in Histograms)
                {
                    h.IsSelected = false;
                }

                histogram.IsSelected = true;
                SelectedHistogram = histogram;
                IsDetailViewVisible = true;

                if (histogram.IsPreviewOnly)
                {
                    await LoadFullHistogramAsync(histogram);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error selecting histogram: {ex.Message}", "Error");
            }
        }

        private async Task LoadFullHistogramAsync(HistogramViewModel histogram)
        {
            if (histogram.IsLoading) return;

            try
            {
                histogram.IsLoading = true;

                HistogramViewModel? detailedHistogram = null;

                if (histogram.ColumnType == "Numeric")
                {
                    detailedHistogram = await CreateNumericHistogramAsync(histogram.ColumnName, CancellationToken.None);
                }
                else if (histogram.ColumnType == "Categorical")
                {
                    detailedHistogram = await CreateCategoricalHistogramAsync(histogram.ColumnName, CancellationToken.None);
                }

                if (detailedHistogram != null)
                {
                    detailedHistogram.IsSelected = histogram.IsSelected;

                    var index = Histograms.IndexOf(histogram);
                    if (index >= 0 && index < Histograms.Count)
                    {
                        Histograms[index] = detailedHistogram;
                        if (SelectedHistogram == histogram)
                        {
                            SelectedHistogram = detailedHistogram;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error loading detailed histogram for {histogram.ColumnName}: {ex.Message}", "Error");
            }
            finally
            {
                histogram.IsLoading = false;
            }
        }

        private void BackToOverview()
        {
            IsDetailViewVisible = false;
            if (SelectedHistogram != null)
            {
                SelectedHistogram.IsSelected = false;
            }
            SelectedHistogram = null;
        }

        private void CancelGeneration()
        {
            _cancellationTokenSource?.Cancel();
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}
