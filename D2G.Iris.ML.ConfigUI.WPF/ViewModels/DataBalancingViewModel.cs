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
        private int _kNeighbors = 5;
        private decimal _undersamplingRatio = 0.9m;
        private decimal _minorityToMajorityRatio = 0.1m;
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
            set => SetProperty(ref _kNeighbors, Math.Max(1, Math.Min(20, value)));
        }

        public decimal UndersamplingRatio
        {
            get => _undersamplingRatio;
            set => SetProperty(ref _undersamplingRatio, Math.Max(0.1m, Math.Min(1.0m, value)));
        }

        public decimal MinorityToMajorityRatio
        {
            get => _minorityToMajorityRatio;
            set => SetProperty(ref _minorityToMajorityRatio, Math.Max(0.01m, Math.Min(1.0m, value)));
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
                KNeighbors = 5;
                UndersamplingRatio = 0.9m;
                MinorityToMajorityRatio = 0.1m;
                return;
            }

            SelectedMethod = config.Method;
            ExecutionOrder = config.ExecutionOrder;
            KNeighbors = config.KNeighbors;
            UndersamplingRatio = (decimal)config.UndersamplingRatio;
            MinorityToMajorityRatio = (decimal)config.MinorityToMajorityRatio;
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