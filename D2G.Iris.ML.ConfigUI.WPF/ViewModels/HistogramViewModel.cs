using System.Collections.Generic;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using SciChart.Charting.Model.DataSeries;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class HistogramViewModel : BaseViewModel
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
    }
}
