using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SciChart.Charting.Model.DataSeries;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.Commands;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class VisualisationViewModel : INotifyPropertyChanged, IDisposable
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

        // Database connection info
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
            GenerateHistogramsCommand = new AsyncRelayCommand(async _ => {
                Console.WriteLine("=== GenerateHistogramsCommand executed ===");
                await GenerateHistogramsAsync();
            }, _ => CanGenerateHistograms());
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
            Console.WriteLine("=== Starting GenerateHistogramPreviewsAsync ===");

            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || _columns == null)
            {
                Console.WriteLine("✗ Database connection not configured");
                _dialogService.ShowErrorDialog("Database connection not configured.", "Configuration Error");
                return;
            }

            Console.WriteLine($"✓ Configuration: Table={_tableName}, Columns={_columns?.Length}");

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            IsGeneratingHistograms = true;
            Histograms.Clear();

            try
            {
                ProgressMessage = "Getting table schema...";
                Console.WriteLine("Retrieving table schema...");
                var schema = await _databaseAnalytics.GetTableSchemaAsync(_connectionString, _tableName);
                Console.WriteLine($"✓ Schema retrieved: {schema.Count} columns found");

                var availableColumns = _columns.Where(col => schema.Any(s => s.ColumnName.Equals(col, StringComparison.OrdinalIgnoreCase))).ToArray();
                Console.WriteLine($"✓ Available columns: {string.Join(", ", availableColumns)}");

                if (!availableColumns.Any())
                {
                    _dialogService.ShowErrorDialog("No valid columns found for visualization.", "Data Error");
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
                            Console.WriteLine($"Added numeric column: {column} (Type: {columnSchema.DataType})");
                        }
                        else if (IsTextType(columnSchema.DataType))
                        {
                            categoricalColumns.Add(column);
                        }
                    }
                }

                var totalColumns = numericColumns.Count + categoricalColumns.Count;
                var processedColumns = 0;

                // Process numeric columns first
                Console.WriteLine($"Starting to process {numericColumns.Count} numeric columns");
                foreach (var column in numericColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    processedColumns++;
                    ProgressMessage = $"Generating histogram for {column} ({processedColumns}/{totalColumns})...";
                    Console.WriteLine($"Processing column: {column}");

                    try
                    {
                        var histogram = await CreateNumericHistogramAsync(column, cancellationToken);
                        if (histogram != null)
                        {
                            Histograms.Add(histogram);
                            Console.WriteLine($"✓ Successfully created histogram for {column}");
                        }
                        else
                        {
                            Console.WriteLine($"✗ Failed to create histogram for {column} (returned null)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Exception while processing {column}: {ex.GetType().Name} - {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"  Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                        }
                    }

                    await Task.Delay(50, cancellationToken);
                }

                // Process categorical columns
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
                // Operation was cancelled by user
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
            Console.WriteLine("=== GenerateHistogramsAsync called ===");
            try
            {
                await GenerateHistogramPreviewsAsync();
                Console.WriteLine("=== GenerateHistogramPreviewsAsync completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== Exception in GenerateHistogramsAsync: {ex.GetType().Name} - {ex.Message} ===");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"=== Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message} ===");
                }
                throw;
            }
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
                // Test 1: Simple connection
                _dialogService.ShowInfoDialog("Testing database connection...", "Test");

                var connectionTest = await _databaseAnalytics.TestConnectionAsync(_connectionString);
                if (!connectionTest)
                {
                    _dialogService.ShowErrorDialog("Connection test failed", "Test Error");
                    return;
                }

                // Test 2: Table schema
                _dialogService.ShowInfoDialog("Testing schema retrieval...", "Test");

                var schema = await _databaseAnalytics.GetTableSchemaAsync(_connectionString, _tableName);
                _dialogService.ShowInfoDialog($"Schema test passed. Found {schema.Count} columns", "Test Success");

                // Test 3: Debug histogram steps on first numeric column
                if (schema.Any())
                {
                    var firstNumericColumn = schema.FirstOrDefault(s => IsNumericType(s.DataType))?.ColumnName;
                    if (firstNumericColumn != null)
                    {
                        _dialogService.ShowInfoDialog($"Testing histogram steps on column: {firstNumericColumn}", "Histogram Test");

                        var histogramDebugInfo = await _databaseAnalytics.DebugHistogramStepsAsync(_connectionString, _tableName, firstNumericColumn);
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
                    Console.WriteLine($"No histogram data returned for column: {columnName}");

                    // Debug basic operations
                    var debugInfo = await _databaseAnalytics.DebugBasicOperationsAsync(
                        _connectionString!, _tableName!, columnName);
                    Console.WriteLine($"Debug info for {columnName}:");
                    Console.WriteLine(debugInfo);

                    return null;
                }

                // Create data series for SciChart
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

                // Get column statistics for the summary
                var columnStats = await GetColumnStatisticsAsync(columnName);
                var statistics = CreateNumericStatisticalSummary(columnStats, histogramData.Sum(h => h.Count));

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
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating numeric histogram for {columnName}: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
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

                // Create data series for SciChart
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

                // Create statistics for categorical data
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating categorical histogram for {columnName}: {ex.Message}");
                return null;
            }
        }

        private async Task<ColumnStatistics?> GetColumnStatisticsAsync(string columnName)
        {
            try
            {
                var stats = await _databaseAnalytics.GetColumnStatisticsAsync(
                    _connectionString!, _tableName!, new[] { columnName }, _whereClause);
                return stats.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private StatisticalSummary CreateNumericStatisticalSummary(ColumnStatistics? columnStats, long totalCount)
        {
            if (columnStats == null)
            {
                return new StatisticalSummary();
            }

            return new StatisticalSummary
            {
                Mean = columnStats.Mean ?? 0,
                Median = 0, // Would need additional query to calculate
                StandardDeviation = columnStats.StandardDeviation ?? 0,
                Variance = columnStats.Variance ?? 0,
                Min = columnStats.Min ?? 0,
                Max = columnStats.Max ?? 0,
                Range = (columnStats.Max ?? 0) - (columnStats.Min ?? 0),
                Q1 = 0, // Would need additional query
                Q3 = 0, // Would need additional query
                IQR = 0, // Would need additional query
                Skewness = 0, // Would need additional query
                Kurtosis = 0, // Would need additional query
                UniqueValues = (int)(columnStats.UniqueCount ?? 0),
                MissingValues = 0, // Could get from missing value analysis
                MostFrequentValue = "",
                MostFrequentCount = 0
            };
        }

        private StatisticalSummary CreateCategoricalStatisticalSummary(List<CategoryFrequency> categoryData)
        {
            var mostFrequent = categoryData.FirstOrDefault();
            var totalCount = categoryData.Sum(c => c.Count);

            return new StatisticalSummary
            {
                UniqueValues = categoryData.Count,
                MissingValues = 0, // Could get from missing value analysis
                MostFrequentValue = mostFrequent?.Category ?? "",
                MostFrequentCount = (int)(mostFrequent?.Count ?? 0),
                // Other numeric stats don't apply to categorical data
                Mean = 0,
                Median = 0,
                StandardDeviation = 0,
                Variance = 0,
                Min = 0,
                Max = 0,
                Range = 0,
                Q1 = 0,
                Q3 = 0,
                IQR = 0,
                Skewness = 0,
                Kurtosis = 0
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

                // If this is a preview-only histogram, load full data
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

                // Reload with more detailed data if needed
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

        #region IDisposable

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    #region Supporting Classes

    public class HistogramViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isLoading;

        public string ColumnName { get; set; } = string.Empty;
        public string ColumnType { get; set; } = string.Empty;
        public List<HistogramBin> Bins { get; set; } = new();
        public int TotalCount { get; set; }
        public IDataSeries DataSeries { get; set; } = null!;
        public StatisticalSummary Statistics { get; set; } = new();
        public bool IsPreviewOnly { get; set; } = false;
        public PreviewInfo PreviewInfo { get; set; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class StatisticalSummary
    {
        public double Mean { get; set; }
        public double Median { get; set; }
        public double StandardDeviation { get; set; }
        public double Variance { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Range { get; set; }
        public double Q1 { get; set; }
        public double Q3 { get; set; }
        public double IQR { get; set; }
        public double Skewness { get; set; }
        public double Kurtosis { get; set; }
        public int UniqueValues { get; set; }
        public int MissingValues { get; set; }
        public string MostFrequentValue { get; set; } = string.Empty;
        public int MostFrequentCount { get; set; }
    }

    public class HistogramBin
    {
        public double BinStart { get; set; }
        public double BinEnd { get; set; }
        public double BinCenter { get; set; }
        public int Count { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int BinIndex { get; set; }
    }

    public class PreviewInfo
    {
        public int SampleSize { get; set; }
        public int NonNullCount { get; set; }
        public int UniqueValueCount { get; set; }
        public int MissingCount { get; set; }
        public double MissingPercentage => SampleSize > 0 ? (double)MissingCount / SampleSize * 100 : 0;
        public double DataQuality => SampleSize > 0 ? (double)NonNullCount / SampleSize * 100 : 0;
    }

    #endregion
}