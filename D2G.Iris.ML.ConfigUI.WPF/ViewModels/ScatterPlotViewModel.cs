using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SciChart.Charting.Model.DataSeries;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.Commands;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ScatterPlotViewModel : INotifyPropertyChanged
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;

        private string? _connectionString;
        private string? _tableName;
        private string[]? _columns;
        private string? _whereClause;

        private ObservableCollection<string> _numericColumns;
        private string? _selectedXColumn;
        private string? _selectedYColumn;
        private XyDataSeries<double, double>? _scatterPlotData;
        private bool _isGeneratingPlot;
        private string _progressMessage = string.Empty;
        private string _correlationInfo = string.Empty;

        public ScatterPlotViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _numericColumns = new ObservableCollection<string>();

            GeneratePlotCommand = new AsyncRelayCommand(async _ => await GenerateScatterPlotAsync(), _ => CanGeneratePlot());
        }

        #region Properties

        public ObservableCollection<string> NumericColumns
        {
            get => _numericColumns;
            set => SetProperty(ref _numericColumns, value);
        }

        public string? SelectedXColumn
        {
            get => _selectedXColumn;
            set
            {
                if (SetProperty(ref _selectedXColumn, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string? SelectedYColumn
        {
            get => _selectedYColumn;
            set
            {
                if (SetProperty(ref _selectedYColumn, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public XyDataSeries<double, double>? ScatterPlotData
        {
            get => _scatterPlotData;
            set => SetProperty(ref _scatterPlotData, value);
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

        public string CorrelationInfo
        {
            get => _correlationInfo;
            set => SetProperty(ref _correlationInfo, value);
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
                    .Where(s => numericTypes.Contains(s.DataType.ToLower()))
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

        private bool CanGeneratePlot()
        {
            return !string.IsNullOrEmpty(_selectedXColumn) &&
                   !string.IsNullOrEmpty(_selectedYColumn) &&
                   !_isGeneratingPlot;
        }

        private async Task GenerateScatterPlotAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) ||
                string.IsNullOrEmpty(_selectedXColumn) || string.IsNullOrEmpty(_selectedYColumn))
            {
                _dialogService.ShowErrorDialog("Please select both X and Y columns.", "Selection Error");
                return;
            }

            IsGeneratingPlot = true;
            CorrelationInfo = string.Empty;

            try
            {
                ProgressMessage = $"Loading scatter plot data for {_selectedXColumn} vs {_selectedYColumn}...";

                var scatterData = await _databaseAnalytics.GetScatterPlotDataAsync(
                    _connectionString,
                    _tableName,
                    _selectedXColumn,
                    _selectedYColumn,
                    null,
                    _whereClause);

                if (scatterData == null || !scatterData.Any())
                {
                    _dialogService.ShowInfoDialog("No data found for the selected columns.", "No Data");
                    return;
                }

                var dataSeries = new XyDataSeries<double, double>
                {
                    SeriesName = $"{_selectedXColumn} vs {_selectedYColumn}",
                    AcceptsUnsortedData = true
                };

                foreach (var point in scatterData)
                {
                    dataSeries.Append(point.X, point.Y);
                }

                ScatterPlotData = dataSeries;

                ProgressMessage = "Calculating correlation...";
                try
                {
                    var correlationMatrix = await _databaseAnalytics.GetCorrelationMatrixAsync(
                        _connectionString,
                        _tableName,
                        new[] { _selectedXColumn, _selectedYColumn },
                        _whereClause);

                    var correlation = correlationMatrix.GetCorrelation(_selectedXColumn, _selectedYColumn);
                    CorrelationInfo = $"Correlation: {correlation:F4} | Points: {scatterData.Count}";
                }
                catch (Exception corrEx)
                {
                    CorrelationInfo = $"Correlation: N/A | Points: {scatterData.Count}";
                    Console.WriteLine($"Correlation calculation failed: {corrEx.Message}");
                }

                ProgressMessage = string.Empty;
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating scatter plot: {ex.Message}", "Error");
                ProgressMessage = string.Empty;
            }
            finally
            {
                IsGeneratingPlot = false;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
