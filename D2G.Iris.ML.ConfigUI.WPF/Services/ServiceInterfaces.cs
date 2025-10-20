using System.Collections.Generic;
using System.Threading.Tasks;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.Services
{
    public interface IConfigurationService
    {
        ModelConfig LoadConfiguration(string filePath);
        void SaveConfiguration(ModelConfig config, string filePath);
        bool ValidateConfiguration(ModelConfig config);
    }

    public interface IDatabaseSchemaLoader
    {
        List<string> LoadTableColumns(DatabaseConfig config);
        bool TestConnection(DatabaseConfig config);
        List<string> LoadDatabases(DatabaseConfig config);
        List<TableInfo> LoadTables(DatabaseConfig config);
        List<ColumnInfo> LoadTableSchema(DatabaseConfig config, string tableName);
        System.Data.DataTable PreviewTableData(DatabaseConfig config, string tableName, int maxRows = 100);
        long GetTableRowCount(DatabaseConfig config, string tableName);
    }

    public interface IDialogService
    {
        bool ShowConfirmationDialog(string message, string title);
        void ShowErrorDialog(string message, string title);
        void ShowInfoDialog(string message, string title);
        string? ShowSaveFileDialog(string filter, string defaultExtension, string defaultFileName);
        string? ShowOpenFileDialog(string filter);
        T? ShowDialog<T>(object viewModel) where T : class;
        bool? ShowInputFieldDialog(object viewModel);
        bool? ShowParameterDialog(object viewModel);
    }

    public interface IDatabaseAnalyticsService
    {
        Task<ColumnStatistics> GetColumnStatisticsAsync(string tableName, string columnName);
        Task<List<ColumnStatistics>> GetColumnStatisticsAsync(string connectionString, string tableName, IEnumerable<string> columns, string? whereClause = null, bool includePercentiles = false);
        Task<Percentiles> GetPercentilesAsync(string tableName, string columnName);
        Task<List<HistogramBin>> GetHistogramAsync(string tableName, string columnName, int binCount = 10);
        Task<List<CategoryFrequency>> GetCategoryFrequencyAsync(string tableName, string columnName, int topN = 20);
        Task<List<ColumnNullInfo>> GetNullAnalysisAsync(string tableName, List<string> columns);
        Task<double> GetCorrelationAsync(string tableName, string column1, string column2);
        Task<int> GetDistinctCountAsync(string tableName, string columnName);
        Task<OutlierInfo> GetOutliersAsync(string tableName, string columnName, double iqrMultiplier = 1.5);
        Task<NumericColumnSummary> GetNumericColumnSummaryAsync(string tableName, string columnName);
        Task<DatasetSummary> GetDatasetSummaryAsync(string connectionString, string tableName, IEnumerable<string> columns, string? whereClause = null);
        Task<CorrelationMatrix> GetCorrelationMatrixAsync(string connectionString, string tableName, IEnumerable<string> numericColumns, string? whereClause = null);
        Task<bool> TestConnectionAsync(string connectionString);
        Task<List<MissingValueInfo>> GetMissingValuesAnalysisAsync(string connectionString, string tableName, IEnumerable<string> columns, string? whereClause = null);
        Task<List<OutlierResult>> DetectOutliersAsync(string connectionString, string tableName, string columnName, OutlierDetectionMethod method, double threshold, string? whereClause = null);
        Task<List<TableSchemaInfo>> GetTableSchemaAsync(string connectionString, string tableName);
        Task<List<HistogramBin>> GetHistogramDataAsync(string connectionString, string tableName, string columnName, int binCount, string? whereClause = null);
        Task<List<CategoryFrequency>> GetCategoricalDataAsync(string connectionString, string tableName, string columnName, int topN, string? whereClause = null);
        Task<string> DebugHistogramStepsAsync(string connectionString, string tableName, string columnName, int binCount, string? whereClause = null);
        Task<string> DebugBasicOperationsAsync(string connectionString, string tableName, string? whereClause = null);
        Task<List<ScatterPlotPoint>> GetScatterPlotDataAsync(string connectionString, string tableName, string xColumn, string yColumn, int? maxPoints = null, string? whereClause = null);
        Task<List<double>> GetDistributionDataAsync(string connectionString, string tableName, string columnName, string? whereClause = null, int maxSampleSize = 10000);
        Task<BoxPlotData> GetBoxPlotDataAsync(string connectionString, string tableName, string columnName, string? whereClause = null);
    }

    public enum OutlierDetectionMethod
    {
        ZScore,
        IQR,
        ModifiedZScore
    }
}
