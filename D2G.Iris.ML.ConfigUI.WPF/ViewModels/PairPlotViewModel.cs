using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.Visuals.Axes;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.Commands;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class PairPlotViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;

        private string? _connectionString;
        private string? _tableName;
        private string[]? _columns;
        private string? _whereClause;

        private ObservableCollection<string> _numericColumns;
        private ObservableCollection<string> _selectedColumns;
        private ObservableCollection<PairPlotChartInfo> _pairPlotCharts;
        private bool _isGeneratingPlot;
        private string _progressMessage = string.Empty;
        private int _maxColumnsToSelect = 5;

        public PairPlotViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _numericColumns = new ObservableCollection<string>();
            _selectedColumns = new ObservableCollection<string>();
            _pairPlotCharts = new ObservableCollection<PairPlotChartInfo>();

            GeneratePlotCommand = new AsyncRelayCommand(async _ => await GeneratePairPlotAsync(), _ => CanGeneratePlot());
            AddColumnCommand = new RelayCommand(_ => AddColumn(), _ => CanAddColumn());
            RemoveColumnCommand = new RelayCommand(param => RemoveColumn(param?.ToString()), _ => true);
        }

        #region Properties

        public ObservableCollection<string> NumericColumns
        {
            get => _numericColumns;
            set => SetProperty(ref _numericColumns, value);
        }

        public ObservableCollection<string> SelectedColumns
        {
            get => _selectedColumns;
            set => SetProperty(ref _selectedColumns, value);
        }

        public ObservableCollection<PairPlotChartInfo> PairPlotCharts
        {
            get => _pairPlotCharts;
            set => SetProperty(ref _pairPlotCharts, value);
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

        public int MaxColumnsToSelect
        {
            get => _maxColumnsToSelect;
            set => SetProperty(ref _maxColumnsToSelect, value);
        }

        public ICommand GeneratePlotCommand { get; }
        public ICommand AddColumnCommand { get; }
        public ICommand RemoveColumnCommand { get; }

        private string? _selectedColumnToAdd;
        public string? SelectedColumnToAdd
        {
            get => _selectedColumnToAdd;
            set
            {
                if (SetProperty(ref _selectedColumnToAdd, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

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
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error loading columns: {ex.Message}", "Error");
            }
        }

        #endregion

        #region Private Methods

        private bool CanAddColumn()
        {
            return !string.IsNullOrEmpty(_selectedColumnToAdd) &&
                   !_selectedColumns.Contains(_selectedColumnToAdd) &&
                   _selectedColumns.Count < _maxColumnsToSelect;
        }

        private void AddColumn()
        {
            if (!string.IsNullOrEmpty(_selectedColumnToAdd) && !_selectedColumns.Contains(_selectedColumnToAdd))
            {
                _selectedColumns.Add(_selectedColumnToAdd);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void RemoveColumn(string? column)
        {
            if (!string.IsNullOrEmpty(column))
            {
                _selectedColumns.Remove(column);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanGeneratePlot()
        {
            return _selectedColumns.Count >= 2 && !_isGeneratingPlot;
        }

        private async Task GeneratePairPlotAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) ||
                _selectedColumns.Count < 2)
            {
                _dialogService.ShowErrorDialog("Please select at least 2 columns for pair plot.", "Selection Error");
                return;
            }

            IsGeneratingPlot = true;
            PairPlotCharts.Clear();

            try
            {
                var selectedColumnsList = _selectedColumns.ToList();
                var totalPairs = selectedColumnsList.Count * selectedColumnsList.Count;
                var currentPair = 0;

                for (int row = 0; row < selectedColumnsList.Count; row++)
                {
                    for (int col = 0; col < selectedColumnsList.Count; col++)
                    {
                        currentPair++;
                        ProgressMessage = $"Generating plot {currentPair} of {totalPairs}...";

                        var xColumn = selectedColumnsList[col];
                        var yColumn = selectedColumnsList[row];

                        var chartInfo = new PairPlotChartInfo
                        {
                            Row = row,
                            Column = col,
                            XColumn = xColumn,
                            YColumn = yColumn,
                            IsHistogram = xColumn == yColumn
                        };

                        if (xColumn == yColumn)
                        {
                            // Create histogram for diagonal
                            await CreateHistogramAsync(chartInfo, xColumn);
                        }
                        else
                        {
                            // Create scatter plot for off-diagonal
                            await CreateScatterPlotAsync(chartInfo, xColumn, yColumn);
                        }

                        PairPlotCharts.Add(chartInfo);
                        await Task.Delay(10); // Allow UI to update
                    }
                }

                ProgressMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating pair plot: {ex.Message}", "Error");
                ProgressMessage = string.Empty;
            }
            finally
            {
                IsGeneratingPlot = false;
            }
        }

        private async Task CreateScatterPlotAsync(PairPlotChartInfo chartInfo, string xColumn, string yColumn)
        {
            try
            {
                var scatterData = await _databaseAnalytics.GetScatterPlotDataAsync(
                    _connectionString!,
                    _tableName!,
                    xColumn,
                    yColumn,
                    10000, // Limit to 10k points for performance
                    _whereClause);

                if (scatterData != null && scatterData.Any())
                {
                    var dataSeries = new XyDataSeries<double, double>
                    {
                        SeriesName = $"{xColumn} vs {yColumn}",
                        AcceptsUnsortedData = true
                    };

                    foreach (var point in scatterData)
                    {
                        dataSeries.Append(point.X, point.Y);
                    }

                    chartInfo.ScatterPlotData = dataSeries;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating scatter plot for {xColumn} vs {yColumn}: {ex.Message}");
            }
        }

        private async Task CreateHistogramAsync(PairPlotChartInfo chartInfo, string column)
        {
            try
            {
                var histogramBins = await _databaseAnalytics.GetHistogramDataAsync(
                    _connectionString!,
                    _tableName!,
                    column,
                    20, // Number of bins
                    _whereClause);

                if (histogramBins != null && histogramBins.Any())
                {
                    var dataSeries = new XyDataSeries<double, double>
                    {
                        SeriesName = column,
                        AcceptsUnsortedData = false
                    };

                    foreach (var bin in histogramBins)
                    {
                        dataSeries.Append(bin.BinStart, bin.Count);
                    }

                    chartInfo.HistogramData = dataSeries;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating histogram for {column}: {ex.Message}");
            }
        }

        #endregion
    }

    public class PairPlotChartInfo
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string XColumn { get; set; } = string.Empty;
        public string YColumn { get; set; } = string.Empty;
        public bool IsHistogram { get; set; }
        public XyDataSeries<double, double>? ScatterPlotData { get; set; }
        public XyDataSeries<double, double>? HistogramData { get; set; }
    }
}
