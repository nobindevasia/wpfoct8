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

                var numericColumns = _columns.Where(c => !string.IsNullOrEmpty(_targetColumn) && !c.Equals(_targetColumn, StringComparison.OrdinalIgnoreCase)).ToArray();

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

        private async Task<string?> CreateWinsorizedTableAsync(HashSet<string> selectedColumns)
        {
            var tempTableName = $"#WinsorizedData_{Guid.NewGuid():N}";

            try
            {
                if (selectedColumns == null || !selectedColumns.Any())
                {
                    throw new ArgumentException("No columns selected for winsorization");
                }

                if (_columns == null || !_columns.Any())
                {
                    throw new ArgumentException("No columns available in the dataset");
                }

                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new ArgumentException("Database connection string is not set");
                }

                if (string.IsNullOrEmpty(_tableName))
                {
                    throw new ArgumentException("Table name is not set");
                }



                if (!selectedColumns.Any())
                {
                    throw new ArgumentException("No columns selected for winsorization");
                }

                var whereCondition = string.IsNullOrEmpty(_whereClause) ? "" : $"WHERE {_whereClause}";


                var createTableSql = $@"
SELECT *
INTO {tempTableName}
FROM {_tableName}
{whereCondition}";

                var updateStatements = new List<string>();
                foreach (var column in selectedColumns)
                {
                    var baseCondition = $"[{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1";
                    var fullCondition = string.IsNullOrEmpty(_whereClause) ? baseCondition : $"({_whereClause}) AND {baseCondition}";

                    var lowerPercentileQuery = $@"
                        (SELECT TOP 1 PERCENTILE_CONT({WinsorLowerPercentile / 100.0})
                         WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER()
                         FROM {_tableName}
                         WHERE {fullCondition})";

                    var upperPercentileQuery = $@"
                        (SELECT TOP 1 PERCENTILE_CONT({WinsorUpperPercentile / 100.0})
                         WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER()
                         FROM {_tableName}
                         WHERE {fullCondition})";

                    updateStatements.Add($@"
UPDATE {tempTableName}
SET [{column}] = CASE
    WHEN CAST([{column}] AS FLOAT) < {lowerPercentileQuery} THEN {lowerPercentileQuery}
    WHEN CAST([{column}] AS FLOAT) > {upperPercentileQuery} THEN {upperPercentileQuery}
    ELSE CAST([{column}] AS FLOAT)
END
WHERE [{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1");
                }

                Console.WriteLine($"=== Winsorisation SQL Debug ===");
                Console.WriteLine($"Selected columns: {string.Join(", ", selectedColumns)}");
                Console.WriteLine($"Table name: {_tableName}");
                Console.WriteLine($"Where clause: '{_whereClause}'");
                Console.WriteLine($"Connection string: {_connectionString?.Substring(0, Math.Min(50, _connectionString?.Length ?? 0))}...");
                Console.WriteLine($"Selected columns for winsorization: {selectedColumns.Count}");
                Console.WriteLine($"Update statements count: {updateStatements.Count}");
                Console.WriteLine($"Temp table name: {tempTableName}");
                Console.WriteLine($"WinsorLowerPercentile: {WinsorLowerPercentile}");
                Console.WriteLine($"WinsorUpperPercentile: {WinsorUpperPercentile}");
                Console.WriteLine($"Create table SQL:\n{createTableSql}");

                if (string.IsNullOrWhiteSpace(createTableSql) || !createTableSql.Contains("SELECT") || !createTableSql.Contains("INTO"))
                {
                    Console.WriteLine("ERROR: Invalid create table SQL generated!");
                    throw new ArgumentException("Invalid create table SQL generated");
                }

                Console.WriteLine($"================================");

                Console.WriteLine("Attempting to open database connection...");
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);

                try
                {
                    await connection.OpenAsync();
                    Console.WriteLine($"Database connection opened successfully. State: {connection.State}");
                }
                catch (Exception connEx)
                {
                    Console.WriteLine($"Failed to open database connection: {connEx.GetType().Name} - {connEx.Message}");
                    throw new InvalidOperationException($"Database connection failed: {connEx.Message}", connEx);
                }

                try
                {
                    Console.WriteLine("Executing table creation...");
                    using (var createCommand = new Microsoft.Data.SqlClient.SqlCommand(createTableSql, connection))
                    {
                        createCommand.CommandTimeout = 60;
                        await createCommand.ExecuteNonQueryAsync();
                    }
                    Console.WriteLine("Table creation completed successfully.");

                    Console.WriteLine("Verifying table creation...");
                    using (var verifyCommand = new Microsoft.Data.SqlClient.SqlCommand($"SELECT COUNT(*) FROM {tempTableName}", connection))
                    {
                        verifyCommand.CommandTimeout = 30;
                        var rowCount = await verifyCommand.ExecuteScalarAsync();
                        Console.WriteLine($"Temporary table {tempTableName} created with {rowCount} rows.");

                        if (Convert.ToInt32(rowCount) == 0)
                        {
                            Console.WriteLine("WARNING: Temporary table was created but contains no data!");
                        }
                    }

                    var selectedColumnsList = selectedColumns.ToList();

                    if (updateStatements.Count != selectedColumnsList.Count)
                    {
                        throw new InvalidOperationException($"Mismatch between update statements ({updateStatements.Count}) and selected columns ({selectedColumnsList.Count})");
                    }

                    for (int i = 0; i < updateStatements.Count; i++)
                    {
                        var updateSql = updateStatements[i];
                        var columnName = selectedColumnsList[i];

                        Console.WriteLine($"Executing update statement {i + 1}/{updateStatements.Count} for column {columnName}...");

                        if (string.IsNullOrWhiteSpace(updateSql) || !updateSql.Contains("UPDATE") || !updateSql.Contains(columnName))
                        {
                            Console.WriteLine($"ERROR: Invalid update SQL for column {columnName}!");
                            throw new ArgumentException($"Invalid update SQL generated for column {columnName}");
                        }

                        Console.WriteLine($"Update SQL:\n{updateSql}");

                        try
                        {
                            using (var updateCommand = new Microsoft.Data.SqlClient.SqlCommand(updateSql, connection))
                            {
                                updateCommand.CommandTimeout = 60;
                                var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                                Console.WriteLine($"Update completed for column {columnName}. Rows affected: {rowsAffected}");
                            }
                        }
                        catch (Exception updateEx)
                        {
                            Console.WriteLine($"ERROR during update of column {columnName}: {updateEx.GetType().Name} - {updateEx.Message}");
                            Console.WriteLine($"Failed SQL:\n{updateSql}");
                            throw new InvalidOperationException($"Failed to update column {columnName}: {updateEx.Message}", updateEx);
                        }
                    }
                    Console.WriteLine("All winsorization updates completed successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during winsorization execution: {ex.GetType().Name} - {ex.Message}");

                    try
                    {
                        using var dropCommand = new Microsoft.Data.SqlClient.SqlCommand($"IF OBJECT_ID('{tempTableName}') IS NOT NULL DROP TABLE {tempTableName}", connection);
                        dropCommand.CommandTimeout = 30;
                        await dropCommand.ExecuteNonQueryAsync();
                        Console.WriteLine("Cleaned up temporary table after error.");
                    }
                    catch (Exception cleanupEx)
                    {
                        Console.WriteLine($"Could not clean up temporary table: {cleanupEx.Message}");
                    }

                    throw;
                }

                return tempTableName;
            }
            catch (Exception ex)
            {
                var error = $"Error creating winsorized table: {ex.GetType().Name} - {ex.Message}";
                Console.WriteLine(error);
                System.Diagnostics.Debug.WriteLine(error);
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    var innerError = $"Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}";
                    Console.WriteLine(innerError);
                    System.Diagnostics.Debug.WriteLine(innerError);
                }

                Console.WriteLine("Attempting fallback winsorization approach...");
                try
                {
                    return await CreateWinsorizedTableFallbackAsync(selectedColumns);
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Fallback approach also failed: {fallbackEx.GetType().Name} - {fallbackEx.Message}");

                    Console.WriteLine("Running basic database diagnostics...");
                    await RunDatabaseDiagnosticsAsync();

                    Console.WriteLine("All winsorization approaches failed. The issue may be:");
                    Console.WriteLine("1. Database permissions (cannot create temporary tables)");
                    Console.WriteLine("2. Connection string issues");
                    Console.WriteLine("3. SQL Server version compatibility");
                    Console.WriteLine("4. Insufficient database privileges");

                    return null;
                }
            }
        }

        private async Task<string?> CreateWinsorizedTableFallbackAsync(HashSet<string> selectedColumns)
        {
            Console.WriteLine("=== FALLBACK Winsorization Approach ===");
            var tempTableName = $"#WinsorizedData_Fallback_{Guid.NewGuid():N}";

            try
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();
                Console.WriteLine("Fallback: Database connection opened successfully.");

                var whereCondition = string.IsNullOrEmpty(_whereClause) ? "" : $"WHERE {_whereClause}";
                var createTableSql = $"SELECT * INTO {tempTableName} FROM {_tableName} {whereCondition}";

                Console.WriteLine($"Fallback: Creating table copy...\nSQL: {createTableSql}");
                using (var createCommand = new Microsoft.Data.SqlClient.SqlCommand(createTableSql, connection))
                {
                    createCommand.CommandTimeout = 60;
                    await createCommand.ExecuteNonQueryAsync();
                }
                Console.WriteLine("Fallback: Table copy created successfully.");

                foreach (var column in selectedColumns)
                {
                    Console.WriteLine($"Fallback: Processing column {column}...");

                    var baseCondition = $"[{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1";
                    var fullCondition = string.IsNullOrEmpty(_whereClause) ? baseCondition : $"({_whereClause}) AND {baseCondition}";

                    var percentilesSql = $@"
                        SELECT DISTINCT
                            PERCENTILE_CONT({WinsorLowerPercentile / 100.0}) WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER() as LowerBound,
                            PERCENTILE_CONT({WinsorUpperPercentile / 100.0}) WITHIN GROUP (ORDER BY CAST([{column}] AS FLOAT)) OVER() as UpperBound
                        FROM {_tableName}
                        WHERE {fullCondition}";

                    Console.WriteLine($"Fallback: Calculating percentiles for {column}...\nSQL: {percentilesSql}");
                    System.Diagnostics.Debug.WriteLine($"Fallback: Percentiles SQL for {column}: {percentilesSql}");

                    decimal lowerBound = 0, upperBound = 0;
                    using (var percentilesCommand = new Microsoft.Data.SqlClient.SqlCommand(percentilesSql, connection))
                    {
                        percentilesCommand.CommandTimeout = 30;
                        using var reader = await percentilesCommand.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            lowerBound = Convert.ToDecimal(reader["LowerBound"]);
                            upperBound = Convert.ToDecimal(reader["UpperBound"]);
                        }
                    }

                    Console.WriteLine($"Fallback: Column {column} bounds: Lower={lowerBound}, Upper={upperBound}");

                    var updateLowerSql = $@"
                        UPDATE {tempTableName}
                        SET [{column}] = {lowerBound}
                        WHERE CAST([{column}] AS FLOAT) < {lowerBound}
                        AND [{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1";

                    var updateUpperSql = $@"
                        UPDATE {tempTableName}
                        SET [{column}] = {upperBound}
                        WHERE CAST([{column}] AS FLOAT) > {upperBound}
                        AND [{column}] IS NOT NULL AND ISNUMERIC([{column}]) = 1";

                    using (var updateLowerCommand = new Microsoft.Data.SqlClient.SqlCommand(updateLowerSql, connection))
                    {
                        updateLowerCommand.CommandTimeout = 30;
                        var rowsAffected = await updateLowerCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Fallback: Updated {rowsAffected} rows for lower bound on column {column}");
                    }

                    using (var updateUpperCommand = new Microsoft.Data.SqlClient.SqlCommand(updateUpperSql, connection))
                    {
                        updateUpperCommand.CommandTimeout = 30;
                        var rowsAffected = await updateUpperCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Fallback: Updated {rowsAffected} rows for upper bound on column {column}");
                    }
                }

                Console.WriteLine("Fallback: Winsorization completed successfully!");
                return tempTableName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback method error: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        private async Task RunDatabaseDiagnosticsAsync()
        {
            try
            {
                Console.WriteLine("=== DATABASE DIAGNOSTICS ===");
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);

                Console.WriteLine("Test 1: Testing basic database connection...");
                try
                {
                    await connection.OpenAsync();
                    Console.WriteLine($"✓ Connection successful. State: {connection.State}");
                    Console.WriteLine($"✓ Database: {connection.Database}");
                    Console.WriteLine($"✓ Server: {connection.DataSource}");
                }
                catch (Exception connEx)
                {
                    Console.WriteLine($"✗ Connection failed: {connEx.Message}");
                    return;
                }

                Console.WriteLine($"Test 2: Testing table access for '{_tableName}'...");
                try
                {
                    var checkTableSql = $"SELECT COUNT(*) FROM {_tableName} WHERE 1=0";
                    using var checkCommand = new Microsoft.Data.SqlClient.SqlCommand(checkTableSql, connection);
                    checkCommand.CommandTimeout = 10;
                    await checkCommand.ExecuteScalarAsync();
                    Console.WriteLine($"✓ Table '{_tableName}' is accessible");
                }
                catch (Exception tableEx)
                {
                    Console.WriteLine($"✗ Table access failed: {tableEx.Message}");
                }

                Console.WriteLine("Test 3: Testing PERCENTILE_CONT function support...");
                try
                {
                    var percentileTestSql = $"SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY val) OVER() FROM (SELECT 1 as val) t";
                    using var percentileCommand = new Microsoft.Data.SqlClient.SqlCommand(percentileTestSql, connection);
                    percentileCommand.CommandTimeout = 10;
                    await percentileCommand.ExecuteScalarAsync();
                    Console.WriteLine("✓ PERCENTILE_CONT function is supported");
                }
                catch (Exception percentileEx)
                {
                    Console.WriteLine($"✗ PERCENTILE_CONT failed: {percentileEx.Message}");
                }

                Console.WriteLine("Test 4: Testing temporary table creation permissions...");
                try
                {
                    var tempTableName = $"#TestTable_{Guid.NewGuid():N}";
                    var createTempSql = $"CREATE TABLE {tempTableName} (id int)";
                    using var createTempCommand = new Microsoft.Data.SqlClient.SqlCommand(createTempSql, connection);
                    createTempCommand.CommandTimeout = 10;
                    await createTempCommand.ExecuteNonQueryAsync();

                    var dropTempSql = $"DROP TABLE {tempTableName}";
                    using var dropTempCommand = new Microsoft.Data.SqlClient.SqlCommand(dropTempSql, connection);
                    await dropTempCommand.ExecuteNonQueryAsync();

                    Console.WriteLine("✓ Temporary table creation is allowed");
                }
                catch (Exception tempEx)
                {
                    Console.WriteLine($"✗ Temporary table creation failed: {tempEx.Message}");
                    Console.WriteLine("This is likely the cause of the winsorization failures.");
                }

                Console.WriteLine("Test 5: Testing SELECT INTO permissions...");
                try
                {
                    var tempTableName = $"#TestSelectInto_{Guid.NewGuid():N}";
                    var selectIntoSql = $"SELECT TOP 1 * INTO {tempTableName} FROM {_tableName}";
                    using var selectIntoCommand = new Microsoft.Data.SqlClient.SqlCommand(selectIntoSql, connection);
                    selectIntoCommand.CommandTimeout = 10;
                    await selectIntoCommand.ExecuteNonQueryAsync();

                    var dropSql = $"DROP TABLE {tempTableName}";
                    using var dropCommand = new Microsoft.Data.SqlClient.SqlCommand(dropSql, connection);
                    await dropCommand.ExecuteNonQueryAsync();

                    Console.WriteLine("✓ SELECT INTO is allowed");
                }
                catch (Exception selectIntoEx)
                {
                    Console.WriteLine($"✗ SELECT INTO failed: {selectIntoEx.Message}");
                    Console.WriteLine("This is likely the cause of the winsorization failures.");
                }

                Console.WriteLine("=== DIAGNOSTICS COMPLETE ===");
            }
            catch (Exception diagEx)
            {
                Console.WriteLine($"Diagnostics failed: {diagEx.Message}");
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
        public int LowSeverityCount { get; set; }
        public int MediumSeverityCount { get; set; }
        public int HighSeverityCount { get; set; }
        public int ExtremeSeverityCount { get; set; }
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