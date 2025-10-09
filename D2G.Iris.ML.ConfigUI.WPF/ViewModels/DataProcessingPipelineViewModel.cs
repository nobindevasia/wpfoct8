using System;
using System.ComponentModel;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class DataProcessingPipelineViewModel : BaseViewModel
    {
        private readonly DataBalancingViewModel _dataBalancingViewModel;
        private readonly FeatureEngineeringViewModel _featureEngineeringViewModel;
        private string _intermediateResultsTableName = "";
        private string _intermediateResultsDatabase = "";
        private string _intermediateResultsSchema = "";

        public DataProcessingPipelineViewModel()
        {
            _dataBalancingViewModel = new DataBalancingViewModel();
            _featureEngineeringViewModel = new FeatureEngineeringViewModel();

            _dataBalancingViewModel.ExecutionOrder = 1;
            _featureEngineeringViewModel.ExecutionOrder = 2;

            _dataBalancingViewModel.PropertyChanged += OnChildViewModelPropertyChanged;
            _featureEngineeringViewModel.PropertyChanged += OnChildViewModelPropertyChanged;
        }

        #region Properties

        public DataBalancingViewModel DataBalancing => _dataBalancingViewModel;
        public FeatureEngineeringViewModel FeatureEngineering => _featureEngineeringViewModel;

        public bool IsDataBalancingEnabled
        {
            get => _dataBalancingViewModel.IsEnabled;
            set
            {
                if (value != _dataBalancingViewModel.IsEnabled)
                {
                    _dataBalancingViewModel.SelectedMethod = value
                        ? DataBalanceMethod.SMOTE
                        : DataBalanceMethod.None;
                }
            }
        }

        public bool IsFeatureEngineeringEnabled
        {
            get => _featureEngineeringViewModel.SelectedMethod != FeatureSelectionMethod.None;
            set
            {
                var currentlyEnabled = _featureEngineeringViewModel.SelectedMethod != FeatureSelectionMethod.None;
                if (value != currentlyEnabled)
                {
                    _featureEngineeringViewModel.SelectedMethod = value
                        ? FeatureSelectionMethod.Correlation
                        : FeatureSelectionMethod.None;
                }
            }
        }

        public int DataBalancingExecutionOrder
        {
            get => _dataBalancingViewModel.ExecutionOrder;
            set => _dataBalancingViewModel.ExecutionOrder = value;
        }

        public int FeatureEngineeringExecutionOrder
        {
            get => _featureEngineeringViewModel.ExecutionOrder;
            set => _featureEngineeringViewModel.ExecutionOrder = value;
        }

        public string DataBalancingExecutionOrderString
        {
            get => _dataBalancingViewModel.ExecutionOrder.ToString();
            set
            {
                if (int.TryParse(value, out int order))
                {
                    _dataBalancingViewModel.ExecutionOrder = order;
                }
            }
        }

        public string FeatureEngineeringExecutionOrderString
        {
            get => _featureEngineeringViewModel.ExecutionOrder.ToString();
            set
            {
                if (int.TryParse(value, out int order))
                {
                    _featureEngineeringViewModel.ExecutionOrder = order;
                }
            }
        }

        public string IntermediateResultsTableName
        {
            get => _intermediateResultsTableName;
            set => SetProperty(ref _intermediateResultsTableName, value);
        }

        public string IntermediateResultsDatabase
        {
            get => _intermediateResultsDatabase;
            set => SetProperty(ref _intermediateResultsDatabase, value);
        }

        public string IntermediateResultsSchema
        {
            get => _intermediateResultsSchema;
            set => SetProperty(ref _intermediateResultsSchema, value);
        }

        /// <summary>
        /// Gets the full table name with database and schema (e.g., MyDB.dbo.IntermediateTable)
        /// </summary>
        public string FullIntermediateTableName
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();

                if (!string.IsNullOrWhiteSpace(_intermediateResultsDatabase))
                    parts.Add($"[{_intermediateResultsDatabase}]");

                if (!string.IsNullOrWhiteSpace(_intermediateResultsSchema))
                    parts.Add($"[{_intermediateResultsSchema}]");

                if (!string.IsNullOrWhiteSpace(_intermediateResultsTableName))
                    parts.Add($"[{_intermediateResultsTableName}]");

                return string.Join(".", parts);
            }
        }

        #endregion

        #region Private Methods

        private void OnChildViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {

            if (e.PropertyName == nameof(DataBalancingViewModel.IsEnabled))
            {
                OnPropertyChanged(nameof(IsDataBalancingEnabled));
            }
            else if (e.PropertyName == nameof(FeatureEngineeringViewModel.SelectedMethod))
            {
                OnPropertyChanged(nameof(IsFeatureEngineeringEnabled));
            }
            else if (e.PropertyName == nameof(DataBalancingViewModel.ExecutionOrder))
            {
                OnPropertyChanged(nameof(DataBalancingExecutionOrder));
                OnPropertyChanged(nameof(DataBalancingExecutionOrderString));
            }
            else if (e.PropertyName == nameof(FeatureEngineeringViewModel.ExecutionOrder))
            {
                OnPropertyChanged(nameof(FeatureEngineeringExecutionOrder));
                OnPropertyChanged(nameof(FeatureEngineeringExecutionOrderString));
            }
        }


        #endregion

        #region Public Methods

        public void LoadFromConfig(ModelConfig config)
        {
            _dataBalancingViewModel.SetConfiguration(config.DataBalancing);
            _featureEngineeringViewModel.SetConfiguration(config.FeatureEngineering);

            // Parse OutputTableName if it contains database.schema.table format
            var outputTableName = config.Database?.OutputTableName ?? "";
            ParseFullTableName(outputTableName);
        }

        public void SaveToConfig(ModelConfig config)
        {
            config.DataBalancing = _dataBalancingViewModel.GetConfiguration();
            config.FeatureEngineering = _featureEngineeringViewModel.GetConfiguration();
            if (config.Database != null)
            {
                // Save as full table name with database and schema
                config.Database.OutputTableName = FullIntermediateTableName;
            }
        }

        public void ResetToDefaults()
        {
            _dataBalancingViewModel.SetConfiguration(null);
            _featureEngineeringViewModel.SetConfiguration(null);
            IntermediateResultsTableName = "";
            IntermediateResultsDatabase = "";
            IntermediateResultsSchema = "";
        }

        private void ParseFullTableName(string fullTableName)
        {
            if (string.IsNullOrWhiteSpace(fullTableName))
            {
                IntermediateResultsTableName = "";
                IntermediateResultsDatabase = "";
                IntermediateResultsSchema = "";
                return;
            }

            // Remove brackets and parse: [Database].[Schema].[Table] or [Schema].[Table] or [Table]
            var cleaned = fullTableName.Replace("[", "").Replace("]", "");
            var parts = cleaned.Split('.');

            if (parts.Length == 3)
            {
                IntermediateResultsDatabase = parts[0];
                IntermediateResultsSchema = parts[1];
                IntermediateResultsTableName = parts[2];
            }
            else if (parts.Length == 2)
            {
                IntermediateResultsDatabase = "";
                IntermediateResultsSchema = parts[0];
                IntermediateResultsTableName = parts[1];
            }
            else
            {
                IntermediateResultsDatabase = "";
                IntermediateResultsSchema = "";
                IntermediateResultsTableName = parts[0];
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dataBalancingViewModel.PropertyChanged -= OnChildViewModelPropertyChanged;
                _featureEngineeringViewModel.PropertyChanged -= OnChildViewModelPropertyChanged;

                _dataBalancingViewModel.Dispose();
                _featureEngineeringViewModel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}