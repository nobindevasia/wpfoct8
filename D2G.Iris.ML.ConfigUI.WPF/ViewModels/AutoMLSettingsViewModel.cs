using D2G.Iris.ML.Core.Models;
using D2G.Iris.ML.Core.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class AutoMLSettingsViewModel : BaseViewModel
    {
        private bool _isEnabled;
        private int _maxExperimentTimeInSeconds = 30;
        private int _maxModelsToTry = 10;
        private string _optimizingMetric = "Accuracy";
        private bool _useCrossValidation = false;
        private int _numberOfFolds = 5;
        private string? _seed = string.Empty;
        private string _description = "AutoML is disabled. Traditional training will be used with the algorithm specified in Training Parameters.";

        public AutoMLSettingsViewModel()
        {
            UpdateAvailableMetrics(ModelType.BinaryClassification);
            UpdateDescription();
        }

        #region Properties

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    UpdateDescription();
                }
            }
        }

        public int MaxExperimentTimeInSeconds
        {
            get => _maxExperimentTimeInSeconds;
            set => SetProperty(ref _maxExperimentTimeInSeconds, System.Math.Max(1, System.Math.Min(3600, value)));
        }

        public int MaxModelsToTry
        {
            get => _maxModelsToTry;
            set => SetProperty(ref _maxModelsToTry, System.Math.Max(1, System.Math.Min(1000, value)));
        }

        public string OptimizingMetric
        {
            get => _optimizingMetric;
            set => SetProperty(ref _optimizingMetric, value);
        }

        public bool UseCrossValidation
        {
            get => _useCrossValidation;
            set => SetProperty(ref _useCrossValidation, value);
        }

        public int NumberOfFolds
        {
            get => _numberOfFolds;
            set => SetProperty(ref _numberOfFolds, System.Math.Max(2, System.Math.Min(10, value)));
        }

        public string? Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }

        public string Description
        {
            get => _description;
            private set => SetProperty(ref _description, value);
        }

        public ObservableCollection<string> AvailableMetrics { get; } = new();
        #endregion

        private void UpdateDescription()
        {
            Description = IsEnabled ? "AutoML will automatically try multiple algorithms and find the best performing model for your data."
                : "AutoML is disabled. Traditional training will be used with the algorithm specified in Training Parameters.";
        }

        public void SetConfiguration(AutoMLConfig? config)
        {
            if (config == null)
            {
                IsEnabled = false;
                MaxExperimentTimeInSeconds = 30;
                MaxModelsToTry = 10;
                OptimizingMetric = "Accuracy";
                UseCrossValidation = false;
                NumberOfFolds = 5;
                Seed = string.Empty;
                return;
            }
            IsEnabled = config.Enabled;
            MaxExperimentTimeInSeconds = config.MaxExperimentTimeInSeconds;
            MaxModelsToTry = config.MaxModels;
            OptimizingMetric = config.OptimizingMetric ?? "Accuracy";
            UseCrossValidation = config.UseCrossValidation;
            NumberOfFolds = config.NumberOfFolds;
            Seed = config.Seed?.ToString() ?? string.Empty;
        }

        public AutoMLConfig GetConfiguration()
        {

            uint? seedValue = null;
            if (!string.IsNullOrWhiteSpace(Seed) && uint.TryParse(Seed, out uint parsedSeed))
            {
                seedValue = parsedSeed;
            }

            return new AutoMLConfig
            {
                Enabled = IsEnabled,
                MaxExperimentTimeInSeconds = MaxExperimentTimeInSeconds,
                MaxModels = MaxModelsToTry,
                OptimizingMetric = OptimizingMetric,
                UseCrossValidation = UseCrossValidation,
                NumberOfFolds = NumberOfFolds,
                Seed = seedValue
            };
        }

        public void UpdateModelType(ModelType modelType)
        {
            UpdateAvailableMetrics(modelType);
        }

        private void UpdateAvailableMetrics(ModelType modelType)
        {
            AvailableMetrics.Clear();

            switch (modelType)
            {
                case ModelType.BinaryClassification:
                    foreach (var metric in new[] { "Accuracy", "AUC", "F1Score" })
                        AvailableMetrics.Add(metric);
                    OptimizingMetric = "Accuracy";
                    break;
                case ModelType.MultiClassClassification:
                    foreach (var metric in new[] { "MicroAccuracy", "MacroAccuracy" })
                        AvailableMetrics.Add(metric);
                    OptimizingMetric = "MicroAccuracy";
                    break;
                case ModelType.Regression:
                    foreach (var metric in new[] { "RSquared", "MeanAbsoluteError", "RootMeanSquaredError" })
                        AvailableMetrics.Add(metric);
                    OptimizingMetric = "RSquared";
                    break;
            }
        }
    }
}