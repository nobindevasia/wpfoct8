using D2G.Iris.ML.Core.Models;
using System.Collections.ObjectModel;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class ClusteringParametersViewModel : BaseViewModel
    {
        private int _numberOfClusters = 3;
        private int _maxIterations = 100;
        private string _algorithm = "KMeans";
        private bool _useNormalization = true;
        private string _description = "K-Means clustering will group your data into distinct clusters based on feature similarity.";

        public ClusteringParametersViewModel()
        {
            AvailableAlgorithms.Add("KMeans");
            UpdateDescription();
        }

        #region Properties

        public int NumberOfClusters
        {
            get => _numberOfClusters;
            set
            {
                if (SetProperty(ref _numberOfClusters, System.Math.Max(2, System.Math.Min(20, value))))
                {
                    UpdateDescription();
                }
            }
        }

        public int MaxIterations
        {
            get => _maxIterations;
            set => SetProperty(ref _maxIterations, System.Math.Max(10, System.Math.Min(1000, value)));
        }

        public string Algorithm
        {
            get => _algorithm;
            set
            {
                if (SetProperty(ref _algorithm, value))
                {
                    UpdateDescription();
                }
            }
        }

        public bool UseNormalization
        {
            get => _useNormalization;
            set => SetProperty(ref _useNormalization, value);
        }

        public string Description
        {
            get => _description;
            private set => SetProperty(ref _description, value);
        }

        public ObservableCollection<string> AvailableAlgorithms { get; } = new();

        #endregion

        private void UpdateDescription()
        {
            Description = $"{Algorithm} clustering will partition your data into {NumberOfClusters} distinct groups. " +
                         $"Data points within the same cluster will be more similar to each other than to points in other clusters.";
        }

        public void SetConfiguration(ClusteringConfig? config)
        {
            if (config == null)
            {
                NumberOfClusters = 3;
                MaxIterations = 100;
                Algorithm = "KMeans";
                UseNormalization = true;
                return;
            }

            NumberOfClusters = config.NumberOfClusters;
            MaxIterations = config.MaxIterations;
            Algorithm = config.Algorithm ?? "KMeans";
            UseNormalization = config.UseNormalization;
        }

        public ClusteringConfig GetConfiguration()
        {
            return new ClusteringConfig
            {
                NumberOfClusters = NumberOfClusters,
                MaxIterations = MaxIterations,
                Algorithm = Algorithm,
                UseNormalization = UseNormalization
            };
        }
    }
}
