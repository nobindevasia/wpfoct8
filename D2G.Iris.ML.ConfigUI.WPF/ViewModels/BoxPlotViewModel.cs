using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.Commands;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class BoxPlotViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;

        private string? _connectionString;
        private string? _tableName;
        private string[]? _columns;
        private string? _whereClause;

        private ObservableCollection<string> _numericColumns;
        private string? _selectedColumn;
        private BoxPlotData? _boxPlotData;
        private bool _isGeneratingPlot;
        private string _progressMessage = string.Empty;
        private string _statisticsInfo = string.Empty;

        public BoxPlotViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _numericColumns = new ObservableCollection<string>();

            GeneratePlotCommand = new AsyncRelayCommand(async _ => await GenerateBoxPlotAsync(), _ => CanGeneratePlot());
        }

        #region Properties

        public ObservableCollection<string> NumericColumns
        {
            get => _numericColumns;
            set => SetProperty(ref _numericColumns, value);
        }

        public string? SelectedColumn
        {
            get => _selectedColumn;
            set
            {
                if (SetProperty(ref _selectedColumn, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public BoxPlotData? BoxPlotData
        {
            get => _boxPlotData;
            set => SetProperty(ref _boxPlotData, value);
        }

        public bool IsGeneratingPlot
        {
            get => _isGeneratingPlot;
            set => SetProperty(ref _isGeneratingPlot, value);
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        public string StatisticsInfo
        {
            get => _statisticsInfo;
            set => SetProperty(ref _statisticsInfo, value);
        }

        public ICommand GeneratePlotCommand { get; }

        #endregion

        #region Public Methods

        public void SetDatabaseConnection(string connectionString, string tableName, string[] columns, string? whereClause = null)
        {
            _connectionString = connectionString;
            _tableName = tableName;
            _columns = columns;
            _whereClause = whereClause;

            LoadNumericColumnsAsync();
        }

        private async void LoadNumericColumnsAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName))
                return;

            try
            {
                var schema = await _databaseAnalytics.GetTableSchemaAsync(_connectionString, _tableName);
                var numericTypes = new[] { "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "money", "smallmoney", "float", "real" };

                var numericCols = schema
                    .Where(s => numericTypes.Contains(s.DataType.ToLower()) &&
                               (_columns == null || _columns.Contains(s.ColumnName)))
                    .Select(s => s.ColumnName)
                    .ToList();

                NumericColumns.Clear();
                foreach (var col in numericCols)
                {
                    NumericColumns.Add(col);
                }

                if (NumericColumns.Any() && string.IsNullOrEmpty(SelectedColumn))
                {
                    SelectedColumn = NumericColumns.First();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error loading columns: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Private Methods

        private bool CanGeneratePlot()
        {
            return !string.IsNullOrEmpty(_selectedColumn) && !_isGeneratingPlot;
        }

        private async Task GenerateBoxPlotAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) ||
                string.IsNullOrEmpty(_selectedColumn))
            {
                _dialogService.ShowErrorDialog("Please select a column.", "Selection Error");
                return;
            }

            IsGeneratingPlot = true;
            StatisticsInfo = string.Empty;
            BoxPlotData = null;

            try
            {
                ProgressMessage = $"Loading statistics for '{_selectedColumn}'...";

                var boxData = await _databaseAnalytics.GetBoxPlotDataAsync(
                    _connectionString,
                    _tableName,
                    _selectedColumn,
                    _whereClause);

                if (boxData == null)
                {
                    _dialogService.ShowInfoDialog("No data found for the selected column.", "No Data");
                    return;
                }

                ProgressMessage = "Calculating quartiles and outliers...";
                await Task.Delay(100);

                ProgressMessage = "Rendering box plot...";
                BoxPlotData = boxData;

                StatisticsInfo = $"Min: {boxData.Min:F2} | Q1: {boxData.Q1:F2} | Median: {boxData.Median:F2} | Q3: {boxData.Q3:F2} | Max: {boxData.Max:F2} | Mean: {boxData.Mean:F2} | Outliers: {boxData.Outliers.Count}";

                ProgressMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating box plot: {ex.Message}\n\nDetails: {ex.StackTrace}", "Error");
                ProgressMessage = string.Empty;
            }
            finally
            {
                IsGeneratingPlot = false;
            }
        }

        #endregion
    }
}
