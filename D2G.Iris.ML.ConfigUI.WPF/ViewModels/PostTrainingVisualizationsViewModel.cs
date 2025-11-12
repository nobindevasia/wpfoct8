using System;
using System.Collections.Generic;
using D2G.Iris.ML.Core.Enums;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class PostTrainingVisualizationsViewModel : BaseViewModel
    {
        private int _selectedVisualizationIndex;
        private ModelType _currentModelType;
        private bool _hasTrainingResults;

        public PostTrainingVisualizationsViewModel(
            ConfusionMatrixViewModel confusionMatrixViewModel,
            RocCurveViewModel rocCurveViewModel,
            PrecisionRecallCurveViewModel precisionRecallCurveViewModel,
            FeatureImportanceViewModel featureImportanceViewModel,
            ResidualPlotViewModel residualPlotViewModel)
        {
            ConfusionMatrix = confusionMatrixViewModel;
            RocCurve = rocCurveViewModel;
            PrecisionRecallCurve = precisionRecallCurveViewModel;
            FeatureImportance = featureImportanceViewModel;
            ResidualPlot = residualPlotViewModel;
            _currentModelType = ModelType.BinaryClassification;
        }

        #region Properties

        public ConfusionMatrixViewModel ConfusionMatrix { get; }

        public RocCurveViewModel RocCurve { get; }

        public PrecisionRecallCurveViewModel PrecisionRecallCurve { get; }

        public FeatureImportanceViewModel FeatureImportance { get; }

        public ResidualPlotViewModel ResidualPlot { get; }

        public int SelectedVisualizationIndex
        {
            get => _selectedVisualizationIndex;
            set => SetProperty(ref _selectedVisualizationIndex, value);
        }

        public ModelType CurrentModelType
        {
            get => _currentModelType;
            set
            {
                if (SetProperty(ref _currentModelType, value))
                {
                    UpdateVisualizationAvailability();
                }
            }
        }

        public bool HasTrainingResults
        {
            get => _hasTrainingResults;
            set => SetProperty(ref _hasTrainingResults, value);
        }




        public bool IsConfusionMatrixAvailable =>
            CurrentModelType == ModelType.BinaryClassification ||
            CurrentModelType == ModelType.MultiClassClassification;




        public bool IsRocCurveAvailable =>
            CurrentModelType == ModelType.BinaryClassification;




        public bool IsPrecisionRecallCurveAvailable =>
            CurrentModelType == ModelType.BinaryClassification;

        /// <summary>
        /// Feature importance is available for all model types
        /// </summary>
        public bool IsFeatureImportanceAvailable => true;

        /// <summary>
        /// Residual plot is only available for regression
        /// </summary>
        public bool IsResidualPlotAvailable =>
            CurrentModelType == ModelType.Regression;

        #endregion

        #region Methods






        public void UpdateConfusionMatrix(double[,] confusionMatrix, List<string> classLabels)
        {
            if (IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.UpdateConfusionMatrix(confusionMatrix, classLabels);
                HasTrainingResults = true;
            }
        }




        public void UpdateConfusionMatrixWithMetrics(double[,] confusionMatrix, List<string> classLabels,
            double accuracy, double precision, double recall, double f1Score)
        {
            if (IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.UpdateConfusionMatrixWithMetrics(confusionMatrix, classLabels, accuracy, precision, recall, f1Score);
                HasTrainingResults = true;
            }
        }








        public void UpdateRocCurve(List<double> fpr, List<double> tpr, List<double> thresholds, double aucScore)
        {
            if (IsRocCurveAvailable)
            {
                RocCurve.UpdateRocCurve(fpr, tpr, thresholds, aucScore);
                HasTrainingResults = true;
            }
        }








        public void UpdatePrecisionRecallCurve(List<double> precision, List<double> recall, List<double> thresholds, double averagePrecision)
        {
            if (IsPrecisionRecallCurveAvailable)
            {
                PrecisionRecallCurve.UpdatePrecisionRecallCurve(precision, recall, thresholds, averagePrecision);
                HasTrainingResults = true;
            }
        }

        /// <summary>
        /// Updates the feature importance visualization
        /// </summary>
        public void UpdateFeatureImportance(List<string> featureNames, List<double> importanceScores)
        {
            if (IsFeatureImportanceAvailable && featureNames != null && importanceScores != null &&
                featureNames.Count > 0 && importanceScores.Count > 0)
            {
                FeatureImportance.UpdateFeatureImportance(featureNames, importanceScores);
                HasTrainingResults = true;
            }
        }

        /// <summary>
        /// Updates the residual plot visualization
        /// </summary>
        public void UpdateResidualPlots(List<double> actualValues, List<double> predictedValues, List<double> residuals)
        {
            if (IsResidualPlotAvailable && actualValues != null && predictedValues != null && residuals != null &&
                actualValues.Count > 0 && predictedValues.Count > 0 && residuals.Count > 0)
            {
                ResidualPlot.UpdateResidualPlots(actualValues, predictedValues, residuals);
                HasTrainingResults = true;
            }
        }




        private void UpdateVisualizationAvailability()
        {
            OnPropertyChanged(nameof(IsConfusionMatrixAvailable));
            OnPropertyChanged(nameof(IsRocCurveAvailable));
            OnPropertyChanged(nameof(IsPrecisionRecallCurveAvailable));
            OnPropertyChanged(nameof(IsFeatureImportanceAvailable));
            OnPropertyChanged(nameof(IsResidualPlotAvailable));


            if (!IsConfusionMatrixAvailable)
            {
                ConfusionMatrix.Clear();
            }

            if (!IsRocCurveAvailable)
            {
                RocCurve.Clear();
            }

            if (!IsPrecisionRecallCurveAvailable)
            {
                PrecisionRecallCurve.Clear();
            }

            if (!IsResidualPlotAvailable)
            {
                ResidualPlot.Clear();
            }

            // Feature importance is always available, no need to clear
        }




        public void ClearAll()
        {
            ConfusionMatrix.Clear();
            RocCurve.Clear();
            PrecisionRecallCurve.Clear();
            FeatureImportance.Clear();
            ResidualPlot.Clear();
            HasTrainingResults = false;
            SelectedVisualizationIndex = 0;
        }




        public void SetModelType(ModelType modelType)
        {
            CurrentModelType = modelType;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ConfusionMatrix?.Dispose();
                RocCurve?.Dispose();
                PrecisionRecallCurve?.Dispose();
                FeatureImportance?.Dispose();
                ResidualPlot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
