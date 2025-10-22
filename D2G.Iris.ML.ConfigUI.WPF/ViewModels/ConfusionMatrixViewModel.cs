using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ConfusionMatrixCell
    {
        public ConfusionMatrixCell(
            int rowIndex,
            int columnIndex,
            double value,
            double normalizedValue,
            string rowLabel,
            string columnLabel,
            string backgroundColor,
            string foregroundColor)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            Value = value;
            NormalizedValue = normalizedValue;
            RowLabel = rowLabel;
            ColumnLabel = columnLabel;
            BackgroundColor = backgroundColor;
            ForegroundColor = foregroundColor;
        }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public double Value { get; }

        public double NormalizedValue { get; }

        public string RowLabel { get; }

        public string ColumnLabel { get; }

        public string BackgroundColor { get; }

        public string ForegroundColor { get; }

        public string DisplayValue => Value >= 1 ? Value.ToString("N0") : Value.ToString("0.###");

        public string Tooltip => $"{RowLabel} -> {ColumnLabel}: {Value:N0}";
    }

    public class ConfusionMatrixViewModel : BaseViewModel
    {
        private static readonly (double Offset, byte R, byte G, byte B)[] HeatmapGradient =
        {
            (0.0, 0xEC, 0xEF, 0xF1),
            (0.1, 0xCF, 0xD8, 0xDC),
            (0.25, 0xB0, 0xBE, 0xC5),
            (0.4, 0x90, 0xA4, 0xAE),
            (0.55, 0x60, 0x7D, 0x8B),
            (0.7, 0x45, 0x5A, 0x64),
            (0.85, 0x37, 0x47, 0x4F),
            (1.0, 0x26, 0x32, 0x38)
        };

        private ObservableCollection<string> _classLabels;
        private ObservableCollection<ConfusionMatrixCell> _cells;
        private string _accuracy = "N/A";
        private string _precision = "N/A";
        private string _recall = "N/A";
        private string _f1Score = "N/A";
        private bool _hasData;
        private double[,]? _matrixData;
        private int _rowCount;
        private int _columnCount;
        private double _maxValue = 1;

        public ConfusionMatrixViewModel()
        {
            _classLabels = new ObservableCollection<string>();
            _cells = new ObservableCollection<ConfusionMatrixCell>();
        }

        public ObservableCollection<string> ClassLabels
        {
            get => _classLabels;
            set => SetProperty(ref _classLabels, value);
        }

        public ObservableCollection<ConfusionMatrixCell> Cells
        {
            get => _cells;
            set => SetProperty(ref _cells, value);
        }

        public string Accuracy
        {
            get => _accuracy;
            set => SetProperty(ref _accuracy, value);
        }

        public string Precision
        {
            get => _precision;
            set => SetProperty(ref _precision, value);
        }

        public string Recall
        {
            get => _recall;
            set => SetProperty(ref _recall, value);
        }

        public string F1Score
        {
            get => _f1Score;
            set => SetProperty(ref _f1Score, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        public int RowCount
        {
            get => _rowCount;
            set => SetProperty(ref _rowCount, value);
        }

        public int ColumnCount
        {
            get => _columnCount;
            set => SetProperty(ref _columnCount, value);
        }

        public double MaxValue
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value);
        }

        public void UpdateConfusionMatrix(double[,] confusionMatrix, List<string> classLabels)
        {
            if (!TryPopulateMatrix(confusionMatrix, classLabels))
            {
                Clear();
                return;
            }

            var metrics = CalculateMetrics(confusionMatrix);
            ExecuteOnUiThread(() =>
            {
                Accuracy = metrics.Accuracy;
                Precision = metrics.Precision;
                Recall = metrics.Recall;
                F1Score = metrics.F1Score;
                HasData = true;
            });
        }

        public void UpdateConfusionMatrixWithMetrics(double[,] confusionMatrix, List<string> classLabels,
            double accuracy, double precision, double recall, double f1Score)
        {
            if (!TryPopulateMatrix(confusionMatrix, classLabels))
            {
                Clear();
                return;
            }

            ExecuteOnUiThread(() =>
            {
                Accuracy = $"{accuracy:P2}";
                Precision = $"{precision:P2}";
                Recall = $"{recall:P2}";
                F1Score = $"{f1Score:P2}";
                HasData = true;
            });
        }

        private bool TryPopulateMatrix(double[,] confusionMatrix, List<string> classLabels)
        {
            if (confusionMatrix == null || classLabels == null || classLabels.Count == 0)
            {
                ExecuteOnUiThread(() => HasData = false);
                return false;
            }

            _matrixData = confusionMatrix;
            int rows = confusionMatrix.GetLength(0);
            int cols = confusionMatrix.GetLength(1);

            if (rows == 0 || cols == 0)
            {
                ExecuteOnUiThread(() => HasData = false);
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"Confusion Matrix: {rows}x{cols}");

            double maxVal = 0;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    System.Diagnostics.Debug.Write($"{confusionMatrix[i, j]:F0} ");
                    if (confusionMatrix[i, j] > maxVal)
                    {
                        maxVal = confusionMatrix[i, j];
                    }
                }
                System.Diagnostics.Debug.WriteLine(string.Empty);
            }

            double safeMax = Math.Max(maxVal, 1);
            System.Diagnostics.Debug.WriteLine($"Max value in matrix: {safeMax}");

            // Check if we need to reverse labels for binary classification (Negative/Positive swap)
            var labelsCopy = classLabels.ToList();
            bool shouldReverse = rows == 2 && cols == 2 &&
                                 classLabels.Count >= 2 &&
                                 classLabels[0].Equals("Negative", StringComparison.OrdinalIgnoreCase) &&
                                 classLabels[1].Equals("Positive", StringComparison.OrdinalIgnoreCase);

            if (shouldReverse)
            {
                labelsCopy.Reverse();
            }

            var cells = new List<ConfusionMatrixCell>(rows * cols);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < cols; column++)
                {
                    double cellValue = confusionMatrix[row, column];
                    double normalized = safeMax > 0 ? cellValue / safeMax : 0;
                    normalized = Math.Clamp(normalized, 0d, 1d);

                    // Use the reversed labels if applicable
                    string rowLabel = row < labelsCopy.Count ? labelsCopy[row] : $"Class {row}";
                    string columnLabel = column < labelsCopy.Count ? labelsCopy[column] : $"Class {column}";
                    string backgroundColor = GetHeatmapColor(normalized);
                    string foregroundColor = GetForegroundColor(normalized);

                    cells.Add(new ConfusionMatrixCell(
                        row,
                        column,
                        cellValue,
                        normalized,
                        rowLabel,
                        columnLabel,
                        backgroundColor,
                        foregroundColor));
                }
            }

            return ExecuteOnUiThread(() =>
            {
                ClassLabels = new ObservableCollection<string>(labelsCopy);
                Cells = new ObservableCollection<ConfusionMatrixCell>(cells);
                RowCount = rows;
                ColumnCount = cols;
                MaxValue = safeMax;
                return true;
            });
        }

        private static string GetHeatmapColor(double normalized)
        {
            normalized = Math.Clamp(normalized, 0d, 1d);

            if (normalized <= HeatmapGradient[0].Offset)
            {
                var (_, r0, g0, b0) = HeatmapGradient[0];
                return ToColorString(0xFF, r0, g0, b0);
            }

            for (int i = 1; i < HeatmapGradient.Length; i++)
            {
                var current = HeatmapGradient[i];
                if (normalized <= current.Offset)
                {
                    var previous = HeatmapGradient[i - 1];
                    var range = current.Offset - previous.Offset;
                    if (range <= double.Epsilon)
                    {
                        return ToColorString(0xFF, current.R, current.G, current.B);
                    }

                    var t = (normalized - previous.Offset) / range;
                    byte r = (byte)Math.Round(previous.R + (current.R - previous.R) * t);
                    byte g = (byte)Math.Round(previous.G + (current.G - previous.G) * t);
                    byte b = (byte)Math.Round(previous.B + (current.B - previous.B) * t);

                    return ToColorString(0xFF, r, g, b);
                }
            }

            var last = HeatmapGradient[HeatmapGradient.Length - 1];
            return ToColorString(0xFF, last.R, last.G, last.B);
        }

        private static string GetForegroundColor(double normalized) =>
            normalized >= 0.55 ? "#FFFFFFFF" : "#FF1C2833";

        private static string ToColorString(byte a, byte r, byte g, byte b) =>
            $"#{a:X2}{r:X2}{g:X2}{b:X2}";

        private (string Accuracy, string Precision, string Recall, string F1Score) CalculateMetrics(double[,] matrix)
        {
            int numClasses = matrix.GetLength(0);

            double correctPredictions = 0;
            double totalPredictions = 0;

            for (int i = 0; i < numClasses; i++)
            {
                for (int j = 0; j < numClasses; j++)
                {
                    totalPredictions += matrix[i, j];
                    if (i == j)
                    {
                        correctPredictions += matrix[i, j];
                    }
                }
            }

            double accuracy = totalPredictions > 0 ? correctPredictions / totalPredictions : 0;

            double totalPrecision = 0;
            double totalRecall = 0;
            int validClasses = 0;

            for (int i = 0; i < numClasses; i++)
            {
                double tp = matrix[i, i];

                double fp = 0;
                for (int j = 0; j < numClasses; j++)
                {
                    if (j != i)
                    {
                        fp += matrix[j, i];
                    }
                }

                double fn = 0;
                for (int j = 0; j < numClasses; j++)
                {
                    if (j != i)
                    {
                        fn += matrix[i, j];
                    }
                }

                double classPrecision = (tp + fp) > 0 ? tp / (tp + fp) : 0;
                double classRecall = (tp + fn) > 0 ? tp / (tp + fn) : 0;

                if ((tp + fp) > 0 || (tp + fn) > 0)
                {
                    totalPrecision += classPrecision;
                    totalRecall += classRecall;
                    validClasses++;
                }
            }

            double avgPrecision = validClasses > 0 ? totalPrecision / validClasses : 0;
            double avgRecall = validClasses > 0 ? totalRecall / validClasses : 0;
            double f1 = (avgPrecision + avgRecall) > 0 ? 2 * (avgPrecision * avgRecall) / (avgPrecision + avgRecall) : 0;

            return (
                $"{accuracy:P2}",
                $"{avgPrecision:P2}",
                $"{avgRecall:P2}",
                $"{f1:P2}");
        }

        public void Clear()
        {
            ExecuteOnUiThread(() =>
            {
                Cells.Clear();
                ClassLabels.Clear();
                RowCount = 0;
                ColumnCount = 0;
                Accuracy = "N/A";
                Precision = "N/A";
                Recall = "N/A";
                F1Score = "N/A";
                MaxValue = 1;
                HasData = false;
            });
            _matrixData = null;
        }

        private static void ExecuteOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        private static T ExecuteOnUiThread<T>(Func<T> func)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return func();
            }

            return dispatcher.Invoke(func);
        }
    }
}
