using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using OutlierDetectionMethod = D2G.Iris.ML.ConfigUI.WPF.Services.OutlierDetectionMethod;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class OutlierDetectionViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;
        private ObservableCollection<OutlierDetectionResult> _outlierResults;
        private ObservableCollection<OutlierSummaryResult> _summaryResults;
        private ObservableCollection<string> _availableColumns;
        private OutlierDetectionMethod _selectedMethod;
        private bool _isAnalyzing;
        private string _analysisMessage = string.Empty;
        private bool _removeOutliersEnabled = false;
        private double _zScoreThreshold = 3.0;
        private double _iqrMultiplier = 1.5;
        private double _modifiedZScoreThreshold = 3.5;
        private double _winsorLowerPercentile = 5.0;
        private double _winsorUpperPercentile = 95.0;
        private bool _applyWinsorization = false;
        private bool _isApplyingWinsorization = false;
        private string _winsorizationProgress = string.Empty;

        private string? _connectionString;
        private string? _tableName;
        private string[]? _columns;
        private string? _targetColumn;
        private string? _whereClause;
        private string? _cleanedTableName;
        private bool _isViewCreated = false;

        public OutlierDetectionViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _outlierResults = new ObservableCollection<OutlierDetectionResult>();
            _summaryResults = new ObservableCollection<OutlierSummaryResult>();
            _availableColumns = new ObservableCollection<string>();
            _selectedMethod = OutlierDetectionMethod.ZScore;

            DetectOutliersCommand = new AsyncRelayCommand(async _ => await DetectOutliersAsync());
            RemoveOutliersCommand = new AsyncRelayCommand(async _ => await RemoveOutliersAsync(), _ => CanRemoveOutliers());
            ApplyWinsorizationCommand = new AsyncRelayCommand(async _ => await ApplyWinsorizationAsync(), _ => CanRemoveOutliers());
            SelectAllColumnsCommand = new RelayCommand(_ => SelectAllColumns(), _ => SummaryResults.Any());
            DeselectAllColumnsCommand = new RelayCommand(_ => DeselectAllColumns(), _ => SummaryResults.Any());
        }

        #region Properties

        public ObservableCollection<OutlierDetectionResult> OutlierResults
        {
            get => _outlierResults;
            set => SetProperty(ref _outlierResults, value);
        }

        public ObservableCollection<OutlierSummaryResult> SummaryResults
        {
            get => _summaryResults;
            set => SetProperty(ref _summaryResults, value);
        }

        public ObservableCollection<string> AvailableColumns
        {
            get => _availableColumns;
            set => SetProperty(ref _availableColumns, value);
        }

        public IEnumerable<OutlierDetectionMethod> OutlierDetectionMethods
        {
            get => Enum.GetValues<OutlierDetectionMethod>();
        }

        public OutlierDetectionMethod SelectedMethod
        {
            get => _selectedMethod;
            set => SetProperty(ref _selectedMethod, value);
        }

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set => SetProperty(ref _isAnalyzing, value);
        }

        public string AnalysisMessage
        {
            get => _analysisMessage;
            set => SetProperty(ref _analysisMessage, value);
        }

        public double ZScoreThreshold
        {
            get => _zScoreThreshold;
            set => SetProperty(ref _zScoreThreshold, value);
        }

        public double IQRMultiplier
        {
            get => _iqrMultiplier;
            set => SetProperty(ref _iqrMultiplier, value);
        }

        public double ModifiedZScoreThreshold
        {
            get => _modifiedZScoreThreshold;
            set => SetProperty(ref _modifiedZScoreThreshold, value);
        }

        public bool RemoveOutliersEnabled
        {
            get => _removeOutliersEnabled;
            set => SetProperty(ref _removeOutliersEnabled, value);
        }

        public double WinsorLowerPercentile
        {
            get => _winsorLowerPercentile;
            set
            {
                var clampedValue = Math.Max(0.1, Math.Min(value, 99.8));
                SetProperty(ref _winsorLowerPercentile, clampedValue);
            }
        }

        public double WinsorUpperPercentile
        {
            get => _winsorUpperPercentile;
            set
            {
                var clampedValue = Math.Max(0.2, Math.Min(value, 99.9));
                SetProperty(ref _winsorUpperPercentile, clampedValue);
            }
        }

        public bool ApplyWinsorization
        {
            get => _applyWinsorization;
            set => SetProperty(ref _applyWinsorization, value);
        }

        public bool IsApplyingWinsorization
        {
            get => _isApplyingWinsorization;
            set => SetProperty(ref _isApplyingWinsorization, value);
        }

        public string WinsorizationProgress
        {
            get => _winsorizationProgress;
            set => SetProperty(ref _winsorizationProgress, value);
        }

        private double _winsorizationProgressPercentage;
        public double WinsorizationProgressPercentage
        {
            get => _winsorizationProgressPercentage;
            set => SetProperty(ref _winsorizationProgressPercentage, value);
        }

        private string _currentColumnBeingProcessed = string.Empty;
        public string CurrentColumnBeingProcessed
        {
            get => _currentColumnBeingProcessed;
            set => SetProperty(ref _currentColumnBeingProcessed, value);
        }

        private string _currentStepDescription = string.Empty;
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }

        #endregion

        #region Commands

        public ICommand DetectOutliersCommand { get; }
        public ICommand RemoveOutliersCommand { get; }
        public ICommand ApplyWinsorizationCommand { get; }
        public ICommand SelectAllColumnsCommand { get; }
        public ICommand DeselectAllColumnsCommand { get; }

        #endregion

        #region Public Methods

        public void SetDatabaseConnection(string connectionString, string tableName, string[] columns, string? targetColumn, string? whereClause)
        {
            _connectionString = connectionString;
            _tableName = tableName;
            _columns = columns;
            _targetColumn = targetColumn;
            _whereClause = whereClause;
            _cleanedTableName = null;

            UpdateAvailableColumns();
        }

        public bool HasOutliersBeenRemoved()
        {
            return !string.IsNullOrEmpty(_cleanedTableName) || RemoveOutliersEnabled;
        }

        public DatabaseDataInfo? GetCleanedDataInfo()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || _columns == null)
                return null;

            return new DatabaseDataInfo
            {
                ConnectionString = _connectionString,
                TableName = !string.IsNullOrEmpty(_cleanedTableName) ? _cleanedTableName : _tableName,
                Columns = _columns,
                WhereClause = _whereClause,
                IsCleanedData = HasOutliersBeenRemoved()
            };
        }

        public Microsoft.ML.IDataView? GetCleanedDataView()
        {
            return null;
        }

        public bool IsUsingInMemoryData()
        {
            return false;
        }

        public void SelectAllColumns()
        {
            foreach (var summary in SummaryResults)
            {
                summary.IsSelectedForRemoval = true;
            }
        }

        public void DeselectAllColumns()
        {
            foreach (var summary in SummaryResults)
            {
                summary.IsSelectedForRemoval = false;
            }
        }

        #endregion

        #region Private Methods

        private void UpdateAvailableColumns()
        {
            AvailableColumns.Clear();

            if (_columns == null) return;

            foreach (var column in _columns)
            {
                if (!string.IsNullOrEmpty(_targetColumn) && column.Equals(_targetColumn, StringComparison.OrdinalIgnoreCase))
                    continue;

                AvailableColumns.Add(column);
            }
        }

        private bool CanRemoveOutliers()
        {
            return OutlierResults.Any() && !IsAnalyzing;
        }

        private async Task DetectOutliersAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || _columns == null)
            {
                _dialogService.ShowErrorDialog("Database connection not configured.", "Error");
                return;
            }

            try
            {
                IsAnalyzing = true;
                AnalysisMessage = "Detecting outliers...";
                OutlierResults.Clear();
                SummaryResults.Clear();

                // Get all columns, excluding target column if specified (for clustering, target is empty so include all)
                var numericColumns = _columns.Where(c =>
                    string.IsNullOrEmpty(_targetColumn) || !c.Equals(_targetColumn, StringComparison.OrdinalIgnoreCase)
                ).ToArray();

                if (!numericColumns.Any())
                {
                    _dialogService.ShowInfoDialog("No numeric columns available for outlier detection.", "Information");
                    return;
                }

                foreach (var column in numericColumns)
                {
                    AnalysisMessage = $"Analyzing column: {column}...";
                    await Task.Delay(50);

                    var outliers = await _databaseAnalytics.DetectOutliersAsync(
                        _connectionString, _tableName, column, SelectedMethod, GetThresholdForMethod(), _whereClause);

                    var totalRows = await GetRowCountAsync(column);
                    var outlierCount = outliers.Count;
                    var percentage = totalRows > 0 ? (double)outlierCount / totalRows * 100 : 0;

                    foreach (var outlier in outliers)
                    {
                        OutlierResults.Add(new OutlierDetectionResult
                        {
                            RowIndex = Convert.ToInt32(outlier.RowId),
                            Value = outlier.Value,
                            Score = outlier.Score,
                            Method = SelectedMethod.ToString(),
                            ColumnName = column,
                            Severity = CalculateSeverity(outlier.Score)
                        });
                    }

                    SummaryResults.Add(new OutlierSummaryResult
                    {
                        ColumnName = column,
                        TotalValues = (int)totalRows,
                        OutlierCount = outlierCount,
                        OutlierPercentage = percentage,
                        Method = SelectedMethod.ToString(),
                        MaxScore = outliers.Any() ? outliers.Max(o => o.Score) : 0,
                        Status = GetColumnStatus(percentage)
                    });
                }

                _dialogService.ShowInfoDialog($"Outlier detection completed. Found {OutlierResults.Count} outliers across {numericColumns.Length} columns.", "Analysis Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error detecting outliers: {ex.Message}", "Error");
                Console.WriteLine($"Outlier detection error: {ex}");
            }
            finally
            {
                IsAnalyzing = false;
                AnalysisMessage = string.Empty;
            }
        }

        private async Task<long> GetRowCountAsync(string column)
        {
            try
            {
                var query = BuildRowCountQuery(_tableName!, column, _whereClause);
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
            catch
            {
                return 0;
            }
        }

        private string BuildRowCountQuery(string tableName, string columnName, string? whereClause)
        {
            var whereCondition = !string.IsNullOrWhiteSpace(whereClause) ? $"WHERE ({whereClause}) AND" : "WHERE";
            return $"SELECT COUNT(*) FROM {tableName} {whereCondition} [{columnName}] IS NOT NULL AND ISNUMERIC([{columnName}]) = 1";
        }

        private async Task RemoveOutliersAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || !OutlierResults.Any())
            {
                _dialogService.ShowErrorDialog("No outliers to remove.", "Error");
                return;
            }

            var selectedColumns = SummaryResults
                .Where(s => s.IsSelectedForRemoval)
                .Select(s => s.ColumnName)
                .ToHashSet();

            if (!selectedColumns.Any())
            {
                _dialogService.ShowInfoDialog("Please select at least one column for outlier removal.", "No Columns Selected");
                return;
            }

            try
            {
                IsAnalyzing = true;
                AnalysisMessage = "Removing outliers from selected columns...";

                _cleanedTableName = await CreateCleanedTableAsync(selectedColumns);

                if (!string.IsNullOrEmpty(_cleanedTableName))
                {
                    _dialogService.ShowInfoDialog($"Outliers removed from {selectedColumns.Count} columns.", "Outliers Removed");

                    OutlierResults.Clear();
                    foreach (var summary in SummaryResults.Where(s => selectedColumns.Contains(s.ColumnName)))
                    {
                        summary.OutlierCount = 0;
                        summary.OutlierPercentage = 0;
                        summary.Status = "Cleaned";
                        summary.IsSelectedForRemoval = false;
                    }
                    RemoveOutliersEnabled = true;
                }
                else
                {
                    _dialogService.ShowErrorDialog("Failed to remove outliers.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error removing outliers: {ex.Message}", "Error");
                Console.WriteLine($"Outlier removal error: {ex}");
            }
            finally
            {
                IsAnalyzing = false;
                AnalysisMessage = string.Empty;
            }
        }

        private async Task ApplyWinsorizationAsync()
        {
            Console.WriteLine("=== WINSORIZATION STARTED ===");
            System.Diagnostics.Debug.WriteLine("=== WINSORIZATION STARTED ===");

            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || !OutlierResults.Any())
            {
                var error = "No data or outliers available for winsorization.";
                Console.WriteLine($"ERROR: {error}");
                System.Diagnostics.Debug.WriteLine($"ERROR: {error}");
                _dialogService.ShowErrorDialog(error, "Error");
                return;
            }

            Console.WriteLine($"Connection string (partial): {_connectionString?.Substring(0, Math.Min(50, _connectionString?.Length ?? 0))}...");
            Console.WriteLine($"Table name: {_tableName}");
            Console.WriteLine($"Outlier results count: {OutlierResults.Count}");
            System.Diagnostics.Debug.WriteLine($"Table name: {_tableName}");
            System.Diagnostics.Debug.WriteLine($"Outlier results count: {OutlierResults.Count}");

            var selectedColumns = SummaryResults
                .Where(s => s.IsSelectedForRemoval)
                .Select(s => s.ColumnName)
                .ToHashSet();

            if (!selectedColumns.Any())
            {
                _dialogService.ShowInfoDialog("Please select at least one column for winsorization.", "No Columns Selected");
                return;
            }

            try
            {
                IsApplyingWinsorization = true;
                WinsorizationProgressPercentage = 0;
                WinsorizationProgress = "Starting winsorization process...";
                CurrentStepDescription = "Initializing winsorization";
                CurrentColumnBeingProcessed = "";

                var dataSize = await EstimateDataSizeAsync();
                Console.WriteLine($"Estimated data size: {dataSize} rows");
                Console.WriteLine("Using database view approach (zero storage overhead, scales to any size)");

                CurrentStepDescription = "Estimating dataset size";
                WinsorizationProgressPercentage = 5;

                _cleanedTableName = await CreateWinsorizedViewAsync(selectedColumns);
                _isViewCreated = !string.IsNullOrEmpty(_cleanedTableName);

                if (!string.IsNullOrEmpty(_cleanedTableName))
                {
                    _dialogService.ShowInfoDialog($"Winsorization completed for {selectedColumns.Count} columns.", "Winsorization Complete");

                    OutlierResults.Clear();
                    foreach (var summary in SummaryResults.Where(s => selectedColumns.Contains(s.ColumnName)))
                    {
                        summary.OutlierCount = 0;
                        summary.OutlierPercentage = 0;
                        summary.Status = "Winsorized";
                        summary.IsSelectedForRemoval = false;
                    }
                    RemoveOutliersEnabled = true;
                }
                else
                {
                    _dialogService.ShowErrorDialog("Failed to apply winsorization.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error during winsorization: {ex.Message}", "Error");
                Console.WriteLine($"Winsorization error: {ex}");
            }
            finally
            {
                IsApplyingWinsorization = false;
                WinsorizationProgress = string.Empty;
                WinsorizationProgressPercentage = 0;
                CurrentStepDescription = string.Empty;
                CurrentColumnBeingProcessed = string.Empty;
            }
        }

        private async Task<string?> CreateCleanedTableAsync(HashSet<string> selectedColumns)
        {
            var guidPart = Guid.NewGuid().ToString("N")[..8];
            var viewName = $"vw_CleanedData_{DateTime.Now:yyyyMMdd_HHmmss}_{guidPart}";

            try
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Build outlier threshold ranges per column (based on detection method used)
                var columnThresholds = new Dictionary<string, (double lowerBound, double upperBound)>();

                foreach (var column in selectedColumns)
                {
                    var columnOutliers = OutlierResults
                        .Where(r => r.ColumnName == column)
                        .ToList();

                    if (!columnOutliers.Any())
                        continue;

                    // Calculate threshold boundaries based on outlier detection method
                    // Use percentile-based approach to find the boundary between normal and outlier values
                    var baseCondition = $"[{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1";
                    var fullCondition = string.IsNullOrEmpty(_whereClause) ? baseCondition : $"({_whereClause}) AND {baseCondition}";

                    // Get the 5th and 95th percentiles as safe boundaries
                    // (values outside these boundaries are likely outliers)
                    var percentilesSql = $@"
                        SELECT DISTINCT
                            PERCENTILE_CONT(0.05) WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER() as LowerBound,
                            PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER() as UpperBound
                        FROM {_tableName}
                        WHERE {fullCondition}";

                    using var percentilesCommand = new Microsoft.Data.SqlClient.SqlCommand(percentilesSql, connection);
                    percentilesCommand.CommandTimeout = 120;
                    using var reader = await percentilesCommand.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        var lowerBound = Convert.ToDouble(reader["LowerBound"]);
                        var upperBound = Convert.ToDouble(reader["UpperBound"]);

                        // Expand boundaries slightly to include edge cases
                        var range = upperBound - lowerBound;
                        var margin = range * 0.1; // 10% margin

                        columnThresholds[column] = (lowerBound - margin, upperBound + margin);
                    }
                }

                if (!columnThresholds.Any())
                    return null;

                // Get all table columns
                var allTableColumns = new List<string>();
                string schemaName = "dbo";
                string actualTableName = _tableName;

                var cleanedTableName = _tableName.Replace("[", "").Replace("]", "");
                if (cleanedTableName.Contains('.'))
                {
                    var parts = cleanedTableName.Split('.');
                    if (parts.Length == 2)
                    {
                        schemaName = parts[0];
                        actualTableName = parts[1];
                    }
                    else
                    {
                        actualTableName = parts[0];
                    }
                }
                else
                {
                    actualTableName = cleanedTableName;
                }

                var schemaQuery = $@"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = '{schemaName}'
                    AND TABLE_NAME = '{actualTableName}'
                    ORDER BY ORDINAL_POSITION";

                using (var schemaCommand = new Microsoft.Data.SqlClient.SqlCommand(schemaQuery, connection))
                {
                    using var schemaReader = await schemaCommand.ExecuteReaderAsync();
                    while (await schemaReader.ReadAsync())
                    {
                        allTableColumns.Add(schemaReader.GetString(0));
                    }
                }

                // Build SELECT with outlier filtering using efficient range checks
                var selectColumns = new List<string>();
                foreach (var col in allTableColumns)
                {
                    if (columnThresholds.ContainsKey(col))
                    {
                        var (lowerBound, upperBound) = columnThresholds[col];

                        // Use simple range check instead of checking each outlier value
                        // This is MUCH faster: O(1) instead of O(n) per row
                        selectColumns.Add($@"
                            CASE
                                WHEN ISNUMERIC([{col}]) = 1 AND (
                                    CAST([{col}] AS FLOAT) < {lowerBound} OR
                                    CAST([{col}] AS FLOAT) > {upperBound}
                                ) THEN NULL
                                ELSE [{col}]
                            END AS [{col}]");
                    }
                    else
                    {
                        selectColumns.Add($"[{col}]");
                    }
                }

                var whereCondition = string.IsNullOrEmpty(_whereClause) ? "" : $"WHERE {_whereClause}";
                var createViewSql = $@"
                    CREATE VIEW {viewName} AS
                    SELECT {string.Join(",\n                           ", selectColumns)}
                    FROM {_tableName}
                    {whereCondition}";

                using var command = new Microsoft.Data.SqlClient.SqlCommand(createViewSql, connection);
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();

                _isViewCreated = true;

                return viewName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating cleaned view: {ex.Message}");
                return null;
            }
        }

        private double GetThresholdForMethod()
        {
            return SelectedMethod switch
            {
                OutlierDetectionMethod.ZScore => ZScoreThreshold,
                OutlierDetectionMethod.IQR => IQRMultiplier,
                OutlierDetectionMethod.ModifiedZScore => ModifiedZScoreThreshold,
                _ => 3.0
            };
        }

        private string CalculateSeverity(double score)
        {
            return score switch
            {
                < 2 => "Low",
                < 3 => "Medium",
                < 5 => "High",
                _ => "Extreme"
            };
        }

        private string GetColumnStatus(double outlierPercentage)
        {
            return outlierPercentage switch
            {
                <= 1.0 => "Good",
                <= 5.0 => "Fair",
                <= 10.0 => "Poor",
                _ => "Critical"
            };
        }

        #endregion

        private async Task<long> EstimateDataSizeAsync()
        {
            try
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();

                var whereCondition = string.IsNullOrEmpty(_whereClause) ? "" : $"WHERE {_whereClause}";
                var countSql = $"SELECT COUNT(*) FROM {_tableName} {whereCondition}";

                using var command = new Microsoft.Data.SqlClient.SqlCommand(countSql, connection);
                command.CommandTimeout = 30;
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error estimating data size: {ex.Message}");
                return 0;
            }
        }


        private async Task<string?> CreateWinsorizedViewAsync(HashSet<string> selectedColumns)
        {
            var guidPart = Guid.NewGuid().ToString("N")[..8];
            var viewName = $"vw_WinsorizedData_{DateTime.Now:yyyyMMdd_HHmmss}_{guidPart}";

            try
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();

                var percentileCalculations = new Dictionary<string, (decimal lower, decimal upper)>();
                var totalColumns = selectedColumns.Count;
                var currentColumn = 0;

                Console.WriteLine($"=== Calculating percentiles for {totalColumns} columns ===");
                CurrentStepDescription = "Calculating percentiles for all columns";

                foreach (var column in selectedColumns)
                {
                    currentColumn++;
                    var stepProgress = 10 + ((currentColumn - 1) * 60) / totalColumns;
                    WinsorizationProgressPercentage = stepProgress;

                    Console.WriteLine($"[{currentColumn}/{totalColumns}] ({stepProgress}%) Processing column: {column}");
                    CurrentColumnBeingProcessed = "";
                    CurrentStepDescription = $"Calculating percentiles for {column}";
                    WinsorizationProgress = $"Processing percentiles ({currentColumn} of {totalColumns})";

                    var baseCondition = $"[{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1";
                    var fullCondition = string.IsNullOrEmpty(_whereClause) ? baseCondition : $"({_whereClause}) AND {baseCondition}";

                    CurrentStepDescription = $"Counting valid values in {column}";
                    var countSql = $"SELECT COUNT(*) FROM {_tableName} WHERE {fullCondition}";
                    using (var countCommand = new Microsoft.Data.SqlClient.SqlCommand(countSql, connection))
                    {
                        countCommand.CommandTimeout = 60;
                        var validCount = Convert.ToInt64(countCommand.ExecuteScalar());
                        Console.WriteLine($"  Found {validCount:N0} valid numeric values in column {column}");
                    }

                    CurrentStepDescription = $"Computing percentiles for {column}";
                    var percentilesSql = $@"
                        SELECT DISTINCT
                            PERCENTILE_CONT({WinsorLowerPercentile / 100.0}) WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER() as LowerBound,
                            PERCENTILE_CONT({WinsorUpperPercentile / 100.0}) WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER() as UpperBound
                        FROM {_tableName}
                        WHERE {fullCondition}";

                    using var percentilesCommand = new Microsoft.Data.SqlClient.SqlCommand(percentilesSql, connection);
                    percentilesCommand.CommandTimeout = 120;
                    using var reader = await percentilesCommand.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        var lowerBound = Convert.ToDecimal(reader["LowerBound"]);
                        var upperBound = Convert.ToDecimal(reader["UpperBound"]);
                        percentileCalculations[column] = (lowerBound, upperBound);

                        Console.WriteLine($"  Percentiles: {WinsorLowerPercentile}% = {lowerBound:F4}, {WinsorUpperPercentile}% = {upperBound:F4}");
                        Console.WriteLine($"  Column {column} percentiles calculated successfully");
                        CurrentStepDescription = $"Completed {column} - Lower: {lowerBound:F4}, Upper: {upperBound:F4}";

                        await Task.Delay(100);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: No data found for column {column}");
                        CurrentStepDescription = $"No data found for {column}";
                    }
                }

                WinsorizationProgressPercentage = 75;
                CurrentStepDescription = "Building winsorization view SQL";
                CurrentColumnBeingProcessed = "";
                WinsorizationProgress = "Generating view definition with winsorization logic";

                var allTableColumns = new List<string>();

                string schemaName = "dbo";
                string actualTableName = _tableName;

                var cleanedTableName = _tableName.Replace("[", "").Replace("]", "");
                if (cleanedTableName.Contains('.'))
                {
                    var parts = cleanedTableName.Split('.');
                    if (parts.Length == 2)
                    {
                        schemaName = parts[0];
                        actualTableName = parts[1];
                    }
                    else if (parts.Length == 1)
                    {
                        actualTableName = parts[0];
                    }
                }
                else
                {
                    actualTableName = cleanedTableName;
                }

                Console.WriteLine($"Querying INFORMATION_SCHEMA for schema='{schemaName}', table='{actualTableName}'");

                var schemaQuery = $@"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = '{schemaName}'
                    AND TABLE_NAME = '{actualTableName}'
                    ORDER BY ORDINAL_POSITION";

                using (var schemaCommand = new Microsoft.Data.SqlClient.SqlCommand(schemaQuery, connection))
                {
                    using var schemaReader = await schemaCommand.ExecuteReaderAsync();
                    while (await schemaReader.ReadAsync())
                    {
                        allTableColumns.Add(schemaReader.GetString(0));
                    }
                }

                var selectColumns = new List<string>();
                foreach (var col in allTableColumns)
                {
                    if (selectedColumns.Contains(col) && percentileCalculations.ContainsKey(col))
                    {
                        var (lower, upper) = percentileCalculations[col];
                        selectColumns.Add($@"
                            CASE
                                WHEN ISNUMERIC([{col}]) = 1 AND CAST([{col}] AS FLOAT) < {lower} THEN {lower}
                                WHEN ISNUMERIC([{col}]) = 1 AND CAST([{col}] AS FLOAT) > {upper} THEN {upper}
                                ELSE [{col}]
                            END AS [{col}]");
                    }
                    else
                    {
                        selectColumns.Add($"[{col}]");
                    }
                }

                var whereCondition = string.IsNullOrEmpty(_whereClause) ? "" : $"WHERE {_whereClause}";
                var createViewSql = $@"
                    CREATE VIEW {viewName} AS
                    SELECT {string.Join(",\n                           ", selectColumns)}
                    FROM {_tableName}
                    {whereCondition}";

                WinsorizationProgressPercentage = 85;
                CurrentStepDescription = $"Creating database view: {viewName}";
                WinsorizationProgress = "Creating database view with winsorization logic";

                Console.WriteLine($"Creating view: {viewName}");
                using var createCommand = new Microsoft.Data.SqlClient.SqlCommand(createViewSql, connection);
                createCommand.CommandTimeout = 120;
                await createCommand.ExecuteNonQueryAsync();

                WinsorizationProgressPercentage = 95;
                CurrentStepDescription = "Finalizing winsorization process";
                WinsorizationProgress = "View created successfully - finalizing";

                await Task.Delay(500);
                WinsorizationProgressPercentage = 100;
                CurrentStepDescription = "Winsorization completed successfully";

                return viewName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating winsorized view: {ex.Message}");
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupDatabaseObjects();
            }
            base.Dispose(disposing);
        }

        public void CleanupView()
        {
            CleanupDatabaseObjects();

            _cleanedTableName = null;
            _isViewCreated = false;
            RemoveOutliersEnabled = false;

            Console.WriteLine("View cleanup completed and state reset");
        }

        private void CleanupDatabaseObjects()
        {
            if (string.IsNullOrEmpty(_cleanedTableName) || string.IsNullOrEmpty(_connectionString) || !_isViewCreated)
                return;

            try
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                connection.Open();

                var dropViewSql = $"DROP VIEW IF EXISTS {_cleanedTableName}";
                using var dropCommand = new Microsoft.Data.SqlClient.SqlCommand(dropViewSql, connection);
                dropCommand.ExecuteNonQuery();
                Console.WriteLine($"Cleaned up view: {_cleanedTableName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not clean up view {_cleanedTableName}: {ex.Message}");
            }
        }
    }

    #region Supporting Classes

    public class OutlierDetectionResult
    {
        public int RowIndex { get; set; }
        public double Value { get; set; }
        public double Score { get; set; }
        public string Method { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    public class OutlierSummaryResult : INotifyPropertyChanged
    {
        private bool _isSelectedForRemoval;

        public string ColumnName { get; set; } = string.Empty;
        public int TotalValues { get; set; }
        public int OutlierCount { get; set; }
        public double OutlierPercentage { get; set; }
        public string Method { get; set; } = string.Empty;
        public double MaxScore { get; set; }
        public string Status { get; set; } = string.Empty;

        public bool IsSelectedForRemoval
        {
            get => _isSelectedForRemoval;
            set
            {
                _isSelectedForRemoval = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    #endregion
}