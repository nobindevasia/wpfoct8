using System;
using System.Collections.Generic;
using System.Linq;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class DataBalancingViewModel : BaseViewModel
    {
        private DataBalanceMethod _selectedMethod = DataBalanceMethod.None;
        private int _executionOrder = 1;
        private int _kNeighbors = 0;
        private decimal _undersamplingRatio = 0.0m;
        private decimal _minorityToMajorityRatio = 0.0m;
        private string _undersamplingRatioText = "";
        private string _minorityToMajorityRatioText = "";
        private string _description = "No data balancing will be applied.";

        public DataBalancingViewModel()
        {
            UpdateDescription();
        }

        #region Properties

        public DataBalanceMethod SelectedMethod
        {
            get => _selectedMethod;
            set
            {
                if (SetProperty(ref _selectedMethod, value))
                {
                    UpdateDescription();
                    OnPropertyChanged(nameof(IsSmoteSelected));
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public int ExecutionOrder
        {
            get => _executionOrder;
            set => SetProperty(ref _executionOrder, Math.Max(1, Math.Min(2, value)));
        }

        public int KNeighbors
        {
            get => _kNeighbors;
            set
            {

                SetProperty(ref _kNeighbors, value);
            }
        }

        public decimal UndersamplingRatio
        {
            get => _undersamplingRatio;
            set
            {

                SetProperty(ref _undersamplingRatio, value);
            }
        }

        public string UndersamplingRatioText
        {
            get => _undersamplingRatioText;
            set
            {
                if (SetProperty(ref _undersamplingRatioText, value))
                {

                    if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                    {
                        _undersamplingRatio = result;
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        _undersamplingRatio = 0m;
                    }
                }
            }
        }

        public decimal MinorityToMajorityRatio
        {
            get => _minorityToMajorityRatio;
            set
            {

                SetProperty(ref _minorityToMajorityRatio, value);
            }
        }

        public string MinorityToMajorityRatioText
        {
            get => _minorityToMajorityRatioText;
            set
            {
                if (SetProperty(ref _minorityToMajorityRatioText, value))
                {

                    if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                    {
                        _minorityToMajorityRatio = result;
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        _minorityToMajorityRatio = 0m;
                    }
                }
            }
        }

        public string Description
        {
            get => _description;
            private set => SetProperty(ref _description, value);
        }


        public bool IsEnabled => SelectedMethod != DataBalanceMethod.None;

        public bool IsSmoteSelected => SelectedMethod == DataBalanceMethod.SMOTE;

        public IEnumerable<DataBalanceMethod> AvailableMethods => Enum.GetValues<DataBalanceMethod>();

        #endregion

        private void UpdateDescription()
        {
            Description = SelectedMethod switch
            {
                DataBalanceMethod.SMOTE => "SMOTE (Synthetic Minority Oversampling Technique) generates synthetic samples for the minority class to balance the dataset.",
                DataBalanceMethod.None => "No data balancing will be applied.",
                _ => "No data balancing will be applied."
            };
        }

        public void SetConfiguration(DataBalancingConfig? config)
        {
            if (config == null)
            {
                SelectedMethod = DataBalanceMethod.None;
                ExecutionOrder = 1;
                KNeighbors = 0;
                UndersamplingRatio = 0.0m;
                MinorityToMajorityRatio = 0.0m;
                UndersamplingRatioText = "";
                MinorityToMajorityRatioText = "";
                return;
            }

            SelectedMethod = config.Method;
            ExecutionOrder = config.ExecutionOrder;
            KNeighbors = config.KNeighbors;
            UndersamplingRatio = (decimal)config.UndersamplingRatio;
            MinorityToMajorityRatio = (decimal)config.MinorityToMajorityRatio;
            UndersamplingRatioText = config.UndersamplingRatio > 0 ? config.UndersamplingRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            MinorityToMajorityRatioText = config.MinorityToMajorityRatio > 0 ? config.MinorityToMajorityRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        }

        public DataBalancingConfig GetConfiguration()
        {

            return new DataBalancingConfig
            {
                Method = SelectedMethod,
                ExecutionOrder = ExecutionOrder,
                KNeighbors = KNeighbors,
                UndersamplingRatio = (float)UndersamplingRatio,
                MinorityToMajorityRatio = (float)MinorityToMajorityRatio
            };
        }
    }
}