using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.ML;
using Microsoft.ML.Data;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;
using D2G.Iris.ML.Core.Interfaces;
using D2G.Iris.ML.Configuration;
using D2G.Iris.ML.Data;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IConfigurationService _configService;
        private readonly IDialogService _dialogService;
        private readonly IConfigManager _configManager;
        private readonly ISqlHandler _sqlHandler;
        private readonly IDataLoader _dataLoader;
        private readonly IDataProcessor _dataProcessor;
        private readonly IModelTrainerFactory _modelTrainerFactory;
        private readonly IDatabaseAnalyticsService _databaseAnalytics;
        private ModelConfig? _currentConfig;
        private string? _currentFilePath;
        private string _windowTitle = "Iris ML Config";
        private bool _isTraining;
        private int _selectedTabIndex;

        public MainWindowViewModel(
            IConfigurationService configService,
            IDialogService dialogService,
            IConfigManager configManager,
            ISqlHandler sqlHandler,
            IDataLoader dataLoader,
            IDataProcessor dataProcessor,
            IModelTrainerFactory modelTrainerFactory,
            IDatabaseAnalyticsService databaseAnalytics,
            GeneralSettingsViewModel generalSettings,
            DatabaseSettingsViewModel databaseSettings,
            InputFieldsViewModel inputFields,
            ExploratoryDataAnalysisViewModel exploratoryDataAnalysis,
            TrainingParametersViewModel trainingParameters,
            DataProcessingPipelineViewModel dataProcessingPipeline,
            TrainingLogsViewModel trainingLogs,
            PostTrainingVisualizationsViewModel postTrainingVisualizations)
        {
            _configService = configService;
            _dialogService = dialogService;
            _configManager = configManager;
            _sqlHandler = sqlHandler;
            _dataLoader = dataLoader;
            _dataProcessor = dataProcessor;
            _modelTrainerFactory = modelTrainerFactory;
            _databaseAnalytics = databaseAnalytics;

            GeneralSettings = generalSettings;
            DatabaseSettings = databaseSettings;
            InputFields = inputFields;
            ExploratoryDataAnalysis = exploratoryDataAnalysis;
            TrainingParameters = trainingParameters;
            DataProcessingPipeline = dataProcessingPipeline;
            TrainingLogs = trainingLogs;
            PostTrainingVisualizations = postTrainingVisualizations;

            InitializeViewModelDependencies();
            InitializeCommands();
            LoadExistingConfigOnStartup();
        }

        #region Properties

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public bool IsTraining
        {
            get => _isTraining;
            set => SetProperty(ref _isTraining, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public GeneralSettingsViewModel GeneralSettings { get; private set; } = null!;
        public DatabaseSettingsViewModel DatabaseSettings { get; private set; } = null!;
        public InputFieldsViewModel InputFields { get; private set; } = null!;
        public ExploratoryDataAnalysisViewModel ExploratoryDataAnalysis { get; private set; } = null!;
        public TrainingParametersViewModel TrainingParameters { get; private set; } = null!;
        public DataProcessingPipelineViewModel DataProcessingPipeline { get; private set; } = null!;
        public TrainingLogsViewModel TrainingLogs { get; private set; } = null!;
        public PostTrainingVisualizationsViewModel PostTrainingVisualizations { get; private set; } = null!;

        #endregion

        #region Commands

        public ICommand NewConfigCommand { get; private set; } = null!;
        public ICommand OpenConfigCommand { get; private set; } = null!;
        public ICommand SaveConfigCommand { get; private set; } = null!;
        public ICommand ExitCommand { get; private set; } = null!;
        public ICommand LaunchTrainingCommand { get; private set; } = null!;

        #endregion

        private void InitializeViewModelDependencies()
        {
            InputFields.SetDependencies(() => DatabaseSettings.GetConfiguration(), () => TrainingParameters.TargetField);
            ExploratoryDataAnalysis.SetDependencies(() => DatabaseSettings.GetConfiguration(), () => InputFields.GetConfiguration(), () => TrainingParameters.TargetField);
        }

        private void InitializeCommands()
        {
            NewConfigCommand = new RelayCommand(CreateNewConfiguration);
            OpenConfigCommand = new RelayCommand(OpenConfiguration);
            SaveConfigCommand = new RelayCommand(SaveConfiguration);
            ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());
            LaunchTrainingCommand = new AsyncRelayCommand(LaunchTraining, () => !IsTraining);
        }

        private void LoadExistingConfigOnStartup()
        {
            try
            {
                string appConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelconfig.json");

                if (File.Exists(appConfigPath))
                {
                    try
                    {
                        _currentConfig = _configService.LoadConfiguration(appConfigPath);
                        _currentFilePath = appConfigPath;
                        UpdateFormTitle();
                        UpdateUIFromConfig();

                        TrainingLogs.LogMessage("Successfully loaded configuration from: " + appConfigPath, "Success");
                        return;
                    }
                    catch (Exception loadEx)
                    {
                        TrainingLogs.LogMessage($"Error loading configuration: {loadEx.Message}", "Error");
                    }
                }

                CreateNewConfiguration();
            }
            catch (Exception ex)
            {
                CreateNewConfiguration();
                TrainingLogs.LogMessage($"Unexpected error: {ex.Message}", "Error");
                TrainingLogs.LogMessage("Created a new configuration.", "Warning");
            }
        }

        private void CreateNewConfiguration()
        {
            _currentConfig = new ModelConfig
            {
                Author = Environment.UserName,
                Description = "New Model Configuration",
                ModelType = ModelType.BinaryClassification,
                TargetField = "Label",
                Database = new DatabaseConfig
                {
                    Server = "localhost",
                    Database = "IrisData",
                    TableName = "DataTable",
                    OutputTableName = "",
                    WhereClause = ""
                },
                TrainingParameters = new TrainingParameters
                {
                    Algorithm = "fasttree",
                    TestFraction = 0.2,
                    AlgorithmParameters = new Dictionary<string, object>
                    {
                        { "NumberOfLeaves", 20 }
                    }
                },
                InputFields = new List<InputField>(),
                FeatureEngineering = new FeatureEngineeringConfig
                {
                    Method = FeatureSelectionMethod.None,
                    ExecutionOrder = 2,
                    NumberOfComponents = 3,
                    MaxFeatures = 10,
                    MulticollinearityThreshold = 0.7
                },
                DataBalancing = new DataBalancingConfig
                {
                    Method = DataBalanceMethod.None,
                    ExecutionOrder = 1,
                    KNeighbors = 5,
                    UndersamplingRatio = 0.9f,
                    MinorityToMajorityRatio = 0.1f
                },
                AutoML = new AutoMLConfig
                {
                    Enabled = false,
                    MaxExperimentTimeInSeconds = 30,
                    MaxModels = 10,
                    OptimizingMetric = "Accuracy"
                }
            };

            _currentFilePath = null;
            UpdateFormTitle();
            UpdateUIFromConfig();

            TrainingLogs.LogMessage("Created new configuration", "Info");
        }

        private void OpenConfiguration()
        {
            var filePath = _dialogService.ShowOpenFileDialog("JSON files (*.json)|*.json|All files (*.*)|*.*");
            if (filePath != null)
            {
                try
                {
                    _currentConfig = _configService.LoadConfiguration(filePath);
                    _currentFilePath = filePath;
                    UpdateFormTitle();
                    UpdateUIFromConfig();

                    TrainingLogs.LogMessage($"Loaded configuration from: {_currentFilePath}", "Success");
                    _dialogService.ShowInfoDialog("Configuration loaded successfully.", "Success");
                }
                catch (Exception ex)
                {
                    TrainingLogs.LogMessage($"Error loading configuration: {ex.Message}", "Error");
                    _dialogService.ShowErrorDialog($"Error loading configuration: {ex.Message}", "Error");
                }
            }
        }

        private void SaveConfiguration()
        {
            UpdateConfigFromUI();

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                var filePath = _dialogService.ShowSaveFileDialog(
                    "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    "json",
                    "modelconfig.json");

                if (filePath == null) return;

                _currentFilePath = filePath;
            }

            try
            {
                _configService.SaveConfiguration(_currentConfig!, _currentFilePath);

                string appConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelconfig.json");
                if (_currentFilePath != appConfigPath)
                {
                    _configService.SaveConfiguration(_currentConfig!, appConfigPath);
                }

                UpdateFormTitle();
                TrainingLogs.LogMessage($"Configuration saved to: {_currentFilePath}", "Success");
                _dialogService.ShowInfoDialog("Configuration saved successfully.", "Success");
            }
            catch (Exception ex)
            {
                TrainingLogs.LogMessage($"Error saving configuration: {ex.Message}", "Error");
                _dialogService.ShowErrorDialog($"Error saving configuration: {ex.Message}", "Error");
            }
        }

        private async Task LaunchTraining()
        {
            try
            {
                if (_currentConfig == null)
                {
                    _dialogService.ShowErrorDialog("Please create a configuration first.", "No Configuration");
                    return;
                }

                UpdateConfigFromUI();

                if (!_configService.ValidateConfiguration(_currentConfig))
                {
                    _dialogService.ShowErrorDialog("Configuration validation failed. Please check all required fields.", "Validation Error");
                    return;
                }

                string confirmationMessage = _currentConfig.AutoML?.Enabled == true
                    ? $"Are you sure you want to start AutoML training? This will run for up to {_currentConfig.AutoML.MaxExperimentTimeInSeconds} seconds."
                    : "Are you sure you want to start the training process?";

                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    if (_dialogService.ShowConfirmationDialog("Configuration needs to be saved before training. Save now?", "Save Required"))
                    {
                        SaveConfiguration();
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    _configService.SaveConfiguration(_currentConfig, _currentFilePath);

                    string appConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelconfig.json");
                    if (_currentFilePath != appConfigPath)
                    {
                        _configService.SaveConfiguration(_currentConfig, appConfigPath);
                    }
                }

                if (!_dialogService.ShowConfirmationDialog(confirmationMessage, "Confirm Training"))
                {
                    return;
                }

                SelectedTabIndex = 6;

                TrainingLogs.ClearLogs();
                IsTraining = true;

                await RunTrainingProcess();

                TrainingLogs.LogMessage("Cleaning up temporary database views...", "Info");
                ExploratoryDataAnalysis.CleanupAfterTraining();
                TrainingLogs.LogMessage("Cleanup completed successfully", "Success");

                _dialogService.ShowInfoDialog("Training process completed! Check the Training Logs tab for details.", "Training Complete");
            }
            catch (Exception ex)
            {
                TrainingLogs.LogMessage($"ERROR: {ex.Message}", "Error");
                if (ex.InnerException != null)
                {
                    TrainingLogs.LogMessage($"Inner exception: {ex.InnerException.Message}", "Error");
                }
                TrainingLogs.LogMessage($"Stack trace: {ex.StackTrace}", "Error");

                _dialogService.ShowErrorDialog($"Error during training: {ex.Message}", "Training Error");
            }
            finally
            {
                IsTraining = false;

                try
                {
                    ExploratoryDataAnalysis.CleanupAfterTraining();
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"Warning: Cleanup after training failed: {cleanupEx.Message}");
                }
            }
        }

        private async Task RunTrainingProcess()
        {
            await Task.Run(async () =>
            {
                try
                {
                    TrainingLogs.LogMessage("=============== Starting Training Process ===============", "Info");
                    TrainingLogs.LogMessage("Using Database-Side Analytics for optimized data loading", "Info");

                    string tempConfigPath = Path.Combine(Path.GetTempPath(), "modelconfig.json");
                    var serializableConfig = new Dictionary<string, ModelConfig>
                    {
                        { "modelConfig", _currentConfig! }
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    string jsonConfig = JsonSerializer.Serialize(serializableConfig, options);
                    File.WriteAllText(tempConfigPath, jsonConfig);

                    var config = _configManager.LoadConfiguration(tempConfigPath);

                    _sqlHandler.Connect(config.Database);

                    var enabledFields = config.InputFields
                        .Where(f => f.IsEnabled)
                        .Select(f => f.Name)
                        .ToArray();

                    int? mlContextSeed = config.AutoML?.Seed.HasValue == true
                        ? (int)config.AutoML.Seed.Value
                        : 42;

                    var mlContext = new Microsoft.ML.MLContext(seed: mlContextSeed);
                    TrainingLogs.LogMessage($"ML Context initialized with seed: {mlContextSeed?.ToString() ?? "random"}", "Info");

                    Microsoft.ML.IDataView rawData;

                    TrainingLogs.LogMessage("=============== Loading Data ===============", "Info");

                    if (ExploratoryDataAnalysis.HasDataBeenLoaded())
                    {
                        TrainingLogs.LogMessage("EDA analysis detected - checking for cleaned data...", "Info");

                        if (ExploratoryDataAnalysis.HasDataBeenCleaned())
                        {
                            TrainingLogs.LogMessage("Using cleaned data from outlier detection analysis", "Info");

                            rawData = await ExploratoryDataAnalysis.GetCleanedDataAsIDataViewAsync(
                                mlContext,
                                enabledFields,
                                config.TargetField,
                                config.ModelType);

                            if (rawData == null)
                            {
                                TrainingLogs.LogMessage("Warning: Could not load cleaned data, falling back to original data", "Warning");
                                rawData = LoadDataFromDatabase(config, enabledFields, mlContext);
                            }
                            else
                            {
                                TrainingLogs.LogMessage("Successfully loaded cleaned data for training", "Success");
                            }
                        }
                        else
                        {
                            TrainingLogs.LogMessage("No data cleaning applied - using original database data", "Info");
                            rawData = LoadDataFromDatabase(config, enabledFields, mlContext);
                        }
                    }
                    else
                    {
                        rawData = LoadDataFromDatabase(config, enabledFields, mlContext);
                    }

                    var dataRowCount = rawData.GetRowCount();
                    var rowCountText = dataRowCount.HasValue ? $"{dataRowCount:N0}" : "streaming (count from database loader)";
                    TrainingLogs.LogMessage($"Target field: {config.TargetField}", "Info");
                    TrainingLogs.LogMessage($"Model type: {config.ModelType}", "Info");

                    TrainingLogs.LogMessage("=============== Processing Data ===============", "Info");
                    var processedData = _dataProcessor.ProcessData(
                        mlContext,
                        rawData,
                        enabledFields,
                        config).GetAwaiter().GetResult();

                    TrainingLogs.LogMessage($"Data processing completed. Feature count: {processedData.FeatureNames?.Length ?? 0}", "Success");

                    TrainingLogs.LogMessage("=============== Training Model ===============", "Info");
                    var modelTrainer = _modelTrainerFactory.CreateTrainer(config.ModelType);

                    var trainingResult = modelTrainer.TrainModel(
                        mlContext,
                        processedData.Data,
                        processedData.FeatureNames,
                        config,
                        processedData).GetAwaiter().GetResult();

                    TrainingLogs.LogMessage("=============== Training Complete ===============", "Success");


                    UpdatePostTrainingVisualizations(trainingResult, config.ModelType);

                    try { File.Delete(tempConfigPath); } catch { }
                }
                catch (Exception ex)
                {
                    TrainingLogs.LogMessage($"Error in training process: {ex.Message}", "Error");
                    throw;
                }
            });
        }

        private Microsoft.ML.IDataView LoadDataFromDatabase(ModelConfig config, string[] enabledFields, Microsoft.ML.MLContext mlContext)
        {
            TrainingLogs.LogMessage("Loading data from database using optimized data loader...", "Info");

            var rawData = _dataLoader.LoadDataFromSql(
                _sqlHandler.GetConnectionString(),
                config.Database.TableName,
                enabledFields,
                config.ModelType,
                config.TargetField,
                config.Database.WhereClause);

            var rowCount = rawData.GetRowCount() ?? 0;
            TrainingLogs.LogMessage($"Loaded {rowCount:N0} rows from database table '{config.Database.TableName}'", "Success");

            return rawData;
        }

        private void UpdateFormTitle()
        {
            string fileName = Path.GetFileName(_currentFilePath) ?? "Untitled";
            WindowTitle = $"Iris ML Config - {fileName}";
        }

        private void UpdateUIFromConfig()
        {
            if (_currentConfig == null) return;

            GeneralSettings.SetConfiguration(_currentConfig);
            DatabaseSettings.SetConfiguration(_currentConfig.Database);
            InputFields.SetConfiguration(_currentConfig.InputFields);

            TrainingParameters.SetConfiguration(
                _currentConfig.TrainingParameters,
                _currentConfig.AutoML,
                _currentConfig.ModelType,
                _currentConfig.TargetField);
            DataProcessingPipeline.LoadFromConfig(_currentConfig);
        }

        private void UpdateConfigFromUI()
        {
            if (_currentConfig == null) return;

            GeneralSettings.UpdateConfiguration(_currentConfig);
            _currentConfig.Database = DatabaseSettings.GetConfiguration();
            _currentConfig.InputFields = InputFields.GetConfiguration();

            var (trainingParams, autoMLConfig, modelType, targetField) = TrainingParameters.GetConfiguration();
            _currentConfig.TrainingParameters = trainingParams;
            _currentConfig.AutoML = autoMLConfig;
            _currentConfig.ModelType = modelType;
            _currentConfig.TargetField = targetField;

            DataProcessingPipeline.SaveToConfig(_currentConfig);
        }

        private void UpdatePostTrainingVisualizations(TrainingResult trainingResult, ModelType modelType)
        {
            if (trainingResult == null) return;


            PostTrainingVisualizations.SetModelType(modelType);


            if (modelType == ModelType.BinaryClassification && trainingResult.Metrics is BinaryClassificationMetrics binaryMetrics)
            {
                var confusionMatrix = binaryMetrics.ConfusionMatrix;
                if (confusionMatrix != null)
                {
                    var matrixCounts = ConvertToMatrix(confusionMatrix.Counts);
                    var labels = new List<string> { "Negative", "Positive" };


                    PostTrainingVisualizations.UpdateConfusionMatrixWithMetrics(
                        matrixCounts,
                        labels,
                        binaryMetrics.Accuracy,
                        binaryMetrics.PositivePrecision,
                        binaryMetrics.PositiveRecall,
                        binaryMetrics.F1Score);
                    TrainingLogs.LogMessage("Confusion matrix visualization updated", "Info");
                }


                if (trainingResult.RocCurveFpr != null && trainingResult.RocCurveTpr != null &&
                    trainingResult.RocCurveThresholds != null && trainingResult.RocCurveFpr.Count > 0)
                {
                    PostTrainingVisualizations.UpdateRocCurve(
                        trainingResult.RocCurveFpr,
                        trainingResult.RocCurveTpr,
                        trainingResult.RocCurveThresholds,
                        trainingResult.AucScore);
                    TrainingLogs.LogMessage($"ROC curve visualization updated (AUC: {trainingResult.AucScore:F4})", "Info");
                }


                if (trainingResult.PrecisionRecallPrecision != null && trainingResult.PrecisionRecallRecall != null &&
                    trainingResult.PrecisionRecallThresholds != null && trainingResult.PrecisionRecallPrecision.Count > 0)
                {
                    PostTrainingVisualizations.UpdatePrecisionRecallCurve(
                        trainingResult.PrecisionRecallPrecision,
                        trainingResult.PrecisionRecallRecall,
                        trainingResult.PrecisionRecallThresholds,
                        trainingResult.AveragePrecision);
                    TrainingLogs.LogMessage($"Precision-Recall curve visualization updated (AP: {trainingResult.AveragePrecision:F4})", "Info");
                }

                // Update Feature Importance
                if (trainingResult.FeatureNames != null && trainingResult.FeatureImportanceScores != null &&
                    trainingResult.FeatureNames.Count > 0 && trainingResult.FeatureImportanceScores.Count > 0)
                {
                    PostTrainingVisualizations.UpdateFeatureImportance(
                        trainingResult.FeatureNames,
                        trainingResult.FeatureImportanceScores);
                    TrainingLogs.LogMessage($"Feature importance visualization updated ({trainingResult.FeatureNames.Count} features)", "Info");
                }
            }
            else if (modelType == ModelType.MultiClassClassification && trainingResult.Metrics is MulticlassClassificationMetrics multiclassMetrics)
            {
                var confusionMatrix = multiclassMetrics.ConfusionMatrix;
                if (confusionMatrix != null)
                {
                    var matrixCounts = ConvertToMatrix(confusionMatrix.Counts);
                    var numberOfClasses = confusionMatrix.NumberOfClasses;

                    var labels = new List<string>();
                    for (int i = 0; i < numberOfClasses; i++)
                    {
                        labels.Add($"Class {i}");
                    }


                    double f1Score = 0;
                    if (multiclassMetrics.PerClassLogLoss != null && multiclassMetrics.PerClassLogLoss.Count > 0)
                    {

                        f1Score = multiclassMetrics.MacroAccuracy;
                    }

                    PostTrainingVisualizations.UpdateConfusionMatrixWithMetrics(
                        matrixCounts,
                        labels,
                        multiclassMetrics.MicroAccuracy,
                        multiclassMetrics.MacroAccuracy,
                        multiclassMetrics.MacroAccuracy,
                        f1Score);
                    TrainingLogs.LogMessage("Confusion matrix visualization updated", "Info");
                }

                // Update Feature Importance for multiclass
                if (trainingResult.FeatureNames != null && trainingResult.FeatureImportanceScores != null &&
                    trainingResult.FeatureNames.Count > 0 && trainingResult.FeatureImportanceScores.Count > 0)
                {
                    PostTrainingVisualizations.UpdateFeatureImportance(
                        trainingResult.FeatureNames,
                        trainingResult.FeatureImportanceScores);
                    TrainingLogs.LogMessage($"Feature importance visualization updated ({trainingResult.FeatureNames.Count} features)", "Info");
                }
            }
            else if (modelType == ModelType.Regression && trainingResult.Metrics is RegressionMetrics regressionMetrics)
            {
                // Update Feature Importance for regression
                if (trainingResult.FeatureNames != null && trainingResult.FeatureImportanceScores != null &&
                    trainingResult.FeatureNames.Count > 0 && trainingResult.FeatureImportanceScores.Count > 0)
                {
                    PostTrainingVisualizations.UpdateFeatureImportance(
                        trainingResult.FeatureNames,
                        trainingResult.FeatureImportanceScores);
                    TrainingLogs.LogMessage($"Feature importance visualization updated ({trainingResult.FeatureNames.Count} features)", "Info");
                }

                // Update Residual Plots for regression
                if (trainingResult.ActualValues != null && trainingResult.PredictedValues != null &&
                    trainingResult.Residuals != null &&
                    trainingResult.ActualValues.Count > 0 && trainingResult.PredictedValues.Count > 0 &&
                    trainingResult.Residuals.Count > 0)
                {
                    PostTrainingVisualizations.UpdateResidualPlots(
                        trainingResult.ActualValues,
                        trainingResult.PredictedValues,
                        trainingResult.Residuals);
                    TrainingLogs.LogMessage($"Residual plot visualization updated ({trainingResult.ActualValues.Count} data points)", "Info");
                }
            }
        }

        private double[,] ConvertToMatrix(IReadOnlyList<IReadOnlyList<double>> counts)
        {
            int rows = counts.Count;
            int cols = counts[0].Count;
            var matrix = new double[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    matrix[i, j] = counts[i][j];
                }
            }

            return matrix;
        }
    }
}