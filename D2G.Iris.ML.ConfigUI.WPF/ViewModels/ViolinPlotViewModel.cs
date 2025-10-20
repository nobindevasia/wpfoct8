using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using Microsoft.FSharp.Core;
using Plotly.NET;
using Plotly.NET.LayoutObjects;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels;

public class ViolinPlotViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IDialogService _dialogService;
    private readonly IDatabaseAnalyticsService _databaseAnalyticsService;

    private string? _connectionString;
    private string? _tableName;
    private string[]? _enabledColumns;
    private string? _whereClause;

    private readonly ObservableCollection<string> _numericColumns = new();
    private string? _selectedColumn;
    private bool _isGeneratingPlot;
    private string _progressMessage = string.Empty;
    private string _statisticsInfo = string.Empty;
    private string? _plotHtml;

    public ViolinPlotViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalyticsService)
    {
        _dialogService = dialogService;
        _databaseAnalyticsService = databaseAnalyticsService;

        GeneratePlotCommand = new AsyncRelayCommand(_ => GenerateViolinPlotAsync(), _ => CanGeneratePlot());
    }

    #region Bindable Properties

    public ObservableCollection<string> NumericColumns => _numericColumns;

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

    public bool IsGeneratingPlot
    {
        get => _isGeneratingPlot;
        private set
        {
            if (SetProperty(ref _isGeneratingPlot, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ProgressMessage
    {
        get => _progressMessage;
        private set => SetProperty(ref _progressMessage, value);
    }

    public string StatisticsInfo
    {
        get => _statisticsInfo;
        private set => SetProperty(ref _statisticsInfo, value);
    }

    public string? PlotHtml
    {
        get => _plotHtml;
        private set => SetProperty(ref _plotHtml, value);
    }

    public ICommand GeneratePlotCommand { get; }

    #endregion

    #region Public API

    public void SetDatabaseConnection(string connectionString, string tableName, string[] columns, string? whereClause = null)
    {
        _connectionString = connectionString;
        _tableName = tableName;
        _enabledColumns = columns;
        _whereClause = whereClause;

        LoadNumericColumnsAsync();
        PlotHtml = null;
    }

    #endregion

    #region Data Loading

    private async void LoadNumericColumnsAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(_tableName))
        {
            return;
        }

        try
        {
            var schema = await _databaseAnalyticsService
                .GetTableSchemaAsync(_connectionString, _tableName)
                .ConfigureAwait(false);

            if (schema == null || schema.Count == 0)
            {
                return;
            }

            var supportedNumericTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "int", "bigint", "smallint", "tinyint",
                "decimal", "numeric", "money", "smallmoney",
                "float", "real"
            };

            var numericColumns = schema
                .Where(column =>
                    supportedNumericTypes.Contains(column.DataType) &&
                    (_enabledColumns == null || _enabledColumns.Contains(column.ColumnName)))
                .Select(column => column.ColumnName)
                .OrderBy(name => name)
                .ToList();

            if (Application.Current == null)
            {
                UpdateNumericColumns(numericColumns);
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() => UpdateNumericColumns(numericColumns));
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Unable to load columns: {ex.Message}");
        }
    }

    private void UpdateNumericColumns(IReadOnlyCollection<string> newColumns)
    {
        var previouslySelected = SelectedColumn;

        _numericColumns.Clear();
        foreach (var column in newColumns)
        {
            _numericColumns.Add(column);
        }

        if (previouslySelected != null && _numericColumns.Contains(previouslySelected))
        {
            SelectedColumn = previouslySelected;
        }
        else
        {
            SelectedColumn = _numericColumns.FirstOrDefault();
        }
    }

    #endregion

    #region Plot Generation

    private bool CanGeneratePlot() =>
        !IsGeneratingPlot &&
        !string.IsNullOrWhiteSpace(_connectionString) &&
        !string.IsNullOrWhiteSpace(_tableName) &&
        !string.IsNullOrWhiteSpace(SelectedColumn);

    private async Task GenerateViolinPlotAsync()
    {
        if (!CanGeneratePlot())
        {
            _dialogService.ShowInfoDialog("Select a numeric column before generating the violin plot.", "Selection Required");
            return;
        }

        try
        {
            IsGeneratingPlot = true;
            ProgressMessage = $"Loading statistics for '{SelectedColumn}'...";
            PlotHtml = null;

            var stats = await LoadColumnStatisticsAsync();
            if (stats == null || stats.NonNullCount == 0)
            {
                await ShowInfoAsync("No data was returned for the selected column.", "No Data");
                return;
            }

            ProgressMessage = "Fetching sample data...";
            var distributionData = await LoadDistributionDataAsync();
            if (distributionData == null || distributionData.Count == 0)
            {
                await ShowInfoAsync("Unable to build the plot because no sample data points were retrieved.", "No Data");
                return;
            }

            ProgressMessage = "Rendering Plotly chart...";
            var htmlContent = CreatePlotlyHtml(distributionData, SelectedColumn!);
            PlotHtml = htmlContent;

            UpdateStatisticsBanner(distributionData, stats);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Error generating violin plot: {ex.Message}");
        }
        finally
        {
            ProgressMessage = string.Empty;
            IsGeneratingPlot = false;
        }
    }

    private async Task<ColumnStatistics?> LoadColumnStatisticsAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString) ||
            string.IsNullOrWhiteSpace(_tableName) ||
            string.IsNullOrWhiteSpace(SelectedColumn))
        {
            return null;
        }

        var stats = await _databaseAnalyticsService
            .GetColumnStatisticsAsync(
                _connectionString,
                _tableName,
                new[] { SelectedColumn },
                _whereClause,
                includePercentiles: true)
            .ConfigureAwait(false);

        return stats?.FirstOrDefault();
    }

    private async Task<List<double>> LoadDistributionDataAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString) ||
            string.IsNullOrWhiteSpace(_tableName) ||
            string.IsNullOrWhiteSpace(SelectedColumn))
        {
            return new List<double>();
        }

        return await _databaseAnalyticsService
            .GetDistributionDataAsync(_connectionString, _tableName, SelectedColumn, _whereClause, maxSampleSize: 5000)
            .ConfigureAwait(false);
    }

    private string CreatePlotlyHtml(IReadOnlyList<double> dataPoints, string columnName)
    {
        var violinChart = Chart2D.Chart.Violin<double, double, string>(
            Y: FSharpOption<IEnumerable<double>>.Some(dataPoints),
            Name: FSharpOption<string>.Some(columnName),
            Points: FSharpOption<StyleParam.BoxPoints>.Some(StyleParam.BoxPoints.All),
            Jitter: FSharpOption<double>.Some(0.15)
        );

        violinChart = violinChart
            .WithTitle($"Violin Plot - {columnName}")
            .WithXAxisStyle(Title.init("Density"))
            .WithYAxisStyle(Title.init(columnName))
            .WithSize(900, 600);

        return GenericChart.toEmbeddedHTML(violinChart);
    }

    private void UpdateStatisticsBanner(IReadOnlyList<double> samples, ColumnStatistics stats)
    {
        if (samples.Count == 0)
        {
            StatisticsInfo = string.Empty;
            return;
        }

        var ordered = samples.OrderBy(value => value).ToArray();

        double Quantile(double q)
        {
            if (ordered.Length == 0)
            {
                return double.NaN;
            }

            var position = (ordered.Length - 1) * q;
            var lowerIndex = (int)Math.Floor(position);
            var upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
            {
                return ordered[lowerIndex];
            }

            var weight = position - lowerIndex;
            return ordered[lowerIndex] + (ordered[upperIndex] - ordered[lowerIndex]) * weight;
        }

        var mean = stats.Mean ?? samples.Average();
        var stdDev = stats.StdDev ?? StandardDeviation(samples, mean);
        var median = Quantile(0.5);

        StatisticsInfo =
            $"Count: {samples.Count:N0} | Mean: {mean:F2} | Median: {median:F2} | Std Dev: {stdDev:F2} | Min: {samples.Min():F2} | Max: {samples.Max():F2}";
    }

    private static double StandardDeviation(IReadOnlyCollection<double> values, double mean)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    #endregion

    #region Helpers

    private async Task ShowInfoAsync(string message, string title)
    {
        if (Application.Current == null)
        {
            _dialogService.ShowInfoDialog(message, title);
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(() => _dialogService.ShowInfoDialog(message, title));
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        if (Application.Current == null)
        {
            _dialogService.ShowErrorDialog(message, "Error");
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(() => _dialogService.ShowErrorDialog(message, "Error"));
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region IDisposable

    public void Dispose()
    {
    }

    #endregion
}
