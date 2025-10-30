using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class FeatureEngineeringViewModel : BaseViewModel
    {
        private FeatureSelectionMethod _selectedMethod = FeatureSelectionMethod.None;
        private int _executionOrder = 2;
        private int _numberOfComponents = 3;
        private int _maxFeatures = 10;
        private decimal _multicollinearityThreshold = 0.7m;
        private string _multicollinearityThresholdText = "0.7";
        private string _description = "No feature selection will be applied. All enabled features will be used for training.";

        public FeatureEngineeringViewModel()
        {
            UpdateDescription();
        }

        #region Properties

        public FeatureSelectionMethod SelectedMethod
        {
            get => _selectedMethod;
            set
            {
                if (SetProperty(ref _selectedMethod, value))
                {
                    UpdateDescription();
                    OnPropertyChanged(nameof(IsPcaSelected));
                    OnPropertyChanged(nameof(IsCorrelationSelected));
                    OnPropertyChanged(nameof(IsMethodSelected));
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public int ExecutionOrder
        {
            get => _executionOrder;
            set => SetProperty(ref _executionOrder, Math.Max(1, Math.Min(2, value)));
        }

        public int NumberOfComponents
        {
            get => _numberOfComponents;
            set => SetProperty(ref _numberOfComponents, Math.Max(1, Math.Min(50, value)));
        }

        public int MaxFeatures
        {
            get => _maxFeatures;
            set => SetProperty(ref _maxFeatures, Math.Max(1, Math.Min(100, value)));
        }

        public decimal MulticollinearityThreshold
        {
            get => _multicollinearityThreshold;
            set => SetProperty(ref _multicollinearityThreshold, Math.Max(0.1m, Math.Min(1.0m, value)));
        }

        public string MulticollinearityThresholdText
        {
            get => _multicollinearityThresholdText;
            set
            {
                if (SetProperty(ref _multicollinearityThresholdText, value))
                {

                    if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                    {
                        _multicollinearityThreshold = Math.Max(0.1m, Math.Min(1.0m, result));
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        _multicollinearityThreshold = 0.7m;
                    }
                }
            }
        }

        public string Description
        {
            get => _description;
            private set => SetProperty(ref _description, value);
        }


        public bool IsEnabled => SelectedMethod != FeatureSelectionMethod.None;

        public bool IsPcaSelected => SelectedMethod == FeatureSelectionMethod.PCA;
        public bool IsCorrelationSelected => SelectedMethod == FeatureSelectionMethod.Correlation;
        public bool IsMethodSelected => SelectedMethod != FeatureSelectionMethod.None;

        public IEnumerable<FeatureSelectionMethod> AvailableMethods => Enum.GetValues<FeatureSelectionMethod>();

        #endregion

        private void UpdateDescription()
        {
            Description = SelectedMethod switch
            {
                FeatureSelectionMethod.None => "No feature selection will be applied. All enabled features will be used for training.",
                FeatureSelectionMethod.Correlation => "Correlation-based feature selection removes highly correlated features and selects features with strong correlation to the target variable. Configure the multicollinearity threshold and maximum number of features.",
                FeatureSelectionMethod.PCA => "Principal Component Analysis (PCA) reduces dimensionality by creating new features that are linear combinations of the original features. Specify the number of principal components to retain.",
                _ => "No feature selection will be applied. All enabled features will be used for training."
            };
        }

        public void SetConfiguration(FeatureEngineeringConfig? config)
        {
            if (config == null)
            {
                SelectedMethod = FeatureSelectionMethod.None;
                ExecutionOrder = 2;
                NumberOfComponents = 3;
                MaxFeatures = 10;
                MulticollinearityThreshold = 0.7m;
                MulticollinearityThresholdText = "0.7";
                return;
            }

            SelectedMethod = config.Method;
            ExecutionOrder = config.ExecutionOrder;
            NumberOfComponents = config.NumberOfComponents;
            MaxFeatures = config.MaxFeatures;
            MulticollinearityThreshold = (decimal)config.MulticollinearityThreshold;
            MulticollinearityThresholdText = config.MulticollinearityThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public FeatureEngineeringConfig GetConfiguration()
        {
            return new FeatureEngineeringConfig
            {
                Method = SelectedMethod,
                ExecutionOrder = ExecutionOrder,
                NumberOfComponents = NumberOfComponents,
                MaxFeatures = MaxFeatures,
                MulticollinearityThreshold = (double)MulticollinearityThreshold
            };
        }
    }
}