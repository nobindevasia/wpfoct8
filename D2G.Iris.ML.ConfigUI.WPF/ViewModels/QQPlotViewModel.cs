using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SciChart.Charting.Model.DataSeries;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.ConfigUI.WPF.Commands;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class QQPlotViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;

        private string? _connectionString;
        private string? _tableName;
        private string[]? _columns;
        private string? _whereClause;

        private ObservableCollection<string> _numericColumns;
        private string? _selectedColumn;
        private XyDataSeries<double, double>? _qqPlotData;
        private XyDataSeries<double, double>? _referenceLine;
        private bool _isGeneratingPlot;
        private string _progressMessage = string.Empty;
        private string _normalityInfo = string.Empty;

        public QQPlotViewModel(IDialogService dialogService, IDatabaseAnalyticsService databaseAnalytics)
        {
            _dialogService = dialogService;
            _databaseAnalytics = databaseAnalytics;
            _numericColumns = new ObservableCollection<string>();

            GeneratePlotCommand = new AsyncRelayCommand(async _ => await GenerateQQPlotAsync(), _ => CanGeneratePlot());
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

        public XyDataSeries<double, double>? QQPlotData
        {
            get => _qqPlotData;
            set => SetProperty(ref _qqPlotData, value);
        }

        public XyDataSeries<double, double>? ReferenceLine
        {
            get => _referenceLine;
            set => SetProperty(ref _referenceLine, value);
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

        public string NormalityInfo
        {
            get => _normalityInfo;
            set => SetProperty(ref _normalityInfo, value);
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

            NumericColumns.Clear();
            foreach (var column in columns)
            {
                NumericColumns.Add(column);
            }

            if (NumericColumns.Any())
            {
                SelectedColumn = NumericColumns.First();
            }
        }

        #endregion

        #region Private Methods

        private bool CanGeneratePlot()
        {
            return !string.IsNullOrEmpty(_selectedColumn) && !_isGeneratingPlot;
        }

        private async Task GenerateQQPlotAsync()
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(_tableName) || string.IsNullOrEmpty(_selectedColumn))
            {
                _dialogService.ShowErrorDialog("Missing required parameters for QQ plot generation.", "Error");
                return;
            }

            try
            {
                IsGeneratingPlot = true;
                ProgressMessage = $"Generating QQ Plot for {_selectedColumn}...";

                var distributionData = await _databaseAnalytics.GetDistributionDataAsync(
                    _connectionString,
                    _tableName,
                    _selectedColumn,
                    _whereClause,
                    10000);

                if (distributionData == null || distributionData.Count == 0)
                {
                    _dialogService.ShowErrorDialog("No data available for the selected column.", "Error");
                    return;
                }

                var sortedData = distributionData.OrderBy((double x) => x).ToList();
                var n = sortedData.Count;

                var qqData = new XyDataSeries<double, double> { SeriesName = "Sample Quantiles" };
                var refLine = new XyDataSeries<double, double> { SeriesName = "Normal Reference" };

                double minTheoretical = double.MaxValue;
                double maxTheoretical = double.MinValue;

                for (int i = 0; i < n; i++)
                {
                    double p = (i + 0.5) / n;
                    double theoreticalQuantile = NormalQuantile(p);
                    double sampleQuantile = sortedData[i];

                    qqData.Append(theoreticalQuantile, sampleQuantile);

                    if (theoreticalQuantile < minTheoretical) minTheoretical = theoreticalQuantile;
                    if (theoreticalQuantile > maxTheoretical) maxTheoretical = theoreticalQuantile;
                }

                var stats = await _databaseAnalytics.GetColumnStatisticsAsync(
                    _connectionString,
                    _tableName,
                    new[] { _selectedColumn },
                    _whereClause);

                if (stats != null && stats.Count > 0)
                {
                    var colStats = stats[0];
                    double mean = colStats.Mean ?? 0;
                    double stdDev = colStats.StdDev ?? 1;

                    double refMin = minTheoretical * stdDev + mean;
                    double refMax = maxTheoretical * stdDev + mean;
                    refLine.Append(minTheoretical, refMin);
                    refLine.Append(maxTheoretical, refMax);

                    double correlation = CalculateCorrelation(qqData);
                    NormalityInfo = $"R² = {correlation:F4} (closer to 1 indicates better fit to normal distribution)";
                }

                QQPlotData = qqData;
                ReferenceLine = refLine;

                ProgressMessage = "QQ Plot generated successfully!";
            }
            catch (Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error generating QQ plot: {ex.Message}", "Error");
                ProgressMessage = "Error generating QQ plot.";
            }
            finally
            {
                IsGeneratingPlot = false;
            }
        }

        private double NormalQuantile(double p)
        {
            if (p <= 0) return double.NegativeInfinity;
            if (p >= 1) return double.PositiveInfinity;

            double a1 = -39.6968302866538;
            double a2 = 220.946098424521;
            double a3 = -275.928510446969;
            double a4 = 138.357751867269;
            double a5 = -30.6647980661472;
            double a6 = 2.50662827745924;

            double b1 = -54.4760987982241;
            double b2 = 161.585836858041;
            double b3 = -155.698979859887;
            double b4 = 66.8013118877197;
            double b5 = -13.2806815528857;

            double c1 = -0.00778489400243029;
            double c2 = -0.322396458041136;
            double c3 = -2.40075827716184;
            double c4 = -2.54973253934373;
            double c5 = 4.37466414146497;
            double c6 = 2.93816398269878;

            double d1 = 0.00778469570904146;
            double d2 = 0.32246712907004;
            double d3 = 2.445134137143;
            double d4 = 3.75440866190742;

            double pLow = 0.02425;
            double pHigh = 1 - pLow;

            double q, r, result;

            if (p < pLow)
            {
                q = Math.Sqrt(-2 * Math.Log(p));
                result = (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) /
                         ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
            }
            else if (p <= pHigh)
            {
                q = p - 0.5;
                r = q * q;
                result = (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * q /
                         (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1);
            }
            else
            {
                q = Math.Sqrt(-2 * Math.Log(1 - p));
                result = -(((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) /
                          ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
            }

            return result;
        }

        private double CalculateCorrelation(XyDataSeries<double, double> data)
        {
            if (data.Count < 2) return 0;

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            int n = data.Count;

            for (int i = 0; i < n; i++)
            {
                double x = data.XValues[i];
                double y = data.YValues[i];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
                sumY2 += y * y;
            }

            double numerator = (n * sumXY) - (sumX * sumY);
            double denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));

            if (denominator == 0) return 0;

            double r = numerator / denominator;
            return r * r;
        }

        #endregion
    }
}
